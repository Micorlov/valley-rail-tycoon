#!/usr/bin/env python3
"""The cinematic trailer's score: an original industrial march in D minor, 100 BPM, 30 bars (72 s), written for this
trailer and played by the game's own synthesizer (Tools/music/synth.py). Sections land on the shot cuts in cues.json:

    bars  0-3   drone and timpani under the city crane-up
    bars  4-6   steam rhythm and rising horns under the bridge
    bars  7-22  the march: kick and snare, anvil on 2 and 4, driving strings, the horn theme
    bars 23-24  siren break under the fire engine
    bars 25-27  the final hit and a slow low theme under the hero engine
    bars 28-29  the title slam, ringing out

    python3 Tools/trailer/march.py [--out DIR]     -> DIR/march.wav + DIR/cues.json (default ~/Movies/ValleyRail-Cinematic)
Standard library only; the render is identical every time.
"""
import argparse
import json
import math
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from music import synth  # noqa: E402

BPM = 100
BEAT = 60 / BPM
BAR = 4 * BEAT
BARS = 30
TAIL = 2.5  # seconds of ring after the last bar, faded
DEFAULT_OUT = Path.home() / "Movies/ValleyRail-Cinematic"

# Shot boundaries in bars: the edit (assemble.py) cuts on these.
CUTS = {"01_city": 0, "02_bridge": 4, "03_fleet": 7, "04_station": 10, "05_charge": 13, "06_street": 16,
        "07_freight": 18, "08_express": 21, "09_fire": 23, "10_hero": 25, "title": 28, "end": 30}

KICK, SNARE, ANVIL, HAT, CHUFF, BASS, HORN, HORN_LOW, OSTINATO, DRONE, TIMPANI, WHISTLE, CRASH = range(13)
LAYERS = [(0.5, 0.05), (0.46, 0.18), (0.62, 0.22), (0.58, 0.05), (0.42, 0.08), (0.5, 0.0), (0.4, 0.3), (0.56, 0.3),
          (0.6, 0.22), (0.5, 0.35), (0.5, 0.3), (0.72, 0.5), (0.5, 0.3)]

NOTE = {f"{name}{octave}": 12 * (octave + 1) + step for octave in range(0, 7)
        for name, step in (("C", 0), ("C#", 1), ("D", 2), ("E", 4), ("F", 5), ("G", 7), ("A", 9), ("Bb", 10))}


def n(name):
    return NOTE[name]


def timpani(note, length, velocity, rng):
    """A tuned drum: damped partials over a soft mallet thump."""
    total = synth.seconds(1.6)
    out = [0.0] * total
    freq = synth.midi_hz(note)
    synth.resonate(out, freq, 0.9, 0.55)
    synth.resonate(out, freq * 1.5, 0.25, 0.25)
    synth.resonate(out, freq * 2.0, 0.12, 0.12)
    for i in range(synth.seconds(0.012)):
        out[i] += 0.35 * rng.uniform(-1, 1) * (1 - i / synth.seconds(0.012))
    return synth._finish(synth.shape(out, synth.seconds(0.002), total - synth.seconds(0.2), synth.seconds(0.2)), velocity, 0.9)


def crash(note, length, velocity, rng):
    """A long splash of brightened noise."""
    total = synth.seconds(2.4)
    noise = synth._highpass(synth._highpass([rng.uniform(-1, 1) for _ in range(total)]))
    out = [noise[i] * (0.8 * math.exp(-i / synth.seconds(0.9)) + 0.2 * math.exp(-i / synth.seconds(0.08))) for i in range(total)]
    return synth._finish(synth.shape(out, synth.seconds(0.001), total - synth.seconds(0.4), synth.seconds(0.4)), velocity, 0.7)


class Score:
    def __init__(self, seed=1929):
        self.rng = random.Random(seed)
        self.mix = synth.Mix(synth.seconds(BARS * BAR + TAIL), LAYERS)

    def play(self, layer, voice, note, bar, beat, beats, velocity):
        if velocity <= 0:
            return
        jitter = 0.0 if layer in (KICK, BASS) else self.rng.uniform(-0.004, 0.004)
        start = bar * BAR + beat * BEAT + jitter
        self.mix.add(layer, synth.seconds(max(0.0, start)), voice(note, beats * BEAT, velocity, self.rng))

    def chord(self, layer, voice, names, bar, beat, beats, velocity):
        for name in names:
            self.play(layer, voice, n(name), bar, beat, beats, velocity / math.sqrt(len(names)))

    def line(self, layer, voice, bar, notes, velocity, octave=0):
        """notes: [(name or None, beats)] from beat 0 of `bar`, spilling over into the next bars."""
        beat = 0.0
        for name, beats in notes:
            if name:
                self.play(layer, voice, n(name) + 12 * octave, bar, beat, beats, velocity)
            beat += beats


