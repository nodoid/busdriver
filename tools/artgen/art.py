"""High-resolution art for Bus Driver.

The old Spectrum graphics were 8x8 character blocks; these are drawn at four
to eight times the size the phone actually needs, with lighting, kerbs, roof
details and readable route blinds.
"""
from __future__ import annotations

import math
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

from common import (ART, add_noise, draw_roof_hatch, draw_text, draw_wheel,
                    grain, mix, rgba, rounded_mask, save, vehicle_canvas,
                    vertical_gradient)

IMPACT = "/System/Library/Fonts/Supplemental/Impact.ttf"
ROUNDED = "/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf"
ARIAL = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"

TILE = 256


# ---------------------------------------------------------------- town tiles
def grass(seed=1):
    img = vertical_gradient((TILE, TILE), (108, 146, 78), (82, 119, 63))
    rng = random.Random(seed)
    d = ImageDraw.Draw(img)
    for _ in range(260):
        x = rng.randrange(TILE)
        y = rng.randrange(TILE)
        h = rng.randrange(3, 9)
        d.line((x, y, x + rng.choice((-2, -1, 1, 2)), y - h),
               fill=rgba((66, 100, 52), rng.randrange(60, 150)), width=1)
    for _ in range(14):
        x, y = rng.randrange(TILE), rng.randrange(TILE)
        r = rng.randrange(4, 13)
        d.ellipse((x - r, y - r, x + r, y + r), fill=rgba((73, 108, 57), 120))
    return add_noise(img, 5, seed, 16)


