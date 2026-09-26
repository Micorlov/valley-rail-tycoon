#!/usr/bin/env python3
"""The trailer's title slam: VALLEY RAIL in steel letters with a red bevel, and the coming-soon line, drawn with Pillow
on a transparent 1920x1080 card (ffmpeg's drawtext is missing from Homebrew builds).

    python3 Tools/trailer/cards.py [--out DIR]   -> DIR/cards/title.png
"""
import argparse
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

DEFAULT_OUT = Path.home() / "Movies/ValleyRail-Cinematic"
W, H = 1920, 1080
TITLE_FONT = "/System/Library/Fonts/Supplemental/Impact.ttf"
SUB_FONT = "/System/Library/Fonts/Supplemental/DIN Condensed Bold.ttf"
RED = (196, 30, 24)
CREAM = (238, 226, 200)


def text_mask(text, font, tracking):
    """White-on-black mask of `text` with extra letter spacing, cropped to the ink."""
    widths = [font.getbbox(ch)[2] - font.getbbox(ch)[0] if ch != " " else font.size // 3 for ch in text]
    ascent, descent = font.getmetrics()
    mask = Image.new("L", (sum(widths) + tracking * (len(text) - 1) + 40, ascent + descent + 40), 0)
    draw, x = ImageDraw.Draw(mask), 20
    for ch, w in zip(text, widths):
        if ch != " ":
            draw.text((x - font.getbbox(ch)[0], 20), ch, font=font, fill=255)
        x += w + tracking
    return mask.crop(mask.getbbox())


def steel(size):
    """A vertical steel gradient: a bright sky reflection above a dark horizon line, warm below."""
    w, h = size
    column = Image.new("RGB", (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        if t < 0.48:
            c = (int(250 - 90 * t), int(248 - 95 * t), int(240 - 100 * t))
        elif t < 0.52:
            c = (70, 66, 62)
        else:
            u = (t - 0.52) / 0.48
            c = (int(150 + 60 * u), int(128 + 40 * u), int(104 + 20 * u))
        column.putpixel((0, y), c)
    return column.resize((w, h))


def layer(mask, at, blur=0, grow=0):
    """`mask` placed at `at` on a full-card alpha, optionally grown and blurred."""
    out = Image.new("L", (W, H), 0)
    out.paste(mask, at)
    if grow:
        out = out.filter(ImageFilter.MaxFilter(grow))
    return out.filter(ImageFilter.GaussianBlur(blur)) if blur else out


def title_card():
    card = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    mask = text_mask("VALLEY RAIL", ImageFont.truetype(TITLE_FONT, 250), 18)
    x, y = (W - mask.width) // 2, H // 2 - mask.height // 2 - 60
    card.paste((0, 0, 0, 200), (0, 0), layer(mask, (x + 14, y + 18), blur=18))
    card.paste(RED + (255,), (0, 0), layer(mask, (x + 7, y + 9)))
    card.paste((24, 20, 18, 255), (0, 0), layer(mask, (x, y), grow=9))
    face = Image.new("RGBA", mask.size)
    face.paste(steel(mask.size), (0, 0), mask)
    edge = ImageChops.subtract(mask, ImageChops.offset(mask, 0, 4)).filter(ImageFilter.GaussianBlur(1))
    face.paste((255, 255, 255, 255), (0, 0), edge.point(lambda v: int(v * 0.8)))  # a highlight along each top edge
    card.alpha_composite(face, (x, y))

    rule_y = y + mask.height + 42
    ImageDraw.Draw(card).rectangle((W // 2 - 360, rule_y, W // 2 + 360, rule_y + 7), fill=RED + (255,))
    sub = text_mask("COMING SOON TO GOOGLE PLAY", ImageFont.truetype(SUB_FONT, 64), 14)
    sx, sy = (W - sub.width) // 2, rule_y + 36
    card.paste((0, 0, 0, 170), (0, 0), layer(sub, (sx, sy + 3), blur=6))
    card.paste(CREAM + (255,), (sx, sy), sub)
    return card


def main(out_dir):
    cards = out_dir / "cards"
    cards.mkdir(parents=True, exist_ok=True)
    title_card().save(cards / "title.png")
    print(cards / "title.png")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    main(parser.parse_args().out)
