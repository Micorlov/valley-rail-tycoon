#!/usr/bin/env python3
"""Render Valley Rail's music: synthesized arrangements of public-domain classical favourites.

Each piece in Tools/music/pieces/ is written out note by note from the public-domain score and played by the
synthesizer in Tools/music/synth.py, so no recording or sample is involved. The output is one 44.1 kHz 16-bit
stereo WAV per piece plus playlist.json (file, title, composer, year) in Assets/Game/Resources/Audio/Music/;
Unity streams the WAVs as Vorbis (Assets/Game/Scripts/Editor/ThemeMusicImport.cs) and Presentation/ThemeMusic.cs
plays them, the first piece on the title screen and then a shuffle. Standard library only; renders are identical.

    python3 Tools/compose_music.py [--only KEY[,KEY]] [--out-dir DIR] [--jobs N] [--preview FILE]
"""
import argparse
import array
import json
import math
import multiprocessing
import sys
import time
import wave
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from music import arrange, synth  # noqa: E402
from music.pieces import PLAYLIST  # noqa: E402

DEFAULT_OUT = Path(__file__).resolve().parent.parent / "Assets/Game/Resources/Audio/Music"
PREVIEW_SECONDS = 12
PREVIEW_GAP = 0.6


def render_one(job):
    key, out_dir = job
    started = time.monotonic()
    piece = arrange.load(key)
    left, right = arrange.render(piece)
    path = out_dir / f"{piece.file}.wav"
    synth.write_wav(path, left, right)
    peak = max(max(map(abs, left)), max(map(abs, right)))
    return {"file": path.name, "seconds": len(left) / synth.SAMPLE_RATE, "peak": 20 * math.log10(peak),
            "rms": 20 * math.log10(synth.rms_of(left, right)), "render": time.monotonic() - started}


def write_playlist(out_dir, pieces):
    tracks = [{"file": p.file, "title": p.title, "composer": p.composer, "year": p.year} for p in pieces]
    path = out_dir / "playlist.json"
    path.write_text(json.dumps({"tracks": tracks}, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return path


def write_preview(out_dir, pieces, path):
    """The first seconds of every piece back to back, faded in and out, for a quick listen."""
    frames, fade = array.array("h"), synth.seconds(0.8)
    for piece in pieces:
        with wave.open(str(out_dir / f"{piece.file}.wav"), "rb") as source:
            clip = array.array("h", source.readframes(synth.seconds(PREVIEW_SECONDS)))
        count = len(clip) // 2
        for i in range(min(fade, count)):
            level = i / fade
            for channel in (0, 1):
                clip[2 * (count - 1 - i) + channel] = int(clip[2 * (count - 1 - i) + channel] * level)
        frames.extend(clip)
        frames.extend(array.array("h", bytes(4 * synth.seconds(PREVIEW_GAP))))
    with wave.open(str(path), "wb") as out:
        out.setnchannels(2)
        out.setsampwidth(2)
        out.setframerate(synth.SAMPLE_RATE)
        out.writeframes(frames.tobytes())
    print(f"wrote preview {path} ({len(frames) // 2 / synth.SAMPLE_RATE:.0f} s)")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--only", help="comma-separated piece keys to render (playlist.json always lists all)")
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUT, help="folder for the WAVs and playlist.json")
    parser.add_argument("--jobs", type=int, default=min(6, multiprocessing.cpu_count()), help="pieces rendered at once")
    parser.add_argument("--preview", type=Path, help="also write a medley of every piece's opening to this WAV")
    args = parser.parse_args()
    pieces = [arrange.load(key) for key in PLAYLIST]  # checks every piece before any rendering starts
    keys = args.only.split(",") if args.only else list(PLAYLIST)
    unknown = sorted(set(keys) - set(PLAYLIST))
    if unknown:
        parser.error(f"unknown pieces {unknown}; choose from {', '.join(PLAYLIST)}")
    args.out_dir.mkdir(parents=True, exist_ok=True)
    with multiprocessing.Pool(max(1, args.jobs)) as pool:
        for stats in pool.imap(render_one, [(key, args.out_dir) for key in keys]):
            print(f"{stats['file']:<28} {stats['seconds']:6.1f} s  peak {stats['peak']:6.2f} dBFS  "
                  f"rms {stats['rms']:6.2f} dBFS  ({stats['render']:.0f} s to render)", flush=True)
    print(f"wrote {write_playlist(args.out_dir, pieces)}")
    if args.preview:
        write_preview(args.out_dir, [p for p in pieces if p.key in keys], args.preview)


if __name__ == "__main__":
    main()
