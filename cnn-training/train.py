import torch
import torch.nn as nn
from torch.utils.data import DataLoader
from torchvision import datasets, transforms, models

DATA_DIR = "data"
BATCH_SIZE = 16
EPOCHS = 15
CLASSES = ["deficient", "diseased", "healthy"]  # alphabetical, matches ImageFolder's default ordering

transform = transforms.Compose([
    transforms.Resize((224, 224)),
    transforms.ToTensor(),
    transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
])

train_data = datasets.ImageFolder(f"{DATA_DIR}/train", transform=transform)
val_data = datasets.ImageFolder(f"{DATA_DIR}/val", transform=transform)

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

torch.save(model.state_dict(), "leaf_model.pth")
print("Model saved to leaf_model.pth")