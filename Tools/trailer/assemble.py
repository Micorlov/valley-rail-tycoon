#!/usr/bin/env python3
"""Cuts the cinematic trailer from the rendered frames (Logs/trailer/<shot>/*.jpg, TrailerRenderTests), the march
(march.py), the barks (barks.py) and the title card (cards.py).

Look: warm dusty grade, film grain, vignette, 2.39:1 letterbox in 1920x1080. Cuts land on the march's bars
(cues.json): dissolves through the calm first half, hard cuts once the march is running, a white-flash title slam.

    python3 Tools/trailer/assemble.py [--out DIR] [--frames Logs/trailer] [--audio-only]
      -> DIR/valley-rail-cinematic-72s.mp4 and DIR/valley-rail-cinematic-72s-nobarks.mp4
"""
import argparse
import json
import subprocess
from pathlib import Path

DEFAULT_OUT = Path.home() / "Movies/ValleyRail-Cinematic"
FPS = 24
LENGTH = 72.0
DISSOLVE = 0.5  # seconds, centred on the cut
# shot -> (frames skipped at the head of its render, dissolve into the NEXT shot)
EDIT = {
    "01_city": (0, True), "02_bridge": (0, True), "03_fleet": (0, True), "04_station": (0, True),
    "05_charge": (0, False), "06_street": (8, False), "07_freight": (0, False), "08_express": (0, False),
    "09_fire": (10, False), "10_hero": (16, False),
}
GRADE = ("eq=saturation=0.84:contrast=1.07:brightness=-0.015:gamma=0.97,"
         "colorbalance=rs=0.08:gs=0.03:bs=-0.1:rm=0.07:gm=0.02:bm=-0.09:rh=0.05:gh=0.02:bh=-0.06,"
         "vignette=angle=PI/4.6,noise=alls=5:allf=t+u")
LETTERBOX = "pad=1920:1080:0:138:black,setsar=1,format=yuv420p"
LOUDNESS = -15.0  # LUFS, integrated
PEAK = 0.56  # limiter ceiling, about -4.4 dBFS: the AAC encode overshoots on limited voice peaks
VOICE_GAIN = {"express": 1.9, "finale": 2.0}  # lines that sit on the busiest music


def run(*args):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *map(str, args)], check=True)


def windows(cuts):
    """(shot, start, end) on the trailer clock, each widened by half a dissolve where one joins it."""
    names = list(EDIT) + ["title"]
    spans = []
    for i, name in enumerate(names):
        start, end = cuts[name], cuts[names[i + 1]] if i + 1 < len(names) else LENGTH
        if i > 0 and EDIT[names[i - 1]][1]:
            start -= DISSOLVE / 2
        if name in EDIT:
            end += DISSOLVE / 2 if EDIT[name][1] else 1 / FPS  # a cut is a one-frame xfade, which overlaps a frame
        spans.append((name, start, end))
    return spans


def shot_clip(frames_dir, name, seconds, skip, out):
    count = round(seconds * FPS)
    have = len(list((frames_dir / name).glob("*.jpg")))
    if have < skip + count:
        raise SystemExit(f"{name}: needs frames {skip}..{skip + count - 1} but only {have} were rendered")
    run("-framerate", FPS, "-start_number", skip, "-i", frames_dir / name / "%04d.jpg", "-frames:v", count,
        "-vf", f"{GRADE},{LETTERBOX}", "-r", FPS, "-c:v", "libx264", "-crf", 15, "-preset", "slow", out)


def title_clip(frames_dir, card, seconds, out):
    last = sorted((frames_dir / "10_hero").glob("*.jpg"))[-1]
    graph = (f"[0:v]gblur=sigma=14,eq=brightness=-0.2:saturation=0.7,{GRADE},{LETTERBOX},format=rgba[bg];"
             "[1:v]format=rgba,fade=t=in:st=0:d=0.12:alpha=1[t];"
             "[bg][t]overlay=0:0,fade=t=in:st=0:d=0.45:color=white,"
             f"fade=t=out:st={seconds - 0.9:.2f}:d=0.9,format=yuv420p[v]")
    run("-loop", 1, "-framerate", FPS, "-t", f"{seconds:.3f}", "-i", last, "-loop", 1, "-framerate", FPS,
        "-t", f"{seconds:.3f}", "-i", card, "-filter_complex", graph, "-map", "[v]", "-r", FPS,
        "-c:v", "libx264", "-crf", 15, out)


