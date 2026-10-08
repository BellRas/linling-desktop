"""Review the corrected standing silhouette and all sixteen mouse directions."""

from pathlib import Path

from PIL import Image, ImageDraw

from build_motion24 import CELL


ROOT = Path(__file__).resolve().parent
OUT = ROOT.parent / "预览"
sheet = Image.open(ROOT / "character05.png").convert("RGBA")


def sprite(ordinal):
    x, y = ordinal % 8 * CELL[0], ordinal // 8 * CELL[1]
    return sheet.crop((x, y, x + CELL[0], y + CELL[1]))


contact = Image.new("RGBA", (CELL[0] * 8, (CELL[1] + 24) * 3), "#e8ebe6")
draw = ImageDraw.Draw(contact)
for index in range(8):
    contact.alpha_composite(sprite(index), (index * CELL[0], 24))
    draw.text((index * CELL[0] + 5, 4), "idle / blink " + str(index), fill="#304535")
for index in range(16):
    x = index % 8 * CELL[0]
    y = (index // 8 + 1) * (CELL[1] + 24)
    contact.alpha_composite(sprite(72 + index), (x, y + 24))
    draw.text((x + 5, y + 4), "look " + str(index), fill="#304535")
contact.save(OUT / "idle-gaze-v07.png")

frames = []
for index in range(16):
    frame = Image.new("RGBA", CELL, "#e8ebe6")
    frame.alpha_composite(sprite(72 + index))
    frames.append(frame.convert("RGB").quantize(colors=192))
frames[0].save(OUT / "idle-gaze-v07.gif", save_all=True,
               append_images=frames[1:], duration=125, loop=0,
               optimize=False, disposal=2)
print(OUT / "idle-gaze-v07.png")
