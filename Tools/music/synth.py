"""Instruments, mixing and mastering for Valley Rail's music (standard library only, deterministic).

Every voice is `voice(note, seconds, velocity, rng) -> array('d')`: a MIDI note held for `seconds`, scaled by
`velocity` (about 0.35 pp to 1.1 ff), with its natural release appended. Plucked and struck voices are sums of
damped partials computed with a two-term recurrence (no per-sample sine call); sustained voices read wavetables.
The flute, pad, bass, harp, celesta and shaker come from the original Valley theme.
"""
import array
import itertools
import math
import sys
import wave
from operator import add, mul

SAMPLE_RATE = 44100
TABLE_SIZE = 4096
TWO_PI = 2 * math.pi
HALF_PI = math.pi / 2
TARGET_RMS = 0.112  # about -19 dBFS before the limiter, the level of the original theme
PEAK_CEILING = 0.84  # -1.5 dBFS; the tanh limiter never exceeds it
REVERB_WET = 0.3


def midi_hz(note):
    return 440.0 * 2 ** ((note - 69) / 12)


def seconds(value):
    return int(value * SAMPLE_RATE)


def wavetable(harmonics):
    """One cycle of the given (harmonic, amplitude) pairs, normalised to a peak of 1."""
    table = [sum(a * math.sin(TWO_PI * h * i / TABLE_SIZE) for h, a in harmonics) for i in range(TABLE_SIZE)]
    peak = max(abs(v) for v in table)
    return [v / peak for v in table]


FLUTE = wavetable(((1, 1.0), (2, 0.22), (3, 0.1), (4, 0.03)))
PAD = wavetable(((1, 1.0), (3, -1 / 9), (5, 1 / 25)))  # a rounded triangle
BASS = wavetable(((1, 1.0), (2, 0.35), (3, 0.08)))  # the 2nd harmonic keeps it audible on phone speakers
STRINGS = wavetable(tuple((k, 0.9 ** k / k) for k in range(1, 16)))  # a softened sawtooth
REED = wavetable(((1, 0.7), (2, 1.0), (3, 0.8), (4, 0.45), (5, 0.3), (6, 0.15), (7, 0.08)))  # nasal, oboe-like
HORN = wavetable(((1, 1.0), (2, 0.55), (3, 0.3), (4, 0.15), (5, 0.07), (6, 0.03)))


def shape(samples, attack, gate, release, sustain=1.0, decay=1.0):
    """Raised-cosine attack, optional exponential decay to a sustain level, squared release after the gate."""
    n = len(samples)
    attack, gate = max(1, attack), min(gate, n)
    held = 1.0
    for i in range(min(attack, n)):
        held = 0.5 - 0.5 * math.cos(math.pi * i / attack)
        samples[i] *= held
    if gate > attack:
        held = 1.0
        if sustain < 1:
            for i in range(attack, gate):
                held = sustain + (1 - sustain) * math.exp(-(i - attack) / decay)
                samples[i] *= held
    for i in range(max(gate, attack), n):
        r = (i - gate) / release
        samples[i] *= held * (1 - r) * (1 - r) if r < 1 else 0.0
    return samples


def oscillate(table, freqs, length, vibrato=0.0):
    """Sum of wavetable oscillators; vibrato fades in after a quarter second, like a breath settling."""
    out = [0.0] * length
    mask = TABLE_SIZE - 1
    delay, ramp, rate = seconds(0.25), seconds(0.3), TWO_PI * 5.1 / SAMPLE_RATE
    for k, freq in enumerate(freqs):
        step = freq * TABLE_SIZE / SAMPLE_RATE
        phase = TABLE_SIZE * k / len(freqs)
        for i in range(length):
            out[i] += table[int(phase) & mask]
            if vibrato and i > delay:
                phase += step * (1 + vibrato * min(1.0, (i - delay) / ramp) * math.sin(rate * i))
            else:
                phase += step
    return out


def resonate(out, freq, amp, tau):
    """Add amp * exp(-t / tau) * sin(2 pi freq t) to `out` with the damped-oscillator recurrence."""
    w = TWO_PI * freq / SAMPLE_RATE
    r = math.exp(-1 / (tau * SAMPLE_RATE))
    c1, c2 = 2 * r * math.cos(w), r * r
    y2, y1 = 0.0, amp * r * math.sin(w)
    for i in range(1, len(out)):
        out[i] += y1
        y1, y2 = c1 * y1 - c2 * y2, y1


