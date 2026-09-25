"""Minute Waltz (Waltz in D-flat major, Op. 64 No. 1): public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Minute Waltz (Waltz in D-flat major, Op. 64 No. 1)"
COMPOSER = "Frédéric Chopin"
YEAR = 1847
KEY = "D-flat major"
METER = 3
BEAT = "quarter"
BPM = 190
PICKUP = "0"
SECTIONS = {
    # Bars 1-20: four-bar right-hand introduction, then the spinning figure over the waltz bass.
    "A1": {
        "melody": """
            !p ab4:1 g4:1/2 ab4 c5 bb4 |     % 1
            g4 ab4 bb4 ab4 c5 bb4 |          % 2
            g4 ab4 c5 bb4 g4 ab4 |           % 3
            c5 bb4 g4 ab4 c5 bb4 |           % 4
            g4 ab4 c5 bb4 g4 ab4 |           % 5
            c5 bb4 g4 ab4 c5 bb4 |           % 6
            g4 ab4 c5 bb4 g4 ab4 |           % 7
            bb4 c5 db5 eb5 f5 gb5 |          % 8
            bb5:3/2 ab5:1/2 gb5 f5 |         % 9
            f5 eb5 eb5 d5 eb5:1 |            % 10
            bb5:3/2 ab5:1/2 gb5 f5 |         % 11
            f5 eb5 d5 eb5 f5 bb4 |           % 12
            g4 ab4 c5 bb4 g4 ab4 |           % 13
            c5 bb4 g4 ab4 c5 bb4 |           % 14
            g4 ab4 c5 bb4 g4 ab4 |           % 15
            bb4 c5 db5 eb5 f5 gb5 |          % 16
            bb5:3/2 ab5:1/2 gb5 f5 |         % 17
            f5 eb5 eb5 d5 eb5:1 |            % 18
            bb5:3/2 ab5:1/2 gb5 f5 |         % 19
            eb5 f5 eb5 d5 eb5 e5 |           % 20
        """,
        "chords": """
            N | N | N | N | Db | Db/F | Db | Db/F | Ab7 | Ab7/Eb | Ab7 | Ab7 |
            Db | Db/F | Db | Db/F | Ab7 | Ab7/Eb | Ab7 | Ab7 |
        """,
        "left": """
            r:3 | r:3 | r:3 | r:3 |                          % 1-4
            db3:1 ab3+db4+f4 ab3+db4+f4 |                    % 5
            f3 ab3+db4+f4 ab3+db4+f4 |                       % 6
            db3 ab3+db4+f4 ab3+db4+f4 |                      % 7
            f3 ab3+db4+f4 ab3+db4+f4 |                       % 8
            ab2 ab3+c4+gb4 ab3+c4+gb4 |                      % 9
            eb3 ab3+c4+gb4 ab3+c4+gb4 |                      % 10
            ab2 c4+gb4 c4+gb4 |                              % 11
            ab3 c4+gb4 c4+gb4 |                              % 12
            db3 ab3+db4+f4 ab3+db4+f4 |                      % 13
            f3 ab3+db4+f4 ab3+db4+f4 |                       % 14
            db3 ab3+db4+f4 ab3+db4+f4 |                      % 15
            f3 ab3+db4+f4 ab3+db4+f4 |                       % 16
            ab2 ab3+c4+gb4 ab3+c4+gb4 |                      % 17
            eb3 ab3+c4+gb4 ab3+c4+gb4 |                      % 18
            ab2 ab3+c4+gb4 ab3+c4+gb4 |                      % 19
            ab3 c4+gb4 ab2 |                                 % 20
        """,
    },
    # Bars 21-36: second strain, climbing twice and cascading down to D-flat.
    "A2": {
        "melody": """
            f5:1/3 gb5 f5 e5:1/2 f5 ab5 gb5 |    % 21
            f5 gb5 f5 e5 f5 bb5 |                % 22
            ab5:1/3 bb5 ab5 g5:1/2 ab5 c6 bb5 |  % 23
            ab5 bb5 ab5 g5 ab5 db6 |             % 24
            c6 bb5 ab5 gb5 f5 eb5 |              % 25
            db5 c5 bb4 ab4 gb4 f4 |              % 26
            eb4 db4 c4 eb4 bb4 ab4 |             % 27
            g4 ab4 bb4 c5 db5 eb5 |              % 28
            f5:1/3 gb5 f5 e5:1/2 f5 ab5 gb5 |    % 29
            f5 gb5 f5 e5 f5 bb5 |                % 30
            ab5:1/3 bb5 ab5 g5:1/2 ab5 c6 bb5 |  % 31
            ab6 bb6 ab6 g6 ab6 f6 |              % 32 (score: last note F7, see NOTES)
            eb6 db6 c6 bb5 ab5 gb5 |             % 33 (score: octave higher)
            f5 eb5 db5 c5 bb4 ab4 |              % 34 (score: octave higher)
            a4 c5 bb4 f4 gb4 c4 |                % 35 (score: octave higher)
            db4:1 r:2 |                          % 36
        """,
        "chords": """
            F7/A | Bbm | Ab7/C | Db | Ebm/Gb | Db/Ab | Ab7 | Db |
            F7/A | Bbm | Ab7/C | Db | Ebm7/Gb | Db/Ab | Ab7 | Db |
        """,
        "left": """
            a2:1 f3+c4+eb4 f3+c4+eb4 |           % 21
            bb2 f3+db4 f3+db4 |                  % 22
            c3 ab3+eb4+gb4 ab3+eb4+gb4 |         % 23
            db3 ab3+f4 r |                       % 24
            gb3 bb3+eb4 r |                      % 25
            ab2 f3+ab3+db4 r |                   % 26
            ab2 gb3+ab3 gb3+ab3+c4 |             % 27
            db3 ab3+db4+f4 r |                   % 28
            a3:3 |                               % 29
            bb3:3 |                              % 30
            c4:3 |                               % 31
            db4:1 f4+ab4 r |                     % 32
            gb3 db4+eb4+bb4 r |                  % 33
            ab2 f3+ab3+db4 r |                   % 34
            ab2 gb3+ab3 gb3+ab3 |                % 35
            db3 ab3+f4 r |                       % 36
        """,
    },
    # Bar 37 (link, Ab upbeat) + bars 38-53: the sostenuto middle theme, first statement and its link.
    "B": {
        "melody": """
            db4:1 r:1 ab4:1~ |                   % 37
            ab4:2 eb4:1 |                        % 38
            ab4:2 e4:1 |                         % 39
            ab4:2 f4:1 |                         % 40
            f5:2 f5:1~ |                         % 41
            f5:2 bb4:1 |                         % 42
            f5:2 c5:1 |                          % 43
            eb5:2 db5:1 |                        % 44
            c5:3/4 eb5 db5 bb4 |                 % 45 (4 against 3)
            ab4:2 eb4:1 |                        % 46
            ab4:2 e4:1 |                         % 47
            ab4:2 f4:1 |                         % 48
            f5:3 |                               % 49
            db5:1/4 c5 db5 c5 b4:1 c5 |          % 50 (short trill written out)
            ab5 bb4 g5 |                         % 51
            a4 gb5 ab4 |                         % 52
            f5 f4 bb4 |                          % 53
        """,
        "chords": """
            Db | Ab7 | Ab7 | Db | Db/Ab | Ab7/Eb | Ab7 | Db | Db/F |
            Ab7/C | Ab7 | Db | Db7/B | Fm/C | C | F7 | N |
        """,
        "left": """
            db3:1 ab3+f4 r |                     % 37
            ab2 gb3+ab3+c4 gb3+ab3+c4 |          % 38
            gb3+ab3+c4 gb3+ab3+c4 gb3+ab3+c4 |   % 39
            db3 ab3+db4 ab3+db4 |                % 40
            ab2 ab3+db4+f4 ab3+db4+f4 |          % 41
            eb3 ab3+c4+gb4 ab3+c4+gb4 |          % 42
            ab2 ab3+eb4+gb4 ab3+eb4+gb4 |        % 43
            db3 ab3+db4+f4 ab3+db4+f4 |          % 44
            f3 ab3+db4+f4 ab3+db4+f4 |           % 45
            c3 gb3+ab3 gb3+ab3 |                 % 46
            ab2 gb3+ab3 gb3+ab3+c4 |             % 47
            db3 ab3+db4 ab3+db4 |                % 48
            b2 ab3+db4+f4 ab3+db4+f4 |           % 49
            c3 ab3+c4+f4 ab3+c4+f4 |             % 50
            c2 g3+c4+e4 r |                      % 51
            f2 r f3+c4 |                         % 52
            r:3 |                                % 53
        """,
    },
    # Bars 21-36 again, closing on a full D-flat chord.
    "A2_end": {
        "melody": """
            f5:1/3 gb5 f5 e5:1/2 f5 ab5 gb5 |    % 21
            f5 gb5 f5 e5 f5 bb5 |                % 22
            ab5:1/3 bb5 ab5 g5:1/2 ab5 c6 bb5 |  % 23
            ab5 bb5 ab5 g5 ab5 db6 |             % 24
            c6 bb5 ab5 gb5 f5 eb5 |              % 25
            db5 c5 bb4 ab4 gb4 f4 |              % 26
            eb4 db4 c4 eb4 bb4 ab4 |             % 27
            g4 ab4 bb4 c5 db5 eb5 |              % 28
            f5:1/3 gb5 f5 e5:1/2 f5 ab5 gb5 |    % 29
            f5 gb5 f5 e5 f5 bb5 |                % 30
            ab5:1/3 bb5 ab5 g5:1/2 ab5 c6 bb5 |  % 31
            ab6 bb6 ab6 g6 ab6 f6 |              % 32
            eb6 db6 c6 bb5 ab5 gb5 |             % 33
            f5 eb5 db5 c5 bb4 ab4 |              % 34
            a4 c5 bb4 f4 gb4 c4 |                % 35
            !f db4+f4+ab4+db5:1 r:2 |            % 36 final chord (arranged)
        """,
        "chords": """
            F7/A | Bbm | Ab7/C | Db | Ebm/Gb | Db/Ab | Ab7 | Db |
            F7/A | Bbm | Ab7/C | Db | Ebm7/Gb | Db/Ab | Ab7 | Db |
        """,
        "left": """
            a2:1 f3+c4+eb4 f3+c4+eb4 |           % 21
            bb2 f3+db4 f3+db4 |                  % 22
            c3 ab3+eb4+gb4 ab3+eb4+gb4 |         % 23
            db3 ab3+f4 r |                       % 24
            gb3 bb3+eb4 r |                      % 25
            ab2 f3+ab3+db4 r |                   % 26
            ab2 gb3+ab3 gb3+ab3+c4 |             % 27
            db3 ab3+db4+f4 r |                   % 28
            a3:3 |                               % 29
            bb3:3 |                              % 30
            c4:3 |                               % 31
            db4:1 f4+ab4 r |                     % 32
            gb3 db4+eb4+bb4 r |                  % 33
            ab2 f3+ab3+db4 r |                   % 34
            ab2 gb3+ab3 gb3+ab3 |                % 35
            db2+db3:1 r:2 |                      % 36 final (arranged)
        """,
    },
}
FORM = ["A1", "A2", "B", "A1", "A2_end"]
SOURCES = [
    "https://www.mutopiaproject.org/cgibin/piece-info.cgi?id=483",
    "https://github.com/MutopiaProject/MutopiaProject/tree/master/ftp/ChopinFF/O64/chopin_valse_op64_no1/chopin_valse_op64_no1-lys",
    "https://en.wikipedia.org/wiki/Minute_Waltz",
]
NOTES = """
Arrangement: intro + A section (bars 1-36), the first statement of the sostenuto theme (bars 37-53), then the
A section again (intro included, as when the figure returns in the score) ending on a D-flat chord.
Source: Mutopia LilyPond files sec1/sec2/sec3 (CC0), relative octaves converted by hand; LH is the
bass-note + two-chord waltz pattern exactly as written (LH rests in the four intro bars).
- Bars 1-31 RH and LH: certain (incl. the Bb-Gb rising scales in bars 8/16, the triplet turns in 21/23/29/31,
  the sustained A3-Bb3-C4-Db4 left-hand line in bars 29-32).
- Bars 32-36 RH: in the score the second climb leaps to F7 and the scale cascades from F7 down to Db5. To keep
  the melody under C7 the leap is replaced by a step down to F6 and the cascade (bars 33-36) is written one
  octave lower (ending Db4). Pitch classes and rhythm unchanged; register is the only change.
- Bars 37-53 (sostenuto): RH certain (Ab-Eb / Ab-E / Ab-F ... and the 4-against-3 bar 45); the trill in bar 50
  is written out briefly (Db-C-Db-C); the LH bass in bar 49 reads B natural in the source (chord: Db7/B).
  LH bar 39 has chords only, as in the source.
- Ornaments omitted: mordents in bars 10/18 and grace notes. Only the sostenuto's first 16 bars are used.
- A2_end bar 36 is arranged as a full Db chord (score: single Db) for a clean ending.
"""

LAYERS = (
    dict(part="melody", voice="piano", gain=0.42, pan=0.56, send=0.3),
    dict(part="left", voice="piano", gain=0.36, pan=0.44, send=0.3, pedal=True),
)
