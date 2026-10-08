"""Check the assembled sixteen-direction, full-body gaze animation.

This intentionally checks the rendered atlases rather than source key images.
The v09 atlas could pass a unique-head check while every torso pixel below the
neck was copied from idle; measuring the lower torso prevents that regression.
"""

import json
from pathlib import Path

import numpy as np
from PIL import Image

from build_motion24 import CELL, FPS, cells


ROOT = Path(__file__).resolve().parent
TORSO_START = 140
TORSO_END = 162
FOOT_START = 162
SHOE_START = 171
SHOE_END = 197
SHOE_LEFT = 65
SHOE_RIGHT = 130
MAX_SIZE_DELTA = 8
MAX_FOOT_BASELINE_DELTA = 1
MAX_FEET_CENTER_SPREAD = 6.0
MAX_ADJACENT_SILHOUETTE_CHANGE = 0.12


def opaque(image):
    return image[:, :, 3] > 128


def bounds(image):
    ys, xs = np.where(opaque(image))
    if not len(xs):
        return None
    return (int(xs.min()), int(ys.min()), int(xs.max() + 1), int(ys.max() + 1))


def silhouette_change(left, right):
    a = opaque(left)
    b = opaque(right)
    union = np.count_nonzero(a | b)
    return float(np.count_nonzero(a ^ b) / union) if union else 0.0


def appearance_change(left, right):
    visible = opaque(left) | opaque(right)
    changed = np.any(left != right, axis=2)
    return float(np.count_nonzero(changed & visible) / np.count_nonzero(visible))


def feet_centre(image):
    # Only the two shoes belong in this anchor measurement.  The tail and coat
    # can descend to the shoe line in some views, but lie outside this x range.
    ys, xs = np.where(opaque(image[SHOE_START:SHOE_END,
                                      SHOE_LEFT:SHOE_RIGHT]))
    return float(xs.mean() + SHOE_LEFT) if len(xs) else None