def _finish(samples, velocity, level):
    return array.array("d", map(mul, samples, itertools.repeat(velocity * level)))


def _gate(length, trim=0.0):
    return max(seconds(0.03), seconds(length - trim))


def flute(note, length, velocity, rng):
    gate, release = _gate(length, 0.02), seconds(0.25)
    out = oscillate(FLUTE, (midi_hz(note),), gate + release, vibrato=0.0045)
    breath = 0.0
    for i in range(len(out)):
        breath += 0.12 * (rng.uniform(-1, 1) - breath)
        out[i] += 0.35 * breath
    return _finish(shape(out, seconds(0.07), gate, release, sustain=0.85, decay=seconds(0.6)), velocity, 1.0)


def reed(note, length, velocity, rng):
    gate, release = _gate(length, 0.02), seconds(0.16)
    out = oscillate(REED, (midi_hz(note),), gate + release, vibrato=0.004)
    return _finish(shape(out, seconds(0.05), gate, release, sustain=0.9, decay=seconds(0.4)), velocity, 0.9)


def horn(note, length, velocity, rng):
    gate, release = _gate(length, 0.03), seconds(0.3)
    freq = midi_hz(note)
    out = oscillate(HORN, (freq * 0.999, freq * 1.001), gate + release, vibrato=0.002)
    return _finish(shape(out, seconds(0.09), gate, release), velocity, 0.5)


def strings(note, length, velocity, rng):
    gate, release = _gate(length, 0.01), seconds(0.28)
    freq = midi_hz(note)
    out = oscillate(STRINGS, (freq * 0.998, freq * 1.002), gate + release, vibrato=0.0045)
    attack = seconds(min(0.14, max(0.02, 0.4 * length)))
    return _finish(shape(out, attack, gate, release), velocity, 0.5)


def pad(note, length, velocity, rng):
    gate, release = _gate(length), seconds(1.4)
    freq = midi_hz(note)
    out = oscillate(PAD, (freq * 0.9975, freq * 1.0025), gate + release)
    return _finish(shape(out, seconds(min(0.8, max(0.05, 0.6 * length))), gate, release), velocity, 1.0)


def bass(note, length, velocity, rng):
    gate, release = _gate(length, 0.05), seconds(0.18)
    out = oscillate(BASS, (midi_hz(note),), gate + release)
    return _finish(shape(out, seconds(0.006), gate, release, sustain=0.55, decay=seconds(0.5)), velocity, 1.0)


