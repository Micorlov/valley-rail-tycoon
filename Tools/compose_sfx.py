#!/usr/bin/env python3
"""Synthesize Valley Rail's sound effects.

Every cue is built from sine partials, filtered noise and simple envelopes. Pitched cues are in D major, so they sit
softly under the music instead of clashing with it. Only the standard library is used and each cue has its
own fixed seed, so every render (and every --only render) is identical.

Clips are 44.1 kHz 16-bit mono WAV, levelled to a target short-term loudness (RMS of the loudest 100 ms) so the game
can play them at similar volumes. `rail_loop` and `siren_loop` wrap every tail around their end and loop without a seam. Unity imports
them decompressed (Assets/Game/Scripts/Editor/ThemeMusicImport.cs) and Presentation/SoundEffects.cs plays them.

    python3 Tools/compose_sfx.py [--out DIR] [--only NAME ...]
"""
import argparse
import array
import math
import random
import sys
import wave
from pathlib import Path

SAMPLE_RATE = 44100
SEED = 2411
TWO_PI = 2 * math.pi
WINDOW = SAMPLE_RATE // 10  # loudness is measured over the loudest 100 ms
PEAK_CEILING = 0.89  # -1 dBFS; the tanh limiter never exceeds it
EDGE_FADE = 0.004  # seconds faded at each end of a one-shot so none starts or stops with a click
SILENCE = 1e-4  # trailing samples quieter than this (-80 dBFS) are trimmed from one-shots
DEFAULT_OUT = Path(__file__).resolve().parent.parent / "Assets/Game/Resources/Audio/Sfx"


def midi_hz(note):
    return 440.0 * 2 ** ((note - 69) / 12)


def n(seconds):
    return int(round(seconds * SAMPLE_RATE))


class Clip:
    """A mono mix bus. A looping clip wraps anything that runs past its end back to the start."""

    def __init__(self, seconds, loop=False):
        self.data = array.array("d", bytes(8 * n(seconds)))
        self.loop = loop

    def add(self, start, samples, gain=1.0):
        data, size, j = self.data, len(self.data), n(start)
        for v in samples:
            if j >= size:
                if not self.loop:
                    break
                j -= size
            data[j] += v * gain
            j += 1


# ---- building blocks ------------------------------------------------------------------------------------------------

def noise(rng, seconds):
    return [rng.uniform(-1, 1) for _ in range(n(seconds))]


def biquad(samples, kind, freq, q=0.707):
    """RBJ cookbook low-pass, high-pass or band-pass (0 dB peak) filter."""
    w = TWO_PI * freq / SAMPLE_RATE
    cos_w, alpha = math.cos(w), math.sin(w) / (2 * q)
    if kind == "low":
        b = ((1 - cos_w) / 2, 1 - cos_w, (1 - cos_w) / 2)
    elif kind == "high":
        b = ((1 + cos_w) / 2, -(1 + cos_w), (1 + cos_w) / 2)
    else:
        b = (alpha, 0.0, -alpha)
    a0 = 1 + alpha
    b0, b1, b2 = (x / a0 for x in b)
    a1, a2 = -2 * cos_w / a0, (1 - alpha) / a0
    out, x1, x2, y1, y2 = [], 0.0, 0.0, 0.0, 0.0
    for x in samples:
        y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def sweep_low(samples, start_hz, end_hz):
    """One-pole low-pass whose cutoff glides exponentially from start_hz to end_hz."""
    out, y, count = [], 0.0, len(samples)
    for i, x in enumerate(samples):
        f = start_hz * (end_hz / start_hz) ** (i / count)
        y += (1 - math.exp(-TWO_PI * f / SAMPLE_RATE)) * (x - y)
        out.append(y)
    return out


