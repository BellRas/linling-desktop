"""Build Linling's 24 fps animation atlas from authored poses.

The source atlases remain untouched. Generated images use the original 192x208
cell, binary transparency, and palette. The standing and wave motions are built
from separately drawn full-body poses, not the old wave arm deformation.
"""

from math import cos, floor, pi, sin
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parent
CELL = (192, 208)
FPS = 24
SOURCE_COUNTS = (16, 24, 24, 16, 20, 24, 18, 18, 18, 0, 0, 16, 16, 12, 12, 12, 32)
TARGET_COUNTS = (96, 18, 18, 72, 36, 29, 24, 96, 96, 0, 0, 20, 20, 29, 29, 72, 62, 86)


def cells(path, count):
    sheet = Image.open(ROOT / path).convert("RGBA")
    return [sheet.crop(((i % 8) * CELL[0], (i // 8) * CELL[1],
                        (i % 8 + 1) * CELL[0], (i // 8 + 1) * CELL[1]))
            for i in range(count)]


def tween(a, b, fraction, translate=True):
    if fraction <= 0.001:
        return a.copy()
    if fraction >= 0.999:
        return b.copy()
    # Cross-fading pixel art produces a second translucent face/hand.  Select
    # one authored pose and ease its silhouette centre instead; the new frames
    # retain one clean outline and exactly the original colors.
    use_first = fraction < .5
    selected = a if use_first else b
    if not translate:
        return selected.copy()
    def centre(image):
        yy, xx = np.where(np.asarray(image.getchannel("A")) > 128)
        return (float(xx.mean()), float(yy.mean()))
    ax, ay = centre(a)
    bx, by = centre(b)
    offset_x = round(max(-3, min(3, (bx - ax) * (fraction if use_first else fraction - 1))))
    offset_y = round(max(-3, min(3, (by - ay) * (fraction if use_first else fraction - 1))))
    if not offset_x and not offset_y:
        return selected.copy()
    canvas = Image.new("RGBA", CELL, (0, 0, 0, 0))
    canvas.alpha_composite(selected, (offset_x, offset_y))
    return canvas


def retime(source, target_count, translate=True):
    if not source:
        return []
    result = []
    for index in range(target_count):
        position = index * len(source) / target_count
        first = floor(position)
        result.append(tween(source[first], source[(first + 1) % len(source)], position - first, translate))
    return result


def authored_pose(path, palette, height):
    """Fit a separately drawn key pose to the original cell and color palette."""
    image = Image.open(ROOT / path).convert("RGBA")
    alpha = image.getchannel("A")
    image = image.crop(alpha.getbbox())
    width = min(180, round(image.width * height / image.height))
    image = image.resize((width, height), Image.Resampling.LANCZOS)
    rgb = image.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE).convert("RGB")
    rgb.putalpha(image.getchannel("A").point(lambda value: 255 if value >= 160 else 0))
    canvas = Image.new("RGBA", CELL)
    canvas.alpha_composite(rgb, ((CELL[0] - width) // 2, CELL[1] - height - 8))
    return canvas


def palette_from(original):
    colors = sorted({tuple(pixel[:3]) for pixel in np.asarray(original[0]).reshape(-1, 4) if pixel[3]})
    palette = Image.new("P", (1, 1))
    data = [component for color in colors for component in color]
    # Pillow may select any of the 256 entries, including unused entries.
    # Fill those with an existing outline color instead of accidental black.
    palette.putpalette((data + list(colors[0]) * (256 - len(colors)))[:768])
    return palette


def deform(base, horizontal=0, vertical=0):
    """Shift one whole jump key pose; no local limb stretching."""
    pixels = np.asarray(base)
    yy, xx = np.indices((CELL[1], CELL[0]))
    sample_x = np.clip(np.rint(xx - horizontal).astype(np.int16), 0, CELL[0] - 1)
    sample_y = np.clip(np.rint(yy - vertical).astype(np.int16), 0, CELL[1] - 1)
    return Image.fromarray(pixels[sample_y, sample_x], "RGBA")


def breath_pose(idle, rise):
    pixels = np.asarray(idle)
    result = np.zeros_like(pixels)
    for y in range(CELL[1]):
        weight = 1 if y < 113 else max(0, (158-y)/45)
        from_y = min(CELL[1]-1, y+round(rise*weight))
        result[y] = pixels[from_y]
    return Image.fromarray(result, "RGBA")


def breathing(idle):
    """Four-second, foot-anchored breathing cycle, using only source pixels."""
    poses = [breath_pose(idle, rise) for rise in range(4)]
    return [poses[round(3 * (1-cos(2*pi*i/TARGET_COUNTS[0])) / 2)].copy()
            for i in range(TARGET_COUNTS[0])]


def new_action(poses):
    """Seven painted keys in a four-second 24 fps action, no warped limbs."""
    if len(poses) != 7:
        raise AssertionError('Seven action poses required')
    schedule = [(poses[0], 3)]
    schedule += [(pose, 5) for pose in poses[1:]]
    schedule += [(poses[-1], 18)]
    schedule += [(pose, 5) for pose in reversed(poses[:-1])]
    schedule += [(poses[0], 15)]
    frames = [frame.copy() for frame, count in schedule for _ in range(count)]
    if len(frames) != 96:
        raise AssertionError('Action frame count')
    return frames


def match_idle_bounds(pose, idle):
    """Keep newly painted action keys on the standing height and foot line."""
    target = idle.getchannel('A').getbbox()
    bounds = pose.getchannel('A').getbbox()
    cropped = pose.crop(bounds)
    height = target[3] - target[1]
    width = round(cropped.width * height / cropped.height)
    cropped = cropped.resize((width, height), Image.Resampling.NEAREST)
    canvas = Image.new('RGBA', CELL)
    canvas.alpha_composite(cropped, (round((target[0]+target[2]-width)/2), target[3]-height))
    return canvas


def wave(poses):
    # Eleven separately painted whole-body keys cover two arm swings.
    # Each transition occupies three 24-fps frames; no sleeve or body warp.
    sequence = ("idle", "start", "start-windup", "windup",
                "windup-mid", "mid", "mid-out", "out",
                "mid-out", "mid", "high", "high-in", "in",
                "high-in", "high", "mid", "mid-out", "out",
                "mid-out", "mid", "windup-mid", "windup",
                "start-windup", "start", "idle")
    frames = []
    for name in sequence[:-1]:
        frames.extend(poses[name].copy() for _ in range(3))
    frames[-1] = poses["idle"].copy()
    if len(frames) != TARGET_COUNTS[3]:
        raise AssertionError("Wave frame count")
    return frames


def normalize_run(frame, idle):
    """Fit each legacy stride to the new standing height and foot baseline."""
    box = frame.getchannel("A").getbbox()
    stand = idle.getchannel("A").getbbox()
    stride = frame.crop(box)
    scale = (stand[3] - stand[1]) / (box[3] - box[1])
    width = round(stride.width * scale)
    height = stand[3] - stand[1]
    stride = stride.resize((width, height), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", CELL)
    canvas.alpha_composite(stride, ((CELL[0] - width) // 2, stand[3] - height))
    return canvas


def top_tail(palette):
    tail = Image.open(ROOT / "top-tail-refined-key-v09.png").convert("RGBA")
    tail = tail.crop(tail.getchannel("A").getbbox())
    tail = tail.resize((round(tail.width * 74 / tail.height), 74), Image.Resampling.NEAREST)
    rgb = tail.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE).convert("RGB")
    rgb.putalpha(tail.getchannel("A").point(lambda value: 255 if value >= 160 else 0))
    return rgb


def top_peek(base, tail):
    """Give the hanging head and separate side tail their own silhouettes."""
    small = base.resize((round(CELL[0]*.83), round(CELL[1]*.83)), Image.Resampling.NEAREST)
    head = Image.new('RGBA', CELL)
    head.alpha_composite(small, (0, 14))
    frames = []
    for index in range(TARGET_COUNTS[15]):
        phase = 2 * pi * index / TARGET_COUNTS[15]
        tail_layer = Image.new("RGBA", CELL)
        tail_layer.alpha_composite(tail, (115, 59))
        angle = 3 * sin(phase)
        swung = tail_layer.rotate(angle, resample=Image.Resampling.NEAREST,
                                  center=(123, 59), expand=False)
        canvas = swung.copy()
        canvas.alpha_composite(head)
        frames.append(canvas)
    return frames


def jump(idle, crouch, takeoff, airborne):
    # Newly drawn crouch and airborne silhouettes. The WPF window supplies the
    # vertical arc; these frames show the actual squash, tuck and landing.
    frames = [idle.copy()]
    frames += [deform(idle, vertical=i) for i in (2, 4)]
    frames += [crouch.copy() for _ in range(4)]
    frames += [takeoff.copy() for _ in range(2)]
    frames += [airborne.copy() for _ in range(18)]
    frames += [takeoff.copy() for _ in range(2)]
    frames += [crouch.copy() for _ in range(4)]
    frames += [deform(idle, vertical=i) for i in (4, 2)]
    frames.append(idle.copy())
    if len(frames) != TARGET_COUNTS[4]:
        raise AssertionError("Jump frame count")
    return frames


def write_sheet(frames, path):
    if any(frame.size != CELL for frame in frames):
        raise ValueError("A frame changed the sprite cell dimensions")
    sheet = Image.new("RGBA", (CELL[0] * 8, CELL[1] * ((len(frames) + 7) // 8)), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        sheet.alpha_composite(frame, ((index % 8) * CELL[0], (index // 8) * CELL[1]))
    sheet.save(ROOT / path, optimize=True)


def main():
    motion = cells("motion.png", sum(SOURCE_COUNTS))
    original = cells("spritesheet.png", 136)
    character = cells("character05.png", 88)
    palette = palette_from(original)
    idle = character[0]
    wave_poses = {"idle": idle}
    for name in ("start", "windup", "mid", "high", "out", "in"):
        wave_poses[name] = authored_pose("wave-" + name + "-key-v05.png", palette, 192)
    for name in ("start-windup", "windup-mid", "mid-out", "high-in"):
        wave_poses[name] = authored_pose("wave-" + name + "-key-v07.png", palette, 192)
    crouch = authored_pose("crouch-key.png", palette, 176)
    takeoff = authored_pose("takeoff-key.png", palette, 184)
    airborne = authored_pose("jump-key.png", palette, 184)
    def action_pose(path):
        pose = match_idle_bounds(authored_pose(path, palette, 187), idle)
        pose.paste(idle.crop((0, 162, CELL[0], CELL[1])), (0, 162))
        return pose
    thinking = [idle] + [action_pose(path) for path in (
        'thinking-rise-key-v09.png', 'thinking-mid-rise-key-v09.png',
        'thinking-transition-key-v08.png', 'thinking-near-chin-key-v09.png',
        'thinking-touch-key-v09.png', 'thinking-key-v08.png')]
    observing = [idle] + [action_pose(path) for path in (
        'observing-turn-key-v09.png', 'observing-mid-turn-key-v09.png',
        'observing-transition-key-v08.png', 'observing-focus-key-v09.png',
        'observing-squint-key-v09.png', 'observing-key-v08.png')]
    cursor = 0
    expanded = []
    for row, count in enumerate(SOURCE_COUNTS):
        source = motion[cursor:cursor + count]
        cursor += count
        if row in (1, 2):
            # The source atlas's original eight large-stride poses are the gait
            # the user selected.  Retime those rather than the small-step set.
            source = [normalize_run(frame, idle)
                      for frame in original[row * 8:row * 8 + 8]]
        if row == 0:
            frames = breathing(idle)
        elif row == 3:
            frames = wave(wave_poses)
        elif row == 4:
            frames = jump(idle, crouch, takeoff, airborne)
        elif row == 7:
            frames = new_action(thinking)
        elif row == 8:
            frames = new_action(observing)
        elif row == 15:
            frames = top_peek(source[0], top_tail(palette))
        else:
            frames = retime(source, TARGET_COUNTS[row])
        expanded.extend(frames)
    write_sheet(expanded, "motion24.png")
    sleep = cells("sleep.png", 24)
    write_sheet(retime(sleep, TARGET_COUNTS[17]), "sleep24.png")
    print(f"24 fps atlases: {len(expanded)} motion frames, {TARGET_COUNTS[17]} sleep frames")


if __name__ == "__main__":
    main()
