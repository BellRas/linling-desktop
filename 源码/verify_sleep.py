"""Check sleep frames against the established Linling sprite grid and palette."""

import json
from pathlib import Path

from PIL import Image
from build_sleep import source_palette

ROOT = Path(__file__).resolve().parent
sheet = Image.open(ROOT / "sleep.png").convert("RGBA")
base = Image.open(ROOT / "sleep-base.png").convert("RGBA")
original = Image.open(ROOT / "spritesheet.png").convert("RGBA").crop((0, 0, 192, 208))
frames = [sheet.crop((i % 8 * 192, i // 8 * 208, (i % 8 + 1) * 192, (i // 8 + 1) * 208)) for i in range(24)]
bounds = [im.getchannel("A").getbbox() for im in frames]
source_colors = source_palette().getpalette()
palette_rgb = {tuple(source_colors[i : i + 3]) for i in range(0, 768, 3)}
used = {(r, g, b) for im in frames for r, g, b, a in im.getdata() if a}
alpha = {a for im in frames for r, g, b, a in im.getdata()}
unique = len({im.tobytes() for im in frames})
report = {
    "ok": sheet.size == (1536, 624)
    and len(frames) == 24
    and unique >= 3
    and all(box and box[0] >= 10 and box[1] >= 20 and box[2] <= 182 and box[3] <= 205 for box in bounds)
    and alpha == {0, 255}
    and used.issubset(palette_rgb),
    "sheet": list(sheet.size),
    "frameSize": [192, 208],
    "frames": len(frames),
    "distinctPixelPoses": unique,
    "alphaValues": sorted(alpha),
    "allFramesInsideCell": all(box and box[0] >= 10 and box[1] >= 20 and box[2] <= 182 and box[3] <= 205 for box in bounds),
    "sourcePaletteColors": len(palette_rgb),
    "sleepColors": len(used),
    "bounds": bounds,
}
print(json.dumps(report, ensure_ascii=False, indent=2))
if not report["ok"]:
    raise SystemExit(1)
