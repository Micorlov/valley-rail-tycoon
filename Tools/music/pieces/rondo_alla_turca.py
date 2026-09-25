"""Rondo alla Turca: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Rondo alla Turca"
COMPOSER = "Wolfgang Amadeus Mozart"
YEAR = 1783
KEY = "A minor"
METER = 2
BEAT = "quarter"
BPM = 120
PICKUP = "1"
SECTIONS = {
    # A-minor theme, bars 1-8 (first time: pickup B-A-G#-A back into the repeat).
    "A": {
        "melody": """
            !p b4:1/4 a4 g#4 a4 |                   % pickup
            c5:1/2 r d5:1/4 c5 b4 c5 |              % 1
            e5:1/2 r f5:1/4 e5 d#5 e5 |             % 2
            b5:1/4 a5 g#5 a5 b5 a5 g#5 a5 |         % 3
            c6:1 a5:1/2 c6 |                        % 4
            b5 f#5+a5 e5+g5 f#5+a5 |                % 5
            b5 f#5+a5 e5+g5 f#5+a5 |                % 6
            b5 f#5+a5 e5+g5 d#5+f#5 |               % 7
            e5:1 b4:1/4 a4 g#4 a4 |                 % 8 + pickup (repeat)
        """,
        "chords": """
            N | Am | Am | Am | Am | Em | Em | Em B | Em |
        """,
        "left": """
            r:1 |                                   % pickup
            a3:1/2 c4+e4 c4+e4 c4+e4 |              % 1
            a3 c4+e4 c4+e4 c4+e4 |                  % 2
            a3 c4+e4 a3 c4+e4 |                     % 3
            a3 c4+e4 c4+e4 c4+e4 |                  % 4
            e3 b3+e4 b3+e4 b3+e4 |                  % 5
            e3 b3+e4 b3+e4 b3+e4 |                  % 6
            e3 b3+e4 b2 b3 |                        % 7
            e3:1 r:1 |                              % 8
        """,
    },
    # Repeat of bars 1-8; bar 8 now carries the C-major upbeat (C-E, D-F thirds).
    "A2": {
        "melody": """
            c5:1/2 r d5:1/4 c5 b4 c5 |              % 1
            e5:1/2 r f5:1/4 e5 d#5 e5 |             % 2
            b5:1/4 a5 g#5 a5 b5 a5 g#5 a5 |         % 3
            c6:1 a5:1/2 c6 |                        % 4
            b5 f#5+a5 e5+g5 f#5+a5 |                % 5
            b5 f#5+a5 e5+g5 f#5+a5 |                % 6
            b5 f#5+a5 e5+g5 d#5+f#5 |               % 7
            e5:1 c5+e5:1/2 d5+f5 |                  % 8 + upbeat into bar 9
        """,
        "chords": """
            Am | Am | Am | Am | Em | Em | Em B | Em |
        """,
        "left": """
            a3:1/2 c4+e4 c4+e4 c4+e4 |              % 1
            a3 c4+e4 c4+e4 c4+e4 |                  % 2
            a3 c4+e4 a3 c4+e4 |                     % 3
            a3 c4+e4 c4+e4 c4+e4 |                  % 4
            e3 b3+e4 b3+e4 b3+e4 |                  % 5
            e3 b3+e4 b3+e4 b3+e4 |                  % 6
            e3 b3+e4 b2 b3 |                        % 7
            e3:1 r:1 |                              % 8
        """,
    },
    # C-major middle phrase, bars 9-16 (ends with the pickup into the theme's return).
    "C": {
        "melody": """
            e5+g5:1/2 e5+g5 a5:1/4 g5 f5 e5 |       % 9
            b4+d5:1 c5+e5:1/2 d5+f5 |               % 10
            e5+g5 e5+g5 a5:1/4 g5 f5 e5 |           % 11
            b4+d5:1 a4+c5:1/2 b4+d5 |               % 12
            c5+e5 c5+e5 f5:1/4 e5 d5 c5 |           % 13
            g#4+b4:1 a4+c5:1/2 b4+d5 |              % 14
            c5+e5 c5+e5 f5:1/4 e5 d5 c5 |           % 15
            g#4+b4:1 b4:1/4 a4 g#4 a4 |             % 16 + pickup
        """,
        "chords": """
            C | G | C | G | Am | E | Am | E |
        """,
        "left": """
            c3:1/2 c4 e3 e4 |                       % 9
            g3:1 r:1 |                              % 10
            c3:1/2 c4 e3 e4 |                       % 11
            g3:1 r:1 |                              % 12
            a2:1/2 a3 c3 c4 |                       % 13
            e3:1 r:1 |                              % 14
            a2:1/2 a3 c3 c4 |                       % 15
            e3:1 r:1 |                              % 16
        """,
    },
    # Return of the theme, bars 17-24, cadencing in A minor; first ending: upbeat back to bar 9.
    "A3": {
        "melody": """
            c5:1/2 r d5:1/4 c5 b4 c5 |              % 17
            e5:1/2 r f5:1/4 e5 d#5 e5 |             % 18
            b5:1/4 a5 g#5 a5 b5 a5 g#5 a5 |         % 19
            c6:1 a5:1/2 c6 |                        % 20
            c6 b5 a5 g#5 |                          % 21
            a5 e5 f5 d5 |                           % 22
            c5:1 b4:1/8 c5 b4 c5 b4:1/4 a4:1/8 b4 | % 23 trill on B written out
            a4:1 c5+e5:1/2 d5+f5 |                  % 24 + upbeat (repeat of bars 9-24)
        """,
        "chords": """
            Am | Am | Am | F7 | Am/E Bdim/D | Am/C Bdim/D | Am/E E | Am |
        """,
        "left": """
            a3:1/2 c4+e4 c4+e4 c4+e4 |              % 17
            a3 c4+e4 c4+e4 c4+e4 |                  % 18
            a3 c4+e4 a3 c4+e4 |                     % 19
            f3 a3+d#4 a3+d#4 a3+d#4 |               % 20 augmented-sixth chord
            e3 a3+e4 d3 f3+b3 |                     % 21
            c3 e3+a3 d3 f3+b3 |                     % 22
            e3+a3 e3+a3 e3+g#3 e3+g#3 |             % 23
            a2+a3:1 r:1 |                           % 24
        """,
    },
    # Same as A3, second ending: upbeat (A-B in octaves) into the A-major refrain.
    "A3b": {
        "melody": """
            c5:1/2 r d5:1/4 c5 b4 c5 |              % 17
            e5:1/2 r f5:1/4 e5 d#5 e5 |             % 18
            b5:1/4 a5 g#5 a5 b5 a5 g#5 a5 |         % 19
            c6:1 a5:1/2 c6 |                        % 20
            c6 b5 a5 g#5 |                          % 21
            a5 e5 f5 d5 |                           % 22
            c5:1 b4:1/8 c5 b4 c5 b4:1/4 a4:1/8 b4 | % 23
            a4:1 !f a4+a5:1/2 b4+b5 |               % 24 + upbeat into the refrain
        """,
        "chords": """
            Am | Am | Am | F7 | Am/E Bdim/D | Am/C Bdim/D | Am/E E | Am |
        """,
        "left": """
            a3:1/2 c4+e4 c4+e4 c4+e4 |              % 17
            a3 c4+e4 c4+e4 c4+e4 |                  % 18
            a3 c4+e4 a3 c4+e4 |                     % 19
            f3 a3+d#4 a3+d#4 a3+d#4 |               % 20
            e3 a3+e4 d3 f3+b3 |                     % 21
            c3 e3+a3 d3 f3+b3 |                     % 22
            e3+a3 e3+a3 e3+g#3 e3+g#3 |             % 23
            a2+a3:1 r:1 |                           % 24
        """,
    },
    # A-major "Turkish march" refrain in octaves, bars 25-32; LH = grace-note drum rolls into repeated notes.
    "R": {
        "melody": """
            c#5+c#6:1 a4+a5:1/2 b4+b5 |             % 25
            c#5+c#6 b4+b5 a4+a5 g#4+g#5 |           % 26
            f#4+f#5 g#4+g#5 a4+a5 b4+b5 |           % 27
            g#4+g#5 e4+e5 a4+a5 b4+b5 |             % 28
            c#5+c#6:1 a4+a5:1/2 b4+b5 |             % 29
            c#5+c#6 b4+b5 a4+a5 g#4+g#5 |           % 30
            f#4+f#5 b4+b5 g#4+g#5 e4+e5 |           % 31
            a4+a5:1 a4+a5:1/2 b4+b5 |               % 32 + upbeat (repeat)
        """,
        "chords": """
            A | A | D D#dim | E | A | A | D E | A |
        """,
        "left": """
            a2:1/16 c#3 e3 a3:5/16 a3:1/2 a3 a3 |                   % 25
            a2:1/16 c#3 e3 a3:5/16 a3:1/2 a3 a3 |                   % 26
            d2:1/16 f#2 a2 d3:5/16 d3:1/2 d#2:1/16 f#2 a2 d#3:5/16 d#3:1/2 |  % 27
            e2:1/16 g#2 b2 e3:5/16 e3:1/2 e3 e3 |                   % 28
            a2:1/16 c#3 e3 a3:5/16 a3:1/2 a3 a3 |                   % 29
            a2:1/16 c#3 e3 a3:5/16 a3:1/2 a3 a3 |                   % 30
            d2:1/16 f#2 a2 d3:5/16 d3:1/2 e2:1/16 g#2 b2 e3:5/16 e3:1/2 |     % 31
            a2:1/16 c#3 e3 a3:13/16 r:1 |                           % 32
        """,
    },
    # Refrain second time, ending on the tonic.
    "R_end": {
        "melody": """
            c#5+c#6:1 a4+a5:1/2 b4+b5 |             % 25
            c#5+c#6 b4+b5 a4+a5 g#4+g#5 |           % 26
            f#4+f#5 g#4+g#5 a4+a5 b4+b5 |           % 27
            g#4+g#5 e4+e5 a4+a5 b4+b5 |             % 28
            c#5+c#6:1 a4+a5:1/2 b4+b5 |             % 29
            c#5+c#6 b4+b5 a4+a5 g#4+g#5 |           % 30
            f#4+f#5 b4+b5 g#4+g#5 e4+e5 |           % 31
            a4+a5:1 r:1 |                           % 32 final
        """,
        "chords": """
            A | A | D D#dim | E | A | A | D E | A |
        """,
        "left": """
            a2:1/16 c#3 e3 a3:5/16 a3:1/2 a3 a3 |                   % 25
            a2:1/16 c#3 e3 a3:5/16 a3:1/2 a3 a3 |                   % 26
            d2:1/16 f#2 a2 d3:5/16 d3:1/2 d#2:1/16 f#2 a2 d#3:5/16 d#3:1/2 |  % 27
            e2:1/16 g#2 b2 e3:5/16 e3:1/2 e3 e3 |                   % 28
            a2:1/16 c#3 e3 a3:5/16 a3:1/2 a3 a3 |                   % 29
            a2:1/16 c#3 e3 a3:5/16 a3:1/2 a3 a3 |                   % 30
            d2:1/16 f#2 a2 d3:5/16 d3:1/2 e2:1/16 g#2 b2 e3:5/16 e3:1/2 |     % 31
            a2:1/16 c#3 e3 a3:13/16 r:1 |                           % 32 final
        """,
    },
}
FORM = ["A", "A2", "C", "A3", "C", "A3b", "R", "R_end"]
SOURCES = [
    "https://www.mutopiaproject.org/cgibin/piece-info.cgi?id=108",
    "https://github.com/MutopiaProject/MutopiaProject/tree/master/ftp/MozartWA/KV331/KV331_3_RondoAllaTurca",
    "https://en.wikipedia.org/wiki/Piano_Sonata_No._11_(Mozart)",
    "https://imslp.org/wiki/Piano_Sonata_No.11_in_A_major,_K.331/300i_(Mozart,_Wolfgang_Amadeus)",
]
NOTES = """
Checked against the Mutopia LilyPond source (Rune Zedeler / Chris Sawer, public domain), read variable by variable.
- Theme bars 1-8 (RH incl. the thirds F#-A / E-G / D#-F# in bars 5-7) and LH (A3 + C4-E4 eighths, E3 + B3-E4,
  bar 7 E3 B3-E4 B2 B3): certain.
