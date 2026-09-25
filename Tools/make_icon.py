#!/usr/bin/env python3
"""Compose Valley Rail's launcher icon: the red steam engine charging out of Sunvale's skyline at sunset.

The explicit PlayMode test ValleyRail.Tests.AppIconRenderTests opens the player's grown downtown fixture and renders,
from one low camera, the city with its ground and track (Logs/icon-city-scene-*.png) and the engine alone
(Logs/icon-city-engine-*.png), each over pure black and over pure white. Where a pair differs, the background shows
through, so their difference is an exact matte without relying on render-pipeline alpha (the project renders in gamma
space, so the stored values blend linearly). This script sets the city under a drawn sunset with the sun between the
towers, adds steam and a headlamp glow, and writes the files Assets/Game/Scripts/Editor/ProjectSetup.cs (ApplyIcons)
points at:

    Assets/Game/Art/icon-bg.png      432x432 adaptive background layer: sky, sun, city, ground and track
    Assets/Game/Art/icon-fg.png      432x432 adaptive foreground layer: engine, steam and headlamp glow
    Assets/Game/Art/icon-legacy.png  512x512 both layers, cropped to what a square launcher mask shows

Logs/icon-preview.png shows the adaptive icon under circle, squircle and rounded-square masks at launcher sizes.
Positions are render pixels (the renders are 2048 square). Only Pillow is needed and every run is identical.

    python3 Tools/make_icon.py [--renders DIR] [--art DIR] [--preview FILE]
"""
import argparse
import math
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
LAYER = 432  # adaptive layers are 108 dp; 432 px is the xxxhdpi size
LEGACY = 512
VISIBLE = 72 / 108  # share of an adaptive layer that any launcher mask can show
# The square a launcher mask shows, in render pixels: centre and side. The towers rise to the top edge, with sky and
# the sun between them, and the engine spans well over half its width.
VIEW_CENTRE, VIEW_SIDE = (1100, 1185), 1480

SKY = [  # (render y, colour), top to horizon
    (-500, (34, 20, 84)),
    (250, (98, 38, 126)),
    (620, (214, 78, 110)),
    (950, (255, 142, 84)),
    (1250, (255, 204, 132)),
]
SUN, SUN_RADIUS = (1480, 800), 95
SUN_DISC, SUN_GLOW = (255, 246, 216), (255, 186, 118)
GROUND_FILL = (150, 112, 92)  # below the horizon where the render ends, outside what masks show
HORIZON = 1230
CHIMNEY = (907, 1250)  # the engine's chimney mouth
PUFFS = [(0, -34, 40), (46, -100, 54), (122, -156, 68), (216, -192, 80), (322, -206, 88)]  # offset from the chimney, radius
STEAM_LIT, STEAM_SHADE, STEAM_ALPHA = (255, 240, 228), (206, 156, 184), 232
LAMP = (736, 1600)
LAMP_GLOW = (255, 238, 180)
ENGINE_SATURATION, ENGINE_CONTRAST = 1.15, 1.06
VIGNETTE = 90  # darkening at the corners, 0..255
MASKS = ["circle", "squircle", "rounded"]


def lerp(a, b, t):
    t = min(1.0, max(0.0, t))
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def gradient(stops, y):
    if y <= stops[0][0]:
        return stops[0][1]
    for (y0, c0), (y1, c1) in zip(stops, stops[1:]):
        if y <= y1:
            return lerp(c0, c1, (y - y0) / (y1 - y0))
    return stops[-1][1]


def matte(black, white):
    """A render as straight-alpha RGBA: over black a pixel is colour x coverage; white minus black is what shows through."""
    through = ImageChops.subtract(white, black).split()
    seen = ImageChops.lighter(ImageChops.lighter(through[0], through[1]), through[2])
    return Image.merge("RGBa", (*black.split(), ImageChops.invert(seen))).convert("RGBA")


def load(renders, name):
    black, white = (Image.open(renders / f"icon-city-{name}-{tone}.png").convert("RGB") for tone in ("black", "white"))
    if black.size != white.size:
        sys.exit(f"The {name} renders differ in size: {black.size} vs {white.size}.")
    return matte(black, white)


