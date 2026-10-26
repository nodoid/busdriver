"""App icons and launch/splash art.

The iOS asset catalogue is generated whole, including every icon size and the
Contents.json that points at them, so Xcode never has to invent one.
"""
from __future__ import annotations

import json
import os

from PIL import Image, ImageDraw, ImageFont

from common import IOS, ANDROID, ART, mix, rgba, save, vertical_gradient
import art

IMPACT = "/System/Library/Fonts/Supplemental/Impact.ttf"
ROUNDED = "/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf"
ARIAL = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"


def app_icon(size=1024):
    img = vertical_gradient((size, size), (214, 62, 52), (120, 20, 28))
    d = ImageDraw.Draw(img)

    # Warm corner light baked into the background gradient.
    for i in range(20):
        r = size * (0.72 - i * 0.032)
        d.ellipse((-size * 0.20 - r, -size * 0.30 - r,
                   -size * 0.20 + r, -size * 0.30 + r),
                  fill=mix((198, 46, 40), (238, 122, 70), i / 19))

    # Big cream roundel.
    m = size * 0.07
    d.ellipse((m, m, size - m, size - m), fill=(246, 236, 208, 255))
    d.ellipse((m + 20, m + 20, size - m - 20, size - m - 20),
              outline=(196, 38, 42, 255), width=14)

    # Front of the bus.
    bw, bh = size * 0.50, size * 0.46
    x0 = (size - bw) / 2
    y0 = size * 0.18
    d.rounded_rectangle((x0, y0, x0 + bw, y0 + bh), radius=size * 0.07,
                        fill=(196, 38, 42, 255))
    d.rounded_rectangle((x0 + 18, y0 + 18, x0 + bw - 18, y0 + bh - 18),
                        radius=size * 0.05, fill=(226, 58, 48, 255))
    # Windscreen.
    d.rounded_rectangle((x0 + 42, y0 + 46, x0 + bw - 42, y0 + bh * 0.40),
                        radius=size * 0.028, fill=(34, 48, 62, 255))
    d.rounded_rectangle((x0 + 54, y0 + 58, x0 + bw - 54, y0 + bh * 0.40 - 12),
                        radius=size * 0.02, fill=(92, 126, 148, 255))
    # Destination blind.
    d.rounded_rectangle((x0 + bw * 0.25, y0 + 12, x0 + bw * 0.75, y0 + 40),
                        radius=6, fill=(248, 238, 206, 255))
    d.text((size / 2, y0 + 26), "BUS", font=ImageFont.truetype(IMPACT, int(size * 0.033)),
           fill=(32, 34, 40, 255), anchor="mm")
    # Headlights and bumper.
    d.rounded_rectangle((x0 + 34, y0 + bh * 0.56, x0 + 92, y0 + bh * 0.70),
                        radius=10, fill=(255, 232, 150, 255))
    d.rounded_rectangle((x0 + bw - 92, y0 + bh * 0.56, x0 + bw - 34, y0 + bh * 0.70),
                        radius=10, fill=(255, 232, 150, 255))
    d.rounded_rectangle((x0 + 24, y0 + bh * 0.78, x0 + bw - 24, y0 + bh * 0.88),
                        radius=8, fill=(58, 42, 38, 255))
    # Wheel arches.
    d.ellipse((x0 - 8, y0 + bh * 0.68, x0 + 46, y0 + bh * 0.92),
              fill=(34, 30, 30, 255))
    d.ellipse((x0 + bw - 46, y0 + bh * 0.68, x0 + bw + 8, y0 + bh * 0.92),
              fill=(34, 30, 30, 255))

    d.text((size * 0.5, size * 0.735), "BUS DRIVER",
           font=ImageFont.truetype(IMPACT, int(size * 0.072)),
           fill=(196, 38, 42, 255), anchor="mm")
    d.text((size * 0.5, size * 0.815), "SCARTHORPE",
           font=ImageFont.truetype(ARIAL, int(size * 0.030)),
           fill=(32, 56, 96, 255), anchor="mm")
    return img.convert("RGB")