def envelope(samples, attack, decay, hold=0.0):
    """Linear attack, optional hold, then an exponential decay with time constant `decay` seconds."""
    a, h = n(attack), n(hold)
    k, level, out = math.exp(-1 / (decay * SAMPLE_RATE)), 1.0, []
    for i, x in enumerate(samples):
        if i < a:
            g = i / a
        elif i < a + h:
            g = 1.0
        else:
            level *= k
            g = level
        out.append(x * g)
    return out


def fade(samples, attack, release):
    """Linear fade in and out."""
    a, r, count = n(attack), n(release), len(samples)
    return [x * min(1.0, i / a if a else 1.0, (count - i) / r if r else 1.0) for i, x in enumerate(samples)]


def partials(freq, seconds, ratios, amps, decays):
    """Struck metal: inharmonic sine partials, each with its own exponential decay (time constants in seconds)."""
    out = [0.0] * n(seconds)
    for ratio, amp, tau in zip(ratios, amps, decays):
        w = TWO_PI * freq * ratio / SAMPLE_RATE
        if w >= math.pi:  # above Nyquist
            continue
        k, a = math.exp(-1 / (tau * SAMPLE_RATE)), amp
        for i in range(len(out)):
            out[i] += a * math.sin(w * i)
            a *= k
    return out


def tone(freq, seconds, harmonics, attack, release, vibrato=0.0, rate=5.0):
    """Additive tone from (harmonic, amplitude) pairs with linear edges and optional vibrato (a fraction of pitch)."""
    out, phase = [], 0.0
    for i in range(n(seconds)):
        phase += TWO_PI * freq * (1 + vibrato * math.sin(TWO_PI * rate * i / SAMPLE_RATE)) / SAMPLE_RATE
        out.append(sum(amp * math.sin(h * phase) for h, amp in harmonics))
    return fade(out, attack, release)


def thud(start_hz, end_hz, seconds, decay):
    """A low body hit: a sine falling in pitch under an exponential decay."""
    out, phase, level = [], 0.0, 1.0
    k, count = math.exp(-1 / (decay * SAMPLE_RATE)), n(seconds)
    for i in range(count):
        phase += TWO_PI * start_hz * (end_hz / start_hz) ** (i / count) / SAMPLE_RATE
        out.append(math.sin(phase) * level)
        level *= k
    return out


def bell(freq, seconds, decay):
    """Glockenspiel-like bar: the classic 1 : 2.76 : 5.40 : 8.93 partial series."""
    return partials(freq, seconds, (1, 2.76, 5.40, 8.93), (1, .42, .22, .1), (decay, decay * .45, decay * .22, decay * .1))


def tick(rng, seconds, cutoff, decay):
    """A short, bright transient: high-passed noise with a fast decay."""
    return envelope(biquad(noise(rng, seconds), "high", cutoff), .0005, decay)


# ---- cues -----------------------------------------------------------------------------------------------------------

def cha(rng, clip, start):
    """The till's mechanical 'cha': a short band of bright noise."""
    clip.add(start, envelope(biquad(noise(rng, .09), "band", 3200, .9), .002, .025), .6)


def coin(rng, clip, start, freq, gain=1.0):
    clip.add(start, partials(freq, .6, (1, 1.49, 2.32, 3.51), (1, .55, .4, .22), (.24, .13, .08, .05)), gain)
    clip.add(start, tick(rng, .02, 5000, .004), .25 * gain)


def coins(rng):
    """Delivery income: 'cha-ching' on A6 and D7 with a quiet F#7 bounce."""
    clip = Clip(.8)
    cha(rng, clip, 0)
    coin(rng, clip, .05, midi_hz(93))
    coin(rng, clip, .07, midi_hz(98), .8)
    coin(rng, clip, .16, midi_hz(102), .45)
    return clip


