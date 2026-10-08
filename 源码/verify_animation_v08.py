"""Regression checks for breathing, head turns, new actions, and top tail."""
import json
from pathlib import Path

import numpy as np
from PIL import Image

from build_motion24 import (CELL, TARGET_COUNTS, SOURCE_COUNTS, cells,
                            palette_from, top_tail)

ROOT = Path(__file__).resolve().parent
idle = cells('character05.png', 88)[0]
character = cells('character05.png', 88)
motion = cells('motion24.png', sum(TARGET_COUNTS[:17]))
gaze_breath = cells('gaze-breath-v08.png', 64)
offsets = [sum(TARGET_COUNTS[:row]) for row in range(18)]


def row(number):
    return motion[offsets[number]:offsets[number + 1]]


def silhouette_change(a, b):
    left = np.asarray(a.getchannel('A')) > 128
    right = np.asarray(b.getchannel('A')) > 128
    return float(np.count_nonzero(left ^ right) / np.count_nonzero(left | right))


def max_step(frames):
    return max(silhouette_change(a, b) for a, b in zip(frames, frames[1:] + frames[:1]))


base = np.asarray(idle)
breath = row(0)
looks = character[72:88]
lower = np.s_[130:, :, :]
feet = np.s_[158:, :, :]
thinking, observing, top = row(7), row(8), row(15)
new_action_baseline = all(frames[0].tobytes() == idle.tobytes()
                          and frames[-1].tobytes() == idle.tobytes()
                          and all(frame.getchannel('A').getbbox()[3] == idle.getchannel('A').getbbox()[3]
                                  for frame in frames)
                          for frames in (thinking, observing))
distinct_head = all(np.any(np.asarray(looks[index])[:82] != base[:82])
                    for index in (2, 4, 10, 12))
body_fixed = (all(np.array_equal(np.asarray(frame)[lower], base[lower]) for frame in looks)
              and all(np.array_equal(np.asarray(frame)[feet], base[feet])
                      for frame in gaze_breath))

# The top head deliberately has its own smaller silhouette. The tail should
# read as a second shape at the edge, rather than painting over the face.
source = cells('spritesheet.png', 136)
palette = palette_from(source)
tail = top_tail(palette)
old_top = cells('motion.png', sum(SOURCE_COUNTS))[sum(SOURCE_COUNTS[:15])]
small = old_top.resize((round(CELL[0] * .83), round(CELL[1] * .83)), Image.Resampling.NEAREST)
head_layer = Image.new('RGBA', CELL)
head_layer.alpha_composite(small, (0, 14))
tail_layer = Image.new('RGBA', CELL)
tail_layer.alpha_composite(tail, (126, 64))
head_alpha = np.asarray(head_layer.getchannel('A'))[66:] > 128
tail_alpha = np.asarray(tail_layer.getchannel('A'))[66:] > 128
overlap = float(np.count_nonzero(head_alpha & tail_alpha) / np.count_nonzero(tail_alpha))

result = {
    'fps': 24,
    'idleFrames': len(breath),
    'idleDistinctBodyPoses': len({frame.tobytes() for frame in breath}),
    'idleFeetFixed': all(np.array_equal(np.asarray(frame)[feet], base[feet]) for frame in breath),
    'idleLoopStep': round(max_step(breath), 5),
    'uniqueGazeDirections': len({frame.tobytes() for frame in looks}),
    'gazeTurnsHeadOutsideEyes': distinct_head,
    'gazeAndBreathingKeepLowerBodyFixed': body_fixed,
    'gazeBreathingFrames': len(gaze_breath),
    'thinkingDistinctPoses': len({frame.tobytes() for frame in thinking}),
    'observingDistinctPoses': len({frame.tobytes() for frame in observing}),
    'newActionsReturnToIdleAndShareFootLine': new_action_baseline,
    'thinkingMaxStep': round(max_step(thinking), 5),
    'observingMaxStep': round(max_step(observing), 5),
    'topTailFrames': len(top),
    'topTailVisiblePixels': int(np.count_nonzero(tail_alpha)),
    'topTailHeadOverlapFraction': round(overlap, 4),
    'topTailRightEdgePixels': max(int(np.count_nonzero(np.asarray(frame.getchannel('A'))[:, -1]))
                                  for frame in top),
    'topTailMaxStep': round(max_step(top), 5),
}
result['ok'] = (result['idleDistinctBodyPoses'] >= 3 and result['idleFeetFixed']
                and result['idleLoopStep'] < .03
                and result['uniqueGazeDirections'] == 16 and distinct_head and body_fixed
                and len(gaze_breath) == 64
                and result['thinkingDistinctPoses'] >= 4
                and result['observingDistinctPoses'] >= 5
                and new_action_baseline and result['thinkingMaxStep'] < .06
                and result['observingMaxStep'] < .08
                and len(top) == 72 and result['topTailVisiblePixels'] > 800
                and overlap < .12 and result['topTailRightEdgePixels'] == 0
                and result['topTailMaxStep'] < .01)
print(json.dumps(result, ensure_ascii=False, indent=2))
if not result['ok']:
    raise SystemExit(1)
