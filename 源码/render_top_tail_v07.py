"""Render the clipped top-edge tail at actual sprite size and 24 fps."""
from pathlib import Path
from PIL import Image, ImageDraw
from build_motion24 import CELL, TARGET_COUNTS, cells

ROOT = Path(__file__).resolve().parent
OUT = ROOT.parent / '预览'
sheet = cells('motion24.png', sum(TARGET_COUNTS[:17]))
start = sum(TARGET_COUNTS[:15])
frames = []
for index in range(TARGET_COUNTS[15]):
    clipped = sheet[start + index].copy()
    clipped.paste((0, 0, 0, 0), (0, 0, CELL[0], 66))
    canvas = Image.new('RGBA', CELL, '#e8ebe6')
    canvas.alpha_composite(clipped)
    frames.append(canvas.convert('RGB').quantize(colors=192))
frames[0].save(OUT / 'top-tail-v07.gif', save_all=True,
               append_images=frames[1:], duration=42, loop=0,
               optimize=False, disposal=2)
contact = Image.new('RGBA', (CELL[0] * 6, CELL[1] * 2), '#e8ebe6')
draw = ImageDraw.Draw(contact)
for position, index in enumerate(range(0, TARGET_COUNTS[15], 6)):
    x, y = position % 6 * CELL[0], position // 6 * CELL[1]
    clipped = sheet[start + index].copy()
    clipped.paste((0, 0, 0, 0), (0, 0, CELL[0], 66))
    contact.alpha_composite(clipped, (x, y))
    draw.line((x, y + 66, x + CELL[0], y + 66), fill='#777777')
    draw.text((x + 4, y + 4), str(index), fill='#223344')
contact.save(OUT / 'top-tail-v07.png')
