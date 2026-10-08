"""Render the repainted sixteen-way gaze and action previews."""
from pathlib import Path

from PIL import Image, ImageDraw

from build_motion24 import CELL, TARGET_COUNTS, cells

ROOT = Path(__file__).resolve().parent
OUT = ROOT.parent / '预览'
OUT.mkdir(exist_ok=True)
character = cells('character05.png', 88)
motion = cells('motion24.png', sum(TARGET_COUNTS[:17]))
offsets = [sum(TARGET_COUNTS[:row]) for row in range(18)]


def on_background(frame):
    canvas = Image.new('RGB', CELL, '#424644')
    canvas.paste(frame, (0, 0), frame)
    return canvas


def gif(frames, path):
    pictures = [on_background(frame) for frame in frames]
    pictures[0].save(OUT / path, save_all=True,
                     append_images=pictures[1:], duration=42, loop=0,
                     optimize=False, disposal=2)


looks = character[72:88]
gif([looks[index] for index in range(16) for _ in range(8)], 'gaze-v09.gif')
for row, name in ((7, 'thinking'), (8, 'observing'), (15, 'top-tail')):
    gif(motion[offsets[row]:offsets[row+1]], f'{name}-v09.gif')

directions = ('上', '上偏右', '右上', '偏右上', '右', '偏右下', '右下', '下偏右',
              '下', '下偏左', '左下', '偏左下', '左', '偏左上', '左上', '上偏左')
sheet = Image.new('RGB', (CELL[0]*4, 235*4), '#424644')
draw = ImageDraw.Draw(sheet)
for index, (label, frame) in enumerate(zip(directions, looks)):
    x, y = index%4*CELL[0], index//4*235
    sheet.paste(frame, (x, y), frame)
    # Numeric labels remain portable where the image renderer lacks CJK fonts.
    draw.text((x+6, y+210), f'{index:02d}', fill='#fff5d8')
sheet.save(OUT / 'gaze-16-v09.png', optimize=True)

key_indices = (0, 3, 8, 13, 18, 23, 28)
action_sheet = Image.new('RGB', (CELL[0]*7, 235*2), '#424644')
action_draw = ImageDraw.Draw(action_sheet)
for row_index, row in enumerate((7, 8)):
    for column, frame_index in enumerate(key_indices):
        frame = motion[offsets[row] + frame_index]
        x, y = column*CELL[0], row_index*235
        action_sheet.paste(frame, (x, y), frame)
        action_draw.text((x+6, y+210),
                         ('thinking' if row == 7 else 'observing') + f' {column}',
                         fill='#fff5d8')
action_sheet.save(OUT / 'action-keys-v09.png', optimize=True)
print('v09 gaze, thinking, observing, and top-tail previews created')
