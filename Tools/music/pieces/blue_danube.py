"""The Blue Danube: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "The Blue Danube"
COMPOSER = "Johann Strauss II"
YEAR = 1866
KEY = "D major"
METER = 3
BEAT = "quarter"
BPM = 172
PICKUP = "0"
SECTIONS = {
    # Arranged 4-bar waltz vamp; its last beat is the melody's famous pickup D.
    "INTRO": {
        "melody": """
            r:3 | r:3 | r:3 | r:2 !p d4:1 |
        """,
        "chords": """
            D | D | D | D |
        """,
        "left": """
            d3:1 f#3+a3 f#3+a3 | d3 f#3+a3 f#3+a3 | d3 f#3+a3 f#3+a3 | d3 f#3+a3 f#3+a3 |
        """,
    },
    # Waltz No. 1A, bars 1-31 (bar numbers counted after the pickup).
    # melody = horns/cellos tune; counter = the violins' "pom-pom" pairs (beat 3 + next downbeat).
    "A": {
        "melody": """
            d4:1 f#4 a4 | a4:2 r:1 | r:3 | r:2 d4:1 |          % 1-4
            d4:1 f#4 a4 | a4:2 r:1 | r:3 | r:2 c#4:1 |         % 5-8
            c#4:1 e4 b4 | b4:2 r:1 | r:3 | r:2 c#4:1 |         % 9-12
            c#4:1 e4 b4 | b4:2 r:1 | r:3 | r:2 d4:1 |          % 13-16
            d4:1 f#4 a4 | d5:2 r:1 | r:3 | r:2 d4:1 |          % 17-20
            d4:1 f#4 a4 | d5:2 r:1 | r:3 | r:2 e4:1 |          % 21-24
            e4:1 g4 b4 | b4:3~ | b4:1 g#4 a4 | f#5:3~ |        % 25-28
            f#5:1 d5 f#4 | f#4:2 e4:1 | b4:2 a4:1 |            % 29-31
        """,
        "counter": """
            r:3 | r:2 f#5+a5:1 | f#5+a5:1 r:1 d5+f#5:1 | d5+f#5:1 r:2 |    % 1-4
            r:3 | r:2 g5+a5:1 | g5+a5:1 r:1 c#5+g5:1 | c#5+g5:1 r:2 |      % 5-8
            r:3 | r:2 g5+b5:1 | g5+b5:1 r:1 c#5+g5:1 | c#5+g5:1 r:2 |      % 9-12
            r:3 | r:2 f#5+b5:1 | f#5+b5:1 r:1 d5+f#5:1 | d5+f#5:1 r:2 |    % 13-16
            r:3 | r:2 a5+d6:1 | a5+d6:1 r:1 f#5+a5:1 | f#5+a5:1 r:2 |      % 17-20
            r:3 | r:2 b5+d6:1 | b5+d6:1 r:1 g5+b5:1 | g5+b5:1 r:2 |        % 21-24
            r:3 | r:3 | r:3 | r:3 | r:3 | r:3 | r:3 |                      % 25-31
        """,
        "chords": """
            D | D | D | D |
            D | A7 | A7 | A7 |
            A7 | A7 | A7 | A7 |
            A7 | D6 | D6:2 D:1 | D |
            D | D/F# | D/F# | D/F# |
            D/F# | Em7/G | Em7/G | Em/G |
            Em | A7/E | A7/C# | D |
            D/F# | G | A/E |
        """,
        "left": """
            d3:1 f#3+a3 f#3+a3 | d3 f#3+a3 f#3+a3 | d3 f#3+a3 f#3+a3 | d3 f#3+a3 f#3+a3 |   % 1-4
            d3 f#3+a3 f#3+a3 | a2 g3+c#4 g3+c#4 | a2 g3+c#4 g3+c#4 | a2 g3+c#4 g3+c#4 |    % 5-8
            a2 g3+c#4 g3+c#4 | a2 g3+c#4 g3+c#4 | a2 g3+c#4 g3+c#4 | a2 g3+c#4 g3+c#4 |   % 9-12
            a2 g3+c#4 g3+c#4 | d3 f#3+a3 f#3+a3 | d3 f#3+a3 f#3+a3 | d3 f#3+a3 f#3+a3 |   % 13-16
            d3 f#3+a3 f#3+a3 | f#3 a3+d4 a3+d4 | f#3 a3+d4 a3+d4 | f#3 a3+d4 a3+d4 |      % 17-20
            f#3 a3+d4 a3+d4 | g3 b3+e4 b3+e4 | g3 b3+e4 b3+e4 | g3+b3+e4:3 |             % 21-24
            b3:1 g3 e3 | e3 g3+a3 g3+a3 | c#3 g3+a3 g3+a3 | d3 f#3+a3 f#3+a3 |          % 25-28
            f#3 a3+d4 a3+d4 | g3+b3+d4:3 | e3+a3+c#4:3 |                                 % 29-31
        """,
    },
    # Bars 32-33 leading back to bar 1 (the pickup D is written on beat 3 of bar 33).
    "R": {
        "melody": """
            d4:3/2 d5:1/2 d5:1 | d5:1 r:1 d4:1 |
        """,
        "chords": """
            D | D |
        """,
        "left": """
            d3+f#3+a3:2 d3+f#3+a3:1 | d3:1 f#3+a3 f#3+a3 |
        """,
    },
    # Bars 32-33 as the final cadence.
    "END": {
        "melody": """
            d4:3/2 d5:1/2 d5:1 | d5:1 r:2 |
        """,
        "chords": """
            D | D |
        """,
        "left": """
            d3+f#3+a3:2 d3+f#3+a3:1 | d3+f#3+a3:1 r:2 |
        """,
    },
}
FORM = ["INTRO", "A", "R", "A", "END"]
SOURCES = [
    "https://www.mutopiaproject.org/cgibin/piece-info.cgi?id=519",
    "https://www.mutopiaproject.org/ftp/StraussJJ/O314/blue_danube/blue_danube.ly",
    "https://abcnotation.com/tunePage?a=trillian.mit.edu%2F%7Ejc%2Fmusic%2Fbook%2FKerr%2FMM4-V1%2F0781",
    "https://en.wikipedia.org/wiki/The_Blue_Danube",
]
NOTES = """
Waltz No. 1A only (32 bars + pickup). Main source: Mutopia's piano reduction of the main theme (from Christian
Mondrup's reduction of the orchestral score, printed in C major); every pitch was transposed up a whole tone to
the original D major. Melody, answering pairs and the bass/harmony pattern of every bar come from it.
Cross-check: James Kerr's fiddle version (abcnotation, printed in G) agrees on the melody of bars 1-8, on all the
violins' answering pairs, and on the rhythm of those pairs (beat 3, then the following downbeat, the "three-one"
figure), and on the dotted/eighth figure of bar 32. So in this transcription the pairs do NOT overlap the pickup
D: the second note of each pair falls on the downbeat and the pickup comes two beats later. They are still a
separate part because they sound against the held melody notes.
Disagreement flagged: in bars 9-16 Kerr's melody tops out on the 7th (C#-E-G) where Mutopia has C#-E-B (the 9th
over A7); I followed Mutopia, which matches the well-known tune and the B-B answers in bars 10-11 and 14-15.
Unverified or arranged: the melody's note values in bars 2, 6, 10, 14, 18, 22 (a half note, as in the piano
reduction; the orchestra may hold it longer); the answers are written as quarter notes (Kerr) with the piano
reduction's thirds/sixths voicing, not the orchestral doubling; the left-hand root-position A7 voicing in bars 6-13
(the reduction puts the 5th, E, in the bass there); bar 1's accompaniment (the reduction leaves it empty); the
4-bar INTRO vamp is an arrangement suggestion, not the score; the repeat link (R, pickup on beat 3 of bar 33) and
the END are arranged. Waltz 1B was not included: the only text source found (Kerr) does not reliably match the
original, so it could not be verified.
Tempo: a steady 172; a Viennese performance would linger on the pickups and the long notes of bars 26-29.
"""

# Horns carry the tune the first time and strings the second, with a flute an octave up; violins answer.
LAYERS = (
    dict(part="melody", voice="strings", gain=0.34, pan=0.42, send=0.35),
    dict(part="melody", voice="horn", gain=0.3, pan=0.38, send=0.35, form=(1,)),
    dict(part="melody", voice="flute", gain=0.16, pan=0.6, send=0.4, octave=1, form=(3, 4)),
    dict(part="counter", voice="strings", gain=0.2, pan=0.64, send=0.35),
    dict(part="counter", voice="bell", gain=0.05, pan=0.7, send=0.5, form=(3,)),
    dict(part="left", voice="pizz", gain=0.17, pan=0.55, send=0.25),
    dict(part="chords", voice="bass", pattern="waltz", role="bass", gain=0.26, bass_low=38),
    dict(part="chords", voice="pad", pattern="block", gain=0.035, center=64),
    dict(part="chords", voice="harp", pattern="arpeggio", gain=0.12, pan=0.35, bass_low=50, form=(0,)),
)
