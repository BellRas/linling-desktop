"""Check the specific regressions reported after development version 0.6."""
import json
from pathlib import Path
import numpy as np
from build_motion24 import CELL, TARGET_COUNTS, authored_pose, cells, palette_from

ROOT = Path(__file__).resolve().parent
character = cells('character05.png', 88)
motion = cells('motion24.png', sum(TARGET_COUNTS[:17]))
source = cells('spritesheet.png', 136)
offsets = [sum(TARGET_COUNTS[:i]) for i in range(18)]
base = np.asarray(character[0])
eye_area = np.ones((CELL[1], CELL[0]), dtype=bool)
eye_area[82:103, 75:136] = False
stable_blink = all(np.array_equal(np.asarray(frame)[eye_area], base[eye_area])
                   for frame in character[1:4])
stable_gaze = all(np.array_equal(np.asarray(frame)[eye_area], base[eye_area])
                  for frame in character[72:88])
unique_gaze = len({frame.tobytes() for frame in character[72:88]})
idle = motion[:TARGET_COUNTS[0]]
idle_stable = len({frame.tobytes() for frame in idle}) == 1

palette = palette_from(source)
keys = [character[0]]
keys += [authored_pose('wave-' + name + '-key-v05.png', palette, 192)
         for name in ('start', 'windup', 'mid', 'high', 'out', 'in')]
keys += [authored_pose('wave-' + name + '-key-v07.png', palette, 192)
         for name in ('start-windup', 'windup-mid', 'mid-out', 'high-in')]
key_pixels = {frame.tobytes() for frame in keys}
wave = motion[offsets[3]:offsets[4]]
wave_clean = all(frame.tobytes() in key_pixels for frame in wave)
unique_wave = len({frame.tobytes() for frame in wave})

top = motion[offsets[15]:offsets[16]]
tail_poses = len({np.asarray(frame)[66:160, 120:192].tobytes() for frame in top})
top_head_stable = all(np.array_equal(np.asarray(frame)[:, :100], np.asarray(top[0])[:, :100])
                      for frame in top)
rightmost_pixels = max(np.count_nonzero(np.asarray(frame.getchannel('A'))[:, -1])
                       for frame in top)
def silhouette_change(left, right):
    a = np.asarray(left.getchannel('A')) > 128
    b = np.asarray(right.getchannel('A')) > 128
    return float(np.count_nonzero(a ^ b) / np.count_nonzero(a | b))
tail_step = max(silhouette_change(top[i], top[(i + 1) % len(top)])
                for i in range(len(top)))

result = {
    'fps': 24,
    'stableBlinkBody': stable_blink,
    'stableGazeBody': stable_gaze,
    'uniqueGazeDirections': unique_gaze,
    'idleHasNoWarpOrJitter': idle_stable,
    'waveOnlyUsesPaintedFullBodyPoses': wave_clean,
    'uniqueWavePoses': unique_wave,
    'tailFrames': len(top),
    'uniqueTailPositions': tail_poses,
    'topHeadStable': top_head_stable,
    'tailRightEdgePixels': int(rightmost_pixels),
    'tailMaxAdjacentSilhouetteChange': round(tail_step, 5),
}
result['ok'] = (stable_blink and stable_gaze and unique_gaze == 16
                and idle_stable and wave_clean and unique_wave >= 11
                and len(top) == 72 and tail_poses >= 25 and top_head_stable
                and rightmost_pixels == 0 and tail_step < .02)
print(json.dumps(result, ensure_ascii=False, indent=2))
if not result['ok']:
    raise SystemExit(1)
