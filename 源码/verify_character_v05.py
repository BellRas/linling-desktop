"""Check that standing, blink and gaze art stay on the same character model."""

import json
from pathlib import Path

import numpy as np
from PIL import Image

from build_motion24 import CELL


ROOT = Path(__file__).resolve().parent
sheet = np.asarray(Image.open(ROOT / "character05.png").convert("RGBA"))
old = np.asarray(Image.open(ROOT / "spritesheet.png").convert("RGBA"))


def frame(index):
    y, x = divmod(index, 8)
    return sheet[y * CELL[1]:(y + 1) * CELL[1],
                 x * CELL[0]:(x + 1) * CELL[0]]


def bounds(image):
    yy, xx = np.where(image[:, :, 3] > 0)
    return (int(xx.min()), int(yy.min()), int(xx.max() + 1), int(yy.max() + 1))


idle = frame(0)
looks = [frame(i) for i in range(72, 88)]
idle_box = bounds(idle)
boxes = [bounds(image) for image in looks]
size_deviation = max(max(abs((b[2] - b[0]) - (idle_box[2] - idle_box[0])),
                         abs((b[3] - b[1]) - (idle_box[3] - idle_box[1])))
                     for b in boxes)
unique_looks = len({image.tobytes() for image in looks})
old_idle = old[:CELL[1], :CELL[0]]

def exposed_leg_pixels(image):
    # Above the old ankle line: the old idle hid this portion behind its coat.
    part = image[160:185, 65:130]
    red, green, blue, alpha = np.moveaxis(part, -1, 0)
    return int(np.count_nonzero((alpha > 128) & (red > 200) &
                                (red > green * 1.09) & (green > blue * 1.01)))


all_alpha = {int(value) for image in [idle, frame(1), frame(2), frame(3)] + looks
             for value in np.unique(image[:, :, 3])}
source_colors = {tuple(pixel[:3]) for pixel in old.reshape(-1, 4) if pixel[3]}
new_colors = {tuple(pixel[:3]) for image in [idle, frame(1), frame(2), frame(3)] + looks
              for pixel in image.reshape(-1, 4) if pixel[3]}
report = {
    "idleBounds": idle_box,
    "lookBounds": boxes,
    "maxLookSizeDeviationPixels": size_deviation,
    "uniqueGazeDirections": unique_looks,
    "newIdleExposedLegPixels": exposed_leg_pixels(idle),
    "oldIdleExposedLegPixels": exposed_leg_pixels(old_idle),
    "binaryAlpha": all_alpha == {0, 255},
    "sourcePaletteOnly": new_colors.issubset(source_colors),
}
report["ok"] = (sheet.shape == old.shape and unique_looks == 16
                and size_deviation <= 18
                and report["newIdleExposedLegPixels"] >= 40
                and report["newIdleExposedLegPixels"] > report["oldIdleExposedLegPixels"]
                and report["binaryAlpha"] and report["sourcePaletteOnly"])
print(json.dumps(report, ensure_ascii=False, indent=2))
if not report["ok"]:
    raise SystemExit(1)
