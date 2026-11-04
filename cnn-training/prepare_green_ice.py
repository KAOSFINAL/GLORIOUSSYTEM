"""Convert Roboflow Lettuce Types boxes into binary variety training crops.

Class 0 is the positive target (``green_ice``). Iceberg and Romaine are merged
into the single negative target (``not_green_ice``); the model never needs to
distinguish those two cultivars. The original train/valid/test source split is
preserved so augmented views of one source photograph cannot leak across
splits.
"""

from __future__ import annotations

import argparse
import ast
import csv
import hashlib
import json
import math
import re
import shutil
from collections import Counter, defaultdict
from pathlib import Path

from PIL import Image


SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_DATA_DIR = SCRIPT_DIR / "data"
DEFAULT_METADATA_DIR = SCRIPT_DIR / "data_sources" / "lettuce_types_v1"
GREEN_ICE_SOURCE_TAG = "green_ice_rf_v1"
NOT_GREEN_ICE_SOURCE_TAG = "not_green_ice_rf_v1"
CLASS_TARGETS = {
    0: ("green ice", "green_ice", GREEN_ICE_SOURCE_TAG),
    1: ("iceberg", "not_green_ice", NOT_GREEN_ICE_SOURCE_TAG),
    2: ("romaine", "not_green_ice", NOT_GREEN_ICE_SOURCE_TAG),
}
TARGET_HEALTH_LABEL = "healthy"
IMAGE_EXTENSIONS = (".jpg", ".jpeg", ".png", ".bmp", ".webp")
SPLIT_MAP = {"train": "train", "valid": "val", "val": "val", "test": "test"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Crop Lettuce Types YOLO boxes for Green Ice binary classification."
    )
    parser.add_argument("source", type=Path, help="Extracted Roboflow dataset directory")
    parser.add_argument(
        "--data-dir",
        type=Path,
        default=DEFAULT_DATA_DIR,
        help=f"CNN data directory (default: {DEFAULT_DATA_DIR})",
    )
    parser.add_argument(
        "--metadata-dir",
        type=Path,
        default=DEFAULT_METADATA_DIR,
        help=f"Provenance output directory (default: {DEFAULT_METADATA_DIR})",
    )
    parser.add_argument(
        "--padding",
        type=float,
        default=0.05,
        help="Padding around each YOLO box as a fraction of box size (default: 0.05)",
    )
    parser.add_argument(
        "--replace",
        action="store_true",
        help="Replace only the generated Green Ice and not-Green-Ice provenance folders",
    )
    parser.add_argument("--dry-run", action="store_true", help="Validate without writing files")
    return parser.parse_args()


def parse_class_names(yaml_path: Path) -> list[str]:
    text = yaml_path.read_text(encoding="utf-8")
    match = re.search(r"(?m)^names:\s*(.+?)\s*$", text)
    if not match:
        raise ValueError(f"Could not find a names list in {yaml_path}")

    names = ast.literal_eval(match.group(1))
    if not isinstance(names, list) or not all(isinstance(name, str) for name in names):
        raise ValueError(f"Unsupported names value in {yaml_path}: {names!r}")
    return names


def source_key(path: Path) -> str:
    """Return the pre-augmentation Roboflow source name."""
    return re.sub(r"\.rf\.[0-9a-f]+$", "", path.stem, flags=re.IGNORECASE)


def find_image(images_dir: Path, stem: str) -> Path:
    matches = [images_dir / f"{stem}{extension}" for extension in IMAGE_EXTENSIONS]
    existing = [path for path in matches if path.exists()]
    if len(existing) != 1:
        raise ValueError(
            f"Expected exactly one image for label {stem!r} in {images_dir}; "
            f"found {len(existing)}"
        )
    return existing[0]


def parse_yolo_label(label_path: Path) -> list[tuple[int, float, float, float, float]]:
    boxes = []
    for line_number, raw_line in enumerate(label_path.read_text(encoding="utf-8").splitlines(), 1):
        if not raw_line.strip():
            continue
        parts = raw_line.split()
        if len(parts) != 5:
            raise ValueError(f"Malformed YOLO row at {label_path}:{line_number}: {raw_line!r}")
        class_id = int(parts[0])
        coordinates = tuple(float(value) for value in parts[1:])
        if any(not 0.0 <= value <= 1.0 for value in coordinates):
            raise ValueError(f"Out-of-range YOLO row at {label_path}:{line_number}")
        boxes.append((class_id, *coordinates))
    return boxes


