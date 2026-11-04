import torch
import torch.nn as nn
from torch.utils.data import DataLoader
from torchvision import datasets, transforms, models
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
DATA_DIR = SCRIPT_DIR / "data"
BATCH_SIZE = 16
EPOCHS = 15
CLASSES = ["deficient", "diseased", "healthy"]  # alphabetical, matches ImageFolder's default ordering

transform = transforms.Compose([
    transforms.Resize((224, 224)),
    transforms.ToTensor(),
    transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
])

def health_dataset(split):
    dataset = datasets.ImageFolder(DATA_DIR / split, transform=transform)
    missing = [name for name in CLASSES if name not in dataset.class_to_idx]
    if missing:
        raise ValueError(f"Missing health folders in {split}: {missing}")

    filtered_samples = []
    for path, source_index in dataset.samples:
        class_name = dataset.classes[source_index]
        if class_name in CLASSES:
            filtered_samples.append((path, CLASSES.index(class_name)))

    dataset.samples = filtered_samples
    dataset.imgs = filtered_samples
    dataset.targets = [label for _, label in filtered_samples]
    dataset.classes = list(CLASSES)
    dataset.class_to_idx = {name: index for index, name in enumerate(CLASSES)}
    return dataset


train_data = health_dataset("train")
val_data = health_dataset("val")

print("Detected classes:", train_data.classes)  # sanity check - confirms folder order matches CLASSES

train_loader = DataLoader(train_data, batch_size=BATCH_SIZE, shuffle=True)
val_loader = DataLoader(val_data, batch_size=BATCH_SIZE)

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
print(f"Using device: {device}")

model = models.mobilenet_v2(weights=models.MobileNet_V2_Weights.DEFAULT)
model.classifier[1] = nn.Linear(model.last_channel, len(CLASSES))
model = model.to(device)

criterion = nn.CrossEntropyLoss()
optimizer = torch.optim.Adam(model.parameters(), lr=0.0005)

for epoch in range(EPOCHS):
    model.train()
    total_loss = 0
    for images, labels in train_loader:
        images, labels = images.to(device), labels.to(device)
        optimizer.zero_grad()
        outputs = model(images)
        loss = criterion(outputs, labels)
        loss.backward()
        optimizer.step()
        total_loss += loss.item()

    model.eval()
    correct, total = 0, 0
    with torch.no_grad():
        for images, labels in val_loader:
            images, labels = images.to(device), labels.to(device)
            outputs = model(images)
            _, predicted = torch.max(outputs, 1)
            total += labels.size(0)
            correct += (predicted == labels).sum().item()

    val_acc = 100 * correct / total if total > 0 else 0
    print(f"Epoch {epoch+1}/{EPOCHS} - Loss: {total_loss:.3f} - Val Accuracy: {val_acc:.1f}%")

model_path = SCRIPT_DIR / "leaf_model.pth"
torch.save(model.state_dict(), model_path)
print(f"Model saved to {model_path}")
