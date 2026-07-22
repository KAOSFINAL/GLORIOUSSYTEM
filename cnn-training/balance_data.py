import os
import random

random.seed(42)

TARGET_TRAIN = 500
TARGET_VAL = 100

def trim_folder(folder, target_count):
    files = [f for f in os.listdir(folder) if f.lower().endswith((".jpg", ".png"))]
    if len(files) <= target_count:
        print(f"{folder}: only {len(files)} images, nothing to trim")
        return
    to_remove = random.sample(files, len(files) - target_count)
    for f in to_remove:
        os.remove(os.path.join(folder, f))
    print(f"{folder}: trimmed to {target_count} images")

trim_folder(r"data\train\deficient", TARGET_TRAIN)
trim_folder(r"data\val\deficient", TARGET_VAL)