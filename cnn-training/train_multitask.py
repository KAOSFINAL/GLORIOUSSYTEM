import argparse
from collections import Counter
import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.utils.data import DataLoader
from torchvision import datasets, transforms, models
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
DATA_DIR = SCRIPT_DIR / "data"
MODEL_PATH = SCRIPT_DIR / "leaf_model_multitask.pth"
BATCH_SIZE = 16
EPOCHS = 20
LR = 0.0005
DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# Task 1: Lettuce detection (binary)
LETTUCE_CLASSES = ["non_lettuce", "lettuce"]

# Task 2: Health classification (3 classes) - only for lettuce
HEALTH_CLASSES = ["deficient", "diseased", "healthy"]

# Task 3: Variety classification (2 classes) - only for lettuce with a variety label
VARIETY_CLASSES = ["green_ice", "not_green_ice"]
GREEN_ICE_SOURCE_TAG = "green_ice_rf_v1"
NOT_GREEN_ICE_SOURCE_TAG = "not_green_ice_rf_v1"

# These names are retained only to discover images in the legacy folder layout.
# They are not prediction targets.
LEGACY_STAGE_DIRS = ["seedling", "vegetative", "mature", "harvest_ready"]
UNKNOWN_AGE_DIR = "unknown_age"
IMAGE_EXTENSIONS = {'.jpg', '.jpeg', '.png', '.bmp', '.webp'}


