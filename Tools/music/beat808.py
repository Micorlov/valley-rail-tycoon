"""A TR-808 beat under every piece: kick, snare, clap, closed and open hi-hats, and a tuned 808 sub-bass.

The groove follows the piece's meter and tempo. A bar holds 2, 3 or 4 felt pulses (2/4 and 6/8 -> 2; 3/4, 3/8 and
9/8 -> 3; 4/4 and 12/8 -> 4). The kick opens the bar and the snare with the clap takes the backbeat. Bars shorter
than SHORT_BAR (fast waltzes and galops) share one kick and one snare across two bars, four-pulse bars shorter than
QUICK_FOUR drop to a half-time snare, and three-pulse bars longer than LONG_BAR put the snare on the second pulse
and a second kick on the third. Hi-hats tick at the beat unit, halved or doubled until they fall 0.1-0.3 s apart,
with an open hat before every other cycle. The 808 bass plays the chord's bass note an octave or two under the
orchestra, restruck with every kick and chord change. In the last bar only a kick and the bass sound, so the final
chord rings out.
"""
from dataclasses import dataclass
from fractions import Fraction

from .notation import Note, place

KICK_PITCH = 34  # about 58 Hz, where an 808 kick sits
BASS_LOW = 28  # 808 bass notes fall between E1 and D#2 (41-78 Hz)
SHORT_BAR = 1.4  # seconds
QUICK_FOUR = 2.2  # seconds
LONG_BAR = 3.2  # seconds
HAT_MIN, HAT_MAX = 0.1, 0.3  # seconds between hi-hat ticks
LEVEL_DB = -4.0  # the whole beat's power relative to the music's, set per piece before mixing

# Added to every piece's LAYERS by arrange.load (a piece can opt out with BEAT_808 = False).
LAYERS = (
    dict(part="groove", voice="kick808", pattern="kick", gain=0.3, pan=0.5, send=0.04),
    dict(part="groove", voice="bass808", pattern="bass", gain=0.2, pan=0.5, send=0.0),
    dict(part="groove", voice="snare808", pattern="snare", gain=0.16, pan=0.48, send=0.2),
    dict(part="groove", voice="clap808", pattern="snare", gain=0.1, pan=0.54, send=0.25),
    dict(part="groove", voice="hat808", pattern="hat", gain=0.05, pan=0.62, send=0.08),
    dict(part="groove", voice="openhat808", pattern="openhat", gain=0.045, pan=0.62, send=0.12),
)


@dataclass(frozen=True)
class Groove:
    kicks: tuple  # beats into each cycle
    snares: tuple
    cycle: Fraction  # beats per repeat of the pattern (one or two bars)
    hat: Fraction  # beats between hi-hat ticks


def pulses(meter):
    """Felt pulses per bar for a meter given in beats per bar."""
    meter = Fraction(meter)
    for count, meters in ((2, (2, 6)), (3, (3, 9)), (4, (4, 12))):
        if meter in meters:
            return count
    raise ValueError(f"no 808 groove for {meter} beats per bar")


def groove(meter, bpm):
    meter, beat = Fraction(meter), 60 / bpm
    bar, count = float(meter) * beat, pulses(meter)
    pulse = meter / count
    offbeat = pulse * Fraction(2, 3) if pulse % 3 == 0 else pulse / 2  # compound pulses split 2 + 1
    if bar < SHORT_BAR:
        kicks, snares, cycle = (Fraction(0),), (meter,), 2 * meter
    elif count == 4:
        kicks, cycle = (Fraction(0), 2 * pulse + offbeat), meter
        snares = (2 * pulse,) if bar < QUICK_FOUR else (pulse, 3 * pulse)
    elif count == 3 and bar > LONG_BAR:
        kicks, snares, cycle = (Fraction(0), 2 * pulse), (pulse,), meter
    elif count == 3:
        kicks, snares, cycle = (Fraction(0),), (2 * pulse,), meter
    else:
        kicks, snares, cycle = (Fraction(0),), (pulse,), meter
    hat = Fraction(1)
    while float(hat) * beat > HAT_MAX:
        hat /= 2
    while float(hat) * beat < HAT_MIN:
        hat *= 2
    return Groove(kicks, snares, cycle, hat)


def _times(start, end, cycle, positions):
    times, base = [], start
    while base < end:
        times.extend(base + p for p in positions if base + p < end)
        base += cycle
    return times


def _chord_at(spans, time):
    return next((s for s in spans if s.start <= time < s.start + s.beats), None)


def notes(meter, bpm, section, pattern, final):
    """The notes of one groove part (kick, snare, hat, openhat or bass) across a section."""
    g = groove(meter, bpm)
    meter = Fraction(meter)
    start = section.pickup  # the beat starts with the first full bar
    end = section.beats - meter if final else section.beats
    kicks = _times(start, end, g.cycle, g.kicks)
    last = [end] if final and end >= start else []
    if pattern == "kick":
        return tuple(Note(t, Fraction(1, 2), KICK_PITCH, 0.95 if (t - start) % g.cycle == 0 else 0.8)
                     for t in kicks + last)
    if pattern == "snare":
        return tuple(Note(t, Fraction(1, 2), 60, 0.9) for t in _times(start, end, g.cycle, g.snares))
    opens = {t for i, t in enumerate(_times(start, end, g.cycle, (g.cycle - g.hat,))) if i % 2 == 1}
    if pattern == "openhat":
        return tuple(Note(t, g.hat, 60, 0.6) for t in sorted(opens))
    if pattern == "hat":
        ticks = _times(start, end, g.hat, (Fraction(0),))
        pulse = meter / pulses(meter)
        return tuple(Note(t, g.hat / 2, 60, 0.7 if (t - start) % pulse == 0 else 0.45) for t in ticks if t not in opens)
    if pattern == "bass":
        triggers = sorted({t for t in kicks + last} | {s.start for s in section.chords if start <= s.start < end})
        out = []
        for i, t in enumerate(triggers):
            span = _chord_at(section.chords, t)
            if span is None or span.chord is None:
                continue
            stop = min(triggers[i + 1] if i + 1 < len(triggers) else section.beats, span.start + span.beats)
            if t in last:
                stop = section.beats
            out.append(Note(t, stop - t, place(span.chord.bass, BASS_LOW), 0.9))
        return tuple(out)
    raise ValueError(f"unknown 808 part {pattern!r}")
