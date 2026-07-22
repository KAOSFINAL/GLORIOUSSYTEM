import torch
import torch.nn as nn
from torch.utils.data import DataLoader
from torchvision import datasets, transforms, models

CLASSES = ["deficient", "diseased", "healthy"]
TEST_DIR = r"C:\Users\akshi\Downloads\lettucedisease\lettuce\test"

transform = transforms.Compose([
    transforms.Resize((224, 224)),
    transforms.ToTensor(),
    transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
])

# Note: this test set only has 'Bacterial', 'fungal', 'healthy' subfolders,
# both disease types map to our "diseased" class, so we remap labels manually.
test_data = datasets.ImageFolder(TEST_DIR, transform=transform)
print("Test set classes found:", test_data.classes)

# Map ImageFolder's own class indices to our 3-class scheme
folder_to_ours = {}
for idx, name in enumerate(test_data.classes):
    if name.lower() == "healthy":
        folder_to_ours[idx] = CLASSES.index("healthy")
    else:
        folder_to_ours[idx] = CLASSES.index("diseased")

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

model = models.mobilenet_v2(weights=None)
model.classifier[1] = nn.Linear(model.last_channel, len(CLASSES))
model.load_state_dict(torch.load("leaf_model.pth", map_location=device))
model = model.to(device)
model.eval()

loader = DataLoader(test_data, batch_size=8)

correct, total = 0, 0
with torch.no_grad():
    for images, labels in loader:
        images = images.to(device)
        remapped_labels = torch.tensor([folder_to_ours[l.item()] for l in labels]).to(device)
        outputs = model(images)
        _, predicted = torch.max(outputs, 1)
        total += remapped_labels.size(0)
        correct += (predicted == remapped_labels).sum().item()

acc = 100 * correct / total if total > 0 else 0
print(f"Held-out TEST accuracy (never seen during training): {acc:.1f}% on {total} images")