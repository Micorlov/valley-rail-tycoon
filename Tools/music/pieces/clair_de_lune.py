"""Clair de lune: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Clair de lune"
COMPOSER = "Claude Debussy"
YEAR = 1905
KEY = "D-flat major"
METER = 9
BEAT = "eighth"
BPM = 108
PICKUP = "0"
SECTIONS = {
    # Bars 1-8: the opening thirds and the descending answer (pp, con sordina).
    "A": {
        "melody": """
            !pp r:2 f5+ab5:4 db5+f5:3~ |                                    % 1
            db5+f5:1 c5+eb5:1 db5+f5:1 c5+eb5:6~ |                          % 2
            c5+eb5:1 bb4+db5:1 c5+eb5:1 db5:3/2 f5:3 db5:3/2~ |             % 3 (duplets)
            db5:1 ab4+c5:1 bb4+db5:1 ab4+c5:6~ |                            % 4
            ab4+c5:1 bb4 c5 bb4 eb5 bb4 ab4 bb4 ab4~ |                      % 5
            ab4:1 gb4 ab4 gb4:3 f4:3 |                                      % 6
            f4:1 f4 gb4 f4 bb4 f4 eb4 f4 eb4~ |                             % 7
            eb4:1 db4 eb4 db4:3 c4:3 |                                      % 8
        """,
        "chords": """
            Db/F | Cdim7 | Fm:3 Bbm7:6 | Ab7 | Ebm7/Db:6 Ab7/C:3 | Ebm7/Bb:6 F7/A:3 |
            Bbm7/Ab:6 Gbmaj7:3 | Bbm7/F:6 Ab7:3 |
        """,
        "left": """
            r:1 f4+ab4:8 |                                                  % 1
            gb4+bbb4:9 |                                                    % 2
            f4+ab4:3 f4+ab4+bb4:6 |                                         % 3 (+ RH inner Bb4)
            eb4+gb4:9 |                                                     % 4
            db4+eb4+gb4:6 c4+eb4+gb4:3 |                                    % 5 (+ RH inner Gb4)
            bb3+db4+eb4:6 a3+c4+eb4:3 |                                     % 6 (+ RH inner Eb4)
            ab3+bb3+db4:6 gb3+bb3+db4:3 |                                   % 7 (+ RH inner Db4)
            f3+ab3+bb3:6 eb3+gb3+ab3:3/2 ab2+gb3+ab3:3/2 |                  % 8 (+ RH inner Bb3/Ab3)
        """,
    },
    # Bars 9-14: the opening motif returns over a rising arpeggio, then the answering phrase in octaves.
    "A2": {
        "melody": """
            r:3 f5+ab5:3 db5+f5:3~ |                                        % 9
            db5+f5:1 eb5:1 f5:1 eb5:6~ |                                    % 10
            eb5:1 db4+db5:1 eb4+eb5:1 ab4+db5+ab5:3 f4+db5+f5:3~ |          % 11
            f4+db5+f5:1 eb5:1 f5:1 eb5:3 db5:3~ |                           % 12
            db5:1 db4+db5:1 eb4+eb5:1 bb4+f5+bb5:3/2 ab4+f5+ab5:3 f4+f5:3/2 |  % 13 (duplets)
            f5:1 eb5:1 f5:1 eb5:3/2 db5:3 bb4:3/2 |                         % 14 (duplets)
        """,
        "chords": """
            Db | Gbmaj7/Db | Db/F | Gbmaj7/Db | Db7/Ab | Bbsus4 Bbm/Db |
        """,
        "left": """
            db2+ab2:1 f3+ab3:1 f4+ab4:7 |                                   % 9
            gb2+db3:1 db2+gb2+bb2+db3+gb4+bb4:8 |                           % 10
            f2+db3:1 f2+ab2+ab4:2 db3+f3:3 ab2+db3:3 |                      % 11
            gb2+db3:1 db2+gb2+bb2+db3+gb4+bb4:8 |                           % 12
            ab2:1 f2+cb3+ab4:2 ab2+cb3+db3+f3:4 r:2 |                       % 13
            bb2:1 bb2+eb3+f3+bb3+f4+bb4:7/2 db3+f3+bb3+f4+bb4:9/2 |         % 14
        """,
    },
    # Bars 15-18: 'Tempo rubato' - melody in octaves with inner fifth, tenuto chords in the left hand.
    "B": {
        "melody": """
            !pp r:3/2 f4+bb4+f5:5/2 eb4+bb4+eb5:1 eb4+bb4+eb5 eb4+bb4+eb5 db4+bb4+db5 db4+bb4+db5 |  % 15
            db4+bb4+db5:1 c4+gb4+bb4+c5 c4+gb4+bb4+c5 c4+gb4+bb4+c5:3/2 db4+bb4+db5:3/2 bb3+gb4+bb4:3 |  % 16
            r:3/2 f4+bb4+f5:5/2 gb4+bb4+gb5:1 f4+bb4+f5 eb4+bb4+eb5 f4+bb4+f5 eb4+bb4+eb5 |  % 17
            db4+bb4+db5:1 eb4+bb4+eb5 db4+bb4+db5 bb4+c5:3/2 db4+bb4+db5:3/2 bb3+gb4+bb4:3 |  % 18
        """,
        "chords": """
            Ebm:7 Gb/Db:2 | Gb/Db:1 Cm7b5:7/2 Gb/Db:3/2 Gb/Bb:3 | Ebm |
            Gb/Db:3 Cm7b5:3/2 Gb/Db:3/2 Ebm/Bb:3 |
        """,
        "left": """
            eb2+eb3:3/2 eb2+f3+gb3+bb3:5/2 eb3+gb3+bb3:1 eb3+gb3+bb3 eb3+gb3+bb3 db3+gb3+bb3 db3+gb3+bb3 |  % 15
            db3+gb3+bb3:1 c3+gb3+bb3 c3+gb3+bb3 c3+gb3+bb3:3/2 db3+gb3+bb3:3/2 bb2+db3+gb3:3 |  % 16
            eb2+eb3:3/2 eb2+f3+gb3+bb3:5/2 gb3+bb3+eb4:1 f3+gb3+bb3 eb3+gb3+bb3 f3+gb3+bb3 eb3+gb3+bb3 |  % 17
            db3+gb3+bb3:1 eb3+gb3+bb3 db3+gb3+bb3 c3+gb3+bb3:3/2 db3+gb3+bb3:3/2 bb2+eb3+gb3:3 |  % 18
        """,
    },
    # Arranged close: V7 - I in D-flat (not in the score at this point).
    "End": {
        "melody": """
            gb4+c5:3 f4+ab4+db5:6 |                                         % arranged cadence
        """,
        "chords": """
            Ab7:3 Db:6 |
        """,
        "left": """
            ab2+eb3:3 db2+ab2+db3:6 |
        """,
    },
}
FORM = ["A", "A2", "B", "End"]
SOURCES = [
    "https://www.mutopiaproject.org/cgibin/piece-info.cgi?id=1778",
    "https://www.ibiblio.org/mutopia/ftp/DebussyC/L75/debussy_Ste_Bergamesq_Clair/debussy_Ste_Bergamesq_Clair.ly",
    "https://en.wikipedia.org/wiki/Suite_bergamasque",
    "https://imslp.org/wiki/Suite_bergamasque_(Debussy,_Claude)",
]
NOTES = """
Shortened arrangement: bars 1-18 of the score (opening, arpeggiated return, first four 'Tempo rubato' bars) plus
one arranged cadence bar (Ab7 -> Db); bars 19-26 ('peu a peu cresc. et anime') are omitted to stay near 95 s.
Source: Mutopia LilyPond file (Fromont 1905 edition), four voices read bar by bar and converted from relative
mode by hand. 9/8 duplets are written as 3/2-beat notes.
- Melody (RH top voice incl. the thirds/octaves it moves in) bars 1-18: pitches and rhythms taken from the source;
  confident except bar 3, where the duplet notes (Db5, F5 tied, Db5) were read from a partly garbled extract.