def pavement(seed=2):
    img = vertical_gradient((TILE, TILE), (176, 176, 172), (150, 151, 149))
    d = ImageDraw.Draw(img)
    step = 64
    for y in range(0, TILE, step):
        for x in range(0, TILE, step):
            shade = 166 + ((x // step + y // step) % 3) * 4
            d.rectangle((x + 2, y + 2, x + step - 3, y + step - 3),
                        fill=rgba((shade, shade, shade - 3)))
            d.line((x + 2, y + 2, x + step - 3, y + 2), fill=rgba((190, 190, 186)))
            d.line((x + 2, y + step - 3, x + step - 3, y + step - 3),
                   fill=rgba((132, 133, 132)))
    for _ in range(170):
        x, y = random.randrange(TILE), random.randrange(TILE)
        d.point((x, y), fill=rgba((110, 110, 108), 90))
    return add_noise(img, 4, seed, 14)


def road(seed=3):
    img = vertical_gradient((TILE, TILE), (70, 72, 76), (55, 57, 61))
    rng = random.Random(seed)
    d = ImageDraw.Draw(img)
    for _ in range(2600):
        x, y = rng.randrange(TILE), rng.randrange(TILE)
        v = rng.randrange(-18, 22)
        c = tuple(max(0, min(255, 62 + v)) for _ in range(3))
        d.point((x, y), fill=rgba(c, 120))
    for _ in range(18):
        x, y = rng.randrange(TILE), rng.randrange(TILE)
        r = rng.randrange(8, 30)
        d.ellipse((x - r, y - r // 2, x + r, y + r // 2),
                  fill=rgba((48, 50, 54), 45))
    return add_noise(img, 6, seed, 22)


def terminus_tile(seed=4):
    img = road(seed)
    overlay = Image.new("RGBA", img.size, (42, 84, 116, 38))
    img = Image.alpha_composite(img, overlay)
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(IMPACT, 62)
    draw_text(d, (TILE // 2, TILE // 2), "BUS", font, (235, 238, 240, 205), anchor="mm")
    return add_noise(img, 4, seed, 12)


def building(variant, seed=5):
    palettes = [
        ((116, 74, 64), (88, 55, 49), (154, 111, 91)),
        ((82, 88, 104), (61, 66, 82), (128, 134, 150)),
        ((128, 110, 78), (96, 80, 55), (168, 148, 108)),
        ((94, 78, 92), (70, 58, 70), (140, 121, 138)),
    ]
    roof, dark, light = palettes[variant % 4]
    img = vertical_gradient((TILE, TILE), light, roof)
    d = ImageDraw.Draw(img)

    # Roof panels, ridge and shadow.
    d.rectangle((0, 0, TILE, 10), fill=rgba(dark))
    d.rectangle((0, TILE - 8, TILE, TILE), fill=rgba((28, 30, 34), 80))
    for i in range(4):
        x = 12 + i * 62
        d.rectangle((x, 18, x + 48, TILE - 26), fill=rgba(roof))
        d.line((x, 18, x, TILE - 26), fill=rgba(light, 130), width=3)
        d.line((x + 48, 18, x + 48, TILE - 26), fill=rgba(dark, 160), width=3)
        d.line((x + 4, 24, x + 44, 24), fill=rgba(light, 90), width=2)

    if variant == 0:
        for x, y in ((60, 70), (160, 130), (80, 190)):
            d.rounded_rectangle((x, y, x + 40, y + 30), radius=6,
                                fill=rgba((190, 214, 224), 210))
            d.line((x + 20, y + 4, x + 20, y + 26), fill=rgba(dark, 160), width=2)
    elif variant == 1:
        d.rectangle((155, 50, 215, 105), fill=rgba((58, 62, 70)))
        d.rectangle((162, 56, 208, 99), fill=rgba((116, 128, 138)))
        d.line((162, 78, 208, 78), fill=rgba((58, 62, 70)), width=3)
    elif variant == 2:
        d.rounded_rectangle((80, 84, 176, 180), radius=14,
                            fill=rgba((150, 132, 96), 220))
        d.ellipse((112, 116, 144, 148), fill=rgba((88, 72, 48)))
    else:
        d.polygon([(128, 40), (200, 132), (56, 132)], fill=rgba(light, 220))
        d.line((128, 40, 200, 132), fill=rgba(dark), width=4)
        d.line((128, 40, 56, 132), fill=rgba(dark), width=4)

    # A chimney or vent gives each block a bit of silhouette.
    rng = random.Random(seed + variant)
    for _ in range(2):
        x, y = rng.randrange(20, 210), rng.randrange(20, 210)
        d.rectangle((x, y, x + 18, y + 22), fill=rgba((56, 48, 44)))
        d.rectangle((x + 2, y + 2, x + 16, y + 8), fill=rgba((112, 98, 88)))
    return add_noise(img, 4, seed + variant, 16)


def park(variant, seed=6):
    img = grass(seed + variant)
    d = ImageDraw.Draw(img)
    rng = random.Random(seed + variant * 9)
    d.line((0, 190, 256, 150), fill=rgba((190, 170, 128)), width=16)
    d.line((0, 190, 256, 150), fill=rgba((208, 190, 146)), width=8)
    for _ in range(5):
        x, y = rng.randrange(20, 236), rng.randrange(20, 236)
        if 150 < y < 200:
            continue
        r = rng.randrange(12, 25)
        d.ellipse((x - r, y - r, x + r, y + r), fill=rgba((58, 96, 48), 220))
        d.ellipse((x - r + 4, y - r + 4, x + r - 4, y + r - 4),
                  fill=rgba((76, 124, 60), 220))
    return img


def water(variant, seed=7):
    img = vertical_gradient((TILE, TILE), (62, 118, 148), (34, 78, 110))
    d = ImageDraw.Draw(img)
    rng = random.Random(seed + variant)
    for _ in range(30):
        x = rng.randrange(-20, 246)
        y = rng.randrange(0, 256)
        w = rng.randrange(20, 90)
        d.arc((x, y, x + w, y + 16), 200, 340, fill=rgba((138, 190, 210), 80), width=2)
    return add_noise(img, 3, seed + variant, 18)


def tree(variant=0):
    size = 192
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((24, 38, 182, 192), fill=rgba((42, 66, 40)))
    for r, c in ((70, (52, 88, 44)), (58, (68, 112, 52)), (44, (88, 138, 64))):
        d.ellipse((96 - r, 88 - r, 96 + r, 88 + r), fill=rgba(c))
    rng = random.Random(variant + 3)
    for _ in range(20):
        a = rng.random() * math.tau
        rr = rng.random() * 38
        x = 96 + math.cos(a) * rr
        y = 88 + math.sin(a) * rr
        d.ellipse((x - 6, y - 6, x + 6, y + 6), fill=rgba((112, 162, 80), 170))
    return img


# ------------------------------------------------------------------ vehicles
def double_decker():
    """A UK double-decker seen from above: red flanks, cream upper-deck roof,
    two axles, a proper destination blind and a nearside door."""
    w, h = 1024, 340
    c = vehicle_canvas(w, h)
    d = ImageDraw.Draw(c)

    # Shadow and red lower body.
    d.rounded_rectangle((44, 38, 982, 302), radius=40, fill=rgba((20, 22, 26)))
    body = vertical_gradient((906, 244), (222, 52, 48), (154, 28, 32))
    mask = rounded_mask(body.size, 32)
    c.paste(body, (60, 48), mask)
    d = ImageDraw.Draw(c)

    # The big cream roof panel is what makes it read as a double-decker from
    # above, rather than a coach or a lorry.
    d.rounded_rectangle((90, 68, 902, 272), radius=30, fill=rgba((247, 238, 214)))
    d.rounded_rectangle((104, 80, 888, 260), radius=24, fill=rgba((252, 246, 226)))
    d.line((122, 92, 870, 92), fill=rgba((230, 210, 168)), width=4)

    # Roof advert panel and hatches.
    d.rounded_rectangle((300, 112, 700, 222), radius=16, fill=rgba((196, 38, 42)))
    draw_text(d, (500, 148), "SCARTHORPE", ImageFont.truetype(ARIAL, 31),
              (250, 240, 214), anchor="mm")
    draw_text(d, (500, 186), "BUS DRIVER", ImageFont.truetype(IMPACT, 36),
              (250, 240, 214), anchor="mm")
    draw_roof_hatch(d, (180, 118, 268, 212), (70, 76, 84))
    draw_roof_hatch(d, (746, 118, 834, 212), (70, 76, 84))

    # Upper-deck front window and lower windscreen at the nose.
    d.rounded_rectangle((890, 72, 944, 268), radius=14, fill=rgba((32, 38, 46)))
    d.rounded_rectangle((896, 82, 938, 162), radius=10, fill=rgba((86, 116, 136)))
    d.rounded_rectangle((896, 176, 938, 258), radius=10, fill=rgba((66, 92, 112)))
    d.line((902, 90, 932, 90), fill=rgba((176, 204, 218)), width=3)

    # Destination blind: white on black, as on a UK bus.
    d.rounded_rectangle((846, 102, 900, 142), radius=6, fill=rgba((22, 24, 28)))
    draw_text(d, (873, 114), "1A", ImageFont.truetype(IMPACT, 20), (250, 222, 110), anchor="mm")
    draw_text(d, (873, 133), "TOWN", ImageFont.truetype(ARIAL, 11), (250, 240, 210), anchor="mm")

    # Side windows along both flanks.
    for x in range(128, 838, 70):
        d.rounded_rectangle((x, 50, x + 52, 70), radius=7, fill=rgba((38, 54, 70)))
        d.rounded_rectangle((x, 272, x + 52, 292), radius=7, fill=rgba((38, 54, 70)))
        d.line((x + 4, 54, x + 48, 54), fill=rgba((132, 168, 188)), width=2)

    # Two axles, four wheels.  The nearside door is on the left flank.
    for cx in (236, 790):
        draw_wheel(d, cx, 34, 31)
        draw_wheel(d, cx, 306, 31)
    d.rounded_rectangle((660, 266, 730, 304), radius=6, fill=rgba((36, 40, 48)))
    d.line((668, 272, 668, 298), fill=rgba((120, 128, 136)), width=2)

    # Lights, mirrors and the little red roundel at the rear.
    d.rounded_rectangle((954, 94, 978, 126), radius=7, fill=rgba((255, 236, 160)))
    d.rounded_rectangle((954, 218, 978, 250), radius=7, fill=rgba((255, 236, 160)))
    d.ellipse((66, 148, 92, 174), fill=rgba((196, 38, 42)))
    d.ellipse((71, 153, 87, 169), fill=rgba((248, 240, 218)))
    draw_text(d, (79, 161), "BUS", ImageFont.truetype(ARIAL, 8), (196, 38, 42), anchor="mm")
    d.line((120, 66, 860, 66), fill=rgba((255, 142, 126)), width=3)
    return c


def single_decker():
    """A red UK single-decker: lower roof, cream band and a route blind."""
    w, h = 1024, 300
    c = vehicle_canvas(w, h)
    d = ImageDraw.Draw(c)

    d.rounded_rectangle((40, 38, 984, 262), radius=38, fill=rgba((20, 22, 26)))
    body = vertical_gradient((912, 210), (212, 48, 44), (146, 26, 30))
    mask = rounded_mask(body.size, 30)
    c.paste(body, (56, 45), mask)
    d = ImageDraw.Draw(c)

    # Cream roof band and a destination board.
    d.rounded_rectangle((88, 62, 916, 238), radius=26, fill=rgba((244, 232, 204)))
    d.rounded_rectangle((104, 76, 900, 224), radius=20, fill=rgba((198, 42, 40)))
    d.rounded_rectangle((186, 98, 820, 202), radius=16, fill=rgba((212, 54, 48)))
    draw_text(d, (503, 130), "SCARTHORPE BUSES", ImageFont.truetype(ARIAL, 28),
              (248, 236, 206), anchor="mm")
    draw_text(d, (503, 170), "SINGLE DECKER", ImageFont.truetype(IMPACT, 26),
              (248, 236, 206), anchor="mm")

    # Front glass and blind.
    d.rounded_rectangle((896, 66, 950, 232), radius=14, fill=rgba((32, 38, 46)))
    d.rounded_rectangle((902, 78, 944, 220), radius=10, fill=rgba((78, 108, 128)))
    d.line((908, 86, 938, 86), fill=rgba((176, 204, 218)), width=3)
    d.rounded_rectangle((842, 92, 896, 132), radius=6, fill=rgba((22, 24, 28)))
    draw_text(d, (869, 104), "2", ImageFont.truetype(IMPACT, 20), (250, 222, 110), anchor="mm")
    draw_text(d, (869, 123), "TOWN", ImageFont.truetype(ARIAL, 11), (250, 240, 210), anchor="mm")

    # Side windows.
    for x in range(126, 846, 70):
        d.rounded_rectangle((x, 50, x + 52, 68), radius=6, fill=rgba((38, 54, 70)))
        d.rounded_rectangle((x, 232, x + 52, 250), radius=6, fill=rgba((38, 54, 70)))
        d.line((x + 4, 54, x + 48, 54), fill=rgba((132, 168, 188)), width=2)

    # Four wheels and the nearside door.
    for cx in (238, 792):
        draw_wheel(d, cx, 32, 29)
        draw_wheel(d, cx, 268, 29)
    d.rounded_rectangle((664, 228, 728, 264), radius=6, fill=rgba((36, 40, 48)))
    d.line((672, 234, 672, 258), fill=rgba((120, 128, 136)), width=2)
    d.rounded_rectangle((952, 84, 974, 112), radius=6, fill=rgba((255, 236, 160)))
    d.rounded_rectangle((952, 188, 974, 216), radius=6, fill=rgba((255, 236, 160)))
    return c


def minibus():
    """A short red-and-cream minibus, nippy enough for the back streets."""
    w, h = 820, 300
    c = vehicle_canvas(w, h)
    d = ImageDraw.Draw(c)

    d.rounded_rectangle((34, 40, w - 34, 260), radius=42, fill=rgba((20, 22, 26)))
    body = vertical_gradient((w - 76, 208), (230, 64, 54), (170, 32, 36))
    mask = rounded_mask(body.size, 34)
    c.paste(body, (46, 46), mask)
    d = ImageDraw.Draw(c)

    d.rounded_rectangle((78, 66, w - 90, 234), radius=26, fill=rgba((244, 234, 210)))
    d.rounded_rectangle((94, 82, w - 106, 218), radius=20, fill=rgba((202, 44, 42)))
    draw_text(d, (w // 2, 132), "SCARTHORPE", ImageFont.truetype(ARIAL, 24),
              (250, 240, 214), anchor="mm")
    draw_text(d, (w // 2, 170), "MINIBUS", ImageFont.truetype(IMPACT, 28),
              (250, 240, 214), anchor="mm")

    # Windscreen and blind at the nose.
    d.rounded_rectangle((w - 132, 70, w - 78, 230), radius=14, fill=rgba((32, 38, 46)))
    d.rounded_rectangle((w - 126, 82, w - 84, 218), radius=10, fill=rgba((82, 112, 132)))
    d.line((w - 120, 90, w - 90, 90), fill=rgba((176, 204, 218)), width=3)
    d.rounded_rectangle((w - 188, 96, w - 134, 136), radius=6, fill=rgba((22, 24, 28)))
    draw_text(d, (w - 161, 108), "M", ImageFont.truetype(IMPACT, 22), (250, 222, 110), anchor="mm")
    draw_text(d, (w - 161, 126), "TOWN", ImageFont.truetype(ARIAL, 10), (250, 240, 210), anchor="mm")

    for x in range(120, w - 230, 66):
        d.rounded_rectangle((x, 54, x + 48, 72), radius=6, fill=rgba((38, 54, 70)))
        d.rounded_rectangle((x, 228, x + 48, 246), radius=6, fill=rgba((38, 54, 70)))

    for cx in (210, 620):
        draw_wheel(d, cx, 34, 29)
        draw_wheel(d, cx, 266, 29)
    d.rounded_rectangle((520, 224, 580, 260), radius=6, fill=rgba((36, 40, 48)))
    d.line((528, 230, 528, 254), fill=rgba((120, 128, 136)), width=2)
    d.rounded_rectangle((w - 58, 84, w - 36, 112), radius=6, fill=rgba((255, 236, 160)))
    d.rounded_rectangle((w - 58, 188, w - 36, 216), radius=6, fill=rgba((255, 236, 160)))
    return c


def car(colour, taxi=False, size=(384, 180)):
    w, h = size
    c = vehicle_canvas(w, h)
    d = ImageDraw.Draw(c)
    d.rounded_rectangle((16, 20, w - 16, h - 20), radius=34, fill=rgba((20, 22, 26)))
    body = vertical_gradient((w - 42, h - 50), mix(colour, (255, 255, 255), 0.18), colour)
    mask = rounded_mask(body.size, 26)
    c.paste(body, (21, 25), mask)
    d = ImageDraw.Draw(c)

    # Cabin and glass.
    d.rounded_rectangle((w * 0.28, 36, w * 0.70, h - 36), radius=24,
                        fill=rgba((38, 50, 62)))
    d.rounded_rectangle((w * 0.31, 44, w * 0.67, h - 44), radius=18,
                        fill=rgba((74, 102, 124)))
    d.rounded_rectangle((w * 0.55, 40, w * 0.68, h - 40), radius=12,
                        fill=rgba((96, 128, 148), 160))

    # Bonnet and boot shine.
    d.line((w * 0.08, 52, w * 0.24, 52), fill=rgba((255, 255, 255), 70), width=4)
    d.line((w * 0.08, h - 58, w * 0.24, h - 58), fill=rgba((255, 255, 255), 50), width=3)
    d.rounded_rectangle((w - 30, h * 0.30, w - 18, h * 0.42), radius=5,
                        fill=rgba((255, 240, 180)))
    d.rounded_rectangle((w - 30, h * 0.58, w - 18, h * 0.70), radius=5,
                        fill=rgba((230, 50, 44)))
    d.rounded_rectangle((14, h * 0.32, 24, h * 0.42), radius=4, fill=rgba((220, 40, 36)))
    d.rounded_rectangle((14, h * 0.58, 24, h * 0.68), radius=4, fill=rgba((255, 230, 170)))

    for cx in (w * 0.26, w * 0.78):
        draw_wheel(d, cx, 21, 19)
        draw_wheel(d, cx, h - 21, 19)

    if taxi:
        d.rounded_rectangle((w * 0.40, h * 0.5 - 16, w * 0.60, h * 0.5 + 16),
                            radius=7, fill=rgba((250, 238, 190)))
        draw_text(d, (w * 0.5, h * 0.5), "TAXI",
                  ImageFont.truetype(ARIAL, 17), (40, 36, 28), anchor="mm")
    return c


def van():
    w, h = 448, 196
    c = vehicle_canvas(w, h)
    d = ImageDraw.Draw(c)
    d.rounded_rectangle((14, 18, w - 14, h - 18), radius=30, fill=rgba((18, 20, 24)))
    body = vertical_gradient((w - 38, h - 46), (238, 238, 232), (190, 194, 196))
    mask = rounded_mask(body.size, 22)
    c.paste(body, (19, 23), mask)
    d = ImageDraw.Draw(c)
    d.rounded_rectangle((w * 0.68, 34, w * 0.96, h - 34), radius=16, fill=rgba((38, 50, 62)))
    d.rounded_rectangle((w * 0.70, 40, w * 0.93, h - 40), radius=12, fill=rgba((82, 110, 130)))
    d.rectangle((w * 0.12, 44, w * 0.60, h - 44), fill=rgba((214, 216, 212)))
    for x in range(int(w * 0.16), int(w * 0.58), 22):
        d.line((x, 48, x, h - 48), fill=rgba((186, 188, 186), 80), width=2)
    d.rounded_rectangle((w - 32, h * 0.28, w - 18, h * 0.40), radius=5, fill=rgba((255, 238, 170)))
    for cx in (w * 0.26, w * 0.80):
        draw_wheel(d, cx, 22, 21)
        draw_wheel(d, cx, h - 22, 21)
    return c


def lorry():
    w, h = 640, 250
    c = vehicle_canvas(w, h)
    d = ImageDraw.Draw(c)
    d.rounded_rectangle((14, 22, w - 14, h - 22), radius=28, fill=rgba((18, 20, 24)))
    body = vertical_gradient((w - 38, h - 44), (200, 204, 208), (134, 140, 146))
    mask = rounded_mask(body.size, 22)
    c.paste(body, (19, 25), mask)
    d = ImageDraw.Draw(c)

    # Cab.
    d.rounded_rectangle((w - 210, 32, w - 34, h - 32), radius=20, fill=rgba((58, 76, 92)))
    d.rounded_rectangle((w - 198, 42, w - 42, h - 42), radius=14, fill=rgba((96, 126, 148)))
    d.rounded_rectangle((w - 94, 46, w - 48, h - 46), radius=10, fill=rgba((118, 148, 166), 180))

    # Container/trailer.
    d.rectangle((40, 42, w - 226, h - 42), fill=rgba((196, 198, 198)))
    for x in range(60, w - 230, 26):
        d.line((x, 46, x, h - 46), fill=rgba((160, 162, 164), 90), width=2)
    d.rectangle((40, h - 64, w - 226, h - 42), fill=rgba((148, 150, 152)))
    d.rectangle((40, 42, w - 226, 58), fill=rgba((172, 174, 176)))

    for cx in (110, 232, w - 150, w - 70):
        draw_wheel(d, cx, 25, 24)
        draw_wheel(d, cx, h - 25, 24)
    return c


# --------------------------------------------------------------------- stops
def bus_stop():
    w, h = 300, 250
    c = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(c)

    # Shelter roof.
    d.rounded_rectangle((58, 34, 276, 202), radius=16, fill=rgba((26, 34, 42)))
    d.rounded_rectangle((68, 44, 266, 192), radius=12, fill=rgba((84, 122, 138), 235))
    for x in range(86, 260, 36):
        d.line((x, 48, x, 188), fill=rgba((186, 214, 222), 100), width=3)
    d.rectangle((68, 180, 266, 192), fill=rgba((44, 52, 58)))
    d.rectangle((88, 158, 150, 176), fill=rgba((128, 92, 58)))
    d.rectangle((88, 158, 150, 162), fill=rgba((164, 122, 78)))
    d.rectangle((166, 158, 230, 176), fill=rgba((128, 92, 58)))
    d.rectangle((166, 158, 230, 162), fill=rgba((164, 122, 78)))

    # Bus stop flag.
    d.rectangle((28, 52, 38, 232), fill=rgba((70, 74, 78)))
    d.ellipse((14, 208, 52, 246), fill=rgba((70, 74, 78)))
    d.rounded_rectangle((18, 34, 116, 108), radius=12, fill=rgba((196, 38, 42)))
    d.rounded_rectangle((24, 40, 110, 102), radius=9, fill=rgba((248, 240, 218)))
    d.rounded_rectangle((44, 52, 90, 90), radius=10, fill=rgba((196, 38, 42)))
    d.rectangle((52, 58, 82, 68), fill=rgba((248, 240, 218)))
    d.rectangle((52, 72, 82, 82), fill=rgba((248, 240, 218)))
    draw_wheel(d, 54, 84, 5, (248, 240, 218))
    draw_wheel(d, 80, 84, 5, (248, 240, 218))
    draw_text(d, (67, 96), "BUS", ImageFont.truetype(ARIAL, 11), (196, 38, 42), anchor="mm")
    return c


def terminus():
    c = bus_stop()
    d = ImageDraw.Draw(c)
    # Replace the little flag with a proper terminus board.
    d.rounded_rectangle((10, 18, 150, 124), radius=12, fill=rgba((32, 56, 96)))
    d.rounded_rectangle((17, 25, 143, 117), radius=9, fill=rgba((246, 238, 210)))
    draw_text(d, (80, 42), "TERMINUS", ImageFont.truetype(IMPACT, 26), (32, 56, 96), anchor="mm")
    draw_text(d, (80, 78), "ALL CHANGE", ImageFont.truetype(ARIAL, 15), (32, 56, 96), anchor="mm")
    draw_text(d, (80, 98), "PASSENGERS ALIGHT", ImageFont.truetype(ARIAL, 9), (94, 96, 100), anchor="mm")
    return c


def traffic_light(red):
    """A UK signal head on a pole, seen from above-left like the rest of the town."""
    w, h = 120, 260
    c = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(c)

    # Pole and base.
    d.rectangle((52, 96, 68, 240), fill=rgba((58, 64, 70)))
    d.rectangle((52, 96, 58, 240), fill=rgba((92, 98, 104)))
    d.ellipse((36, 230, 84, 254), fill=rgba((44, 50, 56)))

    # Signal head.
    d.rounded_rectangle((22, 12, 98, 116), radius=16, fill=rgba((32, 38, 44)))
    d.rounded_rectangle((28, 18, 92, 110), radius=12, fill=rgba((20, 24, 28)))

    lamps = ((60, 38), (60, 64), (60, 92))
    colours = ((212, 52, 46), (216, 158, 52), (72, 196, 108))
    lit = 0 if red else 2
    for index, ((x, y), colour) in enumerate(zip(lamps, colours)):
        if index == lit:
            d.ellipse((x - 16, y - 16, x + 16, y + 16), fill=rgba(colour))
            d.ellipse((x - 16, y - 16, x + 16, y + 16), fill=rgba(mix(colour, (255, 255, 255), 0.35)))
            d.ellipse((x - 7, y - 7, x + 7, y + 7), fill=rgba(mix(colour, (255, 255, 255), 0.7)))
        else:
            d.ellipse((x - 16, y - 16, x + 16, y + 16), fill=rgba(mix(colour, (30, 34, 38), 0.68)))

    # Hood over each lamp so the head reads at phone size.
    for x, y in lamps:
        d.rectangle((x - 17, y - 18, x + 17, y - 13), fill=rgba((44, 50, 56)))

    return c


# ----------------------------------------------------------------------- UI
def logo():
    w, h = 1400, 440
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((12, 12, w - 12, h - 12), radius=34, fill=rgba((22, 28, 34, 246)))
    d.rounded_rectangle((26, 26, w - 26, h - 26), radius=26, outline=rgba((238, 198, 70), 170), width=4)

    d.rounded_rectangle((52, 64, 244, 376), radius=20, fill=rgba((196, 38, 42)))
    d.rounded_rectangle((66, 78, 230, 362), radius=14, fill=rgba((238, 232, 210)))
    draw_text(d, (148, 135), "BUS", ImageFont.truetype(IMPACT, 56), (196, 38, 42), anchor="mm")
    draw_text(d, (148, 222), "DRIVER", ImageFont.truetype(IMPACT, 52), (196, 38, 42), anchor="mm")
    d.rectangle((92, 280, 204, 300), fill=rgba((196, 38, 42)))
    draw_text(d, (148, 330), "SCARTHORPE", ImageFont.truetype(ARIAL, 18), (60, 64, 70), anchor="mm")

    draw_text(d, (292, 104), "BUS DRIVER", ImageFont.truetype(IMPACT, 142),
              (246, 238, 210), anchor="la", stroke=5, stroke_fill=(96, 20, 22))
    draw_text(d, (296, 250), "THE SCARTHORPE ROUTES", ImageFont.truetype(ARIAL, 32),
              (238, 198, 70), anchor="la")
    draw_text(d, (296, 304), "passengers • timetables • rain", ImageFont.truetype(ARIAL, 24),
              (166, 178, 186), anchor="la")
    draw_text(d, (296, 356), "A JUGGERNAUT FOR THE BUS AGE", ImageFont.truetype(ARIAL, 18),
              (126, 138, 148), anchor="la")
    return img


def title_bg():
    w, h = 1600, 900
    img = vertical_gradient((w, h), (26, 36, 62), (120, 104, 118))
    d = ImageDraw.Draw(img)

    # Sunset: concentric discs stepping from the sky colour to the sun, which
    # gives a soft glow without relying on alpha blending in ImageDraw.
    for i in range(26):
        t = i / 25
        r = 90 + 250 * t
        colour = mix((255, 210, 132), (86, 76, 96), t ** 0.8)
        d.ellipse((w * 0.67 - r, 230 - r, w * 0.67 + r, 230 + r), fill=rgba(colour))
    d.ellipse((w * 0.67 - 90, 140, w * 0.67 + 90, 320), fill=rgba((246, 196, 112)))

    # Distant hills.
    d.polygon([(0, 540), (260, 400), (560, 540), (820, 420), (1200, 560),
               (1600, 460), (1600, 900), (0, 900)], fill=rgba((52, 58, 70)))
    d.polygon([(0, 610), (420, 500), (800, 620), (1180, 520), (1600, 590),
               (1600, 900), (0, 900)], fill=rgba((36, 42, 50)))

    # Town skyline.
    rng = random.Random(12)
    x = -40
    while x < w + 40:
        bw = rng.randrange(80, 180)
        bh = rng.randrange(140, 380)
        top = 600 - bh
        d.rectangle((x, top, x + bw, 760), fill=rgba((28, 32, 40)))
        for wx in range(x + 14, x + bw - 14, 26):
            for wy in range(top + 18, 730, 34):
                if rng.random() < 0.55:
                    d.rectangle((wx, wy, wx + 10, wy + 14),
                                fill=rgba((248, 202, 116), rng.randrange(110, 220)))
        x += bw + rng.randrange(6, 20)

    # Road sweeping in from the left.
    d.polygon([(0, 900), (0, 700), (900, 700), (1600, 900)], fill=rgba((46, 48, 54)))
    d.line((0, 748, 1600, 860), fill=rgba((214, 214, 206), 150), width=7)
    for i in range(0, w, 90):
        d.rectangle((i, 800 + i * 0.03, i + 42, 808 + i * 0.03),
                    fill=rgba((226, 226, 218), 90))

    # Street lamps.
    for lx in (180, 520, 900, 1280):
        d.rectangle((lx, 590, lx + 8, 760), fill=rgba((22, 24, 28)))
        d.ellipse((lx - 20, 574, lx + 28, 606), fill=rgba((250, 206, 120), 70))

    # A big UK double-decker on the road.
    b = double_decker()
    b = b.resize((790, 262), Image.LANCZOS)
    b = b.rotate(-7, expand=True, resample=Image.BICUBIC)
    img.alpha_composite(b, (720, 566))
    return img


def panel():
    img = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((8, 8, 504, 504), radius=44, fill=rgba((14, 18, 22, 236)),
                        outline=rgba((238, 198, 70), 150), width=4)
    d.rounded_rectangle((22, 22, 490, 490), radius=34, outline=rgba((255, 255, 255), 22), width=2)
    return img


def button(pressed=False):
    img = Image.new("RGBA", (512, 148), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    top = (250, 244, 222) if not pressed else (218, 210, 188)
    bottom = (228, 202, 132) if not pressed else (196, 170, 112)
    body = vertical_gradient((492, 128), top, bottom)
    mask = rounded_mask(body.size, 28)
    img.paste(body, (10, 10), mask)
    d.rounded_rectangle((10, 10, 502, 138), radius=28, fill=None,
                        outline=rgba((92, 70, 38), 130), width=3)
    return img


def weather_icon(kind):
    img = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if kind == "clear":
        for i in range(12):
            a = i * math.tau / 12
            x1 = 64 + math.cos(a) * 34
            y1 = 64 + math.sin(a) * 34
            x2 = 64 + math.cos(a) * 52
            y2 = 64 + math.sin(a) * 52
            d.line((x1, y1, x2, y2), fill=rgba((255, 206, 84)), width=6)
        d.ellipse((28, 28, 100, 100), fill=rgba((255, 214, 92)))
        d.ellipse((40, 36, 84, 80), fill=rgba((255, 236, 158)))
    elif kind == "overcast":
        d.ellipse((22, 40, 90, 96), fill=rgba((178, 186, 194)))
        d.ellipse((48, 24, 112, 88), fill=rgba((210, 216, 220)))
        d.ellipse((34, 56, 84, 100), fill=rgba((154, 162, 172)))
    elif kind == "rain":
        d.ellipse((24, 30, 104, 84), fill=rgba((144, 156, 166)))
        d.ellipse((40, 18, 110, 70), fill=rgba((178, 188, 196)))
        for x in (38, 58, 78, 98):
            d.line((x, 84, x - 10, 112), fill=rgba((102, 174, 220)), width=5)
    elif kind == "fog":
        for y in (36, 56, 76, 96):
            d.rounded_rectangle((10, y, 118, y + 12), radius=6,
                                fill=rgba((196, 202, 208), 140 - y // 4))
    elif kind == "snow":
        d.ellipse((24, 30, 104, 84), fill=rgba((170, 184, 196)))
        for x, y in ((36, 96), (60, 108), (84, 94), (104, 108)):
            d.line((x - 8, y, x + 8, y), fill=rgba((236, 246, 252)), width=3)
            d.line((x, y - 8, x, y + 8), fill=rgba((236, 246, 252)), width=3)
    return img


def generate():
    written = []
    written += [save(grass(i), f"tile_grass_{i}.png") for i in range(2)]
    written += [save(pavement(i), f"tile_pavement_{i}.png") for i in range(2)]
    written.append(save(road(), "tile_road.png"))
    written.append(save(terminus_tile(), "tile_terminus.png"))
    written += [save(building(i), f"tile_building_{i}.png") for i in range(4)]
    written += [save(park(i), f"tile_park_{i}.png") for i in range(2)]
    written += [save(water(i), f"tile_water_{i}.png") for i in range(2)]
    written.append(save(tree(), "tree.png"))

    written.append(save(double_decker(), "bus_double.png"))
    written.append(save(single_decker(), "bus_single.png"))
    written.append(save(minibus(), "bus_minibus.png"))
    written.append(save(car((198, 40, 40)), "traffic_car_red.png"))
    written.append(save(car((32, 104, 176)), "traffic_car_blue.png"))
    written.append(save(car((46, 134, 78)), "traffic_car_green.png"))
    written.append(save(car((226, 226, 220)), "traffic_car_white.png"))
    written.append(save(car((236, 196, 44), taxi=True), "traffic_taxi.png"))
    written.append(save(van(), "traffic_van.png"))
    written.append(save(lorry(), "traffic_lorry.png"))

    written.append(save(bus_stop(), "bus_stop.png"))
    written.append(save(terminus(), "terminus.png"))
    written.append(save(traffic_light(True), "traffic_light_red.png"))
    written.append(save(traffic_light(False), "traffic_light_green.png"))

    written.append(save(logo(), "logo.png"))
    written.append(save(title_bg(), "title_bg.png"))
    written.append(save(panel(), "panel.png"))
    written.append(save(button(False), "button.png"))
    written.append(save(button(True), "button_pressed.png"))
    for kind in ("clear", "overcast", "rain", "fog", "snow"):
        written.append(save(weather_icon(kind), f"weather_{kind}.png"))
    return written
