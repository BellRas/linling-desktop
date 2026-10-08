"""Build sixteen complete, jointly painted head-and-body gaze directions."""

from pathlib import Path

from PIL import Image, ImageDraw

from build_motion24 import (CELL, authored_pose, breath_pose, cells,
                            palette_from, write_sheet)


ROOT = Path(__file__).resolve().parent
def eyelids(base, closed_source, half=False):
    mask = Image.new("L", CELL)
    draw = ImageDraw.Draw(mask)
    draw.ellipse((78, 85, 101, 100), fill=255)
    draw.ellipse((109, 85, 132, 100), fill=255)
    if half:
        draw.rectangle((0, 94, CELL[0], CELL[1]), fill=0)
    frame = base.copy()
    frame.paste(closed_source, (0, 0), mask)
    return frame


def main():
    source = cells("spritesheet.png", 136)
    palette = palette_from(source)
    idle = authored_pose("idle-key-v06.png", palette, 187)
    closed_master = authored_pose("blink-key-v05.png", palette, 192)
    half_blink = eyelids(idle, closed_master, half=True)
    blink = eyelids(idle, closed_master)
    standing = [idle, half_blink, blink, half_blink,
                idle.copy(), idle.copy(), idle.copy(), idle.copy()]
    # Every direction is a complete sprite.  The old head-only polygon mask
    # introduced a visible neck/shoulder seam and is deliberately not used.
    looks = cells('gaze-full-v10.png', 16)

    sheet = Image.open(ROOT / "spritesheet.png").convert("RGBA")
    for i, frame in enumerate(standing):
        sheet.paste(frame, ((i % 8) * CELL[0], (i // 8) * CELL[1]))
    for i, frame in enumerate(looks, start=72):
        sheet.paste(frame, ((i % 8) * CELL[0], (i // 8) * CELL[1]))
    sheet.save(ROOT / "character05.png", optimize=True)
    write_sheet([breath_pose(look, rise) for look in looks for rise in range(4)],
                'gaze-breath-v10.png')
    print("sixteen complete gaze poses and breathing gaze atlas")


if __name__ == "__main__":
    main()