- Left hand: both LH voices merged into one line; sustained RH inner-voice notes (Bb4 bar 3, Gb4/Eb4/Db4/Bb3
  bars 5-8, Gb4-Bb4 bars 10/12, Ab4 bars 11/13, F4-Bb4 bar 14) are folded into the left-hand chords, and the
  RH inner F5 of bar 13 into the melody chords, so the harmony is complete. Held notes that the score sustains
  under moving voices are re-struck with the next chord (simplification).
- Octaves are as the relative-mode source gives them: the low Db2-Gb2-Bb2-Db3 chords in bars 10/12 and the
  F3-Gb3-Bb3 'Tempo rubato' chords are literal readings; bar 1's first F4-Ab4 dyad (written in both hands in the
  source) is played once, by the LH.
- Small liberties: the tied C5 into bar 5 and the tied F5 into bar 12 keep their lower chord notes for one extra
  eighth (so every tie joins identical chords); grace note in bar 18 omitted; hairpins omitted.
- The 'End' bar is not by Debussy: it resolves bar 18's Ebm/Bb through Ab7 to a D-flat major chord.
"""

LAYERS = (
    dict(part="melody", voice="piano", gain=0.42, pan=0.56, send=0.3, pedal=True),
    dict(part="left", voice="piano", gain=0.36, pan=0.44, send=0.3, pedal=True),
)
