"""Isolate and register sixteen independently painted, complete gaze figures.

The input sheet has freely spaced characters rather than exact cell boundaries;
connected-component extraction prevents hair or feet crossing a notional grid.
"""

from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

from build_motion24 import CELL, cells, palette_from, write_sheet


SOURCE = 'gaze-full-pose-sheet-key-v10.png'


def components(image):
    alpha = np.asarray(image.getchannel('A')) >= 160
    height, width = alpha.shape
    opaque = bytearray(alpha.astype('uint8').tobytes())
    seen = bytearray(len(opaque))
    figures = []
    for seed in range(len(opaque)):
        if not opaque[seed] or seen[seed]:
            continue
        queue = deque([seed])
        seen[seed] = 1
        pixels = []
        while queue:
            point = queue.popleft()
            pixels.append(point)
            x, y = point % width, point // width
            neighbours = (point-1 if x else -1,
                          point+1 if x+1 < width else -1,
                          point-width if y else -1,
                          point+width if y+1 < height else -1)
            for neighbour in neighbours:
                if neighbour >= 0 and opaque[neighbour] and not seen[neighbour]:
                    seen[neighbour] = 1
                    queue.append(neighbour)
        if len(pixels) < 1000:
            continue
        ys = np.fromiter((p // width for p in pixels), dtype=np.int32)
        xs = np.fromiter((p % width for p in pixels), dtype=np.int32)
        left, top, right, bottom = xs.min(), ys.min(), xs.max()+1, ys.max()+1
        mask = np.zeros((bottom-top, right-left), dtype=np.uint8)
        mask[ys-top, xs-left] = 255
        cutout = image.crop((left, top, right, bottom))
        cutout.putalpha(Image.fromarray(mask, 'L'))
        figures.append(((top, left), cutout))
    if len(figures) != 16:
        raise ValueError(f'Expected sixteen complete painted figures, got {len(figures)}')
    figures.sort(key=lambda item: (item[0][0] // 340, item[0][1]))
    return [figure for _, figure in figures]


def normalize(figure, palette, idle, scale):
    bounds = idle.getchannel('A').getbbox()
    target_size = (round(figure.width * scale[0]),
                   round(figure.height * scale[1]))
    figure = figure.resize(target_size, Image.Resampling.NEAREST)
    rgb = figure.convert('RGB').quantize(palette=palette,
                                         dither=Image.Dither.NONE).convert('RGB')
    rgb.putalpha(figure.getchannel('A'))
    frame = Image.new('RGBA', CELL)
    frame.alpha_composite(rgb, ((CELL[0]-rgb.width)//2, bounds[3]-rgb.height))
    # Independent painted frames place their shoes a few pixels apart.  Align
    # the actual stance, not just the outer hair/tail bounding rectangle.
    def foot_centre(pose):
        region = np.asarray(pose.getchannel('A'))[171:197, 65:130] > 128
        _, xx = np.where(region)
        return float(xx.mean() + 65)
    shift = round(foot_centre(idle) - foot_centre(frame))
    if shift:
        pixels = np.asarray(frame)
        moved = np.zeros_like(pixels)
        if shift > 0:
            moved[:, shift:] = pixels[:, :CELL[0]-shift]
        else:
            moved[:, :shift] = pixels[:, -shift:]
        frame = Image.fromarray(moved, 'RGBA')
    return frame


def main():
    source = Image.open(Path(__file__).with_name(SOURCE)).convert('RGBA')
    idle = cells('character05.png', 88)[0]
    palette = palette_from(cells('spritesheet.png', 136))
    figures = components(source)
    box = idle.getchannel('A').getbbox()
    # One calibration for the whole sheet avoids stretching every pose by a
    # different amount as its turned silhouette becomes narrower or wider.
    scale = ((box[2]-box[0]) / float(np.median([f.width for f in figures])),
             (box[3]-box[1]) / float(np.median([f.height for f in figures])))
    frames = [normalize(figure, palette, idle, scale) for figure in figures]
    write_sheet(frames, 'gaze-full-v10.png')
    print('isolated sixteen full-body gaze poses')


if __name__ == '__main__':
    main()
