# Roboflow Lettuce Types v1

Source: https://universe.roboflow.com/lettuce-classification/lettuce-types/dataset/1
License: CC BY 4.0
Imported class: `green ice` (YOLO class `0`)

The source dataset labels lettuce **variety**, not plant health or growth age.
The imported Green Ice photographs were visually reviewed as marketable green
lettuce and are used as weak `healthy` labels. Their age label is deliberately
set to `unknown`, so the multitask trainer includes them in lettuce and health
losses but excludes them from age loss and age accuracy.

The converter preserves the source train/validation/test split and checks that
the pre-augmentation Roboflow source names do not cross those split boundaries.
It writes cropped images beneath:

```text
data/{train,val,test}/healthy/unknown_age/green_ice_rf_v1/
```

Reproduce the import from `cnn-training` with:

```powershell
.\venv\Scripts\python.exe .\prepare_green_ice.py `
  "<path-to-extracted-Lettuce-Types.v1i.yolov8>"
```

The generated `green_ice_manifest.csv` records every crop, bounding box,
source image, output split, and SHA-256 checksum. `import_summary.json` records
the counts and leakage audit.
