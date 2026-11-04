"""Build a leakage-audited, source-held-out benchmark for the multitask CNN."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import shutil
from collections import Counter
from pathlib import Path

from PIL import Image


IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}
ROBOFLOW_SUFFIX = re.compile(r"\.rf\.[0-9a-f]{32}$", re.IGNORECASE)


def parse_args() -> argparse.Namespace:
    script_dir = Path(__file__).resolve().parent
    downloads = Path.home() / "Downloads"
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--training-data",
        type=Path,
        default=script_dir / "data",
        help="Model data directory containing train/ and val/.",
    )
    parser.add_argument(
        "--lettuce-types",
        type=Path,
        default=downloads / "Lettuce Types.v1i.yolov8",
    )
    parser.add_argument(
        "--lettuce-or-not",
        type=Path,
        default=downloads / "Lettuce or Not.v1i.folder",
    )
    parser.add_argument(
        "--lettuce-disease",
        type=Path,
        default=downloads / "lettucedisease" / "lettuce",
    )
    parser.add_argument(
        "--fnnpk",
        type=Path,
        default=downloads / "archive (3)" / "FNNPK",
        help="Independent nutrient-deficiency folders (-K, -N, -P).",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=script_dir.parent / "colab-package" / "GLORIOUS_REAL_TEST",
    )
    return parser.parse_args()


def image_files(root: Path) -> list[Path]:
    if not root.is_dir():
        return []
    return sorted(
        path
        for path in root.rglob("*")
        if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS
    )


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def dhash(path: Path) -> int:
    with Image.open(path) as image:
        pixels = list(image.convert("L").resize((9, 8)).getdata())
    value = 0
    for row in range(8):
        for column in range(8):
            value = (value << 1) | int(
                pixels[row * 9 + column] > pixels[row * 9 + column + 1]
            )
    return value


def roboflow_source_key(path: Path) -> str:
    return ROBOFLOW_SUFFIX.sub("", path.stem).lower()


def yolo_class(label_path: Path) -> int:
    classes = {
        int(line.split()[0])
        for line in label_path.read_text(encoding="utf-8").splitlines()
        if line.strip()
    }
    if len(classes) != 1:
        raise ValueError(f"Expected one class in {label_path}, found {sorted(classes)}")
    return classes.pop()


def main() -> None:
    args = parse_args()
    roots = [
        args.training_data,
        args.lettuce_types,
        args.lettuce_or_not,
        args.lettuce_disease,
        args.fnnpk,
    ]
    missing = [str(root) for root in roots if not root.is_dir()]
    if missing:
        raise FileNotFoundError("Missing required directories: " + ", ".join(missing))

    output = args.output.resolve()
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True)

    training_paths = image_files(args.training_data / "train") + image_files(
        args.training_data / "val"
    )
    print(f"Fingerprinting {len(training_paths)} training/validation images...")
    training_sha = {sha256(path) for path in training_paths}
    training_dhashes = [dhash(path) for path in training_paths]

    candidates: list[dict[str, object]] = []

    # Variety: use the untouched original Roboflow test split, including negative
    # varieties. Class 0 is Green Ice; Iceberg and Romaine are other lettuce.
    variety_split_keys: dict[str, set[str]] = {}
    for split in ("train", "valid", "test"):
        variety_split_keys[split] = {
            roboflow_source_key(path)
            for path in image_files(args.lettuce_types / split / "images")
        }
    variety_forbidden = variety_split_keys["train"] | variety_split_keys["valid"]
    for path in image_files(args.lettuce_types / "test" / "images"):
        label_path = args.lettuce_types / "test" / "labels" / f"{path.stem}.txt"
        class_id = yolo_class(label_path)
        candidates.append(
            {
                "task": "variety",
                "expected": "green_ice" if class_id == 0 else "not_green_ice",
                "path": path,
                "source_key": roboflow_source_key(path),
                "source_overlap": roboflow_source_key(path) in variety_forbidden,
            }
        )

    # Lettuce detection: use the folder dataset's untouched test split. Remove
    # any original-source key that Roboflow also placed in train or validation.
    lettuce_split_keys: dict[str, set[str]] = {}
    for split in ("train", "valid", "test"):
        lettuce_split_keys[split] = {
            roboflow_source_key(path)
            for path in image_files(args.lettuce_or_not / split)
        }
    lettuce_forbidden = lettuce_split_keys["train"] | lettuce_split_keys["valid"]
    for folder, expected in (("lettuce", "lettuce"), ("not lettuce", "non_lettuce")):
        for path in image_files(args.lettuce_or_not / "test" / folder):
            candidates.append(
                {
                    "task": "lettuce",
                    "expected": expected,
                    "path": path,
                    "source_key": roboflow_source_key(path),
                    "source_overlap": roboflow_source_key(path) in lettuce_forbidden,
                }
            )

    # Health: this dataset has a dedicated test split. Bacterial and fungal are
    # both mapped to the trained model's broader diseased class.
    for folder, expected in (
        ("Bacterial", "diseased"),
        ("fungal", "diseased"),
        ("healthy", "healthy"),
    ):
        for path in image_files(args.lettuce_disease / "test" / folder):
            candidates.append(
                {
                    "task": "health",
                    "expected": expected,
                    "path": path,
                    "source_key": path.stem.lower(),
                    "source_overlap": False,
                }
            )

    # Independent nutrient-deficiency collection. Only the unambiguous missing
    # nutrient folders are used; the meaning of the small FN folder is not assumed.
    for folder in ("-K", "-N", "-P"):
        for path in image_files(args.fnnpk / folder):
            candidates.append(
                {
                    "task": "health",
                    "expected": "deficient",
                    "path": path,
                    "source_key": f"fnnpk:{folder}:{path.stem.lower()}",
                    "source_overlap": False,
                }
            )

    rows: list[dict[str, object]] = []
    excluded = Counter()
    included = Counter()
    near_duplicate_counts = Counter()

    print(f"Auditing {len(candidates)} candidate test images...")
    for index, candidate in enumerate(candidates, 1):
        path = candidate["path"]
        assert isinstance(path, Path)
        digest = sha256(path)
        test_dhash = dhash(path)
        min_distance = min((test_dhash ^ known).bit_count() for known in training_dhashes)
        exact_overlap = digest in training_sha
        source_overlap = bool(candidate["source_overlap"])
        exclusion_reason = ""
        if exact_overlap:
            exclusion_reason = "exact_sha256_training_overlap"
        elif source_overlap:
            exclusion_reason = "original_source_key_training_overlap"
        elif min_distance <= 4:
            exclusion_reason = "near_duplicate_dhash_distance_le_4"

        if min_distance <= 4:
            near_duplicate_counts[str(candidate["task"])] += 1

        relative_output = ""
        if exclusion_reason:
            excluded[f"{candidate['task']}:{exclusion_reason}"] += 1
        else:
            task = str(candidate["task"])
            expected = str(candidate["expected"])
            destination = output / task / expected / f"{index:04d}__{path.name}"
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, destination)
            relative_output = destination.relative_to(output).as_posix()
            included[f"{task}:{expected}"] += 1

        rows.append(
            {
                "task": candidate["task"],
                "expected": candidate["expected"],
                "source_path": str(path),
                "source_key": candidate["source_key"],
                "sha256": digest,
                "dhash64": f"{test_dhash:016x}",
                "min_training_dhash_distance": min_distance,
                "near_duplicate_warning": min_distance <= 4,
                "exact_training_overlap": exact_overlap,
                "source_key_training_overlap": source_overlap,
                "excluded": bool(exclusion_reason),
                "exclusion_reason": exclusion_reason,
                "relative_output": relative_output,
            }
        )

    manifest_path = output / "manifest.csv"
    with manifest_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)

    summary = {
        "method": {
            "training_overlap": "SHA-256 equality against every train/val image",
            "source_overlap": "Roboflow filename before .rf.<hash> across source splits",
            "near_duplicate_exclusion": "64-bit dHash Hamming distance <= 4",
            "model_selection_warning": "No training or validation images are included",
        },
        "training_validation_images_fingerprinted": len(training_paths),
        "candidate_images": len(candidates),
        "included": dict(sorted(included.items())),
        "excluded": dict(sorted(excluded.items())),
        "near_duplicate_warnings": dict(sorted(near_duplicate_counts.items())),
        "variety_source_keys": {
            split: len(keys) for split, keys in variety_split_keys.items()
        },
        "lettuce_source_keys": {
            split: len(keys) for split, keys in lettuce_split_keys.items()
        },
        "deficient_test_status": (
            "tested from the separate FNNPK -K/-N/-P collection; the downloaded YOLO "
            "NPK validation split was not reused"
        ),
    }
    (output / "audit_summary.json").write_text(
        json.dumps(summary, indent=2), encoding="utf-8"
    )
    print(json.dumps(summary, indent=2))
    print(f"Prepared benchmark: {output}")


if __name__ == "__main__":
    main()
