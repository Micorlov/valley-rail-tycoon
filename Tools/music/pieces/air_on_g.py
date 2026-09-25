"""Air (\"Air on the G String\"), Orchestral Suite No. 3, BWV 1068: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Air (\"Air on the G String\"), Orchestral Suite No. 3, BWV 1068"
COMPOSER = "Johann Sebastian Bach"
YEAR = 1730
KEY = "D major"
METER = 4
BEAT = "quarter"
BPM = 60
PICKUP = "0"
SECTIONS = {
    "A1": {
        "melody": """
            % bars 1-6, first time (first violin)
            f#5:4~ |
            f#5:1/2 b5:1/4 g5 f#5:1/8 e5 d5:1/4 c#5 d5 c#5:1 b4:1/2 a4 |
            a5:2~ a5:1/4 f#5 c5 b4 e5 d#5 a5 g5 |
            g5:2~ g5:1/4 e5 b4 a4 d5 c#5 g5 f#5 |
            f#5:3/2 g#5:1/4 a5 d5:1/2 d5:1/8 e5 f#5:1/4~ f#5 e5 e5 d5 |
            c#5:1/4 b4 b4:1/8 c#5 d5:1/4~ d5:1/2 c#5:1/4 b4 a4:2 |
        """,
        "chords": """
            D D/C# Bm Bm/A |
            Gmaj7 E7/G# A A7/G |
            F#m7b5 Am/E B7/D# B7 |
            Em Em7/D A7/C# A7 |
            D D/C# Bm7 E7 |
            A:1 E7:1 A:2 |
        """,
        "left": """
            % continuo: the walking octave bass in eighths
            d3:1/2 d4 c#4 c#3 b2 b3 a3 a2 |
            g2:1/2 g3 g#3 g#2 a2 a3 g3 g2 |
            f#2:1/2 f#3 e3 e2 d#2 d#3 b2 b3 |
            e2:1/2 e3 d3 d2 c#2 c#3 a2 a3 |
            d3:1/2 d4 c#4 c#3 b2 b3 g#3 e3 |
            a3:1/2 d3 e3 e2 a2:1/4 b2 c#3 d3 e3 g3 f#3 e3 |
        """,
    },
    "A2": {
        "melody": """
            % bars 1-6 repeated, second ending
            f#5:4~ |
            f#5:1/2 b5:1/4 g5 f#5:1/8 e5 d5:1/4 c#5 d5 c#5:1 b4:1/2 a4 |
            a5:2~ a5:1/4 f#5 c5 b4 e5 d#5 a5 g5 |
            g5:2~ g5:1/4 e5 b4 a4 d5 c#5 g5 f#5 |
            f#5:3/2 g#5:1/4 a5 d5:1/2 d5:1/8 e5 f#5:1/4~ f#5 e5 e5 d5 |
            c#5:1/4 b4 b4:1/8 c#5 d5:1/4~ d5:1/2 c#5:1/4 b4 a4:2 |
        """,
        "chords": """
            D D/C# Bm Bm/A |
            Gmaj7 E7/G# A A7/G |
            F#m7b5 Am/E B7/D# B7 |
            Em Em7/D A7/C# A7 |
            D D/C# Bm7 E7 |
            A:1 E7:1 A:2 |
        """,
        "left": """
            d3:1/2 d4 c#4 c#3 b2 b3 a3 a2 |
            g2:1/2 g3 g#3 g#2 a2 a3 g3 g2 |
            f#2:1/2 f#3 e3 e2 d#2 d#3 b2 b3 |
            e2:1/2 e3 d3 d2 c#2 c#3 a2 a3 |
            d3:1/2 d4 c#4 c#3 b2 b3 g#3 e3 |
            a3:1/2 d3 e3 e2 a2:2 |
        """,
    },
    "B": {
        "melody": """
            % bars 7-10
            c#5:1~ c#5:1/4 d5:1/8 c#5 b4 c#5 a4:1/4 a5:3/2 c5:1/2 |
            b4:1/2 b5:1/2~ b5:1/4 a5 g5 f#5 g5:1~ g5:1/8 f#5 e5 d5 c#5:1/4 b4 |
            a#4:1/4 b4 c#5:1/2~ c#5:1/4 d5 e5:1/2~ e5:1/4 f#5 g5:1/2~ g5:1/2 f#5 |
            e5:1/4 d5 c#5 b4 c#5 d5:1/8 e5 d5:1/2 b4:2 |
            % bars 11-14
            d5:1~ d5:1/4 f#5 e5 d5 b5:1~ b5:1/2 a5:1/4 g#5 |
            f#5:1/8 e5 a5:1/4 a4:1/2 b4:3/4 c#5:1/8 d5 c#5:3/4 b4:1/4 a4:1 |
            d5:3/2 f#5:1/4 e5 e5:3/2 g5:1/4 f#5 |
            f#5:3/2 a5:1/4 g5 g5:2 |
            % bars 15-18
            a4:1~ a4:1/4 c#5 e5 g5 g5 e5 f#5:1/2~ f#5:1/2~ f#5:1/4 g5:1/8 a5 |
            d5:1~ d5:1/4 f#5 a5 c6 b5:3/2 d5:1/2 |
            c#5:1/4 e5 g5:1 b4:1/2 a4 e5:1/4 f#5:1/8 g5~ g5:1/4 f#5:1/2 e5:1/4 |
            d5:1/8 c#5 b4:1/2 c#5:1/4 d5:1/2 c#5:1/4 d5 d5:2 |
        """,
        "chords": """
            A A7/G F#m7b5 F#m7b5/E |
            B7/D# B7 Em Em7/D |
            F#7/C# Em/B F#7/A# F#7 |
            Bm F#7 Bm Bm/A |
            E7/G# D/F# E E7/D |
            A/C# E7 A A7/G |
            D7/F# G E7/G# A7 |
            F#7/A# B7 Em Em7/D |
            A7/C# A7 D D7/C |
            G/B D7/A G Gmaj7/F# |
            Em Em7/D A7/C# D |
            D/A:1 A7:1 D:2 |
        """,
        "left": """
            a2:1/2 a3 g3 g2 f#2 f#3 e3 e2 |
            d#2:1/2 d#3 f#3 b2 e3 e4 d4 d3 |
            c#3:1/2 c#4 b3 b2 a#2 b2 c#3 a#2 |
            b2:1/2 g3 e3 f#3 b2 b3 a3 a2 |
            g#2:1/2 g#3 f#3 f#2 e2 e3 d3 d2 |
            c#2:1/2 c#3 d3 e3 a2 a3 g3 g2 |
            f#2:1/2 f#3 g3 g2 g#2 g#3 a3 a2 |
            a#2:1/2 a#3 b3 b2 e3 e4 d4 d3 |
            c#3:1/2 c#4 a3 c#4 d4 d3 c3 c4 |
            b3:1/2 b2 a2 a3 g3 g2 f#2 f#3 |
            e3:1/2 e2 d2 d3 c#3 a2 d3 g3 |
            a3:1/2 g3 a3 a2 d2:2 |
        """,
    },
}
FORM = ["A1", "A2", "B"]
SOURCES = [
    "https://www.mutopiaproject.org/ftp/BachJS/BWV1068/bach-air/bach-air-lys.zip (Mutopia, after the Bach-Gesellschaft edition; violin 1, violin 2, viola and continuo parts read via https://github.com/MutopiaProject/MutopiaProject/tree/master/ftp/BachJS/BWV1068/bach-air/bach-air-lys)",
    "https://www.mutopiaproject.org/ftp/BachJS/BWV1068/bach_air_bmv_1068/bach_air_bmv_1068.ly (Mutopia flute+guitar setting in D, melody cross-check)",
]
NOTES = """Melody (first violin) and left (continuo) were converted note-by-note for all 18 bars from the Mutopia
string parts, which follow the Bach-Gesellschaft edition; the melody was cross-checked bar-by-bar against a
second, independent Mutopia setting (flute + guitar in D) and the two agree on every pitch and rhythm.
Chord symbols were derived by me beat-by-beat from violin 2 + viola + continuo of the same source (one chord per
beat, i.e. per bass octave pair); a few passing-note beats are simplified to the governing harmony (e.g. bar 6
beat 1 A/D bass pair written as A, bar 10 beat 2 E/F# pair written as F#7, bar 17 beat 4 D/G pair written as D).
Ornaments: the small-note appoggiaturas are written out (bar 2 f#-e as two 32nds, bar 2 b-a as two eighths,
bar 12 f#-e as two 32nds); the cadential trill on c#5 in bar 18 is left as a plain 16th. Form: bars 1-6 with the
first ending (bass run up to D), bars 1-6 with the second ending, then bars 7-18 once (Bach repeats the second
half too; omitted to stay near 96 s). No bars reconstructed."""

# Violin line over held inner strings, a plucked continuo bass and a harpsichord marking each beat.
LAYERS = (
    dict(part="melody", voice="strings", gain=0.36, pan=0.44, send=0.4),
    dict(part="chords", voice="strings", pattern="block", gain=0.07, pan=0.58, send=0.4, center=64),
    dict(part="chords", voice="pad", pattern="block", gain=0.03, center=60),
    dict(part="left", voice="pizz", gain=0.26, pan=0.55, send=0.25),
    dict(part="chords", voice="harpsichord", pattern="pulse", unit=1, gain=0.07, pan=0.3, send=0.3, center=62),
)