# Chords per bar of the march, and the eighth-note string figure over each.
FIGURE = {"Dm": ("D5", "A4", "F4", "A4"), "Bb": ("D5", "Bb4", "F4", "Bb4"), "A": ("C#5", "A4", "E4", "A4"),
          "Gm": ("D5", "Bb4", "G4", "Bb4")}
ROOT = {"Dm": "D2", "Bb": "Bb1", "A": "A1", "Gm": "G1"}
THEME_A = [
    [("D4", 1), ("D4", .5), ("F4", .5), ("A4", 1), ("G4", 1)],
    [("F4", 1.5), ("E4", .5), ("D4", 2)],
    [("D4", 1), ("F4", .5), ("Bb4", .5), ("A4", 1), ("G4", 1)],
    [("A4", 1.5), ("G4", .5), ("A4", 2)],
]
THEME_B = [
    [("Bb4", 1), ("A4", .5), ("G4", .5), ("D5", 2)],
    [("A4", 1), ("G4", .5), ("F4", .5), ("D4", 2)],
    [("F4", 1), ("G4", .5), ("A4", .5), ("Bb4", 1), ("C5", 1)],
    [("A4", 4)],
]
CHORDS_A, CHORDS_B = ["Dm", "Dm", "Bb", "A"], ["Gm", "Dm", "Bb", "A"]


def intro(s):
    for bar in range(4):
        loud = 0.35 + 0.12 * bar
        s.chord(DRONE, synth.strings, ["D2", "A2", "D3"], bar, 0, 4, loud)
        for beat in (0, 2):
            s.play(TIMPANI, timpani, n("D2"), bar, beat, 1, loud + 0.1)
    for i in range(8):  # a roll into the bridge
        s.play(TIMPANI, timpani, n("A1") if i % 2 else n("D2"), 3, 2 + i * 0.25, .25, 0.35 + 0.06 * i)
    s.chord(WHISTLE, synth.flute, ["D5", "F5"], 1, 2, 3, 0.28)  # a distant steam whistle
    s.chord(WHISTLE, synth.flute, ["D5", "F5"], 2, 3, 1.5, 0.2)


def build(s):
    for bar in range(4, 7):
        for eighth in range(8):
            s.play(CHUFF, synth.shaker, 0, bar, eighth / 2, .5, 0.55 if eighth % 2 == 0 else 0.3)
        for beat in range(4):
            s.play(BASS, synth.bass808, n("D2"), bar, beat, .9, 0.55)
        s.chord(DRONE, synth.strings, ["D3", "A3", "F4"], bar, 0, 4, 0.45)
        s.play(TIMPANI, timpani, n("D2"), bar, 0, 1, 0.7)
    rise = [("D4", 2), ("F4", 2), ("G4", 2), ("A4", 2), ("Bb4", 2), ("A4", 2)]
    s.line(HORN, synth.horn, 4, rise, 0.7)
    s.line(HORN_LOW, synth.horn, 4, rise, 0.5, octave=-1)
    for i in range(8):
        s.play(SNARE, synth.snare808, 0, 6, 2 + i * 0.25, .25, 0.25 + 0.08 * i)


def march_bar(s, bar, chord, loud=1.0):
    for beat in (0, 2):
        s.play(KICK, synth.kick808, n("D2") - 12, bar, beat, 1, 0.95 * loud)
    for beat in (1, 3):
        s.play(SNARE, synth.snare808, 0, bar, beat, 1, 0.8 * loud)
        s.play(ANVIL, synth.bell, n("E6"), bar, beat, .5, 0.22 * loud)
    for eighth in range(8):
        s.play(HAT, synth.hat808, 0, bar, eighth / 2, .5, 0.35 if eighth % 2 else 0.5)
        s.play(CHUFF, synth.shaker, 0, bar, eighth / 2, .5, 0.4 if eighth % 2 == 0 else 0.22)
        s.play(BASS, synth.bass808, n(ROOT[chord]) + 12, bar, eighth / 2, .45, 0.6 * loud)
        s.play(OSTINATO, synth.strings, n(FIGURE[chord][eighth % 4]), bar, eighth / 2, .45, 0.42 * loud)


