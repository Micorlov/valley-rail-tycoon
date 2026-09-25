"""Turn a piece module into timed notes and render it to stereo audio.

A piece module (Tools/music/pieces/<key>.py) holds pure data:
    TITLE, COMPOSER, YEAR, METER (beats per bar), BPM, PICKUP (beats, as a string fraction),
    SECTIONS  {name: {"melody": notes, "chords": symbols, optional "left" and "counter": notes}},
    FORM      section names in playing order,
    LAYERS    dicts that give each part an instrument (see Layer), optionally limited to some FORM entries,
    optional TEMPO ((beat, bpm), ...) tempo points after the start (linear between points) and RITARDANDO,
    the tempo factor reached at the end of the last two bars.
Every piece also gets the TR-808 beat of beat808.py unless it sets BEAT_808 = False.
"""
import importlib
import math
import random
from collections import OrderedDict
from dataclasses import dataclass, fields, replace
from fractions import Fraction

from . import beat808, notation, synth

LEAD_IN = 0.25  # seconds of silence before the first note
TAIL = 3.5  # seconds after the last beat for the notes and reverb to ring out
FADE = 1.2  # seconds of fade at the very end
RITARDANDO_BARS = 2
CACHE_SAMPLES = 8_000_000  # rendered notes kept for reuse (about 64 MB)
NOTE_PARTS = ("melody", "counter", "left")


@dataclass(frozen=True)
class Layer:
    part: str  # melody, counter, left, chords, grid (an unpitched pulse every `unit` beats) or groove (the 808)
    voice: str
    gain: float = 0.3
    pan: float = 0.5
    send: float = 0.3
    octave: int = 0  # octaves to transpose the part by
    form: tuple = None  # indices into FORM this layer plays in; None plays everywhere
    pattern: str = "block"  # accompaniment pattern for chords
    role: str = None  # keep only the "bass" or "chord" notes of the pattern
    unit: Fraction = Fraction(1, 2)
    center: int = 60
    bass_low: int = 36
    velocity: float = 0.6  # velocity of notes made from chords or grid
    pedal: bool = False  # hold notes until the harmony changes, except in unaccompanied (N) spans

    @staticmethod
    def of(spec):
        known = {f.name for f in fields(Layer)}
        unknown = set(spec) - known
        if unknown:
            raise ValueError(f"unknown layer settings {sorted(unknown)}")
        values = dict(spec)
        if "unit" in values:
            values["unit"] = Fraction(values["unit"])
        if values.get("form") is not None:
            values["form"] = tuple(values["form"])
        return Layer(**values)


@dataclass(frozen=True)
class Section:
    beats: Fraction
    pickup: Fraction
    lines: dict  # part -> notation.Line
    chords: tuple  # notation.ChordSpan


@dataclass(frozen=True)
class Piece:
    key: str
    title: str
    composer: str
    year: int
    meter: Fraction
    bpm: float
    sections: dict
    form: tuple
    layers: tuple
    tempo: tuple
    ritardando: float

    @property
    def file(self):
        return "".join(word.capitalize() for word in self.key.split("_"))


@dataclass(frozen=True)
class Event:
    start: Fraction
    beats: Fraction
    pitch: int
    velocity: float
    layer: int


def parse_section(name, spec, meter, pickup):
    lines = {part: notation.parse_notes(spec[part], meter, pickup, f"{name}.{part}") for part in NOTE_PARTS if part in spec}
    chords, beats = notation.parse_chords(spec["chords"], meter, pickup, f"{name}.chords")
    for part, line in lines.items():
        if line.beats != beats:
            raise ValueError(f"{name}.{part} lasts {line.beats} beats but {name}.chords lasts {beats}")
    return Section(beats, Fraction(pickup), lines, chords)


def load(key):
    """Import Tools/music/pieces/<key>.py and check it: every bar, every section, every layer."""
    module = importlib.import_module(f"{__package__}.pieces.{key}")
    meter, pickup = Fraction(module.METER), Fraction(getattr(module, "PICKUP", "0"))
    form = tuple(module.FORM)
    missing = [name for name in form if name not in module.SECTIONS]
    if missing:
        raise ValueError(f"{key}: FORM names unknown sections {missing}")
    sections = {
        name: parse_section(f"{key}.{name}", spec, meter, pickup if name == form[0] else 0)
        for name, spec in module.SECTIONS.items()
    }
    specs = tuple(module.LAYERS) + (beat808.LAYERS if getattr(module, "BEAT_808", True) else ())
    layers = tuple(Layer.of(spec) for spec in specs)
    for layer in layers:
        if layer.voice not in synth.VOICES:
            raise ValueError(f"{key}: unknown voice {layer.voice!r}")
    tempo = tuple((Fraction(beat), float(bpm)) for beat, bpm in getattr(module, "TEMPO", ()))
    return Piece(key, module.TITLE, module.COMPOSER, int(module.YEAR), meter, float(module.BPM), sections, form, layers,
                 tempo, float(getattr(module, "RITARDANDO", 0.82)))


def _grid(section, unit, meter, velocity):
    notes, time = [], Fraction(0)
    while time < section.beats:
        on_beat = (time - section.pickup) % 1 == 0
        notes.append(notation.Note(time, unit / 2, 60, velocity * (1.0 if on_beat else 0.6)))
        time += unit
    return notes


