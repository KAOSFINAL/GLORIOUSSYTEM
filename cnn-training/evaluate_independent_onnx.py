"""Evaluate the deployed ONNX model on the leakage-audited benchmark."""

from __future__ import annotations

import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path

import numpy as np
import onnxruntime as ort
from PIL import Image


CLASSES = {
    "lettuce": ["non_lettuce", "lettuce"],
    "variety": ["green_ice", "not_green_ice"],
    "health": ["deficient", "diseased", "healthy"],
}
OUTPUT_NAMES = {
    "lettuce": "lettuce_logits",
    "variety": "variety_logits",
    "health": "health_logits",
}
IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}
BENCHMARK_FOLDER_ALIASES = {
    ("variety", "not_green_ice"): ("not_green_ice", "other_lettuce"),
}
MEAN = np.asarray([0.485, 0.456, 0.406], dtype=np.float32)
STD = np.asarray([0.229, 0.224, 0.225], dtype=np.float32)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--benchmark", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--batch-size", type=int, default=32)
    return parser.parse_args()


def preprocess(path: Path) -> np.ndarray:
    with Image.open(path) as image:
        rgb = image.convert("RGB").resize((224, 224), Image.Resampling.BILINEAR)
        array = np.asarray(rgb, dtype=np.float32) / 255.0
    return ((array - MEAN) / STD).transpose(2, 0, 1)


def softmax(logits: np.ndarray) -> np.ndarray:
    shifted = logits - logits.max(axis=1, keepdims=True)
    exp = np.exp(shifted)
    return exp / exp.sum(axis=1, keepdims=True)


def metric_report(expected: list[str], predicted: list[str], confidence: list[float], classes: list[str]) -> dict:
    matrix = [[0 for _ in classes] for _ in classes]
    for truth, guess in zip(expected, predicted):
        matrix[classes.index(truth)][classes.index(guess)] += 1

    per_class = {}
    f1_scores = []
    supported_classes = []
    for index, label in enumerate(classes):
        tp = matrix[index][index]
        fp = sum(matrix[row][index] for row in range(len(classes))) - tp
        fn = sum(matrix[index]) - tp
        precision = tp / (tp + fp) if tp + fp else 0.0
        recall = tp / (tp + fn) if tp + fn else 0.0
        f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0.0
        if sum(matrix[index]) > 0:
            f1_scores.append(f1)
            supported_classes.append(label)
        per_class[label] = {
            "support": sum(matrix[index]),
            "precision": precision,
            "recall": recall,
            "f1": f1,
        }

    correct = [truth == guess for truth, guess in zip(expected, predicted)]
    accepted = [i for i, value in enumerate(confidence) if value >= 0.70]
    return {
        "samples": len(expected),
        "accuracy": sum(correct) / len(correct) if correct else 0.0,
        "macro_f1": sum(f1_scores) / len(f1_scores) if f1_scores else 0.0,
        "status": "complete" if len(supported_classes) == len(classes) else "partial_missing_classes",
        "supported_classes": supported_classes,
        "missing_classes": [label for label in classes if label not in supported_classes],
        "mean_confidence": float(np.mean(confidence)) if confidence else 0.0,
        "high_confidence_errors": sum(
            not correct[i] and confidence[i] >= 0.90 for i in range(len(correct))
        ),
        "at_70_percent_threshold": {
            "coverage": len(accepted) / len(expected) if expected else 0.0,
            "accuracy": (
                sum(correct[i] for i in accepted) / len(accepted) if accepted else 0.0
            ),
        },
        "classes": classes,
        "confusion_expected_rows_predicted_columns": matrix,
        "per_class": per_class,
    }