- C-major phrase bars 9-16: LH (C3 C4 E3 E4 / G3, A2 A3 C3 C4 / E3) certain; RH thirds certain, and the
  2x C-G then 2x Am-E layout was confirmed by counting the source's chord pairs.
- Theme return bars 17-24 with the augmented-sixth bar 20 (LH F3 + A3-D#4, chord symbol F7 = its enharmonic
  sound), the C6-B-A-G# / A-E-F-D descent and the trilled cadence: certain from the source; the trill on B4 in
  bar 23 is written out as B-C-B-C + held B (ornament realisation is mine), the grace note before B5 in bar 7
  is omitted.
- A-major refrain bars 25-32: RH octaves certain. The LH in the source (and per Wikipedia 'arpeggiated chord
  accompaniment') is NOT broken octaves: each chord change is a quick 32nd-note grace roll (e.g. A2-C#3-E3)
  into repeated staccato eighths on the chord's root (A3 / D3 / D#3 / E3). The grace rolls are written as
  1/16-beat notes taken from the main note. Bar 32's LH (after the last roll) is not in the refrain variable and
  was reconstructed as a single rolled A.
- Arrangement: FORM plays the score's repeats (bars 1-8 twice, bars 9-24 twice) then the refrain twice; the
  first-ending upbeats (C-E/D-F thirds back to bar 9) are placed at the end of bar 24 as the repeat requires.
  The movement's F#-minor episode and coda are omitted. Final bar ends on an A octave (refrain cadence).
- Dynamics: p for the theme, f for the refrain (as in the score); staccato marks omitted.
"""

LAYERS = (
    dict(part="melody", voice="piano", gain=0.42, pan=0.56, send=0.3),
    dict(part="left", voice="piano", gain=0.36, pan=0.44, send=0.3),
)
