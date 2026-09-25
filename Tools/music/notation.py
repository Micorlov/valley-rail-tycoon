"""Text notation for the classical arrangements: melodies note by note, harmony as chord symbols.

Notes (whitespace separated, `|` between bars, `%` starts a comment):
    e5:1/2 d#5 e5 | a4+c5+e5:3/2 r:1/2 | g4:1~ | g4:1 c5:1/2' ...
A pitch is a lowercase letter, an optional accidental (#, ##, b, bb) and an octave digit, with c4 = MIDI 60.
`:N` is a duration in beats (fractions allowed) and is inherited by the following tokens until changed. `+` joins
a chord, `r` is a rest, a `~` suffix ties into the next note of the same pitch, a `'` suffix plays it staccato
(sounding for half its length) and `!pp` .. `!ff` set the velocity of the notes that follow. Every `|` checks that
the bar adds up to the meter; only the first bar may be a shorter pickup.

Chords: `D | D/F# | A7:2 D:1 | N |` — several symbols in one bar share it evenly unless given a duration, and `N`
leaves the bar unaccompanied. Accompaniment patterns turn chord symbols into bass and chord notes.
"""
import re
from dataclasses import dataclass, replace
from fractions import Fraction

LETTERS = {"c": 0, "d": 2, "e": 4, "f": 5, "g": 7, "a": 9, "b": 11}
ACCIDENTALS = {"": 0, "#": 1, "##": 2, "b": -1, "bb": -2}
DYNAMICS = {"pp": 0.35, "p": 0.5, "mp": 0.65, "mf": 0.8, "f": 0.95, "ff": 1.1}
DEFAULT_VELOCITY = DYNAMICS["mf"]
PITCH = re.compile(r"([a-g])(##|#|bb|b)?(\d)")
TOKEN = re.compile(r"([a-g#0-9+r]+)(?::([0-9/]+))?([~']*)")
QUALITIES = {
    "": (0, 4, 7), "m": (0, 3, 7), "7": (0, 4, 7, 10), "maj7": (0, 4, 7, 11), "m7": (0, 3, 7, 10),
    "dim": (0, 3, 6), "dim7": (0, 3, 6, 9), "m7b5": (0, 3, 6, 10), "aug": (0, 4, 8), "sus2": (0, 2, 7),
    "sus4": (0, 5, 7), "7sus4": (0, 5, 7, 10), "6": (0, 4, 7, 9), "m6": (0, 3, 7, 9), "9": (0, 4, 7, 10, 14),
    "add9": (0, 4, 7, 14),
}
CHORD = re.compile(r"([A-G])(#|b)?(" + "|".join(sorted(map(re.escape, QUALITIES), key=len, reverse=True)) + r")(?:/([A-G])(#|b)?)?")


@dataclass(frozen=True)
class Note:
    start: Fraction  # beats from the start of the line
    beats: Fraction  # how long it sounds (staccato halves it)
    pitch: int  # MIDI note number
    velocity: float = DEFAULT_VELOCITY
    role: str = "melody"  # melody, bass or chord


@dataclass(frozen=True)
class Line:
    notes: tuple
    beats: Fraction
    bars: int


@dataclass(frozen=True)
class Chord:
    root: int  # pitch class 0-11
    intervals: tuple  # semitones above the root
    bass: int  # pitch class of the lowest note (the slash bass, else the root)

    @property
    def classes(self):
        return tuple(sorted({(self.root + i) % 12 for i in self.intervals}))


@dataclass(frozen=True)
class ChordSpan:
    start: Fraction
    beats: Fraction
    chord: object  # Chord, or None for an unaccompanied span


def pitch(text):
    """MIDI number of a pitch like `c4`, `f#5` or `bb3`."""
    match = PITCH.fullmatch(text)
    if not match:
        raise ValueError(f"not a pitch: {text!r}")
    letter, accidental, octave = match.groups()
    return 12 * (int(octave) + 1) + LETTERS[letter] + ACCIDENTALS[accidental or ""]


def _tokens(text):
    for line in text.splitlines():
        yield from line.split("%", 1)[0].split()