def crop_box(
    image_width: int,
    image_height: int,
    box: tuple[int, float, float, float, float],
    padding: float,
) -> tuple[int, int, int, int]:
    _, center_x, center_y, width, height = box
    box_width = width * image_width
    box_height = height * image_height
    pad_x = box_width * padding
    pad_y = box_height * padding

    left = max(0, math.floor(center_x * image_width - box_width / 2 - pad_x))
    top = max(0, math.floor(center_y * image_height - box_height / 2 - pad_y))
    right = min(image_width, math.ceil(center_x * image_width + box_width / 2 + pad_x))
    bottom = min(image_height, math.ceil(center_y * image_height + box_height / 2 + pad_y))
    if right - left < 32 or bottom - top < 32:
        raise ValueError(f"Crop is too small: {(left, top, right, bottom)}")
    return left, top, right, bottom


def safe_output_name(source_tag: str, image_path: Path, object_index: int) -> str:
    stem = re.sub(r"[^A-Za-z0-9._-]+", "_", image_path.stem)
    return f"{source_tag}__{stem}__{object_index:02d}.jpg"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def generated_directory(data_dir: Path, split: str, source_tag: str) -> Path:
    return data_dir / split / TARGET_HEALTH_LABEL / "unknown_age" / source_tag


def validate_replace_target(path: Path, data_dir: Path) -> None:
    resolved = path.resolve()
    data_resolved = data_dir.resolve()
    generated_tags = {GREEN_ICE_SOURCE_TAG, NOT_GREEN_ICE_SOURCE_TAG}
    if not generated_tags.intersection(part.lower() for part in resolved.parts) or data_resolved not in resolved.parents:
        raise ValueError(f"Refusing to replace unexpected path: {resolved}")