def picture(frames_dir, work, card, cuts):
    clips, spans = [], windows(cuts)
    for name, start, end in spans:
        clip = work / f"{name}.mp4"
        if name == "title":
            title_clip(frames_dir, card, end - start, clip)
        else:
            shot_clip(frames_dir, name, end - start, EDIT[name][0], clip)
        clips.append(clip)
    # xfade needs every join; a cut is a one-frame fade.
    inputs, graph, last, elapsed = [], [], "0:v", spans[0][2] - spans[0][1]
    for clip in clips:
        inputs += ["-i", clip]
    for i in range(1, len(clips)):
        dissolve = spans[i - 1][0] in EDIT and EDIT[spans[i - 1][0]][1]
        duration = DISSOLVE if dissolve else 1 / FPS
        offset = elapsed - duration
        graph.append(f"[{last}][{i}:v]xfade=transition=fade:duration={duration:.4f}:offset={offset:.4f}[x{i}]")
        last = f"x{i}"
        elapsed = offset + (spans[i][2] - spans[i][1])
    graph.append(f"[{last}]fade=t=in:st=0:d=1.2,trim=0:{LENGTH},setpts=PTS-STARTPTS[v]")
    video = work / "picture.mp4"
    run(*inputs, "-filter_complex", ";".join(graph), "-map", "[v]", "-r", FPS, "-c:v", "libx264", "-crf", 18,
        "-preset", "slow", "-pix_fmt", "yuv420p", video)
    return video


def soundtrack(march, barks, out, with_barks):
    inputs, graph = ["-i", march], [f"[0:a]atrim=0:{LENGTH},asetpts=PTS-STARTPTS[m]"]
    if with_barks and barks:
        labels = []
        for i, bark in enumerate(barks, start=1):
            inputs += ["-i", bark["file"]]
            ms = int(bark["at"] * 1000)
            gain = VOICE_GAIN.get(bark["key"], 1.0)
            graph.append(f"[{i}:a]volume={gain},adelay={ms}|{ms},apad=whole_dur={LENGTH}[b{i}]")
            labels.append(f"[b{i}]")
        graph.append(f"{''.join(labels)}amix=inputs={len(labels)}:normalize=0,volume=2.4,alimiter=limit=0.7:level=false,"
                     "asplit=2[voice][key]")
        graph.append("[m][key]sidechaincompress=threshold=0.025:ratio=8:attack=12:release=400[duck]")
        graph.append("[duck][voice]amix=inputs=2:normalize=0[mix]")
        source = "[mix]"
    else:
        source = "[m]"
    graph.append(f"{source}atrim=0:{LENGTH},aresample=48000[a]")
    raw = out.with_name("raw-" + out.name)
    run(*inputs, "-filter_complex", ";".join(graph), "-map", "[a]", "-c:a", "pcm_f32le", raw)
    # One static gain to the loudness target: single-pass loudnorm rides the level and would undo the ducking.
    gain = LOUDNESS - integrated_loudness(raw)
    run("-i", raw, "-af", f"volume={gain:.2f}dB,alimiter=limit={PEAK}:level=false,afade=t=out:st={LENGTH - 1.6}:d=1.6",
        "-c:a", "pcm_s16le", out)


def integrated_loudness(path):
    out = subprocess.run(["ffmpeg", "-hide_banner", "-i", str(path), "-af", "loudnorm=print_format=json", "-f", "null", "-"],
                         check=True, capture_output=True, text=True).stderr
    return float(json.loads(out[out.rindex("{"):out.rindex("}") + 1])["input_i"])


def main(out_dir, frames_dir, audio_only):
    work = out_dir / "cuts"
    work.mkdir(parents=True, exist_ok=True)
    cuts = json.loads((out_dir / "cues.json").read_text())["cuts"]
    barks = json.loads((out_dir / "barks.json").read_text()) if (out_dir / "barks.json").exists() else []
    video = work / "picture.mp4" if audio_only else picture(frames_dir, work, out_dir / "cards/title.png", cuts)
    for suffix, with_barks in (("", True), ("-nobarks", False)):
        audio = work / f"mix{suffix}.wav"
        soundtrack(out_dir / "march.wav", barks, audio, with_barks)
        final = out_dir / f"valley-rail-cinematic-72s{suffix}.mp4"
        run("-i", video, "-i", audio, "-map", "0:v", "-map", "1:a", "-c:v", "copy", "-c:a", "aac", "-b:a", "256k",
            "-movflags", "+faststart", "-shortest", final)
        print(final)
    small = out_dir / "valley-rail-cinematic-72s-720p.mp4"  # for phones and chat apps
    run("-i", out_dir / "valley-rail-cinematic-72s.mp4", "-vf", "scale=1280:720", "-c:v", "libx264", "-crf", 23,
        "-preset", "slow", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", small)
    print(small)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--frames", type=Path, default=Path(__file__).resolve().parents[2] / "Logs/trailer")
    parser.add_argument("--audio-only", action="store_true", help="re-mix the sound onto the last cut picture")
    args = parser.parse_args()
    main(args.out, args.frames, args.audio_only)