class _BarClock:
    """Tracks the position and checks every bar against the meter (the first may be a pickup)."""

    def __init__(self, meter, pickup, what):
        self.meter, self.pickup, self.what = Fraction(meter), Fraction(pickup), what
        self.position = self.bar_start = Fraction(0)
        self.bars = 0

    def expected(self):
        return self.pickup if self.bars == 0 and self.pickup else self.meter

    def close_bar(self):
        length = self.position - self.bar_start
        if length != self.expected():
            raise ValueError(f"{self.what} bar {self.bars + 1} lasts {length} beats, expected {self.expected()}")
        self.bars += 1
        self.bar_start = self.position

    def finish(self):
        if self.position != self.bar_start:
            self.close_bar()


def parse_notes(text, meter, pickup=0, what="line"):
    """Parse note notation into a Line; raises ValueError naming the bar when a bar does not fit the meter."""
    clock = _BarClock(meter, pickup, what)
    notes, ties = [], {}
    duration, velocity = Fraction(1), DEFAULT_VELOCITY
    for token in _tokens(text):
        if token == "|":
            clock.close_bar()
            continue
        if token.startswith("!"):
            if token[1:] not in DYNAMICS:
                raise ValueError(f"{what}: unknown dynamic {token!r}")
            velocity = DYNAMICS[token[1:]]
            continue
        match = TOKEN.fullmatch(token)
        if not match:
            raise ValueError(f"{what} bar {clock.bars + 1}: cannot read {token!r}")
        body, length, flags = match.groups()
        if length:
            duration = Fraction(length)
            if duration <= 0:
                raise ValueError(f"{what} bar {clock.bars + 1}: duration must be positive in {token!r}")
        if body == "r":
            ties = {}
        else:
            held = {}
            sounding = duration / 2 if "'" in flags else duration
            for name in body.split("+"):
                note = pitch(name)
                if note in ties:
                    index = ties[note]
                    notes[index] = replace(notes[index], beats=notes[index].beats + sounding)
                else:
                    index = len(notes)
                    notes.append(Note(clock.position, sounding, note, velocity))
                if "~" in flags:
                    held[note] = index
            ties = held
        clock.position += duration
    clock.finish()
    return Line(tuple(notes), clock.position, clock.bars)


def parse_chord(symbol):
    """A Chord for a symbol like `D`, `F#m7b5` or `A7/C#`; None for `N` (no chord)."""
    if symbol == "N":
        return None
    match = CHORD.fullmatch(symbol)
    if not match:
        raise ValueError(f"not a chord symbol: {symbol!r}")
    letter, accidental, quality, bass_letter, bass_accidental = match.groups()
    root = (LETTERS[letter.lower()] + ACCIDENTALS[accidental or ""]) % 12
    bass = root if bass_letter is None else (LETTERS[bass_letter.lower()] + ACCIDENTALS[bass_accidental or ""]) % 12
    return Chord(root, QUALITIES[quality], bass)


def parse_chords(text, meter, pickup=0, what="chords"):
    """Parse chord symbols into (ChordSpans, total beats); symbols in one bar share it evenly unless given `:beats`."""
    clock = _BarClock(meter, pickup, what)
    spans, bar = [], []

    def flush():
        explicit = sum((beats for _, beats in bar if beats is not None), Fraction(0))
        free = [symbol for symbol, beats in bar if beats is None]
        share = (clock.expected() - explicit) / len(free) if free else Fraction(0)
        for symbol, beats in bar:
            beats = share if beats is None else beats
            if beats <= 0:
                raise ValueError(f"{what} bar {clock.bars + 1}: no room for {symbol!r}")
            spans.append(ChordSpan(clock.position, beats, parse_chord(symbol)))
            clock.position += beats
        bar.clear()
        clock.close_bar()

    for token in _tokens(text):
        if token == "|":
            flush()
        else:
            symbol, _, beats = token.partition(":")
            bar.append((symbol, Fraction(beats) if beats else None))
    if bar:
        flush()
    return tuple(spans), clock.position


