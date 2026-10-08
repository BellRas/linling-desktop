"""Reproduce the ver0.9 pasted-head gaze seam from the actual packed sprites."""
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
IMAGE = ROOT / '源码' / 'character05.png'
W, H = 192, 208
atlas = Image.open(IMAGE).convert('RGBA')
frames = [np.asarray(atlas.crop(((i % 8) * W, (i // 8) * H,
                                (i % 8 + 1) * W, (i // 8 + 1) * H)))
          for i in range(88)]
idle = frames[0]
looks = frames[72:88]

# Full-body view changes must carry through the shoulders, robe and arms.
# A head drawn onto an unchanged lower half fails regardless of eye direction.
torso = (slice(140, 174), slice(28, 166))
changed = []
for index, frame in enumerate(looks):
    visible = (frame[torso][..., 3] > 0) | (idle[torso][..., 3] > 0)
    different = np.any(frame[torso] != idle[torso], axis=2)
    changed.append(float(np.count_nonzero(different & visible) / np.count_nonzero(visible)))

# A quick deterministic signal for every direction, not just the screenshot.
checked = [2, 4, 6, 8, 10, 12, 14]
violations = [i for i in checked if changed[i] < 0.025]
print('torso_change_fraction', [round(v, 4) for v in changed])
print('unchanged_nonfront_directions', violations)
assert not violations, 'Gaze still pastes a redirected head onto the idle torso'