def coins_big(rng):
    """A large delivery: coins cascading down the D major pentatonic, then a high shimmer."""
    clip = Clip(1.5)
    cha(rng, clip, 0)
    for i, note in enumerate((105, 102, 100, 98, 95, 93)):
        coin(rng, clip, .05 + i * .075 + rng.uniform(0, .02), midi_hz(note), .9 - i * .06)
    shimmer = [math.sin(TWO_PI * midi_hz(110) * i / SAMPLE_RATE) * (.5 + .5 * math.sin(TWO_PI * 9 * i / SAMPLE_RATE))
               for i in range(n(.9))]
    clip.add(.3, envelope(shimmer, .15, .25), .12)
    return clip


def knock(rng, clip, start, gain=1.0):
    """A mallet on a timber post: a falling low tone plus a woody band of noise."""
    clip.add(start, thud(190, 120, .18, .035), gain)
    clip.add(start, envelope(biquad(noise(rng, .08), "band", 900, 2.5), .001, .018), 2.5 * gain)


def station_built(rng):
    """Two hammer knocks, then a bell on D6 with a quiet fifth above."""
    clip = Clip(1.4)
    knock(rng, clip, 0)
    knock(rng, clip, .16, .85)
    clip.add(.36, bell(midi_hz(86), 1.0, .45), .55)
    clip.add(.36, bell(midi_hz(93), 1.0, .3), .2)
    return clip


def clink(rng, clip, start, freq, gain=1.0):
    """A spike driven into a rail: an inharmonic metallic ring over a sharp tick."""
    ring = partials(freq, .35, (1, 1.47, 2.09, 2.56, 3.37), (1, .7, .5, .35, .2), (.07, .05, .04, .03, .02))
    clip.add(start, ring, gain)
    clip.add(start, tick(rng, .02, 3000, .003), .5 * gain)


def track_built(rng):
    """The rail settling on its sleepers, then three spike clinks."""
    clip = Clip(.8)
    clip.add(0, thud(110, 70, .2, .05), .6)
    for i in range(3):
        clink(rng, clip, .02 + i * .13, 2150 * (1 + rng.uniform(-.04, .04)), 1 - i * .12)
    return clip


def bulldoze(rng):
    """A heavy thud, collapsing rubble (noise whose brightness falls away) and scattered debris."""
    clip = Clip(1.2)
    clip.add(0, thud(85, 40, .5, .14))
    clip.add(0, envelope(sweep_low(noise(rng, 1.1), 1600, 250), .01, .32), 2.2)
    for _ in range(14):
        t = rng.uniform(.05, .7)
        grain = envelope(biquad(noise(rng, .06), "band", rng.uniform(350, 1500), 3), .001, .012)
        clip.add(t, grain, rng.uniform(1.5, 4) * (1 - t))
    return clip


HORN = tuple((h, 1 / h) for h in range(1, 9))  # a buzzy, sawtooth-like reed


def horn(clip, start, seconds, notes, gain=1.0):
    """An air horn: each note is two slightly detuned reeds, gently low-passed."""
    for note in notes:
        for detune in (-.003, .003):
            voice = tone(midi_hz(note) * (1 + detune), seconds, HORN, .04, .12, vibrato=.002, rate=5.5)
            clip.add(start, biquad(voice, "low", 2600), .5 * gain)


def train_bought(rng):
    """A new locomotive says hello: two-tone horn on A4 + C#5, short then long."""
    clip = Clip(1.0)
    horn(clip, 0, .22, (69, 73))
    horn(clip, .3, .6, (69, 73))
    return clip


def pea_whistle(rng, seconds):
    """A conductor's whistle: a high tone warbled by the pea, over breath noise."""
    out, phase = [], 0.0
    for i in range(n(seconds)):
        trill = math.sin(TWO_PI * 26 * i / SAMPLE_RATE)
        phase += TWO_PI * 2750 * (1 + .025 * trill) / SAMPLE_RATE
        out.append(math.sin(phase) * (.7 + .3 * trill))
    breath = biquad(noise(rng, seconds), "band", 2750, 4)
    return fade([a + .5 * b for a, b in zip(out, breath)], .015, .06)


