"""Render a compact review GIF for the authored wave and jump paths."""

from math import pi, sin
from pathlib import Path

from PIL import Image, ImageDraw

from build_motion24 import CELL, TARGET_COUNTS


ROOT = Path(__file__).resolve().parent
OUT = ROOT.parent / "预览" / "wave-jump-24fps.gif"
CONTACT = ROOT.parent / "预览" / "wave-jump-keyframes.png"
WAVE_CONTACT = ROOT.parent / "预览" / "wave-11-poses-v07.png"
sheet = Image.open(ROOT / "motion24.png").convert("RGBA")
wave_start = sum(TARGET_COUNTS[:3])
jump_start = sum(TARGET_COUNTS[:4])


def sprite(ordinal):
    x, y = ordinal % 8 * CELL[0], ordinal // 8 * CELL[1]
    return sheet.crop((x, y, x + CELL[0], y + CELL[1]))


def jump_lift(fraction):
    if fraction <= .18 or fraction >= .82:
        return 0
    return round(CELL[1] * .23 * sin(pi * (fraction - .18) / .64))


frames = []
for index in range(TARGET_COUNTS[3]):
    canvas = Image.new("RGBA", (400, 306), "#E9EDE6")
    draw = ImageDraw.Draw(canvas)
    draw.line((0, 292, 399, 292), fill="#B9C4B4", width=2)
    draw.text((68, 3), "WAVE", fill="#344336")
    draw.text((269, 3), "JUMP", fill="#344336")
    canvas.alpha_composite(sprite(wave_start + index), (4, 82))
    jump_index=min(TARGET_COUNTS[4]-1, index)
    canvas.alpha_composite(sprite(jump_start + jump_index),
                           (204, 82 - jump_lift(jump_index / TARGET_COUNTS[4])))
    frames.append(canvas.convert("RGB").quantize(colors=192, method=Image.Quantize.MEDIANCUT))
frames[0].save(OUT, save_all=True, append_images=frames[1:], duration=42,
               loop=0, optimize=False, disposal=2)
contact=Image.new("RGBA",(8*192,2*208),(233,237,230,255))
for column,index in enumerate((0,9,15,21,30,36,51,71)):
    contact.alpha_composite(sprite(wave_start+index),(column*192,0))
for column,index in enumerate((0,3,6,8,16,24,29,35)):
    contact.alpha_composite(sprite(jump_start+index),(column*192,208))
contact.save(CONTACT)
wave_contact = Image.new("RGBA", (11 * CELL[0], CELL[1]), (233, 237, 230, 255))
for column, index in enumerate((0, 3, 6, 9, 12, 15, 18, 21, 30, 33, 36)):
    wave_contact.alpha_composite(sprite(wave_start + index), (column * CELL[0], 0))
wave_contact.save(WAVE_CONTACT)
print(OUT)
