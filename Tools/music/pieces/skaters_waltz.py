"""The Skaters' Waltz (Les Patineurs): public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "The Skaters' Waltz (Les Patineurs)"
COMPOSER = "Emile Waldteufel"
YEAR = 1882
KEY = "A major"
METER = 3
BEAT = "quarter"
BPM = 180
PICKUP = "0"
SECTIONS = {
    # Waltz No. 1, 16 bars (the famous theme).
    "A": {
        "melody": """
            !p c#4:3 |              % 1
            e4:2 f#4:1 |            % 2
            f#4:3~ |                % 3
            f#4:3 |                 % 4
            d4:3 |                  % 5
            f#4:2 g#4:1 |           % 6
            g#4:3~ |                % 7
            g#4:3 |                 % 8
            b4:3 |                  % 9
            a4:2 c#4:1 |            % 10
            e4:3 |                  % 11
            d4+f#4:2 c#4:1 |        % 12
            c#4+g#4:3 |             % 13
            b3+g#4:3 |              % 14
            a4:3~ |                 % 15
            a4:1 r:2 |              % 16
        """,
        "chords": """
            A | A/C# | E7/B | E7 | E7/B | E7 | A/C# | A/C# |
            F#m | F#m | Bm7 | Bm7 | E7 | E7 | A | A |
        """,
        "left": """
            a2:1 e3+a3 e3+a3 |          % 1
            c#3 e3+a3 e3+a3 |           % 2
            b2 d3+e3+g#3 d3+e3+g#3 |    % 3
            e2 d3+e3+g#3 d3+e3+g#3 |    % 4
            b2 d3+e3+g#3 d3+e3+g#3 |    % 5
            e2 d3+e3+g#3 d3+e3+g#3 |    % 6
            c#3 e3+a3 e3+a3 |           % 7
            c#3 e3+a3 e3+a3 |           % 8
            f#2 c#3+f#3+a3 c#3+f#3+a3 | % 9
            f#2 c#3+f#3+a3 c#3+f#3+a3 | % 10
            b2 d3+f#3+a3 d3+f#3+a3 |    % 11
            b2 d3+f#3+a3 d3+f#3+a3 |    % 12
            e2 d3+e3+g#3 d3+e3+g#3 |    % 13
            e2 d3+e3+g#3 d3+e3+g#3 |    % 14
            a2 c#3+e3 c#3+e3 |          % 15
            c#3+e3:1 r:2 |              % 16
        """,
    },
    # Waltz No. 1 again, its last bar carrying the 2-beat pickup of Waltz No. 2.
    "A2": {
        "melody": """
            c#4:3 | e4:2 f#4:1 | f#4:3~ | f#4:3 |
            d4:3 | f#4:2 g#4:1 | g#4:3~ | g#4:3 |
            b4:3 | a4:2 c#4:1 | e4:3 | d4+f#4:2 c#4:1 |
            c#4+g#4:3 | b3+g#4:3 | a4:3~ |
            a4:1 !f f#4:1/2 a4 d5 f#5 |      % 16 + pickup into Waltz 2
        """,
        "chords": """
            A | A/C# | E7/B | E7 | E7/B | E7 | A/C# | A/C# |
            F#m | F#m | Bm7 | Bm7 | E7 | E7 | A | A:1 D:2 |
        """,
        "left": """
            a2:1 e3+a3 e3+a3 | c#3 e3+a3 e3+a3 | b2 d3+e3+g#3 d3+e3+g#3 | e2 d3+e3+g#3 d3+e3+g#3 |
            b2 d3+e3+g#3 d3+e3+g#3 | e2 d3+e3+g#3 d3+e3+g#3 | c#3 e3+a3 e3+a3 | c#3 e3+a3 e3+a3 |
            f#2 c#3+f#3+a3 c#3+f#3+a3 | f#2 c#3+f#3+a3 c#3+f#3+a3 | b2 d3+f#3+a3 d3+f#3+a3 | b2 d3+f#3+a3 d3+f#3+a3 |
            e2 d3+e3+g#3 d3+e3+g#3 | e2 d3+e3+g#3 d3+e3+g#3 | a2 c#3+e3 c#3+e3 |
            c#3+e3:1 r:2 |
        """,
    },
    # Waltz No. 2, first 16 bars (D major, turning to F-sharp minor).
    "B": {
        "melody": """
            b5:1 r:1 a5:1 |                     % 1
            r:1 a5:1/8 a6:7/8 r:1 |             % 2 (grace note written out)
            c#4:3~ |                            % 3
            c#4:1 g4:1/2 a4 c#5 e5 |            % 4
            b5:1 r:1 a5:1 |                     % 5
            r:1 a5:1/8 a6:7/8 r:1 |             % 6
            d4:3~ |                             % 7
            d4:1 f#4:1/2 a4 d5 f#5 |            % 8
            b5:1 r:1 a5:1 |                     % 9
            r:1 a5:1/8 a6:7/8 r:1 |             % 10
            c#4:3~ |                            % 11
            c#4:1 c#5:1/8 c#6:7/8 r:1 |         % 12
            e#4:3~ |                            % 13
            e#4:1 e#5:1/8 e#6:7/8 r:1 |         % 14
            f#4:3~ |                            % 15
            f#4:1 r:2 |                         % 16
        """,
        "chords": """
            D | D/A | A7/E | A7 | A7/E | A7 | D | D/A |
            D | D | F#m/C# | F#m/C# | C#7 | C#7 | F#m | F#m |
        """,
        "left": """
            d3:1 f#3+a3 f#3+a3 |                % 1
            a2 f#3+a3 f#3+a3 |                  % 2
            e3 g3+a3 g3+a3 |                    % 3
            a2 g3+a3 g3+a3 |                    % 4
            e3 g3+a3 g3+a3 |                    % 5
            a2 g3+a3 g3+a3 |                    % 6
            d3 f#3+a3 f#3+a3 |                  % 7
            a2 f#3+a3 f#3+a3 |                  % 8
            d3 f#3+a3 f#3+a3 |                  % 9
            d3 f#3+a3 f#3+a3 |                  % 10
            c#3 f#3+a3 f#3+a3 |                 % 11
            c#3 f#3+a3+c#4 f#3+a3+c#4 |         % 12
            c#3 g#3+b3+c#4 g#3+b3+c#4 |         % 13
            c#3 g#3+b3+c#4 g#3+b3+c#4 |         % 14
            f#3+a3+c#4:1 r:1 c#3:1 |            % 15
            f#2:1 r:2 |                         % 16
        """,
    },
}
FORM = ["A", "A2", "B", "A"]
SOURCES = [
    "https://en.wikipedia.org/wiki/Les_Patineurs_(waltz)",
    "https://en.wikipedia.org/w/index.php?title=Les_Patineurs_(waltz)&action=raw",
]
NOTES = """
Source: the piano-score excerpts ("Waltz 1" and "Waltz 2") in the Wikipedia article's <score> blocks, read note
by note from the raw wikitext and converted from relative octaves. Both staves are read inside one \\relative
block, so the left hand continues from the right hand's last note; that reading puts the bass in a normal bass
register, which confirms the octaves.
Waltz No. 1 (section A, the famous theme): melody and left hand of all 16 bars verified. The melody's long notes
over changing harmony (G# over A/C# in bars 7-8, B over F#m in bar 9, E over Bm7 in bar 11, C# over E7 in bar 13)
are as in the source. Simplified: the final A is an A3+A4 octave in the source; the melody keeps A4 only.
Waltz No. 2 (section B): bars 1-15 and the 2-beat pickup verified (melody + left hand); the slashed grace notes
before the high A6/C#6/E#6 are written as 1/8-beat notes. The excerpt stops on beat 1 of bar 16 (F#4 over an F#2
bass), so the rest of bar 16 (two beats' rest) is arranged; this is only the first strain of Waltz 2, and it ends
in F-sharp minor before FORM returns to Waltz 1 in A major.
Arranged: FORM (A, A2, B, A = Waltz 1 twice, Waltz 2 strain, Waltz 1) and the chord symbol "D" on the last two
beats of A2 bar 16 under the pickup. The introduction, Waltzes 3-4 and the coda are not used.
Tempo: the excerpt's MIDI setting is dotted half = 64 (= 192 quarters/min); BPM 180 is a slightly relaxed version.
"""

# Orchestral waltz: strings with a flute on the repeats, celesta sparkle, plucked accompaniment over a bass.
LAYERS = (
    dict(part="melody", voice="strings", gain=0.34, pan=0.44, send=0.35),
    dict(part="melody", voice="flute", gain=0.16, pan=0.6, send=0.4, form=(1, 3)),
    dict(part="melody", voice="bell", gain=0.04, pan=0.68, send=0.5, octave=1, form=(1, 3)),
    dict(part="left", voice="pizz", gain=0.17, pan=0.55, send=0.25),
    dict(part="chords", voice="bass", pattern="waltz", role="bass", gain=0.24, bass_low=38),
    dict(part="chords", voice="pad", pattern="block", gain=0.03, center=64),
)
