"""Large independent Iceberg stress test for Green Ice false positives.

The GHAI dataset contains 500 field images and 2,476 Iceberg bounding boxes.
Because it has no Green Ice positives, this is intentionally reported as a
negative-class specificity/false-positive stress test, never as balanced
binary accuracy or Green Ice recall.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
from pathlib import Path

import numpy as np
import onnxruntime as ort
from PIL import Image


IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}
MEAN = np.asarray([0.485, 0.456, 0.406], dtype=np.float32)
STD = np.asarray([0.229, 0.224, 0.225], dtype=np.float32)
VARIETY_CLASSES = ["green_ice", "not_green_ice"]
LETTUCE_CLASSES = ["non_lettuce", "lettuce"]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--training-data", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--batch-size", type=int, default=64)
    parser.add_argument("--padding", type=float, default=0.05)
    parser.add_argument("--duplicate-threshold", type=int, default=4)
    parser.add_argument("--max-sources", type=int, default=0)
    return parser.parse_args()


def dhash(image: Image.Image) -> int:
    gray = image.convert("L").resize((9, 8), Image.Resampling.BILINEAR)
    pixels = np.asarray(gray, dtype=np.int16)
    bits = pixels[:, 1:] > pixels[:, :-1]
    value = 0
    for bit in bits.flat:
        value = (value << 1) | int(bit)
    return value


def min_hamming(value: int, references: list[int]) -> int:
    return min((value ^ reference).bit_count() for reference in references)


def preprocess(image: Image.Image) -> np.ndarray:
    rgb = image.convert("RGB").resize((224, 224), Image.Resampling.BILINEAR)
    array = np.asarray(rgb, dtype=np.float32) / 255.0
    return ((array - MEAN) / STD).transpose(2, 0, 1)


def softmax(logits: np.ndarray) -> np.ndarray:
    shifted = logits - logits.max(axis=1, keepdims=True)
    exp = np.exp(shifted)
    return exp / exp.sum(axis=1, keepdims=True)


def crop_bbox(image: Image.Image, bbox: list[float], padding: float) -> Image.Image:
    x, y, width, height = [float(value) for value in bbox]
    pad_x = width * padding
    pad_y = height * padding
    left = max(0, int(np.floor(x - pad_x)))
    top = max(0, int(np.floor(y - pad_y)))
    right = min(image.width, int(np.ceil(x + width + pad_x)))
    bottom = min(image.height, int(np.ceil(y + height + pad_y)))
    if right - left < 32 or bottom - top < 32:
        raise ValueError(f"Invalid crop: {(left, top, right, bottom)} from {bbox}")
    return image.crop((left, top, right, bottom))


def load_training_hashes(training_data: Path) -> list[int]:
    hashes = []
    for path in sorted(training_data.rglob("*")):
        if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS:
            try:
                with Image.open(path) as image:
                    hashes.append(dhash(image))
            except OSError:
                continue
    if not hashes:
        raise RuntimeError(f"No training images found under {training_data}")
    return hashes


def main() -> None:
    args = parse_args()
    if args.batch_size < 1:
        raise ValueError("--batch-size must be positive")
    if not 0 <= args.padding <= 0.5:
        raise ValueError("--padding must be between 0 and 0.5")

    try:
        from datasets import load_dataset
    except ImportError as error:
        raise RuntimeError("Install the Hugging Face datasets package first") from error

    training_hashes = load_training_hashes(args.training_data.resolve())
    print(f"Duplicate audit reference: {len(training_hashes)} training images")

    session = ort.InferenceSession(str(args.model.resolve()), providers=["CPUExecutionProvider"])
    input_name = session.get_inputs()[0].name
    output_names = {output.name for output in session.get_outputs()}
    required = {"lettuce_logits", "variety_logits"}
    if not required.issubset(output_names):
        raise RuntimeError(f"Missing ONNX outputs: {sorted(required - output_names)}")

    dataset = load_dataset(
        "Project-AgML/ghai_iceberg_lettuce_detection",
        split="train",
    )

    pending: list[tuple[int, int, Image.Image, int, str]] = []
    predictions: list[dict[str, object]] = []
    excluded_duplicates = 0
    excluded_invalid_crops = 0
    source_count = 0

    def evaluate_pending() -> None:
        nonlocal pending
        if not pending:
            return
        batch = np.stack([preprocess(item[2]) for item in pending])
        lettuce_logits, variety_logits = session.run(
            ["lettuce_logits", "variety_logits"],
            {input_name: batch},
        )
        lettuce_probs = softmax(lettuce_logits)
        variety_probs = softmax(variety_logits)
        for row_index, (source_index, box_index, _, distance, digest) in enumerate(pending):
            lettuce_index = int(lettuce_probs[row_index].argmax())
            variety_index = int(variety_probs[row_index].argmax())
            predictions.append(
                {
                    "source_index": source_index,
                    "box_index": box_index,
                    "expected": "not_green_ice",
                    "predicted": VARIETY_CLASSES[variety_index],
                    "green_ice_probability": float(variety_probs[row_index, 0]),
                    "not_green_ice_probability": float(variety_probs[row_index, 1]),
                    "lettuce_prediction": LETTUCE_CLASSES[lettuce_index],
                    "lettuce_confidence": float(lettuce_probs[row_index, lettuce_index]),
                    "min_training_dhash_distance": distance,
                    "crop_sha256": digest,
                }
            )
        pending = []

    for source_index, sample in enumerate(dataset):
        if args.max_sources and source_index >= args.max_sources:
            break
        source_count += 1
        image = sample["image"].convert("RGB")
        objects = sample["objects"]
        boxes = objects["bbox"] if isinstance(objects, dict) else objects
        for box_index, bbox in enumerate(boxes):
            try:
                crop = crop_bbox(image, bbox, args.padding)
            except ValueError:
                excluded_invalid_crops += 1
                continue
            crop_hash = dhash(crop)
            distance = min_hamming(crop_hash, training_hashes)
            if distance <= args.duplicate_threshold:
                excluded_duplicates += 1
                continue
            buffer = io.BytesIO()
            crop.save(buffer, "JPEG", quality=95)
            digest = hashlib.sha256(buffer.getvalue()).hexdigest()
            pending.append((source_index, box_index, crop, distance, digest))
            if len(pending) >= args.batch_size:
                evaluate_pending()
        if source_count % 50 == 0:
            print(
                f"Processed {source_count} source images; "
                f"evaluated {len(predictions)} crops; excluded {excluded_duplicates} near-duplicates"
            )
    evaluate_pending()

    if not predictions:
        raise RuntimeError("No independent Iceberg crops remained after duplicate auditing")

    false_positives = [row for row in predictions if row["predicted"] == "green_ice"]
    high_confidence_false_positives = [
        row for row in false_positives if float(row["green_ice_probability"]) >= 0.90
    ]
    lettuce_detected = [row for row in predictions if row["lettuce_prediction"] == "lettuce"]

    by_source: dict[int, list[dict[str, object]]] = {}
    for row in predictions:
        by_source.setdefault(int(row["source_index"]), []).append(row)
    source_majority_green = 0
    sources_with_any_green = 0
    for rows in by_source.values():
        green_votes = sum(row["predicted"] == "green_ice" for row in rows)
        source_majority_green += green_votes > len(rows) / 2
        sources_with_any_green += green_votes > 0

    report = {
        "dataset": "Project-AgML/ghai_iceberg_lettuce_detection",
        "dataset_url": "https://huggingface.co/datasets/Project-AgML/ghai_iceberg_lettuce_detection",
        "purpose": "negative-only not-Green-Ice specificity stress test",
        "model": str(args.model.resolve()),
        "source_images": source_count,
        "evaluated_crops": len(predictions),
        "excluded_near_duplicates_dhash_le_4": excluded_duplicates,
        "excluded_invalid_crops": excluded_invalid_crops,
        "training_reference_images": len(training_hashes),
        "not_green_ice_specificity": 1 - len(false_positives) / len(predictions),
        "green_ice_false_positive_rate": len(false_positives) / len(predictions),
        "green_ice_false_positives": len(false_positives),
        "high_confidence_green_ice_false_positives": len(high_confidence_false_positives),
        "mean_green_ice_probability": float(
            np.mean([row["green_ice_probability"] for row in predictions])
        ),
        "lettuce_detection_recall": len(lettuce_detected) / len(predictions),
        "source_grouped": {
            "sources": len(by_source),
            "not_green_ice_majority_specificity": 1 - source_majority_green / len(by_source),
            "sources_with_any_green_ice_false_positive": sources_with_any_green,
        },
        "limitations": (
            "This dataset contains only Iceberg negatives. It measures false positives/specificity, "
            "not Green Ice recall or balanced binary accuracy."
        ),
    }

    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    with output.with_suffix(".csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(predictions[0]))
        writer.writeheader()
        writer.writerows(predictions)

    print(json.dumps(report, indent=2))
    print(f"Predictions: {output.with_suffix('.csv')}")


if __name__ == "__main__":
    main()
