import torch
import torch.nn as nn
from torchvision import models

CLASSES = ["deficient", "diseased", "healthy"]

device = torch.device("cpu")

# Rebuild the same architecture used in training
model = models.mobilenet_v2(weights=None)
model.classifier[1] = nn.Linear(model.last_channel, len(CLASSES))
model.load_state_dict(torch.load("leaf_model.pth", map_location=device))
model.eval()

# Dummy input matching training input shape: batch=1, channels=3, 224x224
dummy_input = torch.randn(1, 3, 224, 224)

torch.onnx.export(
    model,
    dummy_input,
    "leaf_model.onnx",
    input_names=["input"],
    output_names=["output"],
    dynamic_axes={"input": {0: "batch_size"}, "output": {0: "batch_size"}},
    opset_version=18,
)

print("Exported to leaf_model.onnx")
print("Class order (index -> label):")
for i, c in enumerate(CLASSES):
    print(f"  {i}: {c}")