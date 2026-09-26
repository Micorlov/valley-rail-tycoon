#!/usr/bin/env python3
"""The trailer's voices: one radio "bark" per hero vehicle, Red Alert style, and the closing line. Free neural TTS
(edge-tts, run through uvx) shaped with ffmpeg: barks get a crackly field-radio band, the closing line a deep hall.

    python3 Tools/trailer/barks.py [--out DIR]   -> DIR/audio/<key>.wav + DIR/barks.json (times on the trailer clock)
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

DEFAULT_OUT = Path.home() / "Movies/ValleyRail-Cinematic"

# (key, trailer second it starts, voice, rate, pitch, text)
BARKS = [
    ("bridge", 12.3, "en-US-GuyNeural", "+6%", "-4Hz", "Express reporting."),
    ("fleet", 17.3, "en-GB-RyanNeural", "+8%", "-2Hz", "Full steam ahead!"),
    ("station", 24.7, "en-US-EricNeural", "+4%", "+0Hz", "All aboard!"),
    ("charge", 35.4, "en-US-GuyNeural", "+10%", "-6Hz", "Clear the line!"),
    ("street", 38.8, "en-AU-WilliamNeural", "+6%", "-2Hz", "Rolling out!"),
    ("freight", 44.6, "en-GB-RyanNeural", "+2%", "-6Hz", "Cargo secured."),
    ("express", 50.8, "en-US-EricNeural", "+8%", "+0Hz", "Next stop: Sunvale!"),
    ("fire", 55.5, "en-US-GuyNeural", "+10%", "-4Hz", "Sirens hot!"),
    ("finale", 62.4, "en-US-ChristopherNeural", "-18%", "-14Hz", "The Railway Age... has begun."),
]
# Each chain ends in loudnorm: the radio band and compressor alone take ~17 dB out of a voice.
RADIO = ("highpass=f=330,lowpass=f=3300,acompressor=threshold=-22dB:ratio=6:attack=4:release=60,"
         "aecho=0.8:0.35:35:0.25,loudnorm=I=-13:TP=-1.5:LRA=7")
HALL = ("lowpass=f=9000,acompressor=threshold=-20dB:ratio=3:attack=10:release=120,"
        "aecho=0.8:0.6:140|290|470:0.35|0.22|0.12,loudnorm=I=-14:TP=-1.5:LRA=7")


def speak(voice, rate, pitch, text, mp3):
    subprocess.run(["uvx", "edge-tts", "--voice", voice, f"--rate={rate}", f"--pitch={pitch}", "--text", text,
                    "--write-media", str(mp3)], check=True, capture_output=True)


def shape(mp3, wav, chain):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(mp3), "-af", chain, "-ar", "44100", "-ac", "2",
                    str(wav)], check=True)


def duration(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(path)],
                         check=True, capture_output=True, text=True)
    return float(out.stdout.strip())


def main(out_dir):
    audio = out_dir / "audio"
    audio.mkdir(parents=True, exist_ok=True)
    cues = []
    for key, at, voice, rate, pitch, text in BARKS:
        mp3, wav = audio / f"{key}.mp3", audio / f"{key}.wav"
        try:
            speak(voice, rate, pitch, text, mp3)
        except subprocess.CalledProcessError as error:
            sys.exit(f"edge-tts failed for {key!r}: {error.stderr.decode(errors='replace')[-400:]}")
        shape(mp3, wav, HALL if key == "finale" else RADIO)
        cues.append({"key": key, "at": at, "file": str(wav), "seconds": round(duration(wav), 2), "text": text})
        print(f"{at:6.1f}s  {key:8s} {cues[-1]['seconds']:.2f}s  {text}")
    (out_dir / "barks.json").write_text(json.dumps(cues, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    main(parser.parse_args().out)
