"""Bitmap font atlases.

The game ships its own font format so no content pipeline or system font
lookup is needed on Android or iOS:

    font_<name>.png   white glyphs on transparent, tinted at draw time
    font_<name>.json  metrics per glyph
"""
from __future__ import annotations

import json
import os

from PIL import Image, ImageDraw, ImageFont

from common import FONT

ROUNDED = "/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf"
IMPACT = "/System/Library/Fonts/Supplemental/Impact.ttf"
FALLBACK = "/System/Library/Fonts/Supplemental/Arial Unicode.ttf"

CHARS = (
    "".join(chr(c) for c in range(32, 127))
    + "▲▼◀▶–—·×°’‘“”£€½¼¾…©•✔✖→"
)

FONTS = [
    ("tiny", ROUNDED, 18),
    ("small", ROUNDED, 24),
    ("body", ROUNDED, 32),
    ("medium", ROUNDED, 44),
    ("large", IMPACT, 72),
    ("huge", IMPACT, 108),
]

PAD = 4
COLUMNS = 16
NOTDEF = "\ue000"


def _renders_as_notdef(font, ch: str) -> bool:
    if ch == " ":
        return False
    box = max(16, font.size * 2)

    def render(c: str) -> bytes:
        img = Image.new("L", (box, box), 0)
        ImageDraw.Draw(img).text((font.size, font.size // 2), c, font=font, fill=255)
        return img.tobytes()

    return render(ch) == render(NOTDEF)


def build(name: str, ttf: str, size: int) -> dict:
    font = ImageFont.truetype(ttf, size)
    fallback = ImageFont.truetype(FALLBACK, size) if os.path.exists(FALLBACK) else font
    ascent, descent = font.getmetrics()
    line_height = ascent + descent

    glyphs = {}
    cells = []
    cell_w, cell_h = 0, line_height + PAD * 2
    for ch in CHARS:
        face = fallback if _renders_as_notdef(font, ch) else font
        bbox = face.getbbox(ch)
        if bbox is None:
            continue
        x0, y0, x1, y1 = bbox
        w, h = max(0, x1 - x0), max(1, y1 - y0)
        adv = face.getlength(ch)
        glyphs[ch] = {"ox": x0, "oy": y0, "w": w, "h": h,
                      "adv": round(float(adv), 2)}
        cells.append((ch, x0, y0, w, h, face))
        cell_w = max(cell_w, w + PAD * 2)

    rows = (len(cells) + COLUMNS - 1) // COLUMNS
    atlas = Image.new("RGBA", (cell_w * COLUMNS, cell_h * rows), (0, 0, 0, 0))
    draw = ImageDraw.Draw(atlas)

    for index, (ch, x0, y0, w, h, face) in enumerate(cells):
        cx = (index % COLUMNS) * cell_w
        cy = (index // COLUMNS) * cell_h
        draw.text((cx + PAD - x0, cy + PAD), ch, font=face,
                  fill=(255, 255, 255, 255))
        glyphs[ch]["x"] = cx + PAD
        glyphs[ch]["y"] = cy + PAD + y0
        glyphs[ch]["oy"] = y0

    os.makedirs(FONT, exist_ok=True)
    atlas.save(os.path.join(FONT, f"font_{name}.png"), optimize=True)
    meta = {
        "name": name,
        "size": size,
        "lineHeight": line_height,
        "ascent": ascent,
        "descent": descent,
        "spaceAdvance": round(float(font.getlength(" ")), 2),
        "glyphs": glyphs,
    }
    with open(os.path.join(FONT, f"font_{name}.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=1, ensure_ascii=False)
    return meta


def build_all() -> list[str]:
    return [build(name, ttf, size)["name"] for name, ttf, size in FONTS]
