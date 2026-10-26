"""Shared helpers for the Bus Driver asset generators.

Everything is drawn at a high resolution and then reduced, so the mobile
builds get clean edges instead of the chunky 1985-era pixels.
"""
from __future__ import annotations

import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
ART = os.path.join(ROOT, "Content", "art")
AUDIO = os.path.join(ROOT, "Content", "audio")
FONT = os.path.join(ROOT, "Content", "font")
IOS = os.path.join(ROOT, "src", "BusDriver.iOS")
ANDROID = os.path.join(ROOT, "src", "BusDriver.Android")

for folder in (ART, AUDIO, FONT):
    os.makedirs(folder, exist_ok=True)


def mix(a, b, t):
    return tuple(int(round(x + (y - x) * t)) for x, y in zip(a, b))


def rgba(colour, alpha=255):
    # PIL's ImageDraw replaces pixels rather than blending them, so drawing a
    # translucent colour onto an RGBA image punches a hole in it.  Every
    # colour laid down by a drawing call is therefore opaque; soft effects are
    # composited separately with Image.alpha_composite.
    return tuple(colour[:3]) + (255,)


def vertical_gradient(size, top, bottom):
    w, h = size
    arr = np.zeros((h, 1, 4), dtype=np.uint8)
    for y in range(h):
        t = y / max(1, h - 1)
        arr[y, 0] = rgba(mix(top, bottom, t))
    return Image.fromarray(np.repeat(arr, w, axis=1), "RGBA")


def rounded_mask(size, radius):
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        (0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=255
    )
    return mask


def rounded_panel(size, radius, fill, outline=None, border=0):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(
        (border // 2, border // 2, size[0] - 1 - border // 2, size[1] - 1 - border // 2),
        radius=radius,
        fill=rgba(fill),
        outline=rgba(outline) if outline else None,
        width=border,
    )
    return img


def grain(size, amount=12, seed=1, alpha=30):
    rng = np.random.default_rng(seed)
    noise = rng.normal(0, amount, (size[1], size[0], 1))
    base = np.zeros((size[1], size[0], 4), dtype=np.float32)
    base[..., :3] = 128 + noise
    base[..., 3] = alpha
    return Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGBA")


def add_noise(img, amount=10, seed=2, alpha=26):
    layer = grain(img.size, amount, seed, alpha)
    return Image.alpha_composite(img.convert("RGBA"), layer)


def soft_shadow(size, radius, blur=14, alpha=90):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    layer = Image.new("L", size, 0)
    ImageDraw.Draw(layer).rounded_rectangle(
        (radius, radius, size[0] - radius, size[1] - radius), radius=radius, fill=alpha
    )
    layer = layer.filter(ImageFilter.GaussianBlur(blur))
    img.putalpha(layer)
    return img


def save(img, name, folder=ART, quality=95):
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name)
    if name.endswith(".png"):
        img.save(path, "PNG", optimize=True)
    else:
        img.save(path, quality=quality)
    return path


def downsample(img, factor):
    if factor <= 1:
        return img
    return img.resize(
        (max(1, img.width // factor), max(1, img.height // factor)), Image.LANCZOS
    )


def vehicle_canvas(width, height):
    return Image.new("RGBA", (width, height), (0, 0, 0, 0))


def draw_wheel(d, cx, cy, r, colour=(24, 24, 26)):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=rgba(colour))
    d.ellipse((cx - r * 0.52, cy - r * 0.52, cx + r * 0.52, cy + r * 0.52),
              fill=rgba((92, 92, 96)))


def draw_roof_hatch(d, box, colour=(42, 46, 52)):
    d.rounded_rectangle(box, radius=max(2, (box[2] - box[0]) // 6), fill=rgba(colour))
    inner = (box[0] + 5, box[1] + 4, box[2] - 5, box[3] - 4)
    d.rounded_rectangle(inner, radius=max(1, (inner[2] - inner[0]) // 8),
                        outline=rgba((110, 118, 126), 150), width=3)


def draw_text(d, xy, text, font, fill, anchor="la", stroke=0, stroke_fill=None):
    d.text(xy, text, font=font, fill=rgba(fill), anchor=anchor,
           stroke_width=stroke, stroke_fill=rgba(stroke_fill) if stroke_fill else None)
