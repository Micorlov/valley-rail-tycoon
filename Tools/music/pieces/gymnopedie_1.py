"""Gymnopédie No. 1: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Gymnopédie No. 1"
COMPOSER = "Erik Satie"
YEAR = 1888
KEY = "D major"
METER = 3
BEAT = "quarter"
BPM = 72
PICKUP = "0"
SECTIONS = {
    # Bars 1-4: the rocking Gmaj7 / Dmaj7 introduction.
    "Intro": {
        "melody": """
            r:3 | r:3 | r:3 | r:3 |          % 1-4
        """,
        "chords": """
            Gmaj7 | Dmaj7 | Gmaj7 | Dmaj7 |
        """,
        "left": """
            g2:1 b3+d4+f#4:2 |               % 1
            d3:1 a3+c#4+f#4:2 |              % 2
            g2:1 b3+d4+f#4:2 |               % 3
            d3:1 a3+c#4+f#4:2 |              % 4
        """,
    },
    # Bars 5-12: first phrase, ending on the long F#.
    "A": {
        "melody": """
            r:1 !pp f#5:1 a5 |               % 5
            g5 f#5 c#5 |                     % 6
            b4 c#5 d5 |                      % 7
            a4:3 |                           % 8
            f#4:3~ |                         % 9
            f#4:3~ |                         % 10
            f#4:3~ |                         % 11
            f#4:3 |                          % 12
        """,
        "chords": """
            Gmaj7 | Dmaj7 | Gmaj7 | Dmaj7 | Gmaj7 | Dmaj7 | Gmaj7 | Dmaj7 |
        """,
        "left": """
            g2:1 b3+d4+f#4:2 |               % 5
            d3:1 a3+c#4+f#4:2 |              % 6
            g2:1 b3+d4+f#4:2 |               % 7
            d3:1 a3+c#4+f#4:2 |              % 8
            g2:1 b3+d4+f#4:2 |               % 9
            d3:1 a3+c#4+f#4:2 |              % 10
            g2:1 b3+d4+f#4:2 |               % 11
            d3:1 a3+c#4+f#4:2 |              % 12
        """,
    },
    # Bars 13-21: second phrase, continuing C#-F#-E over new harmony.
    "A2": {
        "melody": """
            r:1 f#5:1 a5 |                   % 13
            g5 f#5 c#5 |                     % 14
            b4 c#5 d5 |                      % 15
            a4:3 |                           % 16
            c#5:3 |                          % 17
            f#5:3 |                          % 18
            e4:3~ |                          % 19
            e4:3~ |                          % 20
            e4:3 |                           % 21
        """,
        "chords": """
            Gmaj7 | Dmaj7 | Gmaj7 | Dmaj7 | F#m | Bm | Em | Em7 | Dm |
        """,
        "left": """
            g2:1 b3+d4+f#4:2 |               % 13
            d3:1 a3+c#4+f#4:2 |              % 14
            g2:1 b3+d4+f#4:2 |               % 15
            d3:1 a3+c#4+f#4:2 |              % 16
            f#3:1 a3+c#4+f#4:2 |             % 17
            b2:1 b3+d4+f#4:2 |               % 18
            e3:1 g3+b3:2 |                   % 19
            e3:1 b3+d4+g4:2 |                % 20
            d3:1 f3+a3+d4:2 |                % 21
        """,
    },
    # Bars 22-31: the middle phrases with C and F naturals over a D pedal.
    "B": {
        "melody": """
            a4:1 b4 c5 |                     % 22
            e5 d5 b4 |                       % 23
            d5 c5 b4 |                       % 24
            d5:3~ |                          % 25
            d5:2 d5:1 |                      % 26
            e5 f5 g5 |                       % 27
            a5 c5 d5 |                       % 28
            e5 d5 b4 |                       % 29
            d5:3~ |                          % 30
            d5:2 d5:1 |                      % 31
        """,
        "chords": """
            Am | Em7/D | Em7/D | Am/D | D7 | Dm7 | Am/D | Em7/D | Am/D | D7 |
        """,
        "left": """
            a2:1 a3+c4+e4:2 |                % 22
            d3:1 g3+b3+e4:2 |                % 23
            d3:1 d3+g3+b3+e4:2 |             % 24
            d3:1 c3+e3+a3+d4:2 |             % 25
            d3:1 c3+f#3+a3+d4:2 |            % 26
            d3:1 a3+c4+f4:2 |                % 27
            d3:1 a3+c4+e4:2 |                % 28
            d3:1 d3+g3+b3+e4:2 |             % 29
            d3:1 c3+e3+a3+d4:2 |             % 30
            d3:1 c3+f#3+a3+d4:2 |            % 31
        """,
    },
    # Bars 32-39 (first ending): closing phrase and the Am7 -> D cadence.
    "C": {
        "melody": """
            g5:3 |                           % 32
            f#5:3 |                          % 33
            b4:1 a4 b4 |                     % 34
            c#5 d5 e5 |                      % 35
            c#5 d5 e5 |                      % 36
            f#4:3 |                          % 37
            c4+e4+a4+c5:3 |                  % 38
            d4+f#4+a4+d5:3 |                 % 39
        """,
        "chords": """
            Em | F#m | Bm | A/E | F#m7/E | E7sus4:2 Em7:1 | Am7 | D |
        """,
        "left": """
            e3:1 b3+e4+g4:2 |                % 32
            f#3:1 a3+c#4+f#4:2 |             % 33
            b2:1 b3+d4+f#4:2 |               % 34
            e3:1 c#4+e4+a4:2 |               % 35
            e3:1 a3+c#4+f#4+a4:2 |           % 36
            e2:1 a3+b3+d4:1 b3+d4+e4+g4:1 |  % 37
            a2+g3:3 |                        % 38
            d2+a2+d3:3 |                     % 39
        """,
    },
}
FORM = ["Intro", "A", "A2", "B", "C"]
SOURCES = [
    "https://www.mutopiaproject.org/cgibin/piece-info.cgi?id=37",
    "https://www.mutopiaproject.org/ftp/SatieE/gymnopedie_1/gymnopedie_1.ly",
    "https://en.wikipedia.org/wiki/Gymnop%C3%A9dies",
]
NOTES = """
The whole first page (bars 1-39, first-time ending, which cadences on D major), played once.
Verified against the Mutopia LilyPond source (Dover edition; top/middle/bottom voices, relative mode converted
by hand) and the Wikipedia score excerpt (bars 1-8):
- Melody bars 5-39: certain, including the leap to E4 in bar 19 (source 'e,'), the C/F naturals in bars 22-31
  and the re-struck D in bars 26 and 31.
- LH chords bars 1-36 (beats 2-3) and bass notes (beat 1): certain from the source voices; the D3 pedal in
  bars 23-31 sits above the lowest chord note C3 in bars 25/26/30/31 exactly as the relative octaves give it.
- Bars 37-39: octaves chosen as E2 / A2+G3 / D2-A2-D3 (the source's '<< >>' block makes the relative octave
  ambiguous; this is the reading that keeps the left hand below the right-hand chords). Bar 37 merges the
  source's inner voice (B3, E4) with the middle-voice chords on beats 2 and 3.
Simplified: the bass is notated in the score as a dotted half under the chord; here (as briefed) the LH plays the
bass on beat 1 and the chord on beats 2-3. Bars 38-39 right-hand chords are in "melody". Only the opening pp is
marked; hairpins omitted. The second half of the piece (repeat with D-minor ending) is not included.
"""

LAYERS = (
    dict(part="melody", voice="piano", gain=0.42, pan=0.56, send=0.3, pedal=True),
    dict(part="left", voice="piano", gain=0.36, pan=0.44, send=0.3, pedal=True),
)
