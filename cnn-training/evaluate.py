import argparse
from collections import Counter
from pathlib import Path

import torch
import torch.nn as nn
from torch.utils.data import DataLoader
from torchvision import datasets, transforms, models

SCRIPT_DIR = Path(__file__).resolve().parent
CLASSES = ["deficient", "diseased", "healthy"]

transform = transforms.Compose([
    transforms.Resize((224, 224)),
    transforms.ToTensor(),
    transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
])

def parse_args():
    parser = argparse.ArgumentParser(description="Evaluate the legacy 3-class health model.")
    parser.add_argument(
        "--test-dir",
        type=Path,
        default=SCRIPT_DIR / "data" / "test",
        help="ImageFolder test root (default: data/test)",
    )
    parser.add_argument(
        "--model",
        type=Path,
        default=SCRIPT_DIR / "leaf_model.pth",
        help="Model checkpoint (default: leaf_model.pth)",
    )
    return parser.parse_args()


def mapped_class(name):
    normalized = name.lower()
    if normalized in CLASSES:
        return CLASSES.index(normalized)
    if normalized in {"bacterial", "fungal", "disease", "diseased"}:
        return CLASSES.index("diseased")
    raise ValueError(f"Cannot map test folder {name!r} to {CLASSES}")


def main():
    args = parse_args()
    test_data = datasets.ImageFolder(args.test_dir, transform=transform)
    print("Test set classes found:", test_data.classes)
    folder_to_ours = {
        index: mapped_class(name) for index, name in enumerate(test_data.classes)
    }

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = models.mobilenet_v2(weights=None)
    model.classifier[1] = nn.Linear(model.last_channel, len(CLASSES))
    model.load_state_dict(torch.load(args.model, map_location=device, weights_only=True))
    model = model.to(device)
    model.eval()

    loader = DataLoader(test_data, batch_size=8)
    correct = Counter()
    totals = Counter()
    with torch.no_grad():
        for images, labels in loader:
            images = images.to(device)
            remapped = torch.tensor([folder_to_ours[label.item()] for label in labels], device=device)
            predicted = model(images).argmax(dim=1)
            for expected, actual in zip(remapped.tolist(), predicted.tolist()):
                totals[expected] += 1
                correct[expected] += int(expected == actual)

    total = sum(totals.values())
    total_correct = sum(correct.values())
    accuracy = 100 * total_correct / total if total else 0
    print(f"Held-out accuracy: {accuracy:.1f}% on {total} images")
    for index, class_name in enumerate(CLASSES):
        if totals[index]:
            class_accuracy = 100 * correct[index] / totals[index]
            print(f"  {class_name}: {class_accuracy:.1f}% ({correct[index]}/{totals[index]})")

    covered = [CLASSES[index] for index in totals]
    missing = [name for name in CLASSES if name not in covered]
    if missing:
        print(f"Coverage warning: this test set does not measure {', '.join(missing)}.")


if __name__ == "__main__":
    main()
