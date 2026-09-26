#!/usr/bin/env python3
"""Compose Valley Rail's city launcher icon: Oakridge's stadium beside its skyscrapers, lettered CITY and VALLEY RAIL.

The explicit PlayMode test ValleyRail.Tests.AppIconRenderTests.RendersTheStadiumSkylineForTheCityIcon renders the grown
world the commercial was filmed in (Tests/Fixtures/phone-stadium.json) from the game's own isometric camera, with the
floating labels hidden, to Logs/icon-stadium.png (3072 square). This script crops the icon square from it, the stadium
in the top-left and the towers from the middle to the right edge, letters it, and writes the files
Assets/Game/Scripts/Editor/ProjectSetup.cs (ApplyIcons) and the Mac build point at:

    Assets/Game/Art/icon-bg.png      432x432 adaptive background layer: the city
    Assets/Game/Art/icon-fg.png      432x432 adaptive foreground layer: the lettering, kept inside the circle mask
    Assets/Game/Art/icon-legacy.png  512x512 city and lettering full width, as the Play Store and square masks show it
    Assets/Game/Art/icon-mac.png     1024x1024 the same face on Apple's rounded-square grid

Logs/icon-preview.png shows the adaptive icon under circle, squircle and rounded-square masks at launcher sizes.
Tools/make_icon.py composes the earlier engine-at-sunset icon from its own renders; whichever runs last wins.

    python3 Tools/make_city_icon.py [--render FILE] [--art DIR] [--preview FILE]
"""
import argparse
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
from make_icon import LAYER, LEGACY, VISIBLE, mac_icon, preview, reduced, visible  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
# The square a launcher mask shows, in render pixels: the bowl's stands fill the top-left corner and the tan and blue
# towers run from the middle to the right edge, as in the commercial's thumbnail.
FACE_CENTRE, FACE_SIDE = (1378, 1458), 1413
SATURATION, CONTRAST = 1.12, 1.05

FONT = "/System/Library/Fonts/Avenir Next.ttc"  # macOS system font; face 8 is Heavy
FONT_FACE = 8
WHITE, MINT, INK = (255, 255, 255), (132, 216, 146), (14, 24, 30)
# Lettering as shares of the face: (text, colour, centre x, centre y, width). Each word is sized to its width, outline
# included. Square masks show the corners, so the name runs nearly edge to edge; a circle cuts them, so the adaptive
# layer sets both words narrower and nearer the middle, inside the 66 dp safe circle.
SQUARE_LETTERING = [("CITY", MINT, 0.735, 0.13, 0.44), ("VALLEY RAIL", WHITE, 0.5, 0.875, 0.93)]
ROUND_LETTERING = [("CITY", MINT, 0.655, 0.225, 0.33), ("VALLEY RAIL", WHITE, 0.5, 0.765, 0.72)]
OUTLINE, SHADOW_BLUR, SHADOW_ALPHA = 0.06, 0.05, 200  # as shares of the point size


def city(render):
    """The adaptive background layer at render resolution: the face plus the layer's margin on every side."""
    side = round(FACE_SIDE / VISIBLE)
    left, top = FACE_CENTRE[0] - side // 2, FACE_CENTRE[1] - side // 2
    if left < 0 or top < 0 or left + side > render.width or top + side > render.height:
        sys.exit(f"The icon layer ({left},{top}) +{side} runs off the {render.width}px render; move FACE_CENTRE or "
                 "raise StadiumZoom in AppIconRenderTests.")
    layer = render.crop((left, top, left + side, top + side))
    return ImageEnhance.Contrast(ImageEnhance.Color(layer).enhance(SATURATION)).enhance(CONTRAST).convert("RGBA")


def lettering(size, words, face_share):
    """Transparent layer of the given size with the words set on the face, which is face_share of the layer, centred."""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    face = size * face_share
    inset = (size - face) / 2
    for text, colour, cx, cy, width in words:
        probe = ImageFont.truetype(FONT, 100, index=FONT_FACE)
        box = ImageDraw.Draw(layer).textbbox((0, 0), text, font=probe, stroke_width=round(100 * OUTLINE))
        points = round(100 * width * face / (box[2] - box[0]))
        font = ImageFont.truetype(FONT, points, index=FONT_FACE)
        x, y = inset + cx * face, inset + cy * face
        stroke = max(1, round(points * OUTLINE))
        shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        ImageDraw.Draw(shadow).text((x, y + stroke), text, font=font, anchor="mm", fill=INK + (SHADOW_ALPHA,),
                                    stroke_width=stroke * 2, stroke_fill=INK + (SHADOW_ALPHA,))
        layer.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(points * SHADOW_BLUR)))
        ImageDraw.Draw(layer).text((x, y), text, font=font, anchor="mm", fill=colour + (255,),
                                   stroke_width=stroke, stroke_fill=INK + (255,))
    return layer


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--render", type=Path, default=ROOT / "Logs/icon-stadium.png")
    parser.add_argument("--art", type=Path, default=ROOT / "Assets/Game/Art", help="where the icon files are written")
    parser.add_argument("--preview", type=Path, default=ROOT / "Logs/icon-preview.png")
    args = parser.parse_args(argv)
    if not args.render.exists():
        sys.exit(f"Missing {args.render}. Render it first with the explicit PlayMode test "
                 "ValleyRail.Tests.AppIconRenderTests.RendersTheStadiumSkylineForTheCityIcon (see README).")
    if not Path(FONT).exists():
        sys.exit(f"Missing the font {FONT}; this tool runs on macOS.")

    back = city(Image.open(args.render).convert("RGB"))
    face = visible(back)
    square = face.copy()
    square.alpha_composite(lettering(square.width, SQUARE_LETTERING, 1.0))
    front = lettering(back.width, ROUND_LETTERING, VISIBLE)

    args.art.mkdir(parents=True, exist_ok=True)
    reduced(back, LAYER).convert("RGB").save(args.art / "icon-bg.png", optimize=True)
    reduced(front, LAYER).save(args.art / "icon-fg.png", optimize=True)
    legacy = reduced(square, LEGACY).convert("RGB")
    legacy.save(args.art / "icon-legacy.png", optimize=True)
    mac_icon(square).save(args.art / "icon-mac.png", optimize=True)
    args.preview.parent.mkdir(parents=True, exist_ok=True)
    preview(reduced(Image.alpha_composite(back, front), LAYER).convert("RGB"), legacy).save(args.preview)
    print(f"Wrote icon-bg.png, icon-fg.png, icon-legacy.png and icon-mac.png to {args.art}, preview {args.preview}")


if __name__ == "__main__":
    main(sys.argv[1:])
