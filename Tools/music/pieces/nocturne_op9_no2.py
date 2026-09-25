"""Nocturne in E-flat major, Op. 9 No. 2: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Nocturne in E-flat major, Op. 9 No. 2"
COMPOSER = "Frédéric Chopin"
YEAR = 1832
KEY = "E-flat major"
METER = 12
BEAT = "eighth"
BPM = 104
PICKUP = "1"
SECTIONS = {
    # Pickup + bars 1-4: the theme.
    "A": {
        "melody": """
            !p bb4:1 |                                                     % pickup
            g5:4 f5:1 g5:1 f5:3 eb5:2 bb4:1 |                              % 1
            g5:2 c5:1 c6:2 g5:1 bb5:3 ab5:2 g5:1 |                         % 2
            f5:3 g5:2 d5:1 eb5:3 c5:3 |                                    % 3
            bb4:1 d6:1 c6:1 bb5:1/2 ab5 g5 ab5 c5 d5 eb5:3 r:2 bb4:1 |     % 4 (+ pickup)
        """,
        "chords": """
            N | Eb Ddim7/Eb Eb Eb/D | C7 C7 Edim7/F Fm | Bb7 G7/B Cm Adim7 | Bb7sus4 Bb7 Eb Eb |
        """,
        "left": """
            r:1 |                                                                                 % pickup
            eb2:1 g3+eb4 bb3+eb4+g4 eb3 ab3+d4 cb4+d4+ab4 eb2 g3+eb4 bb3+eb4+g4 d2 g3+eb4 bb3+eb4+g4 |  % 1
            c2 g3+e4 bb3+e4+g4 c3 g3+e4 c4+e4+bb4 f2 f3+db4 bb3+db4+e4 f2 f3+c4 ab3+c4+f4 |       % 2
            bb2 f3+d4 bb3+d4+ab4 b2 g3+f4 d4+f4+g4 c3 g3+eb4 c4+eb4+g4 a2 gb3+eb4 c4+eb4+gb4 |    % 3
            bb2 f3+eb4 bb3+eb4+ab4 bb2 f3+d4 bb3+ab4 eb2 g3+eb4 bb3+eb4+g4 eb3 g3+eb4 bb3+eb4+g4 |  % 4
        """,
    },
    # Bars 5-8: the theme's first varied (ornamented) return.
    "A2": {
        "melody": """
            g5:3 f5:1/2 g5 f5 e5 f5 g5 f5:1 eb5:2~ eb5:1/2 f5 eb5 d5 eb5 f5 |      % 5
            g5:1/2 b4 c5 db5 c5 f5 e5 ab5 g5 db6 c6 g5 bb5:3 ab5:2 g5:1 |          % 6
            f5:1/4 g5 f5 g5 f5 g5 f5 g5 f5:1/2 e5:1/4 f5 g5:1 g5 d5 eb5:3 c5:3 |   % 7 (trill written out)
            bb4:1 d6:1 c6:1 bb5:1/2 ab5 g5 ab5 c5 d5 eb5:4 d5:1 eb5:1 |            % 8
        """,
        "chords": """
            Eb Ddim7/Eb Eb Eb/D | C7 C7 Edim7/F Fm | Bb7 G7/B Cm Adim7 | Bb7sus4 Bb7 Eb Eb |
        """,
        "left": """
            eb3:1 g3+eb4 bb3+eb4+g4 eb3 ab3+d4 cb4+d4+ab4 eb3 g3+eb4 bb3+eb4+g4 d3 g3+eb4 bb3+eb4+g4 |  % 5
            c3 g3+e4 bb3+e4+g4 c3 g3+e4 c4+e4+bb4 f2 f3+db4 bb3+db4+e4 f2 f3+c4 ab3+c4+f4 |       % 6
            bb2 f3+d4 bb3+d4+ab4 b2 g3+f4 d4+f4+g4 c3 g3+eb4 c4+eb4+g4 a2 gb3+eb4 c4+eb4+gb4 |    % 7
            bb2 f3+eb4 bb3+eb4+ab4 bb2 f3+d4 bb3+ab4 eb2 g3+eb4 bb3+eb4+g4 eb3 g3+eb4 bb3+eb4+g4 |  % 8
        """,
    },
    # Bars 9-12: the contrasting middle phrase, ending on the chromatic run-up to the dominant.
    "B": {
        "melody": """
            f5:3 g5:2 f5:1 f5:3 c5:3 |                                             % 9
            eb5:1 eb5 eb5 eb5 d5:1/2 eb5 f5:3/4 eb5:1/4 eb5:3 bb4:3 |              % 10
            bb5:3 a5:2 g5:1 a4+f5:3 bb4+d5:3 |                                     % 11
            g4+eb5:3 a4+d5:1 a4+c5 a4+d5 f4+bb4 f#4+b4 e4+b4 e4+bb4+c5 f4+a4+c5 ab4+d5 |  % 12
        """,
        "chords": """
            Bb Bb F/A F/A | Ab Abm Eb Eb | Edim7 C7/E F7 Gm |
            Cm:3 F7:3 Bb:1 B7/A:1 E/G#:1 C7/G:1 F7:1 Bb7:1 |
        """,
        "left": """
            bb2:1 f3+d4 bb3+d4+f4 bb2 f3+d4 bb3+d4+f4 a2 f3+c4 c4+f4 a2 f3+c4 c4+f4 |              % 9
            ab2 eb3+c4 ab3+c4+eb4 ab2 eb3+cb4 ab3+cb4+eb4 eb2 g3+eb4 bb3+eb4+g4 eb3 g3+eb4 bb3+eb4+g4 |  % 10
            e2 e3+db4 bb3+db4+g4 e2 e3+c4 bb3+c4+g4 f2 f3+eb4 c4+eb4+f4 g2 g3+d4 bb3+d4+g4 |      % 11
            c2 g3+eb4 c4+eb4+g4 f2 f3+eb4 c4+eb4+f4 bb3+d4 a3+d#4 g#3 g3 f3+c4+eb4 bb3+f4 |       % 12
        """,
    },
    # Arranged close (not in the score here): I - V7 - I.
    "End": {
        "melody": """
            g5:3 f5:3 eb5:6 |
        """,
        "chords": """
            Eb:3 Bb7:3 Eb:6 |
        """,
        "left": """
            eb2:1 g3+eb4 bb3+eb4+g4 bb2 f3+d4 bb3+ab4 eb2+bb2+g3+eb4:6 |
        """,
    },
}
FORM = ["A", "A2", "B", "End"]
SOURCES = [
    "https://www.mutopiaproject.org/cgibin/piece-info.cgi?id=1590",
    "https://www.mutopiaproject.org/ftp/ChopinFF/O9/chopin_nocturne_op9_n2/chopin_nocturne_op9_n2.ly",
    "https://en.wikipedia.org/wiki/Nocturnes,_Op._9_(Chopin)",
]
NOTES = """
Bars 1-12 (theme, its ornamented return, the middle phrase) + one arranged cadence bar.
Source: Mutopia LilyPond file (after the Schirmer 1881 edition); melody and left hand converted from relative
mode by hand, the left hand beat-group by beat-group (all 48 groups of bars 1-12 read from the source).
- Melody bars 1-4 and 9-12: certain (incl. the C5-C6 leap in bar 2, the high D6 turn in bars 4/8, the
  right-hand chords of bars 11-12).
- Bars 5-8 ornaments simplified: bar 5 turn written as six 16ths; the source's note values in bar 5 add up to
  13 eighths as read (a tuplet marking was probably lost), so the Eb after the turn is shortened to a quarter to
  keep beats aligned (flag); bar 6 chromatic fioritura kept as 16ths; bar 7 trill written out as 32nds with
  its E-F termination; grace notes (Ab before C in bar 8) omitted.
- Left hand (bass eighth + two chord eighths per beat): certain. Two bass notes lie below C2 in the score
  (Bb1 in bars 4 and 8, Ab1 in bar 10) and are raised an octave to Bb2 / Ab2 to fit the left-hand range.
- The 'End' bar is arranged: bar 12 ends on Bb7 and the score continues with the theme; here it resolves
  G-F-Eb over Eb - Bb7 - Eb.
"""

LAYERS = (
    dict(part="melody", voice="piano", gain=0.42, pan=0.56, send=0.3, pedal=True),
    dict(part="left", voice="piano", gain=0.36, pan=0.44, send=0.3, pedal=True),
)