def main() -> int:
    args = parse_args()
    source = args.source.resolve()
    data_dir = args.data_dir.resolve()
    metadata_dir = args.metadata_dir.resolve()

    if not 0.0 <= args.padding <= 0.5:
        raise ValueError("--padding must be between 0.0 and 0.5")
    if not source.is_dir():
        raise FileNotFoundError(f"Dataset directory does not exist: {source}")

    yaml_path = source / "data.yaml"
    class_names = parse_class_names(yaml_path)
    for class_id, (source_name, _, _) in CLASS_TARGETS.items():
        if len(class_names) <= class_id or class_names[class_id].strip().lower() != source_name:
            raise ValueError(
                f"Expected class {class_id} to be {source_name!r}; found {class_names!r}"
            )

    output_directories = [
        generated_directory(data_dir, split, source_tag)
        for split in ("train", "val", "test")
        for source_tag in (GREEN_ICE_SOURCE_TAG, NOT_GREEN_ICE_SOURCE_TAG)
    ]
    existing_outputs = [path for path in output_directories if path.exists()]
    if existing_outputs and not args.dry_run:
        if not args.replace:
            formatted = "\n  ".join(str(path) for path in existing_outputs)
            raise FileExistsError(
                "Generated variety folders already exist. Use --replace to recreate only these folders:\n"
                f"  {formatted}"
            )
        for path in existing_outputs:
            validate_replace_target(path, data_dir)
            shutil.rmtree(path)

    split_sources: dict[str, set[str]] = defaultdict(set)
    records: list[dict[str, object]] = []
    planned_outputs: set[Path] = set()
    counts: Counter[str] = Counter()

    for source_split, output_split in SPLIT_MAP.items():
        split_dir = source / source_split
        if not split_dir.exists():
            continue
        images_dir = split_dir / "images"
        labels_dir = split_dir / "labels"
        if not images_dir.is_dir() or not labels_dir.is_dir():
            raise FileNotFoundError(f"Missing images/labels directories under {split_dir}")

        for label_path in sorted(labels_dir.glob("*.txt")):
            image_path = find_image(images_dir, label_path.stem)
            target_boxes = [
                (object_index, box)
                for object_index, box in enumerate(parse_yolo_label(label_path))
                if box[0] in CLASS_TARGETS
            ]
            if not target_boxes:
                continue

            key = source_key(image_path)
            split_sources[output_split].add(key)
            with Image.open(image_path) as image:
                image = image.convert("RGB")
                for object_index, box in target_boxes:
                    class_id = box[0]
                    source_class_name, variety_label, source_tag = CLASS_TARGETS[class_id]
                    pixel_box = crop_box(image.width, image.height, box, args.padding)
                    output_path = generated_directory(data_dir, output_split, source_tag) / safe_output_name(
                        source_tag, image_path, object_index
                    )
                    if output_path in planned_outputs:
                        raise ValueError(f"Output collision: {output_path}")
                    planned_outputs.add(output_path)

                    if not args.dry_run:
                        output_path.parent.mkdir(parents=True, exist_ok=True)
                        image.crop(pixel_box).save(output_path, "JPEG", quality=95, optimize=True)

                    records.append(
                        {
                            "source_dataset": "Roboflow Lettuce Types v1",
                            "source_split": source_split,
                            "source_key": key,
                            "source_image": str(image_path.relative_to(source)),
                            "source_label": str(label_path.relative_to(source)),
                            "source_class_id": class_id,
                            "source_class_name": source_class_name,
                            "assigned_variety_label": variety_label,
                            "assigned_health_label": TARGET_HEALTH_LABEL,
                            "age_label": "unknown",
                            "bbox_center_x": box[1],
                            "bbox_center_y": box[2],
                            "bbox_width": box[3],
                            "bbox_height": box[4],
                            "crop_left": pixel_box[0],
                            "crop_top": pixel_box[1],
                            "crop_right": pixel_box[2],
                            "crop_bottom": pixel_box[3],
                            "output_split": output_split,
                            "output_path": str(output_path.relative_to(SCRIPT_DIR)),
                            "sha256": "" if args.dry_run else sha256(output_path),
                        }
                    )
                    counts[(output_split, variety_label)] += 1

    split_items = list(split_sources.items())
    for left_index, (left_name, left_sources) in enumerate(split_items):
        for right_name, right_sources in split_items[left_index + 1:]:
            overlap = sorted(left_sources & right_sources)
            if overlap:
                raise ValueError(
                    f"Source leakage between {left_name} and {right_name}: {overlap[:10]}"
                )

    if not records:
        raise ValueError("No supported lettuce-variety annotations were found")

    if args.dry_run:
        print("Dry run passed; no files were written.")
    else:
        metadata_dir.mkdir(parents=True, exist_ok=True)
        manifest_path = metadata_dir / "binary_variety_manifest.csv"
        with manifest_path.open("w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(handle, fieldnames=list(records[0]))
            writer.writeheader()
            writer.writerows(records)

        for name in ("data.yaml", "README.dataset.txt", "README.roboflow.txt"):
            source_file = source / name
            if source_file.exists():
                shutil.copy2(source_file, metadata_dir / name)

        summary = {
            "source": str(source),
            "source_url": "https://universe.roboflow.com/lettuce-classification/lettuce-types/dataset/1",
            "license": "CC BY 4.0",
            "class_mapping": {
                str(class_id): {
                    "source_name": source_name,
                    "target": target_name,
                    "source_tag": source_tag,
                }
                for class_id, (source_name, target_name, source_tag) in CLASS_TARGETS.items()
            },
            "assigned_health_label": TARGET_HEALTH_LABEL,
            "age_label": "unknown",
            "padding_fraction": args.padding,
            "counts": {
                split: {
                    label: counts[(split, label)]
                    for label in ("green_ice", "not_green_ice")
                }
                for split in ("train", "val", "test")
            },
            "unique_sources": {
                split: len(keys) for split, keys in sorted(split_sources.items())
            },
            "cross_split_source_overlap": 0,
        }
        (metadata_dir / "import_summary.json").write_text(
            json.dumps(summary, indent=2) + "\n", encoding="utf-8"
        )

    print(f"Binary variety crops: {sum(counts.values())}")
    for split in ("train", "val", "test"):
        print(
            f"  {split}: green_ice={counts[(split, 'green_ice')]}, "
            f"not_green_ice={counts[(split, 'not_green_ice')]} crops from "
            f"{len(split_sources[split])} original source images"
        )
    print("Cross-split original-source overlap: 0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
