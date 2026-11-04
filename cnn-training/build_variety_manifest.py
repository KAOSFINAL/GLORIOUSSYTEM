"""Create reproducible Green Ice versus not-Green-Ice labels.

Only cultivar-explicit Roboflow crops receive a binary variety label. Legacy
lettuce images organized only by health keep no variety label, preventing
unknown cultivars from silently becoming negative training examples.
"""

from __future__ import annotations

import argparse
import csv
import json
import random
from collections import Counter, defaultdict
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps

from train_multitask import (
    DATA_DIR,
    GREEN_ICE_SOURCE_TAG,
    NOT_GREEN_ICE_SOURCE_TAG,
    HEALTH_CLASSES,
    LETTUCE_CLASSES,
    VARIETY_CLASSES,
    MultiTaskDataset,
)


SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_OUTPUT = SCRIPT_DIR / "data_sources" / "variety_labels.csv"
DEFAULT_SUMMARY = SCRIPT_DIR / "data_sources" / "variety_label_summary.json"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build explicit Green Ice versus not-Green-Ice labels without moving images."
    )
    parser.add_argument("--data-dir", type=Path, default=DATA_DIR)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--summary", type=Path, default=DEFAULT_SUMMARY)
    parser.add_argument(
        "--audit-output",
        type=Path,
        help="Optional contact sheet with deterministic samples for visual review.",
    )
    parser.add_argument("--samples-per-group", type=int, default=12)
    parser.add_argument("--seed", type=int, default=20261013)
    return parser.parse_args()


def label_name(classes: list[str], index: int) -> str:
    return classes[index] if index >= 0 else ""


def variety_source(path: Path, variety_index: int) -> str:
    if variety_index < 0:
        return ""
    if GREEN_ICE_SOURCE_TAG in {part.lower() for part in path.parts}:
        return "explicit_green_ice_provenance"
    if NOT_GREEN_ICE_SOURCE_TAG in {part.lower() for part in path.parts}:
        return "explicit_not_green_ice_provenance"
    return ""


def collect_rows(data_dir: Path) -> tuple[list[dict[str, str]], dict[str, list[Path]]]:
    rows: list[dict[str, str]] = []
    audit_groups: dict[str, list[Path]] = defaultdict(list)

    for split in ("train", "val", "test"):
        split_dir = data_dir / split
        if not split_dir.exists():
            continue

        dataset = MultiTaskDataset(split_dir)
        for image_name, lettuce_index, health_index, variety_index in dataset.samples:
            image_path = Path(image_name)
            relative_path = image_path.resolve().relative_to(data_dir.resolve())
            lettuce_label = label_name(LETTUCE_CLASSES, lettuce_index)
            health_label = label_name(HEALTH_CLASSES, health_index)
            variety_label = label_name(VARIETY_CLASSES, variety_index)
            source = variety_source(image_path, variety_index)

            rows.append(
                {
                    "split": split,
                    "relative_path": relative_path.as_posix(),
                    "lettuce_label": lettuce_label,
                    "health_label": health_label,
                    "variety_label": variety_label,
                    "variety_label_source": source,
                    "requires_expert_review": "no",
                }
            )

            if split == "train" and variety_label:
                group = (
                    "green_ice"
                    if variety_label == "green_ice"
                    else "not_green_ice"
                )
                audit_groups[group].append(image_path)

    rows.sort(key=lambda row: (row["split"], row["relative_path"]))
    return rows, audit_groups


def write_contact_sheet(
    groups: dict[str, list[Path]],
    destination: Path,
    samples_per_group: int,
    seed: int,
) -> None:
    randomizer = random.Random(seed)
    selected: list[tuple[str, Path]] = []
    for group in sorted(groups):
        candidates = sorted(groups[group])
        count = min(samples_per_group, len(candidates))
        selected.extend((group, path) for path in randomizer.sample(candidates, count))

    columns = 6
    tile_width = 180
    image_height = 150
    label_height = 30
    rows = max(1, (len(selected) + columns - 1) // columns)
    sheet = Image.new("RGB", (columns * tile_width, rows * (image_height + label_height)), "white")
    draw = ImageDraw.Draw(sheet)

    for index, (group, image_path) in enumerate(selected):
        left = (index % columns) * tile_width
        top = (index // columns) * (image_height + label_height)
        with Image.open(image_path) as source:
            tile = ImageOps.fit(source.convert("RGB"), (tile_width, image_height))
        sheet.paste(tile, (left, top))
        draw.rectangle(
            (left, top + image_height, left + tile_width, top + image_height + label_height),
            fill="#101914",
        )
        draw.text((left + 5, top + image_height + 7), group, fill="white")

    destination.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(destination, "JPEG", quality=90, optimize=True)


def main() -> int:
    args = parse_args()
    data_dir = args.data_dir.resolve()
    if not data_dir.is_dir():
        raise FileNotFoundError(f"Dataset directory does not exist: {data_dir}")
    if args.samples_per_group < 1:
        raise ValueError("--samples-per-group must be at least 1")

    rows, audit_groups = collect_rows(data_dir)
    if not rows:
        raise ValueError(f"No images were found under {data_dir}")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)

    counts = Counter(
        (row["split"], row["variety_label"] or "unlabelled")
        for row in rows
    )
    summary = {
        "scheme": {
            "green_ice": f"explicit folder provenance: {GREEN_ICE_SOURCE_TAG}",
            "not_green_ice": (
                "explicit Iceberg/Romaine provenance merged under "
                f"{NOT_GREEN_ICE_SOURCE_TAG}"
            ),
            "unlabelled": "non-lettuce or lettuce without explicit cultivar provenance",
        },
        "counts": {
            split: {
                label: counts[(split, label)]
                for label in ("green_ice", "not_green_ice", "unlabelled")
            }
            for split in ("train", "val", "test")
        },
        "expert_review_required": sum(
            row["requires_expert_review"] == "yes" for row in rows
        ),
        "total_images": len(rows),
    }
    args.summary.parent.mkdir(parents=True, exist_ok=True)
    args.summary.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    if args.audit_output:
        write_contact_sheet(
            audit_groups,
            args.audit_output,
            args.samples_per_group,
            args.seed,
        )

    print(f"Wrote {len(rows)} labels to {args.output}")
    print(json.dumps(summary["counts"], indent=2))
    if args.audit_output:
        print(f"Wrote visual audit sheet to {args.audit_output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