class Frame:
    """The full adaptive layer in render pixels: VIEW is its middle two thirds."""

    side = round(VIEW_SIDE / VISIBLE)
    left = round(VIEW_CENTRE[0] - side / 2)
    top = round(VIEW_CENTRE[1] - side / 2)

    @classmethod
    def at(cls, point):
        return (point[0] - cls.left, point[1] - cls.top)

    @classmethod
    def place(cls, render):
        """A render (render-pixel sized) positioned on a transparent layer-sized canvas."""
        canvas = Image.new("RGBA", (cls.side, cls.side), (0, 0, 0, 0))
        canvas.alpha_composite(render, dest=(max(0, -cls.left), max(0, -cls.top)), source=(max(0, cls.left), max(0, cls.top)))
        return canvas


def glow(size, centre, radius, colour, strength, squash=1.0):
    """A soft RGBA glow: colour with a blurred elliptical alpha."""
    alpha = Image.new("L", (size, size))
    cx, cy = centre
    ImageDraw.Draw(alpha).ellipse((cx - radius, cy - radius * squash, cx + radius, cy + radius * squash), fill=strength)
    layer = Image.new("RGBA", (size, size), colour + (0,))
    layer.putalpha(alpha.filter(ImageFilter.GaussianBlur(radius * 0.5)))
    return layer


def sky(size):
    image = Image.new("RGBA", (size, size))
    draw = ImageDraw.Draw(image)
    for row in range(size):
        y = row + Frame.top
        draw.line([(0, row), (size, row)], fill=(GROUND_FILL if y > HORIZON + 40 else gradient(SKY, y)) + (255,))
    sun = Frame.at(SUN)
    image.alpha_composite(glow(size, sun, SUN_RADIUS * 6, SUN_GLOW, 190))
    image.alpha_composite(glow(size, sun, SUN_RADIUS * 2.2, SUN_DISC, 170))
    ImageDraw.Draw(image).ellipse((sun[0] - SUN_RADIUS, sun[1] - SUN_RADIUS, sun[0] + SUN_RADIUS, sun[1] + SUN_RADIUS), fill=SUN_DISC + (255,))
    return image


