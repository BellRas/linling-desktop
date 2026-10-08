"""Render development 0.8 pixel-art action previews without changing source art."""
from pathlib import Path

from PIL import Image, ImageDraw

from build_motion24 import CELL, TARGET_COUNTS, cells

ROOT = Path(__file__).resolve().parent
PREVIEW = ROOT.parent / '预览'
PREVIEW.mkdir(exist_ok=True)
motion = cells('motion24.png', sum(TARGET_COUNTS[:17]))
gaze = cells('character05.png', 88)[72:88]
offsets = [sum(TARGET_COUNTS[:i]) for i in range(18)]


def on_background(frame):
    image = Image.new('RGB', CELL, '#424644')
    image.paste(frame, (0, 0), frame)
    return image


for row, name in ((0, 'breathing'), (7, 'thinking'),
                  (8, 'observing'), (15, 'top-tail')):
    frames = motion[offsets[row]:offsets[row + 1]]
    frames = [on_background(frame) for frame in frames]
    frames[0].save(PREVIEW / f'{name}-v08.gif', save_all=True,
                   append_images=frames[1:], duration=42, loop=0,
                   optimize=False, disposal=2)

samples = []
for row, name in ((0, '呼吸'), (7, '思考'), (8, '观察'), (15, '顶部尾巴')):
    frames = motion[offsets[row]:offsets[row + 1]]
    for index in (0, len(frames)//4, len(frames)//2, len(frames)*3//4):
        samples.append((name + f' {index}', frames[index]))
for index in (0, 4, 8, 12):
    samples.append((f'视线 {index}', gaze[index]))
sheet = Image.new('RGB', (CELL[0]*4, 235*5), '#424644')
draw = ImageDraw.Draw(sheet)
for i, (label, frame) in enumerate(samples):
    x, y = i%4*CELL[0], i//4*235
    sheet.paste(frame, (x, y), frame)
    draw.text((x+6, y+210), label, fill='#fff5d8')
sheet.save(PREVIEW / 'animation-v08.png', optimize=True)
print('v08 preview images and 24 fps GIFs created')
