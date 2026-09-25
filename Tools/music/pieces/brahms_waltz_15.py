"""Waltz in A-flat, Op. 39 No. 15: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Waltz in A-flat, Op. 39 No. 15"
COMPOSER = "Johannes Brahms"
YEAR = 1865
KEY = "A-flat major"
METER = 3
BEAT = "quarter"
BPM = 120
PICKUP = "0"
# The whole piece (44 bars, second half written out in the source) is played once:
# P = bars 1-6 (= 9-14 = 23-28), Q1 = 7-8, Q2 = 15-16, B = 17-22 (= 31-36), Q3 = 29-30, CODA = 37-44.
SECTIONS = {
    "P": {
        "melody": """
            !p c5:3/2 ab4:1/2 ab4 c5 |          % 1
            c5:3/2 ab4:1/2 ab4 c5 |             % 2
            db5:1/2 eb5:1/4 db5 c5:1 bb4 |      % 3
            c5:3/2 ab4:1/2 ab4 eb5 |            % 4
            f5:3/2 c5:1/2 c5 eb5 |              % 5
            f5:3/2 c5:1/2 c5 eb5 |              % 6
        """,
        "chords": """
            Ab | Ab | Db/Ab | Ab | Fm7 | Fm7 |
        """,
        "left": """
            ab2:1 eb3+ab3+c4 c3+eb3+ab3 |       % 1
            ab2 eb3+ab3+c4 c3+eb3+ab3 |         % 2 (score: Ab1)
            ab2 db3+f3+db4 f3+ab3 |             % 3
            ab2 eb3+ab3+c4 c3+eb3+ab3 |         % 4 (score: Ab1)
            f2 f3+ab3+c4+f4 ab2+eb3+c4 |        % 5
            f2 f3+ab3+c4+f4 ab2+eb3+c4 |        % 6
        """,
    },
    "Q1": {
        "melody": """
            g5:1/2 f5 eb5:1 d5 |                % 7
            eb5:3/2 g4:1/2 g4 c5 |              % 8
        """,
        "chords": """
            Cm/Eb:2 G7:1 | Cm |
        """,
        "left": """
            eb2:1 eb3+g3+c4 g2+f3+b3 |          % 7
            c2 eb3+g3+c4 c3+eb3 |               % 8
        """,
    },
    "Q2": {
        "melody": """
            g5:1/2 f5 eb5:1 d5 |                % 15
            eb5:3/2 c5:1/2 c5 eb5 |             % 16
        """,
        "chords": """
            Cm/Eb:2 G7:1 | Cm |
        """,
        "left": """
            eb2:1 eb3+g3+c4 g2+f3+b3 |          % 15
            c2 eb3+g3+c4 c3+eb3+g3 |            % 16
        """,
    },
    "B": {
        "melody": """
            eb5:3/2 bb4:1/2 bb4 eb5 |           % 17
            eb5:3/2 c5:1/2 c5 eb5 |             % 18
            ab5:3/2 eb5:1/2 eb5 ab5 |           % 19
            ab5:3/2 f5:1/2 f5 ab5 |             % 20
            bb5:3/2 f5:1/2 f5 ab5 |             % 21
            ab5:1/2 g5 f5:1 eb5 |               % 22
        """,
        "chords": """
            Eb7 | Ab | Ab7 | Db | Bb7 | Eb7 |
        """,
        "left": """
            eb2:1 eb3+g3+db4 eb3+g3 |           % 17
            ab2 eb3+ab3+c4 eb3+ab3 |            % 18
            ab2 ab3+c4+gb4 ab3+c4 |             % 19 (score: Ab1)
            db2 ab3+db4+f4 ab3+db4 |            % 20
            bb2 ab3+bb3+d4+f4 f3+bb3+d4 |       % 21 (score: Bb1+Bb2 octave)
            eb2 eb3+bb3+db4 eb3+bb3+eb4 |       % 22
        """,
    },
    "Q3": {
        "melody": """
            ab5:1/2 eb5 db5:1 bb4 |             % 29
            ab4:1 r:1/2 c5:1/2 c5 eb5 |         % 30
        """,
        "chords": """
            Ab/C:1 Eb7:2 | Ab |
        """,
        "left": """
            c2:1 eb2 eb3+g3+db4 |               % 29
            ab2 eb3+ab3+c4 r |                  % 30 (score: Ab1)
        """,
    },
    "CODA": {
        "melody": """
            c6:1 r:1/3 ab4 c5 ab5 c6 ab5 |      % 37
            c6:1 r:1/3 ab4 c5 ab5 c6 ab5 |      % 38
            db6:1/2 eb6:1/4 db6 c6:1 bb5 |      % 39
            c6:1 r:1/3 ab4 c5 ab5 c6 c6 |       % 40
            f6:1 r:1/3 ab4 c5 ab5 c6 c6 |       % 41
            f6:1 r:1/3 ab4 c5 ab5 c6 c6 |       % 42
            ab6:1/2 eb6 db6:1 bb5 |             % 43
            ab5:1 r:2 |                         % 44
        """,
        "chords": """
            Ab | Ab | Db | Ab | Fm | Fm | Ab/C:1 Eb7:2 | Ab |
        """,
        "left": """
            ab2:1 eb3+ab3+c4 c3+eb3+ab3 |       % 37
            ab2 eb3+ab3+c4 c3+eb3+ab3 |         % 38
            db2 ab3+db4+f4 ab3+db4 |            % 39
            ab2 eb3+ab3+c4 c3+eb3+ab3 |         % 40
            f2 f3+ab3+c4 f3+ab3+c4 |            % 41
            f2 f3+ab3+c4 f3+ab3+c4 |            % 42
            c2 eb2 eb3+g3+db4 |                 % 43
            ab2+eb3+ab3+c4:1 r:2 |              % 44
        """,
    },
}
FORM = ["P", "Q1", "P", "Q2", "B", "P", "Q3", "B", "CODA"]
SOURCES = [
    "https://www.mutopiaproject.org/cgibin/piece-info.cgi?id=794",
    "https://www.mutopiaproject.org/ftp/BrahmsJ/O39/waltz-op39-15/waltz-op39-15.ly",
]
NOTES = """
Source: Mutopia Project's public-domain LilyPond engraving (ed. R. Joseffy; 44 bars, the repeat of the second
half written out). FORM plays the whole piece once, bar for bar: P Q1 = bars 1-8, P Q2 = 9-16, B = 17-22,
P Q3 = 23-30, B = 31-36, CODA = 37-44.
Melody (right hand, top line) verified for all 44 bars from the file and converted from relative octaves. Note that
this is a real two-voice right hand: the melody part keeps only the top line (the inner thirds/sixths are covered by
the chord symbols); in the coda the melody includes the lower notes of the triplet broken chords (Ab4, C5), which are
the top notes of those chords.
Left hand verified bars 1-30 note for note; bars 31-36 confirmed identical to 17-22 in the source. Changed for the
c2 range limit: the bass notes Ab1 (bars 2, 4, 10, 12, 19, 24, 26, 30) and the Bb1+Bb2 octave (bar 21) are written
an octave up (Ab2 / Bb2). Bars 23-28 reuse bars 1-6; in the source only bar 28's left hand differs slightly
(F2, F-Ab-C, Ab-Eb-Ab instead of the bar-6 voicing).
Reconstructed: coda left hand bars 37-44 — only the bass note of each bar could be read from the source (Ab Ab Db Ab F F C
Ab); the chords above it follow the right-hand harmony and the waltz pattern of the earlier bars.
Harmony symbols read from both staves. Grace notes: none. Tempo: BPM 120 is a suggestion (the file has no marking).
"""

LAYERS = (
    dict(part="melody", voice="piano", gain=0.42, pan=0.56, send=0.3, pedal=True),
    dict(part="left", voice="piano", gain=0.36, pan=0.44, send=0.3, pedal=True),
)
