"""Für Elise: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Für Elise"
COMPOSER = "Ludwig van Beethoven"
YEAR = 1810
KEY = "A minor"
METER = 3
BEAT = "eighth"
BPM = 136
PICKUP = "1"
SECTIONS = {
    # Main theme, bars 1-8 with the first ending (pickup E-D# written into the last bar).
    "A": {
        "melody": """
            !pp e5:1/2 d#5 |                % pickup
            e5 d#5 e5 b4 d5 c5 |            % 1
            a4:1 r:1/2 c4 e4 a4 |           % 2
            b4:1 r:1/2 e4 g#4 b4 |          % 3
            c5:1 r:1/2 e4 e5 d#5 |          % 4
            e5 d#5 e5 b4 d5 c5 |            % 5
            a4:1 r:1/2 c4 e4 a4 |           % 6
            b4:1 r:1/2 e4 c5 b4 |           % 7
            a4:2 e5:1/2 d#5 |               % 8 first ending + pickup into the repeat
        """,
        "chords": """
            N | N | Am | E | Am | N | Am | E | Am |
        """,
        "left": """
            r:1 |                           % pickup
            r:3 |                           % 1
            a2:1/2 e3 a3 r:3/2 |            % 2
            e2:1/2 e3 g#3 r:3/2 |           % 3
            a2:1/2 e3 a3 r:3/2 |            % 4
            r:3 |                           % 5
            a2:1/2 e3 a3 r:3/2 |            % 6
            e2:1/2 e3 g#3 r:3/2 |           % 7
            a2:1/2 e3 a3 r:3/2 |            % 8
        """,
    },
    # Repeat of the theme, second ending (B-C-D) leading into the C-major episode.
    "A2": {
        "melody": """
            e5:1/2 d#5 e5 b4 d5 c5 |        % 1
            a4:1 r:1/2 c4 e4 a4 |           % 2
            b4:1 r:1/2 e4 g#4 b4 |          % 3
            c5:1 r:1/2 e4 e5 d#5 |          % 4
            e5 d#5 e5 b4 d5 c5 |            % 5
            a4:1 r:1/2 c4 e4 a4 |           % 6
            b4:1 r:1/2 e4 c5 b4 |           % 7
            a4:1 r:1/2 b4 c5 d5 |           % 8 second ending
        """,
        "chords": """
            N | Am | E | Am | N | Am | E | Am |
        """,
        "left": """
            r:3 |                           % 1
            a2:1/2 e3 a3 r:3/2 |            % 2
            e2:1/2 e3 g#3 r:3/2 |           % 3
            a2:1/2 e3 a3 r:3/2 |            % 4
            r:3 |                           % 5
            a2:1/2 e3 a3 r:3/2 |            % 6
            e2:1/2 e3 g#3 r:3/2 |           % 7
            a2:1/2 e3 a3 r:3/2 |            % 8
        """,
    },
    # C-major episode (bars 9-15) and the return of the theme (bars 16-22), first ending.
    "B": {
        "melody": """
            e5:3/2 g4:1/2 f5 e5 |           % 9
            d5:3/2 f4:1/2 e5 d5 |           % 10
            c5:3/2 e4:1/2 d5 c5 |           % 11
            b4:1 r:1/2 e4 e5 e4 |           % 12 (last E is the left hand's, see NOTES)
            e5 e5 e6 d#5 e5 d#5 |           % 13 hands alternate: LH e5, RH e5-e6, LH d#5-e5, RH d#5
            e5 d#5 e5 d#5 e5 d#5 |          % 14
            e5 d#5 e5 b4 d5 c5 |            % 15
            a4:1 r:1/2 c4 e4 a4 |           % 16
            b4:1 r:1/2 e4 g#4 b4 |          % 17
            c5:1 r:1/2 e4 e5 d#5 |          % 18
            e5 d#5 e5 b4 d5 c5 |            % 19
            a4:1 r:1/2 c4 e4 a4 |           % 20
            b4:1 r:1/2 e4 c5 b4 |           % 21
            a4:1 r:1/2 b4 c5 d5 |           % 22 first ending (back to bar 9)
        """,
        "chords": """
            C | G | Am | E | N | N | N | Am | E | Am | N | Am | E | Am |
        """,
        "left": """
            c3:1/2 g3 c4 r:3/2 |            % 9
            g2:1/2 g3 b3 r:3/2 |            % 10
            a2:1/2 e3 a3 r:3/2 |            % 11
            e2:1/2 e3 e4 r:3/2 |            % 12
            r:3 |                           % 13
            r:3 |                           % 14
            r:3 |                           % 15
            a2:1/2 e3 a3 r:3/2 |            % 16
            e2:1/2 e3 g#3 r:3/2 |           % 17
            a2:1/2 e3 a3 r:3/2 |            % 18
            r:3 |                           % 19
            a2:1/2 e3 a3 r:3/2 |            % 20
            e2:1/2 e3 g#3 r:3/2 |           % 21
            a2:1/2 e3 a3 r:3/2 |            % 22
        """,
    },
    # Second time through the episode; last bar hands back to the theme with the E-D# pickup.
    "B2": {
        "melody": """
            e5:3/2 g4:1/2 f5 e5 |           % 9
            d5:3/2 f4:1/2 e5 d5 |           % 10
            c5:3/2 e4:1/2 d5 c5 |           % 11
            b4:1 r:1/2 e4 e5 e4 |           % 12
            e5 e5 e6 d#5 e5 d#5 |           % 13
            e5 d#5 e5 d#5 e5 d#5 |          % 14
            e5 d#5 e5 b4 d5 c5 |            % 15
            a4:1 r:1/2 c4 e4 a4 |           % 16
            b4:1 r:1/2 e4 g#4 b4 |          % 17
            c5:1 r:1/2 e4 e5 d#5 |          % 18
            e5 d#5 e5 b4 d5 c5 |            % 19
            a4:1 r:1/2 c4 e4 a4 |           % 20
            b4:1 r:1/2 e4 c5 b4 |           % 21
            a4:2 e5:1/2 d#5 |               % 22 arranged: A + pickup back to the theme
        """,
        "chords": """
            C | G | Am | E | N | N | N | Am | E | Am | N | Am | E | Am |
        """,
        "left": """
            c3:1/2 g3 c4 r:3/2 |            % 9
            g2:1/2 g3 b3 r:3/2 |            % 10
            a2:1/2 e3 a3 r:3/2 |            % 11
            e2:1/2 e3 e4 r:3/2 |            % 12
            r:3 |                           % 13
            r:3 |                           % 14
            r:3 |                           % 15
            a2:1/2 e3 a3 r:3/2 |            % 16
            e2:1/2 e3 g#3 r:3/2 |           % 17
            a2:1/2 e3 a3 r:3/2 |            % 18
            r:3 |                           % 19
            a2:1/2 e3 a3 r:3/2 |            % 20
            e2:1/2 e3 g#3 r:3/2 |           % 21
            a2:1/2 e3 a3 r:3/2 |            % 22
        """,
    },
    # Final statement of the theme, ending on the tonic.
    "A_end": {
        "melody": """
            e5:1/2 d#5 e5 b4 d5 c5 |        % 1
            a4:1 r:1/2 c4 e4 a4 |           % 2
            b4:1 r:1/2 e4 g#4 b4 |          % 3
            c5:1 r:1/2 e4 e5 d#5 |          % 4
            e5 d#5 e5 b4 d5 c5 |            % 5
            a4:1 r:1/2 c4 e4 a4 |           % 6
            b4:1 r:1/2 e4 c5 b4 |           % 7
            a4:3 |                          % 8 final
        """,
        "chords": """
            N | Am | E | Am | N | Am | E | Am |
        """,
        "left": """
            r:3 |                           % 1
            a2:1/2 e3 a3 r:3/2 |            % 2
            e2:1/2 e3 g#3 r:3/2 |           % 3
            a2:1/2 e3 a3 r:3/2 |            % 4
            r:3 |                           % 5
            a2:1/2 e3 a3 r:3/2 |            % 6
            e2:1/2 e3 g#3 r:3/2 |           % 7
            a2:1/2 e3 a3:2 |                % 8 final
        """,
    },
}
FORM = ["A", "A2", "B", "B2", "A_end"]
SOURCES = [
    "https://www.mutopiaproject.org/cgibin/piece-info.cgi?id=931",
    "https://www.mutopiaproject.org/ftp/BeethovenLv/WoO59/fur_Elise_WoO59/fur_Elise_WoO59.ly",
    "https://imslp.org/wiki/F%C3%BCr_Elise,_WoO_59_(Beethoven,_Ludwig_van)",
]
NOTES = """
Bars numbered from the first full bar after the E-D# pickup (the episode the brief calls 'C major, B C D |
E . G F E ...' is bars 9-22 of the score, i.e. the second half of the first part, not the F-major episode).
Verified against the Mutopia LilyPond source (Breitkopf & Haertel 1888 edition, public domain):
- RH bars 1-8 incl. both endings, and bars 9-22 incl. the first ending: certain.
- LH bars 1-22 (A2-E3-A3 / E2-E3-G#3 / C3-G3-C4 / G2-G3-B3 sixteenth figures): certain.
Arranged / simplified:
- Bars 12-14: in the score the two hands alternate the E / D#-E figure (LH climbs into the treble clef up to
  E5). Both hands' notes are merged into one 16th-note stream in "melody" so the left part stays in range;
  the RH's 8th-note E5 at the start of bar 14 becomes a 16th because the LH's D#5 follows it. Pitches unchanged.
- B2 bar 22 is not in the score: the real second ending leads to the F-major episode, which is skipped here;
  instead bar 22 reuses the theme's own first-ending formula (A + E-D# pickup) to return to the theme.
- A_end final bar: the score's last bar is A (8th) + rests; lengthened to a full-bar A with a sustained
  A2-E3-A3 for a clean cadence.
- Dynamics other than the opening pp, pedal marks and the 'N' (no chord) bars follow the score's texture.
"""

LAYERS = (
    dict(part="melody", voice="piano", gain=0.42, pan=0.56, send=0.3, pedal=True),
    dict(part="left", voice="piano", gain=0.36, pan=0.44, send=0.3, pedal=True),
)