def main() -> None:
    args = parse_args()
    benchmark = args.benchmark.resolve()
    manifest_rows = list(csv.DictReader((benchmark / "manifest.csv").open(encoding="utf-8")))
    included_manifest = {
        row["relative_output"]: row
        for row in manifest_rows
        if row["excluded"].lower() == "false" and row["relative_output"]
    }

    samples: list[tuple[str, str, Path, str]] = []
    for task, classes in CLASSES.items():
        for expected in classes:
            folder_names = BENCHMARK_FOLDER_ALIASES.get((task, expected), (expected,))
            for folder_name in folder_names:
                class_dir = benchmark / task / folder_name
                if not class_dir.is_dir():
                    continue
                for path in sorted(class_dir.iterdir()):
                    if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS:
                        relative = path.relative_to(benchmark).as_posix()
                        samples.append((task, expected, path, relative))
    if not samples:
        raise RuntimeError(f"No benchmark images found in {benchmark}")

    session = ort.InferenceSession(str(args.model), providers=["CPUExecutionProvider"])
    input_name = session.get_inputs()[0].name
    available_outputs = {output.name for output in session.get_outputs()}
    missing_outputs = set(OUTPUT_NAMES.values()) - available_outputs
    if missing_outputs:
        raise RuntimeError(f"ONNX model is missing outputs: {sorted(missing_outputs)}")

    predictions: list[dict[str, object]] = []
    for offset in range(0, len(samples), args.batch_size):
        batch_samples = samples[offset : offset + args.batch_size]
        batch = np.stack([preprocess(path) for _, _, path, _ in batch_samples])
        outputs = session.run(list(OUTPUT_NAMES.values()), {input_name: batch})
        probabilities = {
            task: softmax(outputs[index]) for index, task in enumerate(OUTPUT_NAMES)
        }
        for row_index, (task, expected, path, relative) in enumerate(batch_samples):
            task_probs = probabilities[task][row_index]
            predicted_index = int(task_probs.argmax())
            predicted = CLASSES[task][predicted_index]
            lettuce_probs = probabilities["lettuce"][row_index]
            lettuce_predicted = CLASSES["lettuce"][int(lettuce_probs.argmax())]
            conditional_task = task in {"variety", "health"}
            end_to_end_correct = predicted == expected and (
                not conditional_task or lettuce_predicted == "lettuce"
            )
            manifest = included_manifest[relative]
            predictions.append(
                {
                    "task": task,
                    "expected": expected,
                    "predicted": predicted,
                    "correct": predicted == expected,
                    "confidence": float(task_probs[predicted_index]),
                    "lettuce_prediction": lettuce_predicted,
                    "lettuce_confidence": float(lettuce_probs.max()),
                    "end_to_end_correct": end_to_end_correct,
                    "source_key": manifest["source_key"],
                    "near_duplicate_warning": manifest["near_duplicate_warning"],
                    "min_training_dhash_distance": int(manifest["min_training_dhash_distance"]),
                    "file": relative,
                }
            )

    report: dict[str, object] = {
        "model": str(args.model),
        "benchmark": str(benchmark),
        "audit": json.loads((benchmark / "audit_summary.json").read_text(encoding="utf-8")),
        "tasks": {},
    }
    for task, classes in CLASSES.items():
        rows = [row for row in predictions if row["task"] == task]
        if not rows:
            report["tasks"][task] = {"status": "not_independently_testable"}
            continue
        task_report = metric_report(
            [str(row["expected"]) for row in rows],
            [str(row["predicted"]) for row in rows],
            [float(row["confidence"]) for row in rows],
            classes,
        )
        if task in {"variety", "health"}:
            task_report["end_to_end_accuracy_with_lettuce_gate"] = sum(
                bool(row["end_to_end_correct"]) for row in rows
            ) / len(rows)
        report["tasks"][task] = task_report

    # Source-grouped results prevent Roboflow augmentations of one source photo
    # from being mistaken for independent samples.
    grouped: dict[tuple[str, str, str], list[dict[str, object]]] = defaultdict(list)
    for row in predictions:
        grouped[(str(row["task"]), str(row["expected"]), str(row["source_key"]))].append(row)
    source_rows = []
    for (task, expected, source_key), rows in grouped.items():
        vote_counts = defaultdict(int)
        for row in rows:
            vote_counts[str(row["predicted"])] += 1
        predicted = sorted(vote_counts, key=lambda label: (-vote_counts[label], label))[0]
        source_rows.append(
            {
                "task": task,
                "expected": expected,
                "predicted": predicted,
                "source_key": source_key,
                "images": len(rows),
                "correct": predicted == expected,
            }
        )
    report["source_grouped"] = {}
    for task, classes in CLASSES.items():
        rows = [row for row in source_rows if row["task"] == task]
        if rows:
            report["source_grouped"][task] = metric_report(
                [str(row["expected"]) for row in rows],
                [str(row["predicted"]) for row in rows],
                [1.0 for _ in rows],
                classes,
            )

    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    csv_path = output.with_suffix(".csv")
    with csv_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(predictions[0]))
        writer.writeheader()
        writer.writerows(predictions)

    print("Independent ONNX benchmark")
    print("=" * 72)
    for task, task_report in report["tasks"].items():
        if "accuracy" not in task_report:
            print(f"{task:8}: not independently testable")
            continue
        print(
            f"{task:8}: accuracy={task_report['accuracy']:.1%}, "
            f"macro_f1={task_report['macro_f1']:.1%}, "
            f"samples={task_report['samples']}"
        )
        if "end_to_end_accuracy_with_lettuce_gate" in task_report:
            print(
                " " * 10
                + f"end-to-end with lettuce gate={task_report['end_to_end_accuracy_with_lettuce_gate']:.1%}"
            )
        print(
            " " * 10
            + f"confusion={task_report['confusion_expected_rows_predicted_columns']}"
        )
    print(f"JSON report: {output}")
    print(f"Predictions: {csv_path}")


if __name__ == "__main__":
    main()