def _pedal(notes, spans):
    """Sustain-pedal each note to the end of the chord span it starts in."""
    held = []
    for note in notes:
        span = next((s for s in spans if s.start <= note.start < s.start + s.beats), None)
        if span is None or span.chord is None:
            held.append(note)
        else:
            held.append(replace(note, beats=max(note.beats, span.start + span.beats - note.start)))
    return tuple(held)


def layer_notes(piece, section, layer, final=False):
    if layer.part in NOTE_PARTS:
        line = section.lines.get(layer.part)
        notes = line.notes if line else ()
        return _pedal(notes, section.chords) if layer.pedal else notes
    if layer.part == "chords":
        notes = notation.accompany(section.chords, layer.pattern, piece.meter, section.pickup, layer.unit,
                                   layer.center, layer.bass_low, layer.velocity)
        return tuple(n for n in notes if layer.role is None or n.role == layer.role)
    if layer.part == "grid":
        return _grid(section, layer.unit, piece.meter, layer.velocity)
    if layer.part == "groove":
        return beat808.notes(piece.meter, piece.bpm, section, layer.pattern, final)
    raise ValueError(f"{piece.key}: unknown layer part {layer.part!r}")


def events(piece):
    """Every note of the piece in playing order, and the total length in beats."""
    out, offset = [], Fraction(0)
    for index, name in enumerate(piece.form):
        section = piece.sections[name]
        for number, layer in enumerate(piece.layers):
            if layer.form is not None and index not in layer.form:
                continue
            for note in layer_notes(piece, section, layer, index == len(piece.form) - 1):
                out.append(Event(offset + note.start, note.beats, note.pitch + 12 * layer.octave, note.velocity, number))
        offset += section.beats
    out.sort(key=lambda e: (e.start, e.layer, e.pitch))
    return out, offset


class TempoMap:
    """Beats to seconds through tempo points; the tempo moves linearly between points and holds after the last."""

    def __init__(self, points):
        self.points = sorted(points, key=lambda p: p[0])

    @staticmethod
    def _span(beats, start_bpm, end_bpm):
        if abs(end_bpm - start_bpm) < 1e-9:
            return 60 * beats / start_bpm
        return 60 * beats * math.log(end_bpm / start_bpm) / (end_bpm - start_bpm)

    def seconds(self, beat):
        beat, total = Fraction(beat), 0.0
        for (b0, v0), (b1, v1) in zip(self.points, self.points[1:]):
            if beat <= b0:
                return total
            if b1 == b0:
                continue
            end = min(beat, b1)
            v_end = v0 + (v1 - v0) * float((end - b0) / (b1 - b0))
            total += self._span(float(end - b0), v0, v_end)
        last_beat, last_bpm = self.points[-1]
        return total + (60 * float(beat - last_beat) / last_bpm if beat > last_beat else 0.0)


def tempo_map(piece, total):
    points = [(Fraction(0), piece.bpm), *piece.tempo]
    slowdown_from = total - RITARDANDO_BARS * piece.meter
    if piece.ritardando != 1 and points[-1][0] < slowdown_from:
        points += [(slowdown_from, points[-1][1]), (total, points[-1][1] * piece.ritardando)]
    return TempoMap(points)


def duration(piece):
    """Playing time in seconds including the lead-in and the ring-out."""
    _, total = events(piece)
    return LEAD_IN + tempo_map(piece, total).seconds(total) + TAIL


class NoteCache:
    """Rendered notes by (voice, pitch, length, velocity), dropping the oldest past CACHE_SAMPLES."""

    def __init__(self):
        self.notes, self.size = OrderedDict(), 0

    def get(self, key, make):
        samples = self.notes.get(key)
        if samples is None:
            samples = make()
            self.notes[key] = samples
            self.size += len(samples)
            while self.size > CACHE_SAMPLES and len(self.notes) > 1:
                self.size -= len(self.notes.popitem(last=False)[1])
        else:
            self.notes.move_to_end(key)
        return samples


def render(piece):
    """Stereo (left, right) arrays of the whole piece, mastered."""
    rng = random.Random(piece.key)
    notes, total = events(piece)
    tempo = tempo_map(piece, total)
    mix = synth.Mix(synth.seconds(LEAD_IN + tempo.seconds(total) + TAIL), ((l.pan, l.send) for l in piece.layers))
    cache = NoteCache()
    for event in notes:
        layer = piece.layers[event.layer]
        start = tempo.seconds(event.start)
        length = max(0.05, round((tempo.seconds(event.start + event.beats) - start) * 20) / 20)
        velocity = round(event.velocity * 16) / 16
        voice = synth.VOICES[layer.voice]
        samples = cache.get((layer.voice, event.pitch, length, velocity),
                            lambda: voice(event.pitch, length, velocity, rng))
        jitter = rng.uniform(-1, 1) * synth.JITTER.get(layer.voice, synth.DEFAULT_JITTER)
        mix.add(event.layer, synth.seconds(LEAD_IN + start + jitter), samples, layer.gain * rng.uniform(0.95, 1.03))
    groove = [number for number, layer in enumerate(piece.layers) if layer.part == "groove"]
    mix.balance(groove, beat808.LEVEL_DB)
    dry_l, dry_r, send = mix.stereo()
    wet_l, wet_r = synth.reverb(send)
    return synth.master(dry_l, dry_r, wet_l, wet_r, synth.seconds(FADE))