def vignette(image):
    size = image.width
    shade = Image.new("L", (size, size), 0)
    ImageDraw.Draw(shade).ellipse((-size * 0.05, -size * 0.05, size * 1.05, size * 1.05), fill=255)
    shade = ImageChops.invert(shade.filter(ImageFilter.GaussianBlur(size * 0.1))).point(lambda p: p * VIGNETTE // 255)
    image.paste(Image.new("RGBA", image.size, (24, 10, 34, 255)), mask=shade)
    return image


def steam(size):
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    cx, cy = Frame.at(CHIMNEY)
    for i, (dx, dy, r) in enumerate(PUFFS):
        x, y = cx + dx, cy + dy
        alpha = round(STEAM_ALPHA * (1 - i * 0.07))
        for ox, oy, share in [(0, 0, 1), (-0.6, 0.3, 0.55), (0.55, 0.35, 0.5)]:
            px, py, pr = x + ox * r, y + oy * r, r * share
            draw.ellipse((px - pr, py - pr, px + pr, py + pr), fill=STEAM_SHADE + (alpha,))
            draw.ellipse((px - pr * 0.9, py - pr, px + pr * 0.75, py + pr * 0.7), fill=STEAM_LIT + (alpha,))
    return layer.filter(ImageFilter.GaussianBlur(2))


def graded(engine):
    alpha = engine.getchannel("A")
    rgb = ImageEnhance.Contrast(ImageEnhance.Color(engine.convert("RGB")).enhance(ENGINE_SATURATION)).enhance(ENGINE_CONTRAST)
    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    return out


def layers(scene, engine):
    """Background and foreground at render resolution (Frame.side square)."""
    back = sky(Frame.side)
    back.alpha_composite(Frame.place(scene))
    back = vignette(back)
    front = steam(Frame.side)
    front.alpha_composite(Frame.place(graded(engine)))
    lamp = Frame.at(LAMP)
    front.alpha_composite(glow(Frame.side, lamp, 80, LAMP_GLOW, 255))
    front.alpha_composite(glow(Frame.side, lamp, 230, LAMP_GLOW, 150, squash=0.13))
    return back, front


def reduced(image, size):
    return image.convert("RGBa").resize((size, size), Image.LANCZOS).convert("RGBA")


def visible(icon):
    """What a launcher mask can show: the middle 72 dp of the 108 dp layer."""
    inset = round(icon.width * (1 - VISIBLE) / 2)
    return icon.crop((inset, inset, icon.width - inset, icon.height - inset))


def mask(shape, size):
    big = size * 4
    image = Image.new("L", (big, big))
    draw = ImageDraw.Draw(image)
    if shape == "circle":
        draw.ellipse((0, 0, big - 1, big - 1), fill=255)
    elif shape == "rounded":
        draw.rounded_rectangle((0, 0, big - 1, big - 1), radius=big * 0.18, fill=255)
    else:  # squircle |x|^4 + |y|^4 = 1, close to the One UI and Pixel shapes
        points = []
        for i in range(720):
            t = 2 * math.pi * i / 720
            x = math.copysign(abs(math.cos(t)) ** 0.5, math.cos(t))
            y = math.copysign(abs(math.sin(t)) ** 0.5, math.sin(t))
            points.append(((x + 1) / 2 * (big - 1), (y + 1) / 2 * (big - 1)))
        draw.polygon(points, fill=255)
    return image.resize((size, size), Image.LANCZOS)


def preview(adaptive, legacy):
    """Top, on light wallpaper: each mask at 192 px and the legacy icon. Bottom, on dark: each mask at 96 and 48 px."""
    pad, large = 24, 192
    face = visible(adaptive)
    width = pad + 4 * (large + pad)
    sheet = Image.new("RGB", (width, 2 * (large + pad) + pad), (242, 242, 242))
    ImageDraw.Draw(sheet).rectangle((0, large + 2 * pad - pad // 2, width, sheet.height), fill=(32, 33, 36))
    for i, shape in enumerate(MASKS):
        sheet.paste(face.resize((large, large), Image.LANCZOS), (pad + i * (large + pad), pad), mask(shape, large))
    sheet.paste(legacy.resize((large, large), Image.LANCZOS), (pad + 3 * (large + pad), pad))
    x, y = pad, large + 2 * pad + pad // 2
    for size in (96, 48):
        for shape in MASKS:
            sheet.paste(face.resize((size, size), Image.LANCZOS), (x, y), mask(shape, size))
            x += size + pad
    return sheet


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--renders", type=Path, default=ROOT / "Logs", help="folder with the icon-city-*-black/white.png renders")
    parser.add_argument("--art", type=Path, default=ROOT / "Assets/Game/Art", help="where the icon layers are written")
    parser.add_argument("--preview", type=Path, default=ROOT / "Logs/icon-preview.png")
    args = parser.parse_args(argv)

    missing = [f"icon-city-{n}-{t}.png" for n in ("scene", "engine") for t in ("black", "white") if not (args.renders / f"icon-city-{n}-{t}.png").exists()]
    if missing:
        sys.exit(f"Missing {', '.join(missing)} in {args.renders}. Render them first with the explicit PlayMode test "
                 "ValleyRail.Tests.AppIconRenderTests (see README).")
    back, front = layers(load(args.renders, "scene"), load(args.renders, "engine"))
    whole = Image.alpha_composite(back, front)
    legacy = visible(whole).convert("RGBa").resize((LEGACY, LEGACY), Image.LANCZOS).convert("RGB")

    args.art.mkdir(parents=True, exist_ok=True)
    reduced(back, LAYER).convert("RGB").save(args.art / "icon-bg.png", optimize=True)
    reduced(front, LAYER).save(args.art / "icon-fg.png", optimize=True)
    legacy.save(args.art / "icon-legacy.png", optimize=True)
    args.preview.parent.mkdir(parents=True, exist_ok=True)
    preview(reduced(whole, LAYER).convert("RGB"), legacy).save(args.preview)
    print(f"Wrote icon-bg.png, icon-fg.png and icon-legacy.png to {args.art}, preview {args.preview}")


if __name__ == "__main__":
    main(sys.argv[1:])
