"""Regression checks for repainted gaze, new action keys, and tapered tail."""
import json

import numpy as np
from PIL import Image

from build_motion24 import (CELL, FPS, SOURCE_COUNTS, TARGET_COUNTS,
                            cells, palette_from, top_tail)

character = cells('character05.png', 88)
gaze_breath = cells('gaze-breath-v09.png', 64)
motion = cells('motion24.png', sum(TARGET_COUNTS[:17]))
source = cells('spritesheet.png', 136)
idle = np.asarray(character[0])
offsets = [sum(TARGET_COUNTS[:row]) for row in range(18)]


def row(number):
    return motion[offsets[number]:offsets[number + 1]]


def silhouette_change(left, right):
    a = np.asarray(left.getchannel('A')) > 128
    b = np.asarray(right.getchannel('A')) > 128
    return float(np.count_nonzero(a ^ b) / np.count_nonzero(a | b))


def max_step(frames):
    return max(silhouette_change(a, b) for a, b in zip(frames, frames[1:] + frames[:1]))


looks = character[72:88]
head_samples = [np.asarray(looks[i]) for i in range(0, 16, 2)]
unique_heads = len({sample[:140].tobytes() for sample in head_samples})
body_fixed = all(np.array_equal(np.asarray(frame)[140:], idle[140:]) for frame in looks)
feet_fixed = all(np.array_equal(np.asarray(frame)[162:], idle[162:]) for frame in gaze_breath)
painted_head_difference = []
for sample in head_samples:
    best = min(float(np.mean(np.any(sample[20:126, 30:160] !=
                                     np.roll(idle, (dy, dx), (0, 1))[20:126, 30:160], axis=2)))
               for dx in range(-4, 5) for dy in range(-3, 4))
    painted_head_difference.append(best)

actions = [row(7), row(8)]
action_keys = [len({frame.tobytes() for frame in frames}) for frames in actions]
action_footline = all(all(np.array_equal(np.asarray(frame)[162:], idle[162:])
                          for frame in frames) for frames in actions)
action_returns = all(frames[0].tobytes() == character[0].tobytes()
                     and frames[-1].tobytes() == character[0].tobytes()
                     for frames in actions)
action_steps = [max_step(frames) for frames in actions]

tail = top_tail(palette_from(source))
tail_alpha = np.asarray(tail.getchannel('A')) > 128
root_width = float(tail_alpha[10:30].sum(axis=1).mean())
tip_width = float(tail_alpha[54:72].sum(axis=1).mean())
old_top = cells('motion.png', sum(SOURCE_COUNTS))[sum(SOURCE_COUNTS[:15])]
small = old_top.resize((round(CELL[0]*.83), round(CELL[1]*.83)), Image.Resampling.NEAREST)
head_layer = Image.new('RGBA', CELL)
head_layer.alpha_composite(small, (0, 14))
tail_layer = Image.new('RGBA', CELL)
tail_layer.alpha_composite(tail, (115, 59))
head_alpha = np.asarray(head_layer.getchannel('A'))[66:] > 128
visible_tail = np.asarray(tail_layer.getchannel('A'))[66:] > 128
tail_overlap = float(np.count_nonzero(head_alpha & visible_tail) / np.count_nonzero(visible_tail))
top = row(15)
top_right_edge = max(int(np.count_nonzero(np.asarray(frame.getchannel('A'))[:, -1]))
                     for frame in top)

all_images = looks + gaze_breath + actions[0] + actions[1] + top
alphas = {int(value) for frame in all_images
          for value in np.unique(np.asarray(frame.getchannel('A')))}
source_colors = {tuple(pixel[:3]) for pixel in np.asarray(source[0]).reshape(-1, 4)
                 if pixel[3]}
all_colors = {tuple(pixel[:3]) for frame in all_images
              for pixel in np.asarray(frame).reshape(-1, 4) if pixel[3]}

result = {
    'fps': FPS,
    'gazeDirections': len({frame.tobytes() for frame in looks}),
    'repaintedHeadPerspectives': unique_heads,
    'minimumHeadDifferenceFromTranslatedIdle': round(min(painted_head_difference), 4),
    'gazeBodyFixedBelowNeck': body_fixed,
    'gazeBreathingFrames': len(gaze_breath),
    'gazeBreathingFeetFixed': feet_fixed,
    'thinkingFrames': len(actions[0]),
    'observingFrames': len(actions[1]),
    'distinctPaintedActionPoses': action_keys,
    'actionFeetFixed': action_footline,
    'actionsReturnToIdle': action_returns,
    'actionMaxAdjacentSilhouetteChange': [round(value, 5) for value in action_steps],
    'tailRootWidth': round(root_width, 2),
    'tailTipWidth': round(tip_width, 2),
    'tailVisiblePixels': int(np.count_nonzero(visible_tail & ~head_alpha)),
    'tailHeadOverlapFraction': round(tail_overlap, 4),
    'tailFrames': len(top),
    'tailMaxAdjacentSilhouetteChange': round(max_step(top), 5),
    'tailRightEdgePixels': top_right_edge,
    'binaryAlpha': alphas == {0, 255},
    'sourcePaletteOnly': all_colors.issubset(source_colors),
}
result['ok'] = (FPS == 24 and result['gazeDirections'] == 16
                and unique_heads == 8 and min(painted_head_difference) > .2
                and body_fixed and len(gaze_breath) == 64 and feet_fixed
                and [len(frames) for frames in actions] == [96, 96]
                and action_keys == [7, 7] and action_footline and action_returns
                and max(action_steps) < .06 and tip_width < root_width * .55
                and result['tailVisiblePixels'] > 500 and tail_overlap < .15
                and len(top) == 72 and result['tailMaxAdjacentSilhouetteChange'] < .01
                and top_right_edge == 0 and result['binaryAlpha']
                and result['sourcePaletteOnly'])
print(json.dumps(result, ensure_ascii=False, indent=2))
if not result['ok']:
    raise SystemExit(1)
