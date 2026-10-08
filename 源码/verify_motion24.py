"""Asset-level regression checks for the 24 fps Linling animation atlases."""

import json
from itertools import accumulate
from pathlib import Path

import numpy as np
from PIL import Image

from build_motion24 import CELL, SOURCE_COUNTS, TARGET_COUNTS


ROOT = Path(__file__).resolve().parent
motion = np.asarray(Image.open(ROOT / "motion24.png").convert("RGBA"))
sleep = np.asarray(Image.open(ROOT / "sleep24.png").convert("RGBA"))
source = np.asarray(Image.open(ROOT / "spritesheet.png").convert("RGBA"))
character = np.asarray(Image.open(ROOT / "character05.png").convert("RGBA"))
offsets = list(accumulate((0,) + TARGET_COUNTS))


def frame(sheet, ordinal):
    return sheet[(ordinal // 8) * CELL[1]:(ordinal // 8 + 1) * CELL[1],
                 (ordinal % 8) * CELL[0]:(ordinal % 8 + 1) * CELL[0]]


def alpha_change(a, b):
    left, right = a[:, :, 3] > 128, b[:, :, 3] > 128
    return float(np.count_nonzero(left ^ right) / np.count_nonzero(left | right))


wave = [frame(motion, offsets[3] + i) for i in range(TARGET_COUNTS[3])]
yy, xx = np.indices((CELL[1], CELL[0]))
hand_centres = []
for pose in wave[17:59]:
    red, green, blue, alpha = np.moveaxis(pose, -1, 0)
    hand = ((xx < 98) & (yy < 72) & (red > 170) &
            (red > green * 1.09) & (green > blue * 1.02) & (alpha > 128))
    if not np.any(hand):
        raise AssertionError("Painted waving hand disappeared")
    hand_centres.append(float(xx[hand].mean()))
wave_swing = max(hand_centres) - min(hand_centres)
wave_step = max(alpha_change(wave[i], wave[(i + 1) % len(wave)])
                for i in range(len(wave)))
wave_foot_change = alpha_change(wave[0][160:205], wave[24][160:205])
wave_torso_change = alpha_change(wave[0][100:160], wave[24][100:160])
wave_head_change = alpha_change(wave[0][10:105], wave[24][10:105])
jump = [frame(motion, offsets[4] + i) for i in range(TARGET_COUNTS[4])]
jump_returns_to_idle = (np.array_equal(jump[0], frame(character, 0))
                        and np.array_equal(jump[-1], frame(character, 0)))
wave_returns_to_idle = (np.array_equal(wave[0], frame(character, 0))
                        and np.array_equal(wave[-1], frame(character, 0)))
jump_pose_change = alpha_change(jump[3], jump[9])
jump_inbetween_change = alpha_change(jump[7], jump[9])

source_colors = {tuple(pixel[:3]) for pixel in source.reshape(-1, 4) if pixel[3]}
all_colors = {tuple(pixel[:3]) for sheet in (motion, sleep)
              for pixel in sheet.reshape(-1, 4) if pixel[3]}
alphas = {int(value) for sheet in (motion, sleep) for value in np.unique(sheet[:, :, 3])}
expected_motion_height = ((sum(TARGET_COUNTS[:17]) + 7) // 8) * CELL[1]
expected_sleep_height = ((TARGET_COUNTS[17] + 7) // 8) * CELL[1]
reported = {
    "fps": 24,
    "motionFrames": sum(TARGET_COUNTS[:17]),
    "sleepFrames": TARGET_COUNTS[17],
    "waveHandSwingPixels": round(wave_swing, 2),
    "waveMaxAdjacentSilhouetteChange": round(wave_step, 4),
    "waveFootSilhouetteChange": round(wave_foot_change, 4),
    "waveTorsoSilhouetteChange": round(wave_torso_change, 4),
    "waveHeadSilhouetteChange": round(wave_head_change, 4),
    "waveReturnsToNewIdle": wave_returns_to_idle,
    "jumpReturnsToIdle": jump_returns_to_idle,
    "jumpCrouchToAirborneChange": round(jump_pose_change, 4),
    "jumpTakeoffToAirborneChange": round(jump_inbetween_change, 4),
    "binaryAlpha": alphas == {0, 255},
    "sourcePaletteOnly": all_colors.issubset(source_colors),
    "motionSheet": [motion.shape[1], motion.shape[0]],
    "sleepSheet": [sleep.shape[1], sleep.shape[0]],
    "counts": list(TARGET_COUNTS),
}
reported["ok"] = (motion.shape[:2] == (expected_motion_height, CELL[0] * 8)
                  and sleep.shape[:2] == (expected_sleep_height, CELL[0] * 8)
                  and reported["binaryAlpha"] and reported["sourcePaletteOnly"]
                  and TARGET_COUNTS[3] >= 60 and TARGET_COUNTS[4] >= 32
                  and wave_swing >= 25 and wave_step < .19
                  and wave_foot_change > .3 and wave_torso_change > .1
                  and wave_head_change > .15
                  and wave_returns_to_idle and jump_returns_to_idle and jump_pose_change >= .20
                  and .05 <= jump_inbetween_change < jump_pose_change)
print(json.dumps(reported, ensure_ascii=False, indent=2))
if not reported["ok"]:
    raise SystemExit(1)
