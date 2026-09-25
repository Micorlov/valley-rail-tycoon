"""The Entertainer: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "The Entertainer"
COMPOSER = "Scott Joplin"
YEAR = 1902
KEY = "C major"
METER = 2
BEAT = "quarter"
BPM = 82
PICKUP = "0"
SECTIONS = {
    "Intro": {
        "melody": """
            % bars 1-4: the descending octave intro ("Not fast"); bar 1 both hands play the octave
            d6+d5:1/4 e6+e5 c6+c5 a5+a4:1/2 b5+b4:1/4 g5+g4:1/2 |
            d5:1/4 e5 c5 a4:1/2 b4:1/4 g4:1/2 |
            d4:1/4 e4 c4 a3:1/2 b3:1/4 a3 ab3 |
            g3:1/2 r:1/2 g5+d5+b4+g4:1/2 d4:1/4 d#4 |
        """,
        "chords": """
            G7 | G7 | G7 | G |
        """,
        "left": """
            r:2 |
            d4:1/4 e4 c4 a3:1/2 b3:1/4 g3:1/2 |
            d3:1/4 e3 c3 a2:1/2 b2:1/4 a2 ab2 |
            g2:1/2 r g2 g3+b3 |
        """,
    },
    "A1": {
        "melody": """
            % bars 5-8
            e4:1/4 c5:1/2 e4:1/4 c5:1/2 e4:1/4 c5:1/4~ |
            c5:1~ c5:1/4 c6 d6 d#6 |
            e6:1/4 c6 d6 e6~ e6 b5 d6:1/2 |
            c6:3/2 d4:1/4 d#4 |
            % bars 9-12
            e4:1/4 c5:1/2 e4:1/4 c5:1/2 e4:1/4 c5:1/4~ |
            c5:3/2 a5:1/4 g5 |
            f#5:1/4 a5 c6 e6~ e6 d6 c6 a5 |
            d6:3/2 d4:1/4 d#4 |
            % bars 13-16
            e4:1/4 c5:1/2 e4:1/4 c5:1/2 e4:1/4 c5:1/4~ |
            c5:1~ c5:1/4 c6 d6 d#6 |
            e6:1/4 c6 d6 e6~ e6 b5 d6:1/2 |
            c6:3/2 c6:1/4 d6 |
            % bars 17-20 (first ending: pickup back into A)
            e6:1/4 c6 d6 e6~ e6 c6 d6 c6 |
            e6:1/4 c6 d6 e6~ e6 c6 d6 c6 |
            e6:1/4 c6 d6 e6~ e6 b5 d6:1/2 |
            c6:3/2 d4:1/4 d#4 |
        """,
        "chords": """
            C C7 | F C/E | C/G G7 | C |
            C C7 | F C/E | D7 | G |
            C C7 | F C/E | C/G G7 | C |
            C C7/Bb | F/A Fm/Ab | C/G G7 | C G |
        """,
        "left": """
            c3:1/2 e3+g3+c4 g2+g3 g3+bb3+c4 |
            f2+f3:1/2 a3+c4 e2+e3 g3+c4 |
            g2:1/2 e3+g3+c4 g2 f3+g3+b3 |
            c3:1/2 e3+g3+c4 e3+g3+c4 g3+b3 |
            c3:1/2 e3+g3+c4 g2+g3 g3+bb3+c4 |
            f2+f3:1/2 a3+c4 e2+e3 eb2+eb3 |
            d2+d3:1/2 d3+f#3+a3+c4 d3 f#3+a3+c4 |
            g3+b3:1/2 g2+g3 a2+a3 b2+b3 |
            c3:1/2 e3+g3+c4 g2+g3 g3+bb3+c4 |
            f2+f3:1/2 a3+c4 e2+e3 g3+c4 |
            g2:1/2 e3+g3+c4 g2 f3+g3+b3 |
            c3:1/2 e3+g3+c4 g3+c4+e4 r |
            c3+c4:1/2 g3+c4+e4 bb2+bb3 g3+c4+e4 |
            a2+a3:1/2 a3+c4+f4 ab2+ab3 ab3+c4+f4 |
            g2+g3:1/2 g3+c4+e4 g2 g3+b3 |
            c3+g3+c4:1/2 g2+g3 a2+a3 b2+b3 |
        """,
    },
    "A2": {
        "melody": """
            % bars 5-19 again, second ending leads into the B strain
            e4:1/4 c5:1/2 e4:1/4 c5:1/2 e4:1/4 c5:1/4~ |
            c5:1~ c5:1/4 c6 d6 d#6 |
            e6:1/4 c6 d6 e6~ e6 b5 d6:1/2 |
            c6:3/2 d4:1/4 d#4 |
            e4:1/4 c5:1/2 e4:1/4 c5:1/2 e4:1/4 c5:1/4~ |
            c5:3/2 a5:1/4 g5 |
            f#5:1/4 a5 c6 e6~ e6 d6 c6 a5 |
            d6:3/2 d4:1/4 d#4 |
            e4:1/4 c5:1/2 e4:1/4 c5:1/2 e4:1/4 c5:1/4~ |
            c5:1~ c5:1/4 c6 d6 d#6 |
            e6:1/4 c6 d6 e6~ e6 b5 d6:1/2 |
            c6:3/2 c6:1/4 d6 |
            e6:1/4 c6 d6 e6~ e6 c6 d6 c6 |
            e6:1/4 c6 d6 e6~ e6 c6 d6 c6 |
            e6:1/4 c6 d6 e6~ e6 b5 d6:1/2 |
            c6:1~ c6:1/4 e5 f5 f#5 |
        """,
        "chords": """
            C C7 | F C/E | C/G G7 | C |
            C C7 | F C/E | D7 | G |
            C C7 | F C/E | C/G G7 | C |
            C C7/Bb | F/A Fm/Ab | C/G G7 | C |
        """,
        "left": """
            c3:1/2 e3+g3+c4 g2+g3 g3+bb3+c4 |
            f2+f3:1/2 a3+c4 e2+e3 g3+c4 |
            g2:1/2 e3+g3+c4 g2 f3+g3+b3 |
            c3:1/2 e3+g3+c4 e3+g3+c4 g3+b3 |
            c3:1/2 e3+g3+c4 g2+g3 g3+bb3+c4 |
            f2+f3:1/2 a3+c4 e2+e3 eb2+eb3 |
            d2+d3:1/2 d3+f#3+a3+c4 d3 f#3+a3+c4 |
            g3+b3:1/2 g2+g3 a2+a3 b2+b3 |
            c3:1/2 e3+g3+c4 g2+g3 g3+bb3+c4 |
            f2+f3:1/2 a3+c4 e2+e3 g3+c4 |
            g2:1/2 e3+g3+c4 g2 f3+g3+b3 |
            c3:1/2 e3+g3+c4 g3+c4+e4 r |
            c3+c4:1/2 g3+c4+e4 bb2+bb3 g3+c4+e4 |
            a2+a3:1/2 a3+c4+f4 ab2+ab3 ab3+c4+f4 |
            g2+g3:1/2 g3+c4+e4 g2 g3+b3 |
            c3+g3+c4:1/2 g2+g3 c2+c3 r |
        """,
    },
    "B": {
        "melody": """
            % B strain, bars 22-25 (first time, at written pitch; the 8va repeat is omitted)
            g5:1/2 a5:1/4 g5~ g5 e5 f5 f#5 |
            g5:1/2 a5:1/4 g5~ g5 e5 c5 g4 |
            a4:1/4 b4 c5 d5 e5 d5 c5 d5 |
            g4:1/4 e5 f5 g5 a5 g5 e5 f5 |
            % bars 26-29
            g5:1/2 a5:1/4 g5~ g5 e5 f5 f#5 |
            g5:1/2 a5:1/4 g5~ g5 g5 a5 a#5 |
            b5:1/4 b5:1/2 b5:1/4~ b5 a5 f#5 d5 |
            g5:1~ g5:1/4 e5 f5 f#5 |
            % bars 30-33
            g5:1/2 a5:1/4 g5~ g5 e5 f5 f#5 |
            g5:1/2 a5:1/4 g5~ g5 e5 c5 g4 |
            a4:1/4 b4 c5 d5 e5 d5 c5 d5 |
            c5:1~ c5:1/4 g4 f#4 g4 |
            % bars 34-37 (second ending: pickup back into A)
            c5:1/2 a4:1/4 c5~ c5 a4 c5 a4 |
            g4:1/4 c5 e5 g5~ g5 e5 c5 g4 |
            a4:1/2 c5 e5:1/4 d5:1/2 c5:1/4~ |
            c5:3/2 d4:1/4 d#4 |
        """,
        "chords": """
            C | C | F Fm | C |
            C | C | G/D D7 | G G7 |
            C | C | F Fm | C C7 |
            F F#dim7 | C/G | D7 G7 | C |
        """,
        "left": """
            c2+c3:1/2 g3+c4+e4 g2 g3+c4+e4 |
            c3:1/2 g3+c4+e4 g2 g3+c4+e4 |
            f2:1/2 a3+c4+f4 f3 ab3+c4+f4 |
            e3:1/2 g3+c4+e4 g2 g3+c4+e4 |
            c3:1/2 g3+c4+e4 g2 g3+c4+e4 |
            c3:1/2 g3+c4+e4 e3 eb3 |
            d3:1/2 g3+b3+d4 d3 a3+c4+d4 |
            g3+b3+d4:1/2 f2+f3 e2+e3 d2+d3 |
            c2+c3:1/2 g3+c4+e4 g2 g3+c4+e4 |
            c3:1/2 g3+c4+e4 g2 g3+c4+e4 |
            f2:1/2 a3+c4+f4 f3 ab3+c4+f4 |
            e3:1/2 g3+c4+e4 c3 bb3+c4+e4 |
            f3+a3+c4+f4:1/2 f3+a3+c4+f4 f#3+a3+c4+d#4 f#3+a3+c4+d#4 |
            g3+c4+e4:1/2 g3+c4+e4 g3+c4+e4 g3+c4+e4 |
            d3+c4:1/2 d3+a3 g3+b3 g3+b3 |
            c3+c4:1/2 g2+g3 c2+c3 r |
        """,
    },
    "A_end": {
        "melody": """
            % A strain da capo, closing on the tonic
            e4:1/4 c5:1/2 e4:1/4 c5:1/2 e4:1/4 c5:1/4~ |
            c5:1~ c5:1/4 c6 d6 d#6 |
            e6:1/4 c6 d6 e6~ e6 b5 d6:1/2 |
            c6:3/2 d4:1/4 d#4 |
            e4:1/4 c5:1/2 e4:1/4 c5:1/2 e4:1/4 c5:1/4~ |
            c5:3/2 a5:1/4 g5 |
            f#5:1/4 a5 c6 e6~ e6 d6 c6 a5 |
            d6:3/2 d4:1/4 d#4 |
            e4:1/4 c5:1/2 e4:1/4 c5:1/2 e4:1/4 c5:1/4~ |
            c5:1~ c5:1/4 c6 d6 d#6 |
            e6:1/4 c6 d6 e6~ e6 b5 d6:1/2 |
            c6:3/2 c6:1/4 d6 |
            e6:1/4 c6 d6 e6~ e6 c6 d6 c6 |
            e6:1/4 c6 d6 e6~ e6 c6 d6 c6 |
            e6:1/4 c6 d6 e6~ e6 b5 d6:1/2 |
            c6+e5+c5:3/2 r:1/2 |
        """,
        "chords": """
            C C7 | F C/E | C/G G7 | C |
            C C7 | F C/E | D7 | G |
            C C7 | F C/E | C/G G7 | C |
            C C7/Bb | F/A Fm/Ab | C/G G7 | C |
        """,
        "left": """
            c3:1/2 e3+g3+c4 g2+g3 g3+bb3+c4 |
            f2+f3:1/2 a3+c4 e2+e3 g3+c4 |
            g2:1/2 e3+g3+c4 g2 f3+g3+b3 |
            c3:1/2 e3+g3+c4 e3+g3+c4 g3+b3 |
            c3:1/2 e3+g3+c4 g2+g3 g3+bb3+c4 |
            f2+f3:1/2 a3+c4 e2+e3 eb2+eb3 |
            d2+d3:1/2 d3+f#3+a3+c4 d3 f#3+a3+c4 |
            g3+b3:1/2 g2+g3 a2+a3 b2+b3 |
            c3:1/2 e3+g3+c4 g2+g3 g3+bb3+c4 |
            f2+f3:1/2 a3+c4 e2+e3 g3+c4 |
            g2:1/2 e3+g3+c4 g2 f3+g3+b3 |
            c3:1/2 e3+g3+c4 g3+c4+e4 r |
            c3+c4:1/2 g3+c4+e4 bb2+bb3 g3+c4+e4 |
            a2+a3:1/2 a3+c4+f4 ab2+ab3 ab3+c4+f4 |
            g2+g3:1/2 g3+c4+e4 g2 g3+b3 |
            c3+g3+c4:1/2 g2+g3 c2+c3 r |
        """,
    },
}
FORM = ["Intro", "A1", "A2", "B", "A_end"]
SOURCES = [
    "https://www.mutopiaproject.org/ftp/JoplinS/entertainer/entertainer.ly",
    "https://imslp.org/wiki/The_Entertainer_(Joplin,_Scott)",
    "https://en.wikipedia.org/wiki/The_Entertainer_(rag)",
]
NOTES = """Pitches and rhythms of the intro, A strain (both endings) and B strain were converted note-by-note from the
Mutopia LilyPond source (a public-domain re-engraving of the 1902 Stark edition, marked "Not fast"); the original
form Intro-AA-BB-A-CC-Intro2-DD was confirmed on Wikipedia. This arrangement plays Intro, A, A, B, A.
Melody = the top voice of the right hand only (the inner octave/third doublings are carried by "chords");
in intro bar 1 the two hands play the same line in octaves, so it is written as octaves in the melody.
Left = Joplin's written stride bass (bass octave on the beat, chord on the off-beat) - converted, not invented.
Simplifications: the low G1 octave in intro bar 4 is written as a single G2 (range); the B strain is played once at
written pitch (Joplin marks the repeat 8va) using its second ending; the final bar of A_end is a plain tonic ending
(the A strain's second-ending bass, C-G-C) instead of a pickup. Chord symbols are my harmonic reading of the
written notes (e.g. C C7/Bb | F/A Fm/Ab | C/G G7 in bars 17-19). No bars reconstructed without a source."""

LAYERS = (
    dict(part="melody", voice="piano", gain=0.42, pan=0.56, send=0.3),
    dict(part="left", voice="piano", gain=0.36, pan=0.44, send=0.3),
)