def parse_args():
    parser = argparse.ArgumentParser(description="Train or fine-tune the multitask lettuce model.")
    parser.add_argument("--epochs", type=int, default=EPOCHS)
    parser.add_argument("--learning-rate", type=float, default=LR)
    parser.add_argument(
        "--variety-weight",
        type=float,
        default=2.0,
        help="Weight for the explicit Green-Ice binary objective (default: 2.0)",
    )
    parser.add_argument(
        "--resume",
        type=Path,
        help="Optional checkpoint to fine-tune instead of starting from ImageNet weights",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=MODEL_PATH,
        help=f"Best-checkpoint output path (default: {MODEL_PATH})",
    )
    return parser.parse_args()

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
    Dataset that provides labels for three tasks.
    For non-lettuce images: lettuce_label=0 and all conditional labels=-1 (ignored)
    For lettuce images: lettuce_label=1, health_label=0-2, variety_label=0-1

    Variety labels come only from explicit provenance folders. Legacy health
    images still train lettuce detection and health, but their unknown cultivar
    is ignored by the variety loss rather than being guessed as a negative.
    """
    def __init__(self, data_dir, transform=None):
        self.transform = transform
        self.samples = []  # (image_path, lettuce_label, health_label, variety_label)
        self._seen_paths = set()

        def infer_variety_label(image_path, lettuce_label):
            if lettuce_label == 0:
                return -1
            normalized_parts = {part.lower() for part in image_path.parts}
            if GREEN_ICE_SOURCE_TAG in normalized_parts:
                return VARIETY_CLASSES.index("green_ice")
            if NOT_GREEN_ICE_SOURCE_TAG in normalized_parts:
                return VARIETY_CLASSES.index("not_green_ice")
            return -1

        def add_images(directory, lettuce_label, health_label, recursive=True):
            if not directory.exists():
                return
            candidates = directory.rglob("*") if recursive else directory.iterdir()
            for img_path in candidates:
                if not img_path.is_file() or img_path.suffix.lower() not in IMAGE_EXTENSIONS:
                    continue
                resolved = str(img_path.resolve())
                if resolved in self._seen_paths:
                    continue
                self._seen_paths.add(resolved)
                variety_label = infer_variety_label(img_path, lettuce_label)
                self.samples.append((str(img_path), lettuce_label, health_label, variety_label))

        # Non-lettuce images (lettuce_label=0, health/variety ignored)
        non_lettuce_dir = Path(data_dir) / "non_lettuce"
        add_images(non_lettuce_dir, 0, -1)

        # Optional nested legacy layout. Stage folders are used only for image
        # discovery; this model intentionally has no age/growth-stage output.
        for health_idx, health_class in enumerate(HEALTH_CLASSES):
            for stage_dir in LEGACY_STAGE_DIRS:
                class_dir = Path(data_dir) / "lettuce" / stage_dir / health_class
                add_images(class_dir, 1, health_idx)

        # Current layout: data/{health}/, including imported provenance folders.
        for health_idx, health_class in enumerate(HEALTH_CLASSES):
            health_dir = Path(data_dir) / health_class
            add_images(health_dir, 1, health_idx)

        print(f"Loaded {len(self.samples)} samples from {data_dir}")
        lettuce_count = sum(1 for s in self.samples if s[1] == 1)
        non_lettuce_count = sum(1 for s in self.samples if s[1] == 0)
        variety_counts = Counter(s[3] for s in self.samples if s[3] >= 0)
        print(f"  Lettuce: {lettuce_count}, Non-lettuce: {non_lettuce_count}")
        print(
            "  Variety-labelled: "
            + ", ".join(
                f"{VARIETY_CLASSES[index]}={variety_counts.get(index, 0)}"
                for index in range(len(VARIETY_CLASSES))
            )
        )

    def __len__(self):
        return len(self.samples)

    def __getitem__(self, idx):
        img_path, lettuce_label, health_label, variety_label = self.samples[idx]
        image = datasets.folder.default_loader(img_path)
        if self.transform:
            image = self.transform(image)
        return (
            image,
            torch.tensor(lettuce_label),
            torch.tensor(health_label),
            torch.tensor(variety_label),
        )


class MultiTaskMobileNetV2(nn.Module):
    """MobileNetV2 with lettuce, variety, and health output heads."""
    def __init__(self, num_lettuce=2, num_health=3, num_variety=2, pretrained=True):
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

        # Head 3: Variety classification (Green Ice vs not Green Ice)
        self.variety_head = nn.Linear(512, num_variety)

    def forward(self, x):
        features = self.backbone(x)
        shared = self.shared_fc(features)

        lettuce_out = self.lettuce_head(shared)
        variety_out = self.variety_head(shared)
        health_out = self.health_head(shared)

        return lettuce_out, variety_out, health_out


def multitask_loss(lettuce_pred, variety_pred, health_pred,
                   lettuce_target, variety_target, health_target,
                   variety_class_weights=None,
                   lettuce_weight=1.0, variety_weight=1.0,
                   health_weight=1.0):
    """
    Combined loss for multi-task learning.
    For non-lettuce samples, health and variety targets are -1 and ignored.
    """
    # Lettuce detection loss (all samples)
    lettuce_loss = F.cross_entropy(lettuce_pred, lettuce_target)

    # Health and variety losses use independent masks.
    variety_mask = (lettuce_target == 1) & (variety_target >= 0)
    health_mask = (lettuce_target == 1) & (health_target >= 0)

    if variety_mask.any():
        variety_loss = F.cross_entropy(
            variety_pred[variety_mask],
            variety_target[variety_mask],
            weight=variety_class_weights,
        )
    else:
        variety_loss = torch.tensor(0.0, device=lettuce_pred.device)

    if health_mask.any():
        health_loss = F.cross_entropy(health_pred[health_mask], health_target[health_mask])
    else:
        health_loss = torch.tensor(0.0, device=lettuce_pred.device)

    total_loss = (lettuce_weight * lettuce_loss +
                  variety_weight * variety_loss +
                  health_weight * health_loss)

    return total_loss, lettuce_loss, variety_loss, health_loss


def macro_f1(confusion):
    scores = []
    for class_index in range(len(confusion)):
        true_positive = confusion[class_index][class_index]
        false_positive = sum(confusion[row][class_index] for row in range(len(confusion))) - true_positive
        false_negative = sum(confusion[class_index]) - true_positive
        denominator = 2 * true_positive + false_positive + false_negative
        scores.append(2 * true_positive / denominator if denominator else 0.0)
    return 100 * sum(scores) / len(scores) if scores else 0.0


def evaluate(model, loader):
    model.eval()
    total = 0
    lettuce_correct = 0
    variety_correct = 0
    health_correct = 0
    variety_total = 0
    health_total = 0
    variety_confusion = [
        [0 for _ in VARIETY_CLASSES]
        for _ in VARIETY_CLASSES
    ]

    with torch.no_grad():
        for images, lettuce_t, health_t, variety_t in loader:
            images = images.to(DEVICE)
            lettuce_t = lettuce_t.to(DEVICE)
            health_t = health_t.to(DEVICE)
            variety_t = variety_t.to(DEVICE)

            lettuce_p, variety_p, health_p = model(images)

            # Lettuce accuracy (all samples)
            lettuce_pred = lettuce_p.argmax(dim=1)
            lettuce_correct += (lettuce_pred == lettuce_t).sum().item()
            total += lettuce_t.size(0)

            variety_mask = (lettuce_t == 1) & (variety_t >= 0)
            if variety_mask.any():
                variety_pred = variety_p[variety_mask].argmax(dim=1)
                variety_expected = variety_t[variety_mask]
                variety_correct += (variety_pred == variety_expected).sum().item()
                variety_total += variety_mask.sum().item()
                for expected, actual in zip(variety_expected.tolist(), variety_pred.tolist()):
                    variety_confusion[expected][actual] += 1

            health_mask = (lettuce_t == 1) & (health_t >= 0)
            if health_mask.any():
                health_pred = health_p[health_mask].argmax(dim=1)
                health_correct += (health_pred == health_t[health_mask]).sum().item()
                health_total += health_mask.sum().item()

    lettuce_acc = 100 * lettuce_correct / total if total > 0 else 0
    variety_acc = 100 * variety_correct / variety_total if variety_total > 0 else 0
    variety_macro_f1 = macro_f1(variety_confusion)
    health_acc = 100 * health_correct / health_total if health_total > 0 else 0

    return {
        "lettuce_acc": lettuce_acc,
        "variety_acc": variety_acc,
        "variety_macro_f1": variety_macro_f1,
        "health_acc": health_acc,
        "total": total,
        "variety_total": variety_total,
        "health_total": health_total,
        "variety_confusion": variety_confusion,
    }


def main():
    args = parse_args()
    if args.epochs < 1:
        raise ValueError("--epochs must be at least 1")
    if args.learning_rate <= 0:
        raise ValueError("--learning-rate must be positive")
    if args.variety_weight <= 0:
        raise ValueError("--variety-weight must be positive")

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
        num_variety=len(VARIETY_CLASSES),
        pretrained=args.resume is None
    ).to(DEVICE)

    if args.resume:
        resume_path = args.resume.resolve()
        load_result = model.load_state_dict(
            torch.load(resume_path, map_location=DEVICE, weights_only=True),
            strict=False,
        )
        allowed_missing = {"variety_head.weight", "variety_head.bias"}
        allowed_unexpected = {"age_head.weight", "age_head.bias"}
        unexpected_missing = set(load_result.missing_keys) - allowed_missing
        unexpected_keys = set(load_result.unexpected_keys) - allowed_unexpected
        if unexpected_missing or unexpected_keys:
            raise RuntimeError(
                "Checkpoint is incompatible. "
                f"Missing: {sorted(unexpected_missing)}; "
                f"unexpected: {sorted(unexpected_keys)}"
            )
        print(f"Fine-tuning checkpoint: {resume_path}")
        if load_result.missing_keys:
            print("  Initialized a new variety head from the previous lettuce/health/age checkpoint.")
        if set(load_result.unexpected_keys) & allowed_unexpected:
            print("  Dropped the previous age head as requested.")

    variety_counts = Counter(sample[3] for sample in train_dataset.samples if sample[3] >= 0)
    missing_variety_classes = [
        VARIETY_CLASSES[index]
        for index in range(len(VARIETY_CLASSES))
        if variety_counts[index] == 0
    ]
    if missing_variety_classes:
        raise RuntimeError(
            "Training data is missing explicit variety labels for: "
            + ", ".join(missing_variety_classes)
        )
    labelled_variety_total = sum(variety_counts.values())
    variety_class_weights = torch.tensor(
        [
            labelled_variety_total / (len(VARIETY_CLASSES) * variety_counts[index])
            for index in range(len(VARIETY_CLASSES))
        ],
        dtype=torch.float32,
        device=DEVICE,
    )
    print(
        "Variety class weights: "
        + ", ".join(
            f"{label}={variety_class_weights[index].item():.3f}"
            for index, label in enumerate(VARIETY_CLASSES)
        )
    )

    optimizer = torch.optim.Adam(model.parameters(), lr=args.learning_rate)
    scheduler = torch.optim.lr_scheduler.ReduceLROnPlateau(optimizer, mode='max', factor=0.5, patience=3)

    print(f"\nModel: MobileNetV2 Multi-Task")
    print(f"Parameters: {sum(p.numel() for p in model.parameters()):,}")
    print(f"Train samples: {len(train_dataset)}")
    print(f"Val samples: {len(val_dataset)}")
    print("=" * 60)

    best_val_score = -1

    output_path = args.output.resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)

    for epoch in range(args.epochs):
        model.train()
        total_loss = 0
        total_lettuce_loss = 0
        total_variety_loss = 0
        total_health_loss = 0

        for images, lettuce_t, health_t, variety_t in train_loader:
            images = images.to(DEVICE)
            lettuce_t = lettuce_t.to(DEVICE)
            health_t = health_t.to(DEVICE)
            variety_t = variety_t.to(DEVICE)

            optimizer.zero_grad()
            lettuce_p, variety_p, health_p = model(images)

            loss, l_loss, v_loss, h_loss = multitask_loss(
                lettuce_p, variety_p, health_p,
                lettuce_t, variety_t, health_t,
                variety_class_weights=variety_class_weights,
                lettuce_weight=1.0, variety_weight=args.variety_weight,
                health_weight=1.0
            )

            loss.backward()
            optimizer.step()

            total_loss += loss.item()
            total_lettuce_loss += l_loss.item()
            total_variety_loss += v_loss.item()
            total_health_loss += h_loss.item()

        # Validation
        metrics = evaluate(model, val_loader)
        # Green-Ice identification is the deployment priority. Lettuce and
        # health remain useful secondary heads, but they cannot outweigh a weak
        # binary-variety checkpoint during model selection.
        val_score = (
            0.70 * metrics["variety_macro_f1"]
            + 0.15 * metrics["lettuce_acc"]
            + 0.15 * metrics["health_acc"]
        )
        scheduler.step(val_score)

        print(f"Epoch {epoch+1}/{args.epochs}")
        print(
            f"  Train Loss: {total_loss:.3f} "
            f"(Lettuce: {total_lettuce_loss:.3f}, Variety: {total_variety_loss:.3f}, "
            f"Health: {total_health_loss:.3f})"
        )
        print(
            f"  Val - Lettuce: {metrics['lettuce_acc']:.1f}% ({metrics['total']}), "
            f"Variety: {metrics['variety_acc']:.1f}% / macro F1 {metrics['variety_macro_f1']:.1f}% "
            f"({metrics['variety_total']}), Health: {metrics['health_acc']:.1f}% "
            f"({metrics['health_total']})"
        )
        print(f"  Variety confusion [expected rows, predicted columns]: {metrics['variety_confusion']}")

        # Save the best model across lettuce detection, variety, and health.
        if val_score > best_val_score:
            best_val_score = val_score
            torch.save(model.state_dict(), output_path)
            print(f"  *** New best model saved (deployment score: {best_val_score:.1f}%) ***")

    print("=" * 60)
    print(f"Training complete. Best deployment validation score: {best_val_score:.1f}%")
    print(f"Model saved to {output_path}")


if __name__ == "__main__":
    main()
