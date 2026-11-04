import argparse
import torch
import onnx
import onnxruntime as ort
import numpy as np
import os
from pathlib import Path

from train_multitask import (
    HEALTH_CLASSES,
    LETTUCE_CLASSES,
    VARIETY_CLASSES,
    MultiTaskMobileNetV2,
)

# Fix Windows console encoding issue
os.environ['PYTHONIOENCODING'] = 'utf-8'

SCRIPT_DIR = Path(__file__).resolve().parent
CHECKPOINT_PATH = SCRIPT_DIR / "leaf_model_multitask.pth"
OUTPUT_PATH = SCRIPT_DIR / "leaf_model_multitask.onnx"
APP_MODELS_DIR = SCRIPT_DIR.parent / "src" / "GLORIOUSSYSTEM.App" / "Models"


def parse_args():
    parser = argparse.ArgumentParser(description="Export a multitask lettuce checkpoint to ONNX.")
    parser.add_argument("--checkpoint", type=Path, default=CHECKPOINT_PATH)
    parser.add_argument("--output", type=Path, default=OUTPUT_PATH)
    parser.add_argument(
        "--skip-app-copy",
        action="store_true",
        help="Verify the candidate without replacing the packaged app model",
    )
    return parser.parse_args()


def export_to_onnx():
    args = parse_args()
    checkpoint_path = args.checkpoint.resolve()
    output_path = args.output.resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)

    # Load trained model
    model = MultiTaskMobileNetV2(
        num_lettuce=len(LETTUCE_CLASSES),
        num_health=len(HEALTH_CLASSES),
        num_variety=len(VARIETY_CLASSES),
        pretrained=False
    )

    state_dict = torch.load(checkpoint_path, map_location="cpu", weights_only=True)
    model.load_state_dict(state_dict)
    model.eval()

    print(f"Model loaded. Parameters: {sum(p.numel() for p in model.parameters()):,}")

    # Create dummy input
    dummy_input = torch.randn(1, 3, 224, 224)

    # Test forward pass
    with torch.no_grad():
        lettuce_out, variety_out, health_out = model(dummy_input)
        print(f"\nOutput shapes:")
        print(f"  Lettuce: {lettuce_out.shape} -> {LETTUCE_CLASSES}")
        print(f"  Variety: {variety_out.shape} -> {VARIETY_CLASSES}")
        print(f"  Health: {health_out.shape} -> {HEALTH_CLASSES}")

        lettuce_probs = torch.softmax(lettuce_out, dim=1)
        variety_probs = torch.softmax(variety_out, dim=1)
        health_probs = torch.softmax(health_out, dim=1)
        print(f"\nSample probs (random input):")
        print(f"  Lettuce: {lettuce_probs[0].tolist()}")
        print(f"  Variety: {variety_probs[0].tolist()}")
        print(f"  Health: {health_probs[0].tolist()}")

    # Export to ONNX (suppress verbose logging to avoid encoding issues)
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
            output_names=["lettuce_logits", "variety_logits", "health_logits"],
            dynamic_axes={
                "input": {0: "batch_size"},
                "lettuce_logits": {0: "batch_size"},
                "variety_logits": {0: "batch_size"},
                "health_logits": {0: "batch_size"},
            },
            verbose=False
        )
    finally:
        sys.stderr = old_stderr

    print(f"\nExported to {output_path}")

    # Verify with ONNX Runtime
    print("\nVerifying with ONNX Runtime...")
    ort_session = ort.InferenceSession(str(output_path), providers=['CPUExecutionProvider'])

    ort_inputs = {"input": dummy_input.numpy()}
    ort_outputs = ort_session.run(None, ort_inputs)

    print(f"ONNX Runtime outputs:")
    print(f"  Lettuce logits: {ort_outputs[0].shape}")
    print(f"  Variety logits: {ort_outputs[1].shape}")
    print(f"  Health logits: {ort_outputs[2].shape}")

    # Compare PyTorch vs ONNX
    with torch.no_grad():
        torch_lettuce, torch_variety, torch_health = model(dummy_input)

    max_diff_lettuce = np.max(np.abs(torch_lettuce.numpy() - ort_outputs[0]))
    max_diff_variety = np.max(np.abs(torch_variety.numpy() - ort_outputs[1]))
    max_diff_health = np.max(np.abs(torch_health.numpy() - ort_outputs[2]))

    print(f"\nMax difference (PyTorch vs ONNX):")
    print(f"  Lettuce: {max_diff_lettuce:.6f}")
    print(f"  Variety: {max_diff_variety:.6f}")
    print(f"  Health: {max_diff_health:.6f}")

    if max(max_diff_lettuce, max_diff_variety, max_diff_health) < 1e-4:
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

    if not args.skip_app_copy:
        # Copy to MAUI App Models folder only for an accepted deployment export.
        import shutil
        dest_dir = APP_MODELS_DIR
        dest_dir.mkdir(parents=True, exist_ok=True)

        shutil.copy2(output_path, dest_dir / output_path.name)
        print(f"\nCopied {output_path} to {dest_dir}")

        # Also copy the .data file if it exists (for large models)
        data_file = Path(str(output_path) + ".data")
        if data_file.exists():
            shutil.copy2(data_file, dest_dir / data_file.name)
            print(f"Copied {data_file} to {dest_dir}")


if __name__ == "__main__":
    export_to_onnx()
