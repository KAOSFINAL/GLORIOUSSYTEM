import torch
import torch.nn as nn
from torchvision import models
import onnx
import onnxruntime as ort
import numpy as np
import os
from pathlib import Path

# Fix Windows console encoding issue
os.environ['PYTHONIOENCODING'] = 'utf-8'

# Task definitions
LETTUCE_CLASSES = ["non_lettuce", "lettuce"]
HEALTH_CLASSES = ["deficient", "diseased", "healthy"]
AGE_CLASSES = ["seedling", "vegetative", "mature", "harvest_ready"]

class MultiTaskMobileNetV2(nn.Module):
    """MobileNetV2 with three output heads for multi-task learning."""
    def __init__(self, num_lettuce=2, num_health=3, num_age=4, pretrained=False):
        super().__init__()
        self.backbone = models.mobilenet_v2(weights=None)
        in_features = self.backbone.last_channel

        # Remove original classifier
        self.backbone.classifier = nn.Identity()

        # Shared feature processing
        self.shared_fc = nn.Sequential(
            nn.Dropout(0.2),
            nn.Linear(in_features, 512),
            nn.ReLU(inplace=True),
            nn.Dropout(0.2),
        )

        # Head 1: Lettuce detection (binary)
        self.lettuce_head = nn.Linear(512, num_lettuce)

        # Head 2: Health classification (3 classes)
        self.health_head = nn.Linear(512, num_health)

        # Head 3: Age classification (4 classes)
        self.age_head = nn.Linear(512, num_age)

    def forward(self, x):
        features = self.backbone(x)
        shared = self.shared_fc(features)

        lettuce_out = self.lettuce_head(shared)
        health_out = self.health_head(shared)
        age_out = self.age_head(shared)

        return lettuce_out, health_out, age_out


def export_to_onnx():
    # Load trained model
    model = MultiTaskMobileNetV2(
        num_lettuce=len(LETTUCE_CLASSES),
        num_health=len(HEALTH_CLASSES),
        num_age=len(AGE_CLASSES),
        pretrained=False
    )

    state_dict = torch.load("leaf_model_multitask.pth", map_location="cpu", weights_only=True)
    model.load_state_dict(state_dict)
    model.eval()

    print(f"Model loaded. Parameters: {sum(p.numel() for p in model.parameters()):,}")

    # Create dummy input
    dummy_input = torch.randn(1, 3, 224, 224)

    # Test forward pass
    with torch.no_grad():
        lettuce_out, health_out, age_out = model(dummy_input)
        print(f"\nOutput shapes:")
        print(f"  Lettuce: {lettuce_out.shape} -> {LETTUCE_CLASSES}")
        print(f"  Health: {health_out.shape} -> {HEALTH_CLASSES}")
        print(f"  Age: {age_out.shape} -> {AGE_CLASSES}")

        lettuce_probs = torch.softmax(lettuce_out, dim=1)
        health_probs = torch.softmax(health_out, dim=1)
        age_probs = torch.softmax(age_out, dim=1)
        print(f"\nSample probs (random input):")
        print(f"  Lettuce: {lettuce_probs[0].tolist()}")
        print(f"  Health: {health_probs[0].tolist()}")
        print(f"  Age: {age_probs[0].tolist()}")

    # Export to ONNX (suppress verbose logging to avoid encoding issues)
    output_path = "leaf_model_multitask.onnx"

    # Redirect stderr to suppress torch.onnx logging
    import sys
    from io import StringIO
    old_stderr = sys.stderr
    sys.stderr = StringIO()

    try:
        torch.onnx.export(
            model,
            dummy_input,
            output_path,
            export_params=True,
            opset_version=18,
            do_constant_folding=True,
            input_names=["input"],
            output_names=["lettuce_logits", "health_logits", "age_logits"],
            dynamic_axes={
                "input": {0: "batch_size"},
                "lettuce_logits": {0: "batch_size"},
                "health_logits": {0: "batch_size"},
                "age_logits": {0: "batch_size"},
            },
            verbose=False
        )
    finally:
        sys.stderr = old_stderr

    print(f"\nExported to {output_path}")

    # Verify with ONNX Runtime
    print("\nVerifying with ONNX Runtime...")
    ort_session = ort.InferenceSession(output_path, providers=['CPUExecutionProvider'])

    ort_inputs = {"input": dummy_input.numpy()}
    ort_outputs = ort_session.run(None, ort_inputs)

    print(f"ONNX Runtime outputs:")
    print(f"  Lettuce logits: {ort_outputs[0].shape}")
    print(f"  Health logits: {ort_outputs[1].shape}")
    print(f"  Age logits: {ort_outputs[2].shape}")

    # Compare PyTorch vs ONNX
    with torch.no_grad():
        torch_lettuce, torch_health, torch_age = model(dummy_input)

    max_diff_lettuce = np.max(np.abs(torch_lettuce.numpy() - ort_outputs[0]))
    max_diff_health = np.max(np.abs(torch_health.numpy() - ort_outputs[1]))
    max_diff_age = np.max(np.abs(torch_age.numpy() - ort_outputs[2]))

    print(f"\nMax difference (PyTorch vs ONNX):")
    print(f"  Lettuce: {max_diff_lettuce:.6f}")
    print(f"  Health: {max_diff_health:.6f}")
    print(f"  Age: {max_diff_age:.6f}")

    if max(max_diff_lettuce, max_diff_health, max_diff_age) < 1e-4:
        print("\n[OK] ONNX export verified successfully!")
    else:
        print("\n⚠ Warning: Significant difference between PyTorch and ONNX outputs")

    # Also verify the ONNX model structure
    onnx_model = onnx.load(output_path)
    onnx.checker.check_model(onnx_model)
    print(f"\nONNX model structure:")
    print(f"  IR version: {onnx_model.ir_version}")
    print(f"  Opset version: {onnx_model.opset_import[0].version}")
    print(f"  Inputs: {[i.name for i in onnx_model.graph.input]}")
    print(f"  Outputs: {[o.name for o in onnx_model.graph.output]}")

    # Copy to MAUI App Models folder
    import shutil
    dest_dir = Path("../src/GLORIOUSSYSTEM.App/Models")
    dest_dir.mkdir(parents=True, exist_ok=True)

    shutil.copy2(output_path, dest_dir / output_path)
    print(f"\nCopied {output_path} to {dest_dir}")

    # Also copy the .data file if it exists (for large models)
    data_file = output_path + ".data"
    if Path(data_file).exists():
        shutil.copy2(data_file, dest_dir / data_file)
        print(f"Copied {data_file} to {dest_dir}")


if __name__ == "__main__":
    export_to_onnx()