def march(s):
    first = 7
    phrases = [(THEME_A, CHORDS_A, False), (THEME_A, CHORDS_A, True), (THEME_B, CHORDS_B, True), (THEME_A, CHORDS_A, True)]
    for phrase, (theme, chords, horns_low) in enumerate(phrases):
        for i in range(4):
            bar = first + 4 * phrase + i
            if bar > 22:
                break
            march_bar(s, bar, chords[i], 0.9 + 0.04 * phrase)
            s.line(HORN, synth.horn, bar, theme[i], 0.85)
            if horns_low:
                s.line(HORN_LOW, synth.horn, bar, theme[i], 0.6, octave=-1)
            if phrase == 3:
                s.line(OSTINATO, synth.strings, bar, theme[i], 0.4, octave=1)
    for bar, loud in ((first, 0.9), (first + 8, 0.7), (first + 12, 0.8)):
        s.play(CRASH, crash, 0, bar, 0, 4, loud)


def siren(s):
    for bar in (23, 24):
        for beat in range(4):
            s.play(KICK, synth.kick808, n("D2") - 12, bar, beat, 1, 0.9)
            s.play(HORN, synth.horn, n("A4") if beat % 2 == 0 else n("Bb4"), bar, beat, .95, 0.8)
            s.play(HORN_LOW, synth.horn, n("A3") if beat % 2 == 0 else n("Bb3"), bar, beat, .95, 0.55)
        for eighth in range(8):
            s.play(HAT, synth.hat808, 0, bar, eighth / 2, .5, 0.5)
        s.play(BASS, synth.bass808, n("D3"), bar, 0, 4, 0.6)
    for i in range(16):
        s.play(SNARE, synth.snare808, 0, 24, i * 0.25, .25, 0.3 + 0.04 * i)


def finale(s):
    def hit(bar, beats):
        s.chord(HORN, synth.horn, ["D4", "F4", "A4", "D5"], bar, 0, beats, 1.0)
        s.chord(HORN_LOW, synth.horn, ["D2", "D3", "A3"], bar, 0, beats, 0.9)
        s.chord(DRONE, synth.strings, ["D2", "A2", "D3", "F3", "A3"], bar, 0, beats, 0.8)
        s.play(KICK, synth.kick808, n("D2") - 12, bar, 0, 1, 1.0)
        s.play(TIMPANI, timpani, n("D2"), bar, 0, 1, 1.0)
        s.play(CRASH, crash, 0, bar, 0, 4, 1.0)
        s.play(BASS, synth.bass808, n("D2"), bar, 0, min(beats, 6), 0.8)

    hit(25, 12)
    for bar in (25, 26, 27):  # the heartbeat under the hero engine
        for beat in (0, 2):
            if (bar, beat) != (25, 0):
                s.play(TIMPANI, timpani, n("D2"), bar, beat, 1, 0.45)
    s.line(HORN_LOW, synth.horn, 26, [("D3", 2), ("F3", 2), ("A3", 3), ("G3", 1)], 0.7)
    for i in range(8):
        s.play(SNARE, synth.snare808, 0, 27, 2 + i * 0.25, .25, 0.3 + 0.07 * i)
    hit(28, 8)
    s.play(ANVIL, synth.bell, n("D6"), 28, 0, 2, 0.5)


def cues():
    return {"bpm": BPM, "bar": BAR, "cuts": {name: round(bar * BAR, 3) for name, bar in CUTS.items()}}


def render(out_dir):
    s = Score()
    intro(s)
    build(s)
    march(s)
    siren(s)
    finale(s)
    left, right, send = s.mix.stereo()
    wet_l, wet_r = synth.reverb(send)
    left, right = synth.master(left, right, wet_l, wet_r, synth.seconds(TAIL))
    out_dir.mkdir(parents=True, exist_ok=True)
    synth.write_wav(out_dir / "march.wav", left, right)
    (out_dir / "cues.json").write_text(json.dumps(cues(), indent=2) + "\n", encoding="utf-8")
    return out_dir / "march.wav"


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    print(render(parser.parse_args().out))
