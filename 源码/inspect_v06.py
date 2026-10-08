"""Create a focused contact sheet for anatomy regression work."""

from pathlib import Path
from PIL import Image, ImageDraw

from build_motion24 import CELL, TARGET_COUNTS, cells

ROOT = Path(__file__).resolve().parent
character = cells("character05.png", 88)
original = cells("spritesheet.png", 136)
motion = cells("motion24.png", sum(TARGET_COUNTS[:17]))
offsets = [sum(TARGET_COUNTS[:i]) for i in range(18)]
entries = [("idle", character[0]), ("look right", character[76]),
           ("run R 0", motion[offsets[1]]), ("run R 4", motion[offsets[1] + 4]),
           ("run L 0", motion[offsets[2]]), ("run L 4", motion[offsets[2] + 4]),
           ("wave clean 20", motion[offsets[3] + 20]),
           ("wave clean 24", motion[offsets[3] + 24]),
           ("top edge 0", motion[offsets[15]]),
           ("top edge 6", motion[offsets[15] + 6])]
scale = 3
canvas = Image.new("RGB", (CELL[0] * scale * 5, (CELL[1] + 24) * scale * 2), "#e1e4dd")
draw = ImageDraw.Draw(canvas)
for index, (label, frame) in enumerate(entries):
    x = index % 5 * CELL[0] * scale
    y = index // 5 * (CELL[1] + 24) * scale
    canvas.paste(frame.resize((CELL[0] * scale, CELL[1] * scale), Image.Resampling.NEAREST), (x, y),
                 frame.resize((CELL[0] * scale, CELL[1] * scale), Image.Resampling.NEAREST))
    draw.text((x + 12, y + CELL[1] * scale + 8), label, fill="#111111")
path = ROOT.parent / "预览" / "anatomy-v06-after.png"
canvas.save(path)
print(path)