def main():
    failures = []
    expected_character_size = (CELL[0] * 8, CELL[1] * 11)
    expected_breath_size = (CELL[0] * 8, CELL[1] * 8)
    try:
        character_size = Image.open(ROOT / 'character05.png').size
        breath_size = Image.open(ROOT / 'gaze-breath-v10.png').size
        source = cells('spritesheet.png', 136)
        character = cells('character05.png', 88)
        breaths = cells('gaze-breath-v10.png', 64)
    except (FileNotFoundError, OSError, ValueError) as error:
        print(json.dumps({'ok': False, 'failures': [str(error)]},
                         ensure_ascii=False, indent=2))
        raise SystemExit(1)

    if character_size != expected_character_size:
        failures.append('character05.png has unexpected atlas dimensions')
    if breath_size != expected_breath_size:
        failures.append('gaze-breath-v10.png has unexpected atlas dimensions')

    idle = np.asarray(character[0])
    looks = [np.asarray(frame) for frame in character[72:88]]
    breathing = [np.asarray(frame) for frame in breaths]
    idle_bounds = bounds(idle)
    look_bounds = [bounds(frame) for frame in looks]
    if idle_bounds is None or any(box is None for box in look_bounds):
        failures.append('one or more gaze frames are empty')
        print(json.dumps({'ok': False, 'failures': failures},
                         ensure_ascii=False, indent=2))
        raise SystemExit(1)

    unique_full = len({frame.tobytes() for frame in looks})
    torso = [frame[TORSO_START:TORSO_END] for frame in looks]
    idle_torso = idle[TORSO_START:TORSO_END]
    unique_torso = len({frame.tobytes() for frame in torso})
    torso_changes = [int(np.count_nonzero(np.any(frame != idle_torso, axis=2)))
                     for frame in torso]
    adjacent_silhouette = [silhouette_change(looks[i], looks[(i + 1) % 16])
                           for i in range(16)]
    leg_silhouette = [silhouette_change(looks[i][170:197, 65:130],
                                       looks[(i + 1) % 16][170:197, 65:130])
                      for i in range(16)]
    adjacent_appearance = [appearance_change(looks[i], looks[(i + 1) % 16])
                           for i in range(16)]
    idle_to_neutral = silhouette_change(idle, looks[0])
    substantive_transitions = sum(change >= 0.005 for change in adjacent_silhouette)

    idle_width = idle_bounds[2] - idle_bounds[0]
    idle_height = idle_bounds[3] - idle_bounds[1]
    size_deltas = [max(abs(box[2] - box[0] - idle_width),
                       abs(box[3] - box[1] - idle_height)) for box in look_bounds]
    foot_baseline_deltas = [abs(box[3] - idle_bounds[3]) for box in look_bounds]
    feet_centres = [feet_centre(frame) for frame in looks]
    feet_spread = (max(feet_centres) - min(feet_centres)
                   if all(center is not None for center in feet_centres) else float('inf'))

    breath_first_matches = all(np.array_equal(breathing[direction * 4], looks[direction])
                               for direction in range(16))
    breath_feet_stable = all(
        np.array_equal(breathing[direction * 4 + phase][FOOT_START:],
                       looks[direction][FOOT_START:])
        for direction in range(16) for phase in range(4))
    breath_baselines = [bounds(frame)[3] if bounds(frame) else None
                        for frame in breathing]

    selected = looks + breathing
    alpha_values = sorted({int(value) for frame in selected
                           for value in np.unique(frame[:, :, 3])})
    source_colors = {tuple(pixel[:3]) for pixel in np.asarray(source[0]).reshape(-1, 4)
                     if pixel[3]}
    actual_colors = {tuple(pixel[:3]) for frame in selected
                     for pixel in frame.reshape(-1, 4) if pixel[3]}
    unexpected_colors = sorted(actual_colors - source_colors)

    if unique_full != 16:
        failures.append('gaze atlas does not have sixteen unique full sprites')
    if unique_torso < 8 or sum(change > 16 for change in torso_changes) < 8:
        failures.append('torso remains copied from idle in too many directions')
    if substantive_transitions < 12:
        failures.append('too many adjacent directions have no visible pose transition')
    if max(size_deltas) > MAX_SIZE_DELTA:
        failures.append('gaze character size differs too much from idle')
    if max(foot_baseline_deltas) > MAX_FOOT_BASELINE_DELTA:
        failures.append('gaze feet do not share idle baseline')
    if feet_spread > MAX_FEET_CENTER_SPREAD:
        failures.append('gaze feet jump horizontally between directions')
    if max(adjacent_silhouette) > MAX_ADJACENT_SILHOUETTE_CHANGE:
        failures.append('adjacent gaze silhouettes jump too far')
    if max(leg_silhouette) > .32:
        failures.append('gaze leg silhouette jumps too far between directions')
    if idle_to_neutral > .12:
        failures.append('idle to neutral gaze silhouette jumps too far')
    if not breath_first_matches:
        failures.append('a breathing cycle does not start on its base direction')
    if not breath_feet_stable or any(b != idle_bounds[3] for b in breath_baselines):
        failures.append('breathing gaze changes the foot line')
    if alpha_values != [0, 255]:
        failures.append('gaze artwork contains partial transparency')
    if unexpected_colors:
        failures.append('gaze artwork contains colors outside the source palette')

    report = {
        'fps': FPS,
        'cell': list(CELL),
        'gazeDirections': len(looks),
        'uniqueFullBodySprites': unique_full,
        'uniqueLowerTorsoRegions': unique_torso,
        'torsoPixelsDifferentFromIdle': torso_changes,
        'substantiveAdjacentTransitions': substantive_transitions,
        'idleBounds': idle_bounds,
        'gazeBounds': look_bounds,
        'maxSizeDeviationPx': max(size_deltas),
        'maxFootBaselineDeviationPx': max(foot_baseline_deltas),
        'footCentreXs': [round(center, 2) if center is not None else None
                         for center in feet_centres],
        'feetCentreSpreadPx': round(feet_spread, 3),
        'adjacentSilhouetteChanges': [round(value, 5) for value in adjacent_silhouette],
        'maxAdjacentSilhouetteChange': round(max(adjacent_silhouette), 5),
        'legSilhouetteChanges': [round(value, 5) for value in leg_silhouette],
        'maxLegSilhouetteChange': round(max(leg_silhouette), 5),
        'idleToNeutralSilhouetteChange': round(idle_to_neutral, 5),
        'adjacentAppearanceChanges': [round(value, 5) for value in adjacent_appearance],
        'maxAdjacentAppearanceChange': round(max(adjacent_appearance), 5),
        'breathingFrames': len(breathing),
        'breathingStartsOnBasePose': breath_first_matches,
        'breathingFeetStable': breath_feet_stable,
        'binaryAlpha': alpha_values == [0, 255],
        'sourcePaletteOnly': not unexpected_colors,
        'unexpectedPaletteColorCount': len(unexpected_colors),
        'failures': failures,
        'ok': not failures,
    }
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if failures:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
