import os
import shutil

SOURCE_ROOT = r"C:\Users\akshi\Downloads\lettucenpk\Lettuce-NPK-YoloV9"
DEST_ROOT = r"C:\Dev\GLORIOUSSYSTEM\cnn-training\data"

# Class index -> our target class name
CLASS_MAP = {
    0: "deficient",  # Calcium Deficiency
    1: "healthy",    # Healthy
    2: "deficient",  # Magnesium Deficiency
    3: "deficient",  # Nitrogen Deficiency
    4: "deficient",  # Phosphorus Deficiency
    5: "deficient",  # Potassium Deficiency
}

# YOLO folder name -> our folder name
SPLIT_MAP = {
    "train": "train",
    "valid": "val",
}

counts = {"train": {"healthy": 0, "deficient": 0}, "val": {"healthy": 0, "deficient": 0}}

for yolo_split, our_split in SPLIT_MAP.items():
    images_dir = os.path.join(SOURCE_ROOT, yolo_split, "images")
    labels_dir = os.path.join(SOURCE_ROOT, yolo_split, "labels")

    if not os.path.exists(images_dir):
        print(f"Skipping {yolo_split} - folder not found")
        continue

    for label_file in os.listdir(labels_dir):
        if not label_file.endswith(".txt"):
            continue

        label_path = os.path.join(labels_dir, label_file)
        with open(label_path, "r") as f:
            first_line = f.readline().strip()
            if not first_line:
                continue
            class_index = int(first_line.split()[0])

        target_class = CLASS_MAP.get(class_index)
        if target_class is None:
            continue

        image_name = label_file.replace(".txt", ".jpg")
        image_path = os.path.join(images_dir, image_name)
        if not os.path.exists(image_path):
            image_path = image_path.replace(".jpg", ".png")
            if not os.path.exists(image_path):
                continue

        dest_dir = os.path.join(DEST_ROOT, our_split, target_class)
        os.makedirs(dest_dir, exist_ok=True)
        shutil.copy(image_path, dest_dir)
        counts[our_split][target_class] += 1

print("Done. Copied:")
for split, classes in counts.items():
    for cls, n in classes.items():
        print(f"  {split}/{cls}: {n} images")