"""Morning Mood (Peer Gynt Suite No. 1, Op. 46): public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Morning Mood (Peer Gynt Suite No. 1, Op. 46)"
COMPOSER = "Edvard Grieg"
YEAR = 1875
KEY = "E major"
METER = 6
BEAT = "eighth"
BPM = 144
PICKUP = "0"
SECTIONS = {
    "Flute1": {
        "melody": """
            % bars 1-4, flute solo (verified); the small-note turns are written out as two 32nds
            !p b5:1 g#5 f#5 e5 f#5 g#5 |
            b5:1 g#5:1/4 a5 g#5:1/2 f#5:1 e5 f#5:1/2 g#5 f#5 g#5 |
            g#5:1/4 a5 b5:1/2 g#5:1 b5 c#6 g#5 c#6 |
            b5:1 g#5 f#5 e5:3 |
        """,
        "chords": """
            E | E | E | E |
        """,
    },
    "Oboe1": {
        "melody": """
            % bars 5-8, oboe answers a major third higher (G-sharp major), in its middle register
            !p d#5:1 b#4 a#4 g#4 a#4 b#4 |
            d#5:1 b#4:1/4 c#5 b#4:1/2 a#4:1 g#4 a#4:1/2 b#4 a#4 b#4 |
            b#4:1/4 c#5 d#5:1/2 b#4:1 d#5 e#5 b#4 e#5 |
            d#5:1 b#4 a#4 g#4:3 |
        """,
        "chords": """
            G# | G# | G# | G# |
        """,
    },
    "Exchange": {
        "melody": """
            % bars 9-10 flute (E), bars 11-12 oboe (G-sharp): shortened two-bar exchanges
            b5:1 g#5 f#5 e5 f#5 g#5 |
            b5:1 g#5:1/4 a5 g#5:1/2 f#5:1 e5 f#5:1/2 g#5 f#5 g#5 |
            r:6 | r:6 |
        """,
        "counter": """
            r:6 | r:6 |
            d#5:1 b#4 a#4 g#4 a#4 b#4 |
            d#5:1 b#4:1/4 c#5 b#4:1/2 a#4:1 g#4 a#4:1/2 b#4 a#4 b#4 |
        """,
        "chords": """
            E | E | G# | G# |
        """,
    },
    "Tutti": {
        "melody": """
            % the forte statement: the theme in E for full orchestra, melody doubled in octaves
            !f b5+b4:1 g#5+g#4 f#5+f#4 e5+e4 f#5+f#4 g#5+g#4 |
            b5+b4:1 g#5+g#4 f#5+f#4 e5+e4 f#5+f#4:1/2 g#5+g#4 f#5+f#4 g#5+g#4 |
            b5+b4:1 g#5+g#4 b5+b4 c#6+c#5 g#5+g#4 c#6+c#5 |
            b5+b4:1 g#5+g#4 f#5+f#4 e5+e4:3 |
        """,
        "chords": """
            E | E | E | E |
        """,
    },
    "Flute_end": {
        "melody": """
            % the flute alone again, closing on the tonic
            !p b5:1 g#5 f#5 e5 f#5 g#5 |
            b5:1 g#5:1/4 a5 g#5:1/2 f#5:1 e5 f#5:1/2 g#5 f#5 g#5 |
            g#5:1/4 a5 b5:1/2 g#5:1 b5 c#6 g#5 c#6 |
            b5:1 g#5 f#5 e5:3~ |
            e5:6 |
        """,
        "chords": """
            E | E | E | B7:3 E:3 | E |
        """,
    },
}
FORM = ["Flute1", "Oboe1", "Exchange", "Tutti", "Oboe1", "Flute_end"]
SOURCES = [
    "https://no.wikipedia.org/w/index.php?title=Morgenstemning&action=raw (score excerpt of the opening flute melody, bars 1-4, E major)",
    "https://en.wikipedia.org/wiki/Morning_Mood (E major, 6/8, Allegretto pastorale, melody alternates flute/oboe, early forte climax)",
]
NOTES = """MEDIUM-LOW CONFIDENCE outside bars 1-4 - no readable full score (LilyPond/ABC/MusicXML) of the orchestral
movement could be found; IMSLP only has scans, which the spec says not to download.
Verified: bars 1-4 (flute theme incl. the g#-a turn figures) from the Norwegian Wikipedia score excerpt; key, meter,
tempo marking, the flute/oboe alternation and the early forte climax from Wikipedia.
Instruments: Flute1 = flute (bars 1-4); Oboe1 = oboe (bars 5-8); Exchange = flute (first 2 bars), then oboe (last 2);
Tutti = full orchestra; Flute_end = flute. No overlap between flute and oboe statements was verified, so no "counter".
Reconstructed from memory / unverified: the oboe answer as an exact transposition a major third up into G-sharp
major (the G-sharp major turn is also mentioned by a secondary web source; register, rhythm and exact pitches are
not checked against the score); the two-bar exchanges in "Exchange"; the tutti statement's pitches/doubling (the
real climax is re-orchestrated and its bars before the forte build up differently); the whole harmony (plain E / G#
pedals, B7-E final cadence); the closing bar. The long crescendo build-up and the later statements in other keys
are omitted."""

# Flute and oboe trade the pastoral theme over held strings; the full orchestra sings it once in octaves.
LAYERS = (
    dict(part="melody", voice="flute", gain=0.34, pan=0.44, send=0.45, form=(0, 2, 5)),
    dict(part="melody", voice="reed", gain=0.3, pan=0.6, send=0.45, form=(1, 4)),
    dict(part="counter", voice="reed", gain=0.3, pan=0.6, send=0.45),
    dict(part="melody", voice="strings", gain=0.3, pan=0.46, send=0.45, form=(3,)),
    dict(part="melody", voice="horn", gain=0.14, pan=0.4, send=0.45, octave=-1, form=(3,)),
    dict(part="chords", voice="strings", pattern="block", gain=0.08, pan=0.56, send=0.5, center=60),
    dict(part="chords", voice="pad", pattern="block", gain=0.035, center=56),
    dict(part="chords", voice="bass", pattern="bass", gain=0.15, bass_low=40),
)
