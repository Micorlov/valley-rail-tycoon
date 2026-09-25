"""Eine kleine Nachtmusik (I. Allegro): public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Eine kleine Nachtmusik (I. Allegro)"
COMPOSER = "Wolfgang Amadeus Mozart"
YEAR = 1787
KEY = "G major"
METER = 4
BEAT = "quarter"
BPM = 140
PICKUP = "0"
SECTIONS = {
    # Bars 1-10: the unison "rocket" opening and the answering piano phrase.
    # melody = Violin I, left = cello (sounding pitch), chords from all four string parts.
    "A": {
        "melody": """
            !f d4+b4+g5:1 r:1/2 d5:1/2 g5:1 r:1/2 d5:1/2 |          % 1
            g5:1/2 d5 g5 b5 d6:1 r:1 |                              % 2
            c6:1 r:1/2 a5:1/2 c6:1 r:1/2 a5:1/2 |                   % 3
            c6:1/2 a5 f#5 a5 d5:1 r:1 |                             % 4
            d4+b4+g5:1/2 r g5:3/2 b5:1/2 a5 g5' |                   % 5
            a5:1/8 g5 a5 g5 f#5:1/2 f#5:3/2 a5:1/2 c6 f#5' |        % 6 (trill written out)
            a5:1/2 g5 g5:3/2 b5:1/2 a5 g5' |                        % 7
            a5:1/8 g5 a5 g5 f#5:1/2 f#5:3/2 a5:1/2 c6 f#5' |        % 8
            g5:1/2' g5' g5:1/4 f#5 e5 f#5 g5:1/2' g5' b5:1/4 a5 g5 a5 |  % 9 (appoggiaturas as 16ths)
            b5:1/2' b5' d6:1/4 c6 b5 c6 d6:1 r:1 |                  % 10
        """,
        "chords": """
            N | N | N | N |
            G | D7/G | G | D7/G |
            G D7/A G/B D7/F# |
            G:1 D7/A:1 G/B:2 |
        """,
        "left": """
            g3:1 r:1/2 d3:1/2 g3:1 r:1/2 d3:1/2 |       % 1 unison with the violins
            g3:1/2 d3 g3 b3 d4:1 r:1 |                  % 2
            c4:1 r:1/2 a3:1/2 c4:1 r:1/2 a3:1/2 |       % 3
            c4:1/2 a3 f#3 a3 d3:1 r:1 |                 % 4
            g3:1/2 g3 g3 g3 g3 g3 g3 g3 |               % 5 tonic pedal
            g3 g3 g3 g3 g3 g3 g3 g3 |                   % 6
            g3 g3 g3 g3 g3 g3 g3 g3 |                   % 7
            g3 g3 g3 g3 g3 g3 g3 g3 |                   % 8
            g3 g3 a3 a3 b3 b3 f#3 f#3 |                 % 9
            g3 g3 a3 a3 b3:1 r:1 |                      % 10
        """,
    },
    # Bars 11-17: the piano phrase in thirds (Violin II in "counter"), cadence into bar 18.
    "B": {
        "melody": """
            !p d5:2 e5:2 |                                  % 11
            d5:1/8 c5:7/8 c5:1 c5:1/8 b4:7/8 b4:1 |         % 12 (acciaccaturas)
            b4:1/8 a4:7/8 a4:1 g4:1/2 f#4 e4' f#4' |        % 13
            g4:1/2 r a4 r b4 r r:1 |                        % 14
            d5:2 e5:2 |                                     % 15
            d5:1/2 c5 c5' c5' c5 b4 b4' b4' |               % 16
            b4:1/2 a4 a4' a4' g4 f#4 e4 f#4 |               % 17
        """,
        "counter": """
            b4:2 c5:2 |                                     % 11 Violin II
            b4:1/8 a4:7/8 a4:1 a4:1/8 g4:7/8 g4:1 |         % 12
            e4:1 e4 c4 a3 |                                 % 13
            d4:1/2 r f#4 r g4 r r:1 |                       % 14
            b4:2 c5:2 |                                     % 15
            b4:1/2 a4 a4' a4' a4 g4 g4' g4' |               % 16
            e4:1 e4:1/2 e4 c4:1 c4:1/2 c4 |                 % 17
        """,
        "chords": """
            G:2 C:2 | D7:2 Em:2 | Am/C:2 D7:2 | G/B:1 D:1 G:2 |
            G:2 C:2 | D7:2 Em:2 | Am/C:2 D7:2 |
        """,
        "left": """
            r:4 |                                  % 11 cello tacet
            d3:2 e3:2 |                            % 12
            c3:1 c3 d3 d3 |                        % 13
            b2:1/2 r d3 r g3:1 r:1 |               % 14
            r:4 |                                  % 15
            d3:2 e3:2 |                            % 16
            c3:1 c3 d3 d3 |                        % 17
        """,
    },
    # Bars 18-27: sforzando G, tremolo crescendo, modulation towards D major (ends on A = V of D).
    "T": {
        "melody": """
            !f g3+g4:5/2 !p a4:1/8 g4 f#4 g4 a4:1/2 f#4 |                       % 18
            !f b4:5/2 !p c5:1/8 b4 a4 b4 c5:1/2 a4 |                            % 19
            !mp d5:1/4 d5 d5 d5 d5 d5 d5 d5 e5 e5 e5 e5 f#5 f#5 f#5 f#5 |       % 20 tremolo, cresc.
            !mf g5 g5 g5 g5 a5 a5 a5 a5 b5 b5 b5 b5 c#6 c#6 c#6 c#6 |           % 21
            !f d6:3/2 a5:1/2 c#6:3/4 a5:1/4 c#6:3/4 a5:1/4 |                    % 22
            d6:3/2 a5:1/2 c#6:3/4 a5:1/4 c#6:3/4 a5:1/4 |                       % 23
            d6:1/2 d6:1 d6 d6 d6:1/2~ |                                         % 24 syncopated
            d6:1/2 d6:1 d6 d6 d6:1/2 |                                          % 25
            c#6:1/2 a5 d6 a5 c#6 a5 d6 a5 |                                     % 26
            c#6:1/2 a4 a4 a4 a4:1 r:1 |                                         % 27
        """,
        "counter": """
            b3:5/2 c4:1/8 b3 a3 b3 c4:1/2 a3 |                                  % 18 Violin II
            g4:5/2 a4:1/8 g4 f#4 g4 a4:1/2 f#4 |                                % 19
            b4:1/4 b4 b4 b4 b4 b4 b4 b4 c5 c5 c5 c5 c5 c5 c5 c5 |               % 20
            b4 b4 b4 b4 d5 d5 d5 d5 d5 d5 d5 d5 g5 g5 g5 g5 |                   % 21
            f#5 f#5 f#5 f#5 f#5 f#5 f#5 f#5 g5 g5 g5 g5 g5 g5 g5 g5 |           % 22
            f#5 f#5 f#5 f#5 f#5 f#5 f#5 f#5 g5 g5 g5 g5 g5 g5 g5 g5 |           % 23
            d5+f#5:1/2 d5+f#5:1 d5+f#5 d5+f#5 d5+f#5:1/2 |                      % 24
            d5+e5:1/2 d5+e5:1 d5+e5 d5+e5 d5+e5:1/2 |                           % 25
            c#5+e5:1/4 c#5+e5 c#5+e5 c#5+e5 d5+f#5 d5+f#5 d5+f#5 d5+f#5
            c#5+e5 c#5+e5 c#5+e5 c#5+e5 d5+f#5 d5+f#5 d5+f#5 d5+f#5 |           % 26
            c#5+e5:1/2 a4 a4 a4 a4:1 r:1 |                                      % 27
        """,
        "chords": """
            G:3 D7/G:1 | G:3 D7/G:1 | G:2 C/G:1 D7/G:1 | G D/F# G A7/E |
            D:2 A7/D:2 | D:2 A7/D:2 | D | Em7:3 E7/G#:1 |
            A D/A A D/A | A |
        """,
        "left": """
            g3:1/2 g3 g3 g3 g3 g3 g3 g3 |          % 18
            g3 g3 g3 g3 g3 g3 g3 g3 |              % 19
            g3 g3 g3 g3 g3 g3 g3 g3 |              % 20
            g3 g3 f#3 f#3 g3 g3 e3 e3 |            % 21
            d3:4 |                                 % 22
            d3:4 |                                 % 23
            d3:1/2 e3 f#3 e3 d3 e3 f#3 d3 |        % 24
            g3 a3 b3 a3 g3 a3 b3 g#3 |             % 25
            a3 a3 a3 a3 a3 a3 a3 a3 |              % 26
            a3 a2 a2 a2 a2:1 r:1 |                 % 27
        """,
    },
    # Arrangement ending: bars 18-19 (G with turns over the tonic pedal) + a final tonic chord.
    "CODA": {
        "melody": """
            !f g3+g4:5/2 !p a4:1/8 g4 f#4 g4 a4:1/2 f#4 |       % = bar 18
            !f b4:5/2 !p c5:1/8 b4 a4 b4 c5:1/2 a4 |            % = bar 19
            !f d4+b4+g5:2 r:2 |                                 % final tonic (arranged)
        """,
        "counter": """
            b3:5/2 c4:1/8 b3 a3 b3 c4:1/2 a3 |
            g4:5/2 a4:1/8 g4 f#4 g4 a4:1/2 f#4 |
            g4:2 r:2 |
        """,
        "chords": """
            G:3 D7/G:1 | G:3 D7/G:1 | G |
        """,
        "left": """
            g3:1/2 g3 g3 g3 g3 g3 g3 g3 |
            g3 g3 g3 g3 g3 g3 d3 d3 |
            g2+g3:2 r:2 |
        """,
    },
}
FORM = ["A", "B", "T", "A", "B", "CODA"]
SOURCES = [
    "https://www.mutopiaproject.org/cgibin/piece-info.cgi?id=900",
    "https://www.mutopiaproject.org/ftp/MozartWA/KV525/eine-kleine-nachtmusik-mvt1/eine-kleine-nachtmusik-mvt1-lys/violin1.ly",
    "https://www.mutopiaproject.org/ftp/MozartWA/KV525/eine-kleine-nachtmusik-mvt1/eine-kleine-nachtmusik-mvt1-lys/violin2.ly",
    "https://www.mutopiaproject.org/ftp/MozartWA/KV525/eine-kleine-nachtmusik-mvt1/eine-kleine-nachtmusik-mvt1-lys/viola.ly",
    "https://www.mutopiaproject.org/ftp/MozartWA/KV525/eine-kleine-nachtmusik-mvt1/eine-kleine-nachtmusik-mvt1-lys/cello.ly",
    "https://en.wikipedia.org/wiki/Eine_kleine_Nachtmusik",
]
NOTES = """
Verified: melody = Violin I bars 1-27 read note-by-note from the Mutopia (public-domain) LilyPond part
violin1.ly and converted from relative octaves; bars 1-4 also match the Wikipedia score excerpt (second
theme starts at bar 28 there, so bars 1-27 are the complete first group + transition). Counter = Violin II
bars 11-27 (violin2.ly); left = cello bars 1-27 (cello.ly, sounding pitch; the double bass would add the
octave below).
Harmony: derived from cello + viola + both violins for bars 1-17; bars 18-27 from cello and both violins
(viola not read there). Bars 1-4 are an unaccompanied unison in the score: the symbols G/G/D7/D7 are the
implied harmony only (a renderer that wants the bare unison can treat bars 1-4 as N).
Simplified / written out: the trills in bars 6 and 8 as Mozart's printed A-G-A-G 32nd turn; the 16th
appoggiaturas in bars 9-10 as on-beat 16ths; the acciaccaturas in bars 12, 13 as 1/8-beat grace notes;
measured tremolo (bars 20-23, Violin II 20-26) as repeated 16ths; in bars 24-26 the melody carries only
Violin I's top line (its lower double-stop notes are doubled by Violin II in the counter); the tie of the
upper D across bars 24-25 is kept, the Violin II tie is re-struck. Dynamics follow the part (f, p at bar 11,
sf/p in 18-19, cresc. 20-21, f at 22); the cresc. is approximated with !mp/!mf steps.
Arrangement (not in the score): FORM A B T A B CODA; T ends on A major (V of D) and goes straight back to the
G unison opening; CODA = bars 18-19 plus a final G chord (bar 3 of CODA is invented, cello bar 2 of CODA ends
with D3 D3 instead of G3 G3 to lead into it).
"""

# String quartet: first and second violins, cello doubled an octave down by the bass, a soft viola-like pad.
LAYERS = (
    dict(part="melody", voice="strings", gain=0.34, pan=0.4, send=0.3),
    dict(part="counter", voice="strings", gain=0.24, pan=0.6, send=0.3),
    dict(part="left", voice="strings", gain=0.26, pan=0.52, send=0.25),
    dict(part="left", voice="bass", gain=0.14, pan=0.5, send=0.1, octave=-1),
    dict(part="chords", voice="pad", pattern="block", gain=0.03, center=62),
)
