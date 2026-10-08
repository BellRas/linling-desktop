"""Create reviewable v10 full-body gaze and transition previews."""

from pathlib import Path

from PIL import Image, ImageDraw

from build_motion24 import CELL, cells


OUT = Path(__file__).resolve().parent.parent / '预览'
OUT.mkdir(exist_ok=True)
character = cells('character05.png', 88)
looks = character[72:88]
idle = character[0]


def on_background(frame):
    canvas = Image.new('RGB', CELL, '#424644')
    canvas.paste(frame, (0, 0), frame)
    return canvas


def save_gif(frames, name):
    images = [on_background(frame) for frame in frames]
    images[0].save(OUT / name, save_all=True, append_images=images[1:],
                   duration=42, loop=0, disposal=2, optimize=False)


sheet = Image.new('RGB', (CELL[0] * 4, 235 * 4), '#424644')
draw = ImageDraw.Draw(sheet)
for i, frame in enumerate(looks):
    x, y = (i % 4) * CELL[0], (i // 4) * 235
    sheet.paste(frame, (x, y), frame)
    draw.text((x+5, y+209), f'{i:02d}', fill='#fff5d8')
sheet.save(OUT / 'gaze-16-v10.png', optimize=True)

# Both sweeps use a 24-fps GIF clock.  The short version exposes the largest
# possible per-frame sector jump; the slower one reflects ordinary motion.
save_gif([idle] * 8 + [look for look in looks] +
         [look for look in reversed(looks)] + [idle] * 8,
         'gaze-sweep-fast-v10.gif')
save_gif([idle] * 8 + [look for look in looks for _ in range(3)] +
         [look for look in reversed(looks) for _ in range(3)] + [idle] * 8,
         'gaze-sweep-v10.gif')
print('v10 gaze contacts and 24-fps transition previews created')
