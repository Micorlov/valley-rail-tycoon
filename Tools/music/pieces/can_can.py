"""Can-can (Galop infernal): public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Can-can (Galop infernal)"
COMPOSER = "Jacques Offenbach"
YEAR = 1858
KEY = "G major"
METER = 2
BEAT = "quarter"
BPM = 150
PICKUP = "0"
SECTIONS = {
    # Arranged 4-bar galop vamp (not from the score).
    "INTRO": {
        "melody": """
            r:2 | r:2 | r:2 | r:2 |
        """,
        "chords": """
            G | G | D7 | D7 |
        """,
        "left": """
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |
        """,
    },
    # The famous can-can tune, 16 bars.
    "A": {
        "melody": """
            !mf g4:2 |                        % 1
            a4:1/2 c5 b4 a4 |                 % 2
            d5:1 d5:1 |                       % 3
            d5:1/2 e5 b4 c5 |                 % 4
            a4:1 a4:1 |                       % 5
            a4:1/2 c5 b4 a4 |                 % 6
            g4:1/2 g5 f#5 e5 |                % 7
            d5:1/2 c5 b4 a4 |                 % 8
            g4:2 |                            % 9
            a4:1/2 c5 b4 a4 |                 % 10
            d5:1 d5:1 |                       % 11
            d5:1/2 e5 b4 c5 |                 % 12
            a4:1 a4:1 |                       % 13
            a4:1/2 c5 b4 a4 |                 % 14
            g4:1/2 d5 a4 b4 |                 % 15
            g4:1 r:1 |                        % 16
        """,
        "chords": """
            G | D7 | G | G | D7 | D7 | G | D7 |
            G | D7 | G | G | D7 | D7 | G:1 D7:1 | G |
        """,
        "left": """
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |       % 1 G
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |     % 2 D7
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |       % 3
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |       % 4
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |     % 5
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |     % 6
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |       % 7
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |     % 8
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |       % 9
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |     % 10
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |       % 11
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |       % 12
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |     % 13
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |     % 14
            g2:1/2 b3+d4:1/2' d3:1/2 c4+f#4:1/2' |      % 15 G, D7
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |       % 16
        """,
    },
    # Same tune, tutti: melody an octave higher, counter doubles it in the original octave (arranged).
    "A2": {
        "melody": """
            !f g5:2 |
            a5:1/2 c6 b5 a5 |
            d6:1 d6:1 |
            d6:1/2 e6 b5 c6 |
            a5:1 a5:1 |
            a5:1/2 c6 b5 a5 |
            g5:1/2 g6 f#6 e6 |
            d6:1/2 c6 b5 a5 |
            g5:2 |
            a5:1/2 c6 b5 a5 |
            d6:1 d6:1 |
            d6:1/2 e6 b5 c6 |
            a5:1 a5:1 |
            a5:1/2 c6 b5 a5 |
            g5:1/2 d6 a5 b5 |
            g5:1 r:1 |
        """,
        "counter": """
            g4:2 |
            a4:1/2 c5 b4 a4 |
            d5:1 d5:1 |
            d5:1/2 e5 b4 c5 |
            a4:1 a4:1 |
            a4:1/2 c5 b4 a4 |
            g4:1/2 g5 f#5 e5 |
            d5:1/2 c5 b4 a4 |
            g4:2 |
            a4:1/2 c5 b4 a4 |
            d5:1 d5:1 |
            d5:1/2 e5 b4 c5 |
            a4:1 a4:1 |
            a4:1/2 c5 b4 a4 |
            g4:1/2 d5 a4 b4 |
            g4:1 r:1 |
        """,
        "chords": """
            G | D7 | G | G | D7 | D7 | G | D7 |
            G | D7 | G | G | D7 | D7 | G:1 D7:1 | G |
        """,
        "left": """
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |
            d3:1/2 c4+f#4:1/2' a2:1/2 c4+f#4:1/2' |
            g2:1/2 b3+d4:1/2' d3:1/2 c4+f#4:1/2' |
            g2:1/2 b3+d4:1/2' d3:1/2 b3+d4:1/2' |
        """,
    },
    # Arranged 2-bar stinger ending on the tonic.
    "CODA": {
        "melody": """
            !ff g5:1/2' r:1/2 d5:1/2' r:1/2 | d4+b4+g5:1' r:1 |
        """,
        "chords": """
            G:1 D7:1 | G |
        """,
        "left": """
            g2:1/2' r:1/2 d3:1/2' r:1/2 | g2+g3:1' r:1 |
        """,
    },
}
FORM = ["INTRO", "A", "A2", "A", "A2", "A", "CODA"]
SOURCES = [
    "http://listenlearnread.blogspot.com/2011/05/cancan.html",
    "https://www.flutetunes.com/tunes.php?id=69",
    "https://en.wikipedia.org/wiki/Orpheus_in_the_Underworld",
]
NOTES = """
Only the famous 16-bar can-can tune is used; it is verified bars 1-15 against a LilyPond transcription of the
galop's trombone line (Listen Learn Read blog, printed in F major, absolute pitches) and transposed up a whole
tone to G major, the key in which the main theme is first presented according to flutetunes.com. Bar 16 (tonic G
quarter + rest) is assumed from the cadence in bar 15 and was not read in the source.
Arranged / not from the score: all harmony symbols (standard I-V7 reading of the tune, not checked against the
orchestra), the galop oom-pah left hand, the 4-bar INTRO vamp, the A2 variation (melody up an octave with the
original octave doubled in the counter), and the 2-bar CODA stinger. The galop's other strains (second theme,
chorus) are NOT included: no readable source for them could be found, so the arrangement repeats the main tune
(A, A2, A, A2, A) instead. Tempo is a steady 150; in performance the tune usually accelerates towards the end.
"""

# A galop that gathers speed: the tune passes between strings, flute and oboe over an oom-pah band and a shaker.
LAYERS = (
    dict(part="melody", voice="strings", gain=0.3, pan=0.44, send=0.3),
    dict(part="melody", voice="flute", gain=0.2, pan=0.6, send=0.35, form=(1, 2, 4, 6)),
    dict(part="melody", voice="reed", gain=0.24, pan=0.62, send=0.35, form=(3, 5)),
    dict(part="counter", voice="strings", gain=0.22, pan=0.56, send=0.3),
    dict(part="left", voice="pizz", gain=0.18, pan=0.54, send=0.25),
    dict(part="chords", voice="bass", pattern="oompah", role="bass", unit="1/2", gain=0.22, bass_low=38),
    dict(part="grid", voice="shaker", unit="1/2", gain=0.05, pan=0.7, send=0.2, form=(2, 4, 5, 6)),
)
TEMPO = ((72, 150), (168, 176))
RITARDANDO = 1.0
