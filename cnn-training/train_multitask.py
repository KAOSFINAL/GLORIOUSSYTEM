import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.utils.data import DataLoader, ConcatDataset
from torchvision import datasets, transforms, models
from pathlib import Path
import os

DATA_DIR = "data"
BATCH_SIZE = 16
EPOCHS = 20
LR = 0.0005
DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# Task 1: Lettuce detection (binary)
LETTUCE_CLASSES = ["non_lettuce", "lettuce"]

# Task 2: Health classification (3 classes) - only for lettuce
HEALTH_CLASSES = ["deficient", "diseased", "healthy"]

# Task 3: Age classification (4 classes) - only for lettuce
AGE_CLASSES = ["seedling", "vegetative", "mature", "harvest_ready"]

# Transforms
train_transform = transforms.Compose([
    transforms.RandomResizedCrop(224, scale=(0.8, 1.0)),
    transforms.RandomHorizontalFlip(),
    transforms.RandomRotation(15),
    transforms.ColorJitter(brightness=0.2, contrast=0.2, saturation=0.2),
    transforms.ToTensor(),
    transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
])

val_transform = transforms.Compose([
    transforms.Resize((224, 224)),
    transforms.ToTensor(),
    transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
])

print(f"Using device: {DEVICE}")
print("=" * 60)


class MultiTaskDataset(torch.utils.data.Dataset):
    """
    Dataset that provides labels for all three tasks.
    For non-lettuce images: lettuce_label=0, health_label=-1 (ignored), age_label=-1 (ignored)
    For lettuce images: lettuce_label=1, health_label=0-2, age_label=0-3
    """
    def __init__(self, data_dir, transform=None):
        self.transform = transform
        self.samples = []  # (image_path, lettuce_label, health_label, age_label)

        # Non-lettuce images (lettuce_label=0, health/age ignored)
        non_lettuce_dir = Path(data_dir) / "non_lettuce"
        if non_lettuce_dir.exists():
            for img_path in non_lettuce_dir.rglob("*"):
                if img_path.suffix.lower() in ['.jpg', '.jpeg', '.png', '.bmp']:
                    self.samples.append((str(img_path), 0, -1, -1))

        # Lettuce images with health + age labels
        for health_idx, health_class in enumerate(HEALTH_CLASSES):
            for age_idx, age_class in enumerate(AGE_CLASSES):
                class_dir = Path(data_dir) / "lettuce" / age_class / health_class
                if class_dir.exists():
                    for img_path in class_dir.rglob("*"):
                        if img_path.suffix.lower() in ['.jpg', '.jpeg', '.png', '.bmp']:
                            self.samples.append((str(img_path), 1, health_idx, age_idx))

        # Also check flat structure: data/lettuce/{health}/{age} or data/{health}/{age}
        # For backward compatibility with existing data structure
        for health_idx, health_class in enumerate(HEALTH_CLASSES):
            health_dir = Path(data_dir) / health_class
            if health_dir.exists():
                # Check for age subdirectories
                for age_idx, age_class in enumerate(AGE_CLASSES):
                    age_dir = health_dir / age_class
                    if age_dir.exists():
                        for img_path in age_dir.rglob("*"):
                            if img_path.suffix.lower() in ['.jpg', '.jpeg', '.png', '.bmp']:
                                self.samples.append((str(img_path), 1, health_idx, age_idx))
                    else:
                        # Flat structure - assign default age (vegetative)
                        for img_path in health_dir.rglob("*"):
                            if img_path.suffix.lower() in ['.jpg', '.jpeg', '.png', '.bmp']:
                                self.samples.append((str(img_path), 1, health_idx, 1))

        print(f"Loaded {len(self.samples)} samples from {data_dir}")
        lettuce_count = sum(1 for s in self.samples if s[1] == 1)
        non_lettuce_count = sum(1 for s in self.samples if s[1] == 0)
        print(f"  Lettuce: {lettuce_count}, Non-lettuce: {non_lettuce_count}")

    def __len__(self):
        return len(self.samples)

    def __getitem__(self, idx):
        img_path, lettuce_label, health_label, age_label = self.samples[idx]
        image = datasets.folder.default_loader(img_path)
        if self.transform:
            image = self.transform(image)
        return image, torch.tensor(lettuce_label), torch.tensor(health_label), torch.tensor(age_label)


class MultiTaskMobileNetV2(nn.Module):
    """MobileNetV2 with three output heads for multi-task learning."""
    def __init__(self, num_lettuce=2, num_health=3, num_age=4, pretrained=True):
        super().__init__()
        self.backbone = models.mobilenet_v2(weights=models.MobileNet_V2_Weights.DEFAULT if pretrained else None)
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


def multitask_loss(lettuce_pred, health_pred, age_pred,
                   lettuce_target, health_target, age_target,
                   lettuce_weight=1.0, health_weight=1.0, age_weight=1.0):
    """
    Combined loss for multi-task learning.
    For non-lettuce samples (lettuce_target=0), health and age targets are -1 and ignored.
    """
    # Lettuce detection loss (all samples)
    lettuce_loss = F.cross_entropy(lettuce_pred, lettuce_target)

    # Health and age loss (only for lettuce samples)
    lettuce_mask = (lettuce_target == 1)

    if lettuce_mask.any():
        health_loss = F.cross_entropy(health_pred[lettuce_mask], health_target[lettuce_mask])
        age_loss = F.cross_entropy(age_pred[lettuce_mask], age_target[lettuce_mask])
    else:
        health_loss = torch.tensor(0.0, device=lettuce_pred.device)
        age_loss = torch.tensor(0.0, device=lettuce_pred.device)

    total_loss = (lettuce_weight * lettuce_loss +
                  health_weight * health_loss +
                  age_weight * age_loss)

    return total_loss, lettuce_loss, health_loss, age_loss