def route_start(rng):
    """'All aboard!': a short then a long pea-whistle blast."""
    clip = Clip(.8)
    clip.add(0, pea_whistle(rng, .1))
    clip.add(.16, pea_whistle(rng, .5))
    return clip


def steam_whistle(rng, clip, start, seconds):
    """A three-chime steam whistle voiced as a D major triad, with breath."""
    for note in (74, 78, 81):
        clip.add(start, tone(midi_hz(note), seconds, ((1, 1), (2, .12)), .06, .12, vibrato=.003, rate=6), .33)
    clip.add(start, fade(biquad(noise(rng, seconds), "band", 1400, 1.2), .06, .12), .25)


def chuff(rng, clip, start, gain=1.0):
    """One exhaust beat: a puff of mid-band noise."""
    clip.add(start, envelope(biquad(noise(rng, .3), "band", 650, .8), .012, .07), gain)


def train_depart(rng):
    """Whistle toot, then the first three exhaust beats as the train pulls away."""
    clip = Clip(1.4)
    steam_whistle(rng, clip, 0, .45)
    for i, t in enumerate((.55, .82, 1.05)):
        chuff(rng, clip, t, 1.6 - i * .35)
    return clip


def train_arrive(rng):
    """Brake hiss with a faint squeal, then the station's two-note chime (F#5, D5)."""
    clip = Clip(1.7)
    hiss = biquad(biquad(noise(rng, .9), "high", 3500), "low", 7000)
    clip.add(0, envelope(hiss, .05, .35, hold=.15), .7)
    clip.add(.02, tone(3150, .5, ((1, 1),), .08, .2, vibrato=.004, rate=7), .08)
    clip.add(.6, bell(midi_hz(78), 1.0, .35), .5)
    clip.add(.9, bell(midi_hz(74), .8, .45), .5)
    return clip


def town_level_up(rng):
    """A town grows a level: a rising D major arpeggio on bells over a soft swell."""
    clip = Clip(2.0)
    for i, note in enumerate((86, 90, 93, 98)):
        clip.add(i * .11, bell(midi_hz(note), 1.5, .35 if i < 3 else .7), .5)
    pad = [math.sin(TWO_PI * midi_hz(62) * i / SAMPLE_RATE) + .6 * math.sin(TWO_PI * midi_hz(69) * i / SAMPLE_RATE)
           for i in range(n(1.6))]
    clip.add(.05, fade(envelope(pad, .25, .5), 0, .3), .15)
    return clip


def error(rng):
    """Two soft, low, falling blips (A3 then F3): noticeable, never alarming."""
    clip = Clip(.4)
    rounded_square = ((1, 1), (3, 1 / 3), (5, 1 / 5), (7, 1 / 7))
    for start, note in ((0, 57), (.14, 53)):
        clip.add(start, biquad(tone(midi_hz(note), .11, rounded_square, .005, .05), "low", 1200))
    return clip


def rail_loop(rng):
    """Wheels over rail joints: each bogie gives a quick double click ('clickety-clack') over a low rumble.
    Two slightly different bars keep the repeat from sounding mechanical."""
    bar = 1.2
    clip = Clip(2 * bar, loop=True)
    for b in range(2):
        for bogie in (0.0, .5):
            for axle in (0.0, .09):
                t = b * bar + bogie + axle + rng.uniform(-.004, .004)
                gain = rng.uniform(.75, 1.0) * (1 if axle == 0 else .8)
                clip.add(t, envelope(biquad(noise(rng, .06), "band", 1700, 1.4), .0008, .012), gain)
                clip.add(t, thud(140, 90, .08, .02), .5 * gain)
    # Filter one period of noise twice over and keep the second pass, so the filter state is periodic as well.
    period = len(clip.data)
    source = noise(rng, 2 * bar)[:period]
    clip.add(0, biquad(source + source, "low", 180, .9)[period:], 1.2)
    return clip


