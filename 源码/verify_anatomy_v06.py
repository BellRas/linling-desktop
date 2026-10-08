"""Regression checks for shoulder, stride, wave, and top-edge corrections."""

import json
from pathlib import Path

import numpy as np
from PIL import Image

from build_motion24 import CELL, TARGET_COUNTS, authored_pose, cells, palette_from


ROOT = Path(__file__).resolve().parent
character = cells("character05.png", 88)
motion = cells("motion24.png", sum(TARGET_COUNTS[:17]))
source = cells("spritesheet.png", 136)
old_motion = cells("motion.png", sum((16, 24, 24, 16, 20, 24, 18, 18, 18, 0, 0, 16, 16, 12, 12, 12, 32)))
offsets = [sum(TARGET_COUNTS[:i]) for i in range(18)]
old_top_index = sum((16, 24, 24, 16, 20, 24, 18, 18, 18, 0, 0, 16, 16, 12, 12))

def bounds(image):
    return image.getchannel("A").getbbox()

idle = character[0]
idle_bounds = bounds(idle)
corrected = authored_pose("idle-key-v06.png", palette_from(source), 187)
shoulder_rect = (63, 132, 129, 155)
shoulder_reference = np.asarray(corrected.crop(shoulder_rect))
gaze_shoulder_matches = [bool(np.array_equal(np.asarray(character[i].crop(shoulder_rect)),
                                            shoulder_reference)) for i in range(72, 88)]

run = motion[offsets[1]:offsets[3]]
run_bounds = [bounds(frame) for frame in run]
run_height_deviation = max(abs((b[3] - b[1]) - (idle_bounds[3] - idle_bounds[1]))
                           for b in run_bounds)
run_feet_deviation = max(abs(b[3] - idle_bounds[3]) for b in run_bounds)

wave = motion[offsets[3]:offsets[4]]
keys = [idle] + [authored_pose(f"wave-{name}-key-v05.png", palette_from(source), 192)
                 for name in ("start", "windup", "mid", "high", "out", "in")]
keys += [authored_pose(f"wave-{name}-key-v07.png", palette_from(source), 192)
         for name in ("start-windup", "windup-mid", "mid-out", "high-in")]
key_pixels = {frame.tobytes() for frame in keys}
wave_uses_unwarped_keys = all(frame.tobytes() in key_pixels for frame in wave)

top = motion[offsets[15]]
old_top = old_motion[old_top_index]
new_pixels = np.asarray(top.getchannel("A")) > 128
old_pixels = np.asarray(old_top.getchannel("A")) > 128
tail_region = (slice(66, 157), slice(155, 192))
tail_added_pixels = int(np.count_nonzero(new_pixels[tail_region] & ~old_pixels[tail_region]))
tail_color_samples = np.asarray(top)[tail_region]
tail_spots = int(np.count_nonzero((tail_color_samples[:, :, 1] > tail_color_samples[:, :, 0] * .88)
                                   & (tail_color_samples[:, :, 3] > 128)))

result = {
    "idleBounds": idle_bounds,
    "allGazeShoulderBasesMatchCorrectedMaster": all(gaze_shoulder_matches),
    "runHeightDeviationPixels": run_height_deviation,
    "runFeetDeviationPixels": run_feet_deviation,
    "waveUsesOnlyUnwarpedPaintedPoses": wave_uses_unwarped_keys,
    "visibleTopTailAddedPixels": tail_added_pixels,
    "topTailColoredPixels": tail_spots,
}
result["ok"] = (result["allGazeShoulderBasesMatchCorrectedMaster"]
                and run_height_deviation <= 1 and run_feet_deviation <= 1
                and wave_uses_unwarped_keys and tail_added_pixels >= 50
                and tail_spots >= 100)
print(json.dumps(result, ensure_ascii=False, indent=2))
if not result["ok"]:
    raise SystemExit(1)