def evaluate(model, loader):
    model.eval()
    total = 0
    lettuce_correct = 0
    health_correct = 0
    age_correct = 0
    health_total = 0
    age_total = 0

    with torch.no_grad():
        for images, lettuce_t, health_t, age_t in loader:
            images = images.to(DEVICE)
            lettuce_t = lettuce_t.to(DEVICE)
            health_t = health_t.to(DEVICE)
            age_t = age_t.to(DEVICE)

            lettuce_p, health_p, age_p = model(images)

            # Lettuce accuracy (all samples)
            lettuce_pred = lettuce_p.argmax(dim=1)
            lettuce_correct += (lettuce_pred == lettuce_t).sum().item()
            total += lettuce_t.size(0)

            # Health & Age accuracy (only lettuce samples)
            lettuce_mask = (lettuce_t == 1)
            if lettuce_mask.any():
                health_pred = health_p[lettuce_mask].argmax(dim=1)
                health_correct += (health_pred == health_t[lettuce_mask]).sum().item()
                health_total += lettuce_mask.sum().item()

                age_pred = age_p[lettuce_mask].argmax(dim=1)
                age_correct += (age_pred == age_t[lettuce_mask]).sum().item()
                age_total += lettuce_mask.sum().item()

    lettuce_acc = 100 * lettuce_correct / total if total > 0 else 0
    health_acc = 100 * health_correct / health_total if health_total > 0 else 0
    age_acc = 100 * age_correct / age_total if age_total > 0 else 0

    return lettuce_acc, health_acc, age_acc, total, health_total, age_total


def main():
    # Datasets
    train_dataset = MultiTaskDataset(f"{DATA_DIR}/train", transform=train_transform)
    val_dataset = MultiTaskDataset(f"{DATA_DIR}/val", transform=val_transform)

    if len(train_dataset) == 0:
        print("ERROR: No training data found!")
        print("Please add images to:")
        print("  data/train/non_lettuce/")
        print("  data/train/lettuce/{seedling,vegetative,mature,harvest_ready}/{deficient,diseased,healthy}/")
        return

    train_loader = DataLoader(train_dataset, batch_size=BATCH_SIZE, shuffle=True, num_workers=0)
    val_loader = DataLoader(val_dataset, batch_size=BATCH_SIZE, shuffle=False, num_workers=0)

    # Model
    model = MultiTaskMobileNetV2(
        num_lettuce=len(LETTUCE_CLASSES),
        num_health=len(HEALTH_CLASSES),
        num_age=len(AGE_CLASSES),
        pretrained=True
    ).to(DEVICE)

    optimizer = torch.optim.Adam(model.parameters(), lr=LR)
    scheduler = torch.optim.lr_scheduler.ReduceLROnPlateau(optimizer, mode='max', factor=0.5, patience=3)

    print(f"\nModel: MobileNetV2 Multi-Task")
    print(f"Parameters: {sum(p.numel() for p in model.parameters()):,}")
    print(f"Train samples: {len(train_dataset)}")
    print(f"Val samples: {len(val_dataset)}")
    print("=" * 60)

    best_val_lettuce_acc = 0

    for epoch in range(EPOCHS):
        model.train()
        total_loss = 0
        total_lettuce_loss = 0
        total_health_loss = 0
        total_age_loss = 0

        for images, lettuce_t, health_t, age_t in train_loader:
            images = images.to(DEVICE)
            lettuce_t = lettuce_t.to(DEVICE)
            health_t = health_t.to(DEVICE)
            age_t = age_t.to(DEVICE)

            optimizer.zero_grad()
            lettuce_p, health_p, age_p = model(images)

            loss, l_loss, h_loss, a_loss = multitask_loss(
                lettuce_p, health_p, age_p,
                lettuce_t, health_t, age_t,
                lettuce_weight=1.0, health_weight=1.0, age_weight=1.0
            )

            loss.backward()
            optimizer.step()

            total_loss += loss.item()
            total_lettuce_loss += l_loss.item()
            total_health_loss += h_loss.item()
            total_age_loss += a_loss.item()

        # Validation
        lettuce_acc, health_acc, age_acc, total, health_total, age_total = evaluate(model, val_loader)
        scheduler.step(lettuce_acc)

        print(f"Epoch {epoch+1}/{EPOCHS}")
        print(f"  Train Loss: {total_loss:.3f} (Lettuce: {total_lettuce_loss:.3f}, Health: {total_health_loss:.3f}, Age: {total_age_loss:.3f})")
        print(f"  Val - Lettuce: {lettuce_acc:.1f}% ({total}), Health: {health_acc:.1f}% ({health_total}), Age: {age_acc:.1f}% ({age_total})")

        # Save best model based on lettuce detection accuracy
        if lettuce_acc > best_val_lettuce_acc:
            best_val_lettuce_acc = lettuce_acc
            torch.save(model.state_dict(), "leaf_model_multitask.pth")
            print(f"  *** New best model saved (lettuce acc: {best_val_lettuce_acc:.1f}%) ***")

    print("=" * 60)
    print(f"Training complete. Best lettuce detection accuracy: {best_val_lettuce_acc:.1f}%")


if __name__ == "__main__":
    main()