def siren_loop(rng):
    """An emergency vehicle's wail: a buzzy tone that sweeps up quickly and falls away slowly, softened as if heard
    down the street. The sweep is tuned to a whole number of cycles, so the phase meets itself at the loop point."""
    seconds, low, high, rise = 3.6, 640.0, 1320.0, .4
    count = n(seconds)

    def pitch(u):
        if u < rise:
            return low + (high - low) * math.sin(math.pi / 2 * u / rise)
        return low + (high - low) * math.cos(math.pi / 2 * (u - rise) / (1 - rise))

    cycles = sum(pitch(i / count) for i in range(count)) / SAMPLE_RATE
    scale = round(cycles) / cycles
    phase, wave_ = 0.0, []
    for i in range(count):
        wave_.append(math.sin(phase) + .3 * math.sin(2 * phase) + .33 * math.sin(3 * phase) + .2 * math.sin(5 * phase))
        phase += TWO_PI * pitch(i / count) * scale / SAMPLE_RATE
    clip = Clip(seconds, loop=True)
    # Filter one period twice over and keep the second pass, so the filter state is periodic as well.
    clip.add(0, biquad(wave_ + wave_, "low", 2600, .8)[count:])
    return clip


# name: (builder, target loudness as the RMS of the loudest 100 ms). Money is the loudest; the rail loop sits beneath.
CUES = {
    "coins": (coins, .09),
    "coins_big": (coins_big, .09),
    "station_built": (station_built, .075),
    "track_built": (track_built, .07),
    "bulldoze": (bulldoze, .075),
    "train_bought": (train_bought, .065),
    "route_start": (route_start, .055),
    "train_depart": (train_depart, .065),
    "train_arrive": (train_arrive, .06),
    "town_level_up": (town_level_up, .07),
    "error": (error, .06),
    "rail_loop": (rail_loop, .045),
    "siren_loop": (siren_loop, .05),
}


# ---- levelling and output -------------------------------------------------------------------------------------------

def loudness(data):
    """RMS of the loudest 100 ms window."""
    squares = [v * v for v in data]
    best = acc = 0.0
    for i, s in enumerate(squares):
        acc += s
        if i >= WINDOW:
            acc -= squares[i - WINDOW]
        best = max(best, acc)
    return math.sqrt(best / min(WINDOW, len(data)))


def finish(clip, target):
    """Level to the target loudness under a soft limiter; trim and fade one-shots."""
    gain = target / loudness(clip.data)
    peak = max(abs(v) for v in clip.data) * gain
    if peak > PEAK_CEILING:
        data = [PEAK_CEILING * math.tanh(v * gain / PEAK_CEILING) for v in clip.data]
    else:
        data = [v * gain for v in clip.data]
    if clip.loop:
        return data
    end = len(data)
    while end > 0 and abs(data[end - 1]) < SILENCE:
        end -= 1
    return fade(data[:end], EDGE_FADE, EDGE_FADE)


def write(path, data):
    frames = array.array("h", (int(round(max(-1.0, min(1.0, v)) * 32767)) for v in data))
    if sys.byteorder == "big":
        frames.byteswap()
    with wave.open(str(path), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(SAMPLE_RATE)
        out.writeframes(frames.tobytes())


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help="output folder (default: %(default)s)")
    parser.add_argument("--only", nargs="+", choices=sorted(CUES), help="render just these cues")
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    for name in args.only or CUES:
        build, target = CUES[name]
        data = finish(build(random.Random(f"{SEED}:{name}")), target)
        path = args.out / f"{name}.wav"
        write(path, data)
        print(f"{name:14s} {len(data) / SAMPLE_RATE:5.2f} s  loudness {20 * math.log10(loudness(data)):6.1f} dBFS"
              f"  peak {20 * math.log10(max(abs(v) for v in data)):6.1f} dBFS  -> {path}")


if __name__ == "__main__":
    main()