def place(pitch_class, low):
    """The pitch of this class in [low, low + 12)."""
    return low + (pitch_class - low) % 12


def voicing(chord, center):
    """Close voicing of the chord's tones within a tritone of `center`."""
    return tuple(sorted(place(pc, center - 6) for pc in chord.classes))


def _grid(span, unit):
    """(offset, length) steps of `unit` beats covering the span; the last step may be shorter."""
    offset = Fraction(0)
    while offset < span.beats:
        yield offset, min(unit, span.beats - offset)
        offset += unit


def _bar_beat(time, meter, pickup):
    return (time - pickup) % meter


def accompany(spans, pattern, meter, pickup=0, unit=Fraction(1, 2), center=60, bass_low=36, velocity=0.6):
    """Expand chord spans into notes with the named pattern. Bass notes get role "bass", the rest "chord".

    block    the chord held for the whole span        bass     the bass note held for the whole span
    waltz    bass on the downbeat, chord on the other beats (one note per beat)
    oompah   bass on each beat (root, then fifth), chord on the off-beats, `unit` apart
    stride   oompah with the bass doubled an octave below
    alberti  low-high-middle-high broken chord in `unit` steps
    arpeggio rising and falling chord ladder in `unit` steps
    octaves  the bass note alternating low and high octaves in `unit` steps
    pulse    the chord repeated every `unit`
    """
    unit = Fraction(unit)
    notes = []

    def add(start, beats, pitches, role, level=1.0):
        notes.extend(Note(start, beats, p, velocity * level, role) for p in pitches)

    for span in spans:
        chord = span.chord
        if chord is None:
            continue
        low = place(chord.bass, bass_low)
        upper = voicing(chord, center)
        fifth = place((chord.root + 7) % 12, bass_low)
        if pattern == "block":
            add(span.start, span.beats, upper, "chord")
        elif pattern == "bass":
            add(span.start, span.beats, (low,), "bass")
        elif pattern == "waltz":
            for offset, length in _grid(span, Fraction(1)):
                if _bar_beat(span.start + offset, meter, pickup) == 0:
                    add(span.start + offset, length, (low,), "bass")
                else:
                    add(span.start + offset, length * Fraction(2, 3), upper, "chord", 0.8)
        elif pattern in ("oompah", "stride"):
            for step, (offset, length) in enumerate(_grid(span, unit)):
                if step % 2 == 0:
                    bass = low if step % 4 == 0 else fifth
                    add(span.start + offset, length, (bass, bass - 12) if pattern == "stride" else (bass,), "bass")
                else:
                    add(span.start + offset, length * Fraction(3, 4), upper, "chord", 0.75)
        elif pattern == "alberti":
            base = place(chord.bass, center - 6)
            root = place(chord.root, base)
            intervals = sorted(chord.intervals)
            third, fifth = root + intervals[1], root + (7 if 7 in intervals else intervals[2])
            order = (base, fifth, third, fifth)
            for step, (offset, length) in enumerate(_grid(span, unit)):
                add(span.start + offset, length, (order[step % 4],), "chord", 1.0 if step % 4 == 0 else 0.8)
        elif pattern == "arpeggio":
            third = next(i for i in sorted(chord.intervals) if i > 0)
            ladder = (low, low + 7, low + 12, low + 12 + third, low + 19)
            shape = (0, 1, 2, 3, 4, 3, 2, 1)
            for step, (offset, length) in enumerate(_grid(span, unit)):
                add(span.start + offset, length, (ladder[shape[step % 8]],), "bass" if step % 8 == 0 else "chord")
        elif pattern == "octaves":
            for step, (offset, length) in enumerate(_grid(span, unit)):
                add(span.start + offset, length * Fraction(3, 4), (low if step % 2 == 0 else low + 12,), "bass")
        elif pattern == "pulse":
            for step, (offset, length) in enumerate(_grid(span, unit)):
                add(span.start + offset, length * Fraction(3, 4), upper, "chord", 1.0 if step % 2 == 0 else 0.85)
        else:
            raise ValueError(f"unknown accompaniment pattern {pattern!r}")
    return tuple(notes)