def _plucked(note, total, partials, level, velocity):
    """Damped partials (harmonic k -> (amplitude, time constant)) faded out over the last 0.25 s of `total`."""
    freq, out = midi_hz(note), [0.0] * total
    for k, (amp, tau) in partials:
        if freq * k < 9000:
            resonate(out, freq * k, amp, tau)
    fade = min(seconds(0.25), total // 3)
    return _finish(shape(out, seconds(0.002), total - fade, fade), velocity, level)


def harp(note, length, velocity, rng):
    partials = ((k, (k ** -1.3, 1 / (1.8 + 0.7 * k ** 1.5))) for k in range(1, 11))
    return _plucked(note, seconds(2.0), partials, 1.0, velocity)


def pizz(note, length, velocity, rng):
    partials = ((k, (k ** -1.6, 0.28 / (1 + 0.6 * (k - 1)))) for k in range(1, 9))
    return _plucked(note, seconds(0.8), partials, 1.0, velocity)


def harpsichord(note, length, velocity, rng):
    """Bright plucked string whose damper falls when the key is released."""
    freq, total = midi_hz(note), seconds(min(length, 2.0) + 0.08)
    out = [0.0] * total
    for k in range(1, 21):
        if freq * k >= 9000:
            break
        comb = abs(math.sin(math.pi * k * 0.13))
        resonate(out, freq * k, k ** -0.8 * comb, 0.9 / (1 + 0.25 * (k - 1)))
    return _finish(shape(out, seconds(0.001), total - seconds(0.08), seconds(0.08)), velocity, 0.6)


def piano(note, length, velocity, rng):
    """Slightly inharmonic partials, each a fast and a slow decay on two strings a hair apart, then the damper."""
    freq = midi_hz(note)
    slow = 0.6 + 4.0 * min(1.0, max(0.0, (96 - note) / 60))  # low strings ring longer
    held = min(length, slow * 2.5)
    release = seconds(0.12)
    out = [0.0] * (seconds(held) + release)
    tilt = 1.9 - 0.8 * min(velocity, 1.1)  # harder strikes are brighter
    for k in range(1, 13):
        partial = freq * k * math.sqrt(1 + 0.00035 * k * k)
        if partial >= 8000:
            break
        amp, tau = k ** -tilt, slow / (1 + 0.3 * (k - 1) ** 1.3)
        resonate(out, partial, 0.55 * amp, 0.18 * tau)
        resonate(out, partial * 1.0006, 0.45 * amp, tau)
    thump = 0.0
    for i in range(seconds(0.006)):
        thump += 0.3 * (rng.uniform(-1, 1) - thump)
        out[i] += 0.08 * thump * (1 - i / seconds(0.006))
    return _finish(shape(out, seconds(0.002), seconds(held), release), velocity, 0.5)


def bell(note, length, velocity, rng):
    """FM celesta: a 4:1 modulator whose brightness fades quickly, leaving a pure ring."""
    freq, total = midi_hz(note), seconds(2.2)
    carrier, modulator = TWO_PI * freq / SAMPLE_RATE, TWO_PI * freq * 4 / SAMPLE_RATE
    out = [0.0] * total
    for i in range(total):
        t = i / SAMPLE_RATE
        index = 1.4 * math.exp(-t / 0.18)
        out[i] = math.exp(-t / 0.9) * math.sin(carrier * i + index * math.sin(modulator * i))
    return _finish(shape(out, seconds(0.002), total - seconds(0.3), seconds(0.3)), velocity, 1.0)


def shaker(note, length, velocity, rng):
    total, previous, out = seconds(0.08), 0.0, []
    for i in range(total):
        white = rng.uniform(-1, 1)
        out.append((white - previous) * math.exp(-i / seconds(0.018)))
        previous = white
    return _finish(shape(out, seconds(0.004), total, 1), velocity, 1.0)


def _highpass(samples):
    """First difference: a gentle 6 dB/octave tilt towards the treble."""
    previous, out = 0.0, []
    for v in samples:
        out.append(v - previous)
        previous = v
    return out


def kick808(note, length, velocity, rng):
    """TR-808 bass drum: a sine sweeping down onto its pitch with a long boom, driven a little for punch."""
    total, base, phase, out = seconds(0.9), midi_hz(note), 0.0, []
    for i in range(total):
        t = i / SAMPLE_RATE
        phase += TWO_PI * base * (1 + 2.2 * math.exp(-t / 0.035)) / SAMPLE_RATE
        out.append(math.tanh(1.8 * math.sin(phase) * math.exp(-t / 0.32)))
    for i in range(seconds(0.003)):
        out[i] += 0.3 * rng.uniform(-1, 1) * (1 - i / seconds(0.003))
    return _finish(shape(out, seconds(0.001), total - seconds(0.1), seconds(0.1)), velocity, 0.8)


def bass808(note, length, velocity, rng):
    """Tuned 808 sub-bass: a sine that drops onto its pitch, with a touch of second harmonic and soft saturation so
    phone speakers, which cannot reproduce 40-80 Hz, still hear its overtones."""
    held, release = min(length, 4.0), seconds(0.08)
    total, base, phase, out = seconds(held) + release, midi_hz(note), 0.0, []
    for i in range(total):
        t = i / SAMPLE_RATE
        phase += TWO_PI * base * (1 + 0.12 * math.exp(-t / 0.03)) / SAMPLE_RATE
        tone = math.sin(phase) + 0.25 * math.sin(2 * phase)
        out.append(math.tanh(2.2 * tone * (0.35 + 0.65 * math.exp(-t / 1.2))))
    return _finish(shape(out, seconds(0.003), seconds(held), release), velocity, 0.7)


def snare808(note, length, velocity, rng):
    """808 snare: two damped drum tones under a snappy burst of brightened noise."""
    total = seconds(0.25)
    out = [0.0] * total
    resonate(out, 180.0, 0.6, 0.06)
    resonate(out, 330.0, 0.4, 0.04)
    noise = _highpass([rng.uniform(-1, 1) for _ in range(total)])
    for i in range(total):
        out[i] += 0.55 * noise[i] * math.exp(-i / seconds(0.07))
    return _finish(shape(out, seconds(0.001), total - seconds(0.05), seconds(0.05)), velocity, 0.9)


def clap808(note, length, velocity, rng):
    """808 hand clap: three quick noise bursts and a short diffuse tail."""
    total, level = seconds(0.3), 0.0
    noise = _highpass(_highpass([rng.uniform(-1, 1) for _ in range(total)]))
    out = []
    for i in range(total):
        t = i / SAMPLE_RATE
        burst = max((math.exp(-(t - start) / 0.006) for start in (0.0, 0.011, 0.022) if t >= start), default=0.0)
        tail = math.exp(-(t - 0.03) / 0.09) if t >= 0.03 else 0.0
        level += 0.5 * (noise[i] - level)  # soften the very top so it reads as a clap, not a hiss
        out.append(level * max(burst, 0.6 * tail))
    return _finish(shape(out, seconds(0.001), total - seconds(0.05), seconds(0.05)), velocity, 1.2)


HAT_FREQUENCIES = (205.3, 304.4, 369.6, 522.7, 540.0, 800.0)  # the 808's six detuned square oscillators


def _hat(decay, velocity, rng):
    total = seconds(decay * 4)
    phases = [rng.uniform(0, TWO_PI) for _ in HAT_FREQUENCIES]
    steps = [TWO_PI * f * 8 / SAMPLE_RATE for f in HAT_FREQUENCIES]  # three octaves up, into the hat's range
    metal = []
    for i in range(total):
        metal.append(sum(1.0 if math.sin(p + s * i) >= 0 else -1.0 for p, s in zip(phases, steps)) / 6)
    bright = _highpass(_highpass(metal))
    noise = _highpass([rng.uniform(-1, 1) for _ in range(total)])
    out = [(0.7 * bright[i] + 0.3 * noise[i]) * math.exp(-i / seconds(decay)) for i in range(total)]
    return _finish(shape(out, seconds(0.0005), total - seconds(decay), seconds(decay)), velocity, 0.6)


def hat808(note, length, velocity, rng):
    return _hat(0.04, velocity, rng)


def openhat808(note, length, velocity, rng):
    return _hat(0.22, velocity, rng)


VOICES = {
    "flute": flute, "reed": reed, "horn": horn, "strings": strings, "pad": pad, "bass": bass, "harp": harp,
    "pizz": pizz, "harpsichord": harpsichord, "piano": piano, "bell": bell, "shaker": shaker,
    "kick808": kick808, "bass808": bass808, "snare808": snare808, "clap808": clap808, "hat808": hat808,
    "openhat808": openhat808,
}
# Lowest and highest MIDI notes each voice is written for.
RANGES = {
    "flute": (59, 98), "reed": (52, 91), "horn": (34, 79), "strings": (28, 100), "pad": (36, 86), "bass": (24, 62),
    "harp": (24, 103), "pizz": (28, 96), "harpsichord": (29, 89), "piano": (21, 108), "bell": (60, 108),
    "shaker": (0, 127), "kick808": (0, 127), "bass808": (24, 45), "snare808": (0, 127), "clap808": (0, 127),
    "hat808": (0, 127), "openhat808": (0, 127),
}
# Largest random timing offset, in seconds, so parts do not strike with machine precision; the drum machine is tight.
JITTER = {"strings": 0.008, "pad": 0.01, "flute": 0.006, "reed": 0.006, "horn": 0.008, "shaker": 0.003,
          "kick808": 0.0, "bass808": 0.0, "snare808": 0.001, "clap808": 0.001, "hat808": 0.001, "openhat808": 0.001}
DEFAULT_JITTER = 0.004


def _scaled_sum(total, bus, level):
    return array.array("d", map(add, total, map(mul, bus, itertools.repeat(level))))


class Mix:
    """One mono bus per layer; `stereo()` pans each layer once into the dry pair and the mono reverb send."""

    def __init__(self, length, layers):
        self.length = length
        self.layers = tuple(layers)  # (pan, send) per layer
        self.buses = [array.array("d", bytes(8 * length)) for _ in self.layers]

    def add(self, layer, start, samples, gain=1.0):
        if start < 0:
            samples, start = samples[-start:], 0
        end = min(start + len(samples), self.length)
        if end <= start:
            return
        if end - start < len(samples):
            samples = samples[: end - start]
        bus, span = self.buses[layer], slice(start, end)
        bus[span] = _scaled_sum(bus[span], samples, gain)

    def balance(self, layers, level_db):
        """Scale the given layers together so their power sits `level_db` relative to all the other layers."""
        power = [sum(map(mul, bus, bus)) for bus in self.buses]
        chosen = sum(power[i] for i in layers)
        rest = sum(p for i, p in enumerate(power) if i not in layers)
        if not chosen or not rest:
            return
        factor = math.sqrt(rest / chosen) * 10 ** (level_db / 20)
        for i in layers:
            self.buses[i] = array.array("d", map(mul, self.buses[i], itertools.repeat(factor)))

    def stereo(self):
        """(left, right, send) summed over the layers."""
        left, right, send = (array.array("d", bytes(8 * self.length)) for _ in range(3))
        for bus, (pan, level) in zip(self.buses, self.layers):
            left = _scaled_sum(left, bus, math.cos(pan * HALF_PI))
            right = _scaled_sum(right, bus, math.sin(pan * HALF_PI))
            if level:
                send = _scaled_sum(send, bus, level)
        return left, right, send


def reverb(bus):
    """Four-line feedback delay network (Householder matrix) behind two diffusing allpasses, RT60 2.4 s."""
    n = len(bus)
    out_l, out_r = array.array("d", bytes(8 * n)), array.array("d", bytes(8 * n))
    l0, l1, l2, l3 = 1427, 1777, 2143, 2593
    g0, g1, g2, g3 = (10 ** (-3 * d / (2.4 * SAMPLE_RATE)) for d in (l0, l1, l2, l3))
    b0, b1, b2, b3 = [0.0] * l0, [0.0] * l1, [0.0] * l2, [0.0] * l3
    p0 = p1 = p2 = p3 = 0
    f0 = f1 = f2 = f3 = 0.0
    a1, a2, q1, q2 = [0.0] * 223, [0.0] * 557, 0, 0
    damp = 0.4
    for j in range(n):
        x = bus[j]
        v = a1[q1]
        y = v - 0.5 * x
        a1[q1] = x + 0.5 * y
        q1 = q1 + 1 if q1 < 222 else 0
        v = a2[q2]
        x = v - 0.5 * y
        a2[q2] = y + 0.5 * x
        q2 = q2 + 1 if q2 < 556 else 0
        f0 += damp * (b0[p0] - f0)
        f1 += damp * (b1[p1] - f1)
        f2 += damp * (b2[p2] - f2)
        f3 += damp * (b3[p3] - f3)
        s = (f0 + f1 + f2 + f3) * 0.5
        out_l[j] = f0 + f2
        out_r[j] = f1 + f3
        b0[p0] = x + g0 * (f0 - s)
        b1[p1] = x + g1 * (f1 - s)
        b2[p2] = x + g2 * (f2 - s)
        b3[p3] = x + g3 * (f3 - s)
        p0 = p0 + 1 if p0 < l0 - 1 else 0
        p1 = p1 + 1 if p1 < l1 - 1 else 0
        p2 = p2 + 1 if p2 < l2 - 1 else 0
        p3 = p3 + 1 if p3 < l3 - 1 else 0
    return out_l, out_r


def rms_of(*channels):
    return math.sqrt(sum(sum(v * v for v in ch) for ch in channels) / sum(len(ch) for ch in channels))


def master(dry_l, dry_r, wet_l, wet_r, fade):
    """Blend dry and wet, set loudness, soft-limit below the peak ceiling and fade the last `fade` samples out."""
    left = _scaled_sum(dry_l, wet_l, REVERB_WET)
    right = _scaled_sum(dry_r, wet_r, REVERB_WET)
    scale = TARGET_RMS / rms_of(left, right) / PEAK_CEILING
    n = len(left)

    def limit(channel):
        out = array.array("d", (PEAK_CEILING * math.tanh(v * scale) for v in channel))
        for i in range(max(0, n - fade), n):
            out[i] *= 0.5 + 0.5 * math.cos(math.pi * (i - (n - fade)) / fade)
        return out

    return limit(left), limit(right)


def write_wav(path, left, right):
    frames = array.array("h", bytes(4 * len(left)))
    frames[0::2] = array.array("h", (int(round(v * 32767)) for v in left))
    frames[1::2] = array.array("h", (int(round(v * 32767)) for v in right))
    if sys.byteorder == "big":
        frames.byteswap()
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as out:
        out.setnchannels(2)
        out.setsampwidth(2)
        out.setframerate(SAMPLE_RATE)
        out.writeframes(frames.tobytes())
