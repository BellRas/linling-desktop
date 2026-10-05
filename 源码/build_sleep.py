"""Build Linling's sleeping sprite sheet from its transparent master pose.

The fixed crop, dimensions and source palette make every frame repeatable. The
master is generated art; this script only normalizes it to the existing sprite
grid and animates a very gentle sleeping breath.
"""

from collections import Counter
from pathlib import Path
from math import sin, pi

from PIL import Image


ROOT = Path(__file__).resolve().parent
CELL = (192, 208)
FRAMES = 24
MASTER_CROP = (178, 197, 1019, 1127)


def source_palette():
    source = Image.open(ROOT / "spritesheet.png").convert("RGBA")
    standing = source.crop((0, 0, *CELL))
    counts = Counter(rgb for r, g, b, a in standing.getdata() if a for rgb in [(r, g, b)])
    # Include colors from the seated pose; skin and sage-green shades differ
    # slightly from standing and should not drift when the sprite breathes.
    motion = Image.open(ROOT / "motion.png").convert("RGBA")
    sitting_start = sum([16, 24, 24, 16, 20, 24, 18, 18, 18, 0, 0, 16, 16, 12, 12, 12])
    x, y = sitting_start % 8 * 192, sitting_start // 8 * 208
    counts.update((r, g, b) for r, g, b, a in motion.crop((x, y, x + 192, y + 208)).getdata() if a)
    colors = [rgb for rgb, _ in counts.most_common(255)]
    while len(colors) < 256:
        colors.append(colors[-1])
    palette = Image.new("P", (16, 16))
    palette.putpalette([component for rgb in colors for component in rgb])
    return palette


def prepare_master():
    original = Image.open(ROOT / "sleep-master.png").convert("RGBA")
    if original.size != (1203, 1307):
        raise ValueError("sleep-master.png dimensions changed; check MASTER_CROP")
    art = original.crop(MASTER_CROP)
    art = art.resize((158, 174), Image.Resampling.LANCZOS)
    alpha = art.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
    indexed = art.convert("RGB").quantize(palette=source_palette(), dither=Image.Dither.NONE)
    art = indexed.convert("RGBA")
    art.putalpha(alpha)
    art.save(ROOT / "sleep-base.png")
    return art


def frame(base, index):
    phase = 2 * pi * index / FRAMES
    # Discrete 1-pixel chest expansion and head lowering create a quiet breath.
    # Keep the tail, knot and bell in one connected silhouette.
    breathe = sin(phase)
    width = 158 + (1 if breathe > 0.42 else 0)
    height = 174 + (1 if breathe > 0.15 else 0)
    y = 27 + (1 if breathe < -0.42 else 0)
    resized = base.resize((width, height), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", CELL, (0, 0, 0, 0))
    canvas.alpha_composite(resized, ((CELL[0] - width) // 2, y))
    return canvas


def main():
    base = prepare_master()
    sheet = Image.new("RGBA", (CELL[0] * 8, CELL[1] * 3), (0, 0, 0, 0))
    for n in range(FRAMES):
        sheet.alpha_composite(frame(base, n), ((n % 8) * CELL[0], (n // 8) * CELL[1]))
    sheet.save(ROOT / "sleep.png", optimize=True)
    print(f"Wrote {ROOT / 'sleep.png'}: {sheet.size}, {FRAMES} frames")


if __name__ == "__main__":
    main()