def launch_image(w, h):
    img = vertical_gradient((w, h), (10, 18, 32), (30, 24, 32))
    d = ImageDraw.Draw(img)

    # A few town lights.
    for x, y, r in ((w * 0.08, h * 0.72, 4), (w * 0.17, h * 0.78, 3),
                    (w * 0.86, h * 0.66, 4), (w * 0.93, h * 0.75, 3),
                    (w * 0.76, h * 0.8, 3)):
        d.ellipse((x - r, y - r, x + r, y + r), fill=(248, 200, 118, 220))

    logo = art.logo().resize((int(w * 0.72), int(h * 0.28)), Image.LANCZOS)
    img.alpha_composite(logo, ((w - logo.width) // 2, int(h * 0.30)))

    d.text((w // 2, int(h * 0.66)), "LOADING THE TIMETABLE...",
           font=ImageFont.truetype(ARIAL, max(12, int(h * 0.034))),
           fill=(238, 198, 70, 255), anchor="mm")
    d.text((w // 2, int(h * 0.74)), "SCARTHORPE TRANSPORT",
           font=ImageFont.truetype(ARIAL, max(9, int(h * 0.023))),
           fill=(140, 150, 160, 255), anchor="mm")
    return img


def write_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


def generate_ios():
    base = app_icon(1024)
    iconset = os.path.join(IOS, "Assets.xcassets", "AppIcon.appiconset")
    os.makedirs(iconset, exist_ok=True)

    # Xcode 27 wants a single 1024 universal icon; the old multi-size set is
    # accepted but its legacy 1x phone entries are left unassigned.
    for name in os.listdir(iconset):
        if name.endswith(".png"):
            os.remove(os.path.join(iconset, name))
    base.save(os.path.join(iconset, "AppIcon.png"), "PNG")
    write_json(os.path.join(iconset, "Contents.json"), {
        "images": [{
            "filename": "AppIcon.png",
            "idiom": "universal",
            "platform": "ios",
            "size": "1024x1024",
        }],
        "info": {"version": 1, "author": "xcode"},
    })

    launchset = os.path.join(IOS, "Assets.xcassets", "LaunchImage.imageset")
    os.makedirs(launchset, exist_ok=True)
    for scale, (w, h) in {1: (844, 390), 2: (1688, 780), 3: (2532, 1170)}.items():
        launch_image(w, h).save(
            os.path.join(launchset, f"LaunchImage{'' if scale == 1 else '@' + str(scale) + 'x'}.png"),
            "PNG")
    write_json(os.path.join(launchset, "Contents.json"), {
        "images": [
            {"idiom": "universal", "scale": "1x", "filename": "LaunchImage.png"},
            {"idiom": "universal", "scale": "2x", "filename": "LaunchImage@2x.png"},
            {"idiom": "universal", "scale": "3x", "filename": "LaunchImage@3x.png"},
        ],
        "info": {"version": 1, "author": "xcode"},
    })

    colorset = os.path.join(IOS, "Assets.xcassets", "LaunchBackground.colorset")
    os.makedirs(colorset, exist_ok=True)
    write_json(os.path.join(colorset, "Contents.json"), {
        "colors": [{
            "idiom": "universal",
            "color": {
                "color-space": "srgb",
                "components": {
                    "alpha": "1.000",
                    "blue": "0.125",
                    "green": "0.070",
                    "red": "0.040",
                },
            },
        }],
        "info": {"version": 1, "author": "xcode"},
    })

    save(launch_image(1600, 900), "splash.png")


def generate_android():
    base = app_icon(1024)
    densities = {
        "mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192,
    }
    for density, px in densities.items():
        folder = os.path.join(ANDROID, "Resources", f"mipmap-{density}")
        os.makedirs(folder, exist_ok=True)
        base.resize((px, px), Image.LANCZOS).save(
            os.path.join(folder, "appicon.png"), "PNG")


def generate():
    generate_ios()
    generate_android()
