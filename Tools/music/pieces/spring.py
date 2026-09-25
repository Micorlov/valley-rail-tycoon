"""La primavera (Spring), RV 269 - I. Allegro: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "La primavera (Spring), RV 269 - I. Allegro"
COMPOSER = "Antonio Vivaldi"
YEAR = 1725
KEY = "E major"
METER = 4
BEAT = "quarter"
BPM = 100
PICKUP = "1/2"
SECTIONS = {
    "Rit1": {
        "melody": """
            e5:1/2 |
            % bars 1-3: ritornello, forte
            g#5:1/2 g#5 g#5 f#5:1/4 e5 b5:3/2 b5:1/4 a5 |
            g#5:1/2 g#5 g#5 f#5:1/4 e5 b5:3/2 b5:1/4 a5 |
            g#5:1/2 a5:1/4 b5 a5:1/2 g#5 f#5 d#5 b4 !p e5 |
            % bars 4-6: piano echo
            g#5:1/2 g#5 g#5 f#5:1/4 e5 b5:3/2 b5:1/4 a5 |
            g#5:1/2 g#5 g#5 f#5:1/4 e5 b5:3/2 b5:1/4 a5 |
            g#5:1/2 a5:1/4 b5 a5:1/2 g#5 f#5:1 r:1/2 !f e5:1/2 |
            % bars 7-9: second phrase, forte (cadential trill written out)
            b5:1/2 a5:1/4 g#5 a5:1/2 b5 c#6 b5:1 e5:1/2 |
            b5:1/2 a5:1/4 g#5 a5:1/2 b5 c#6 b5:1 e5:1/2 |
            c#6:1/2 b5:1 a5:1/2 g#5 f#5:1/4 e5 g#5 f#5 g#5 f#5 |
            % bars 10-12: piano echo
            e5:1 r:1/2 !p e5:1/2 b5 a5:1/4 g#5 a5:1/2 b5 |
            c#6:1/2 b5:1 e5:1/2 b5 a5:1/4 g#5 a5:1/2 b5 |
            c#6:1/2 b5:1 e5:1/2 c#6 b5:1 a5:1/2 |
        """,
        "chords": """
            E |
            E | E | E:1 A:1 B:2 |
            E | E | E:1 A:1 B:2 |
            E E A/E E | E E A/E E | A/E E E B7 |
            E | A/E:1 E:3 | A/E E A/E E |
        """,
        "left": """
            e3:1/2 |
            e3:1 e3 e3 e3 |
            e3:1 e3 e3 e3 |
            e3:1/2 e3 a2 a#2 b2:1 r:1/2 e3 |
            e3:1 e3 e3 e3 |
            e3:1 e3 e3 e3 |
            e3:1/2 e3 a2 a#2 b2:1 r:1/2 e3 |
            e3:1 e3 e3 e3 |
            e3:1 e3 e3 e3 |
            e3:1 e3:1/2 b2 e3 e2 b3 b2 |
            e2:1 r:1/2 e3 e3:1 e3 |
            e3:1 e3 e3 e3 |
            e3:1 e3 e3 e3 |
        """,
    },
    "Birds": {
        "melody": """
            % bar 13: ritornello cadence, then the solo violin starts the birdsong ("Canto de gl'Uccelli")
            g#5:1/2 f#5:1/4 e5 g#5 f#5 g#5 f#5 !mf b5:1/8 a5 b5:3/4 b5:1/8 a5 b5:3/4 |
            b5:1/8 a5 b5:3/4 b5:1/8 a5 b5:3/4 b5:1/8 a5 b5:3/4 b5:1/8 a5 b5:3/4 |
            b5:1/2' b5' b5' b5' b5' b5' b5' b5' |
            b5:1/2' b5' b5' b5' b5' b5' b5' c#6:1/4 d#6 |
            % bars 17-19: scale, then the first violins' repeated-note answer (solo rests)
            e6:1/4 d#6 c#6 b5 a5 g#5 f#5 e5 b5:1/2' b5' b5' b5' |
            b5:1/2' b5' b5' b5' b5' b5' b5' c#6:1/4 d#6 |
            e6:1/4 d#6 c#6 b5 a5 g#5 f#5 e5 e6:1/2' e6' e6' e6' |
            % bars 20-23: trills (written out as 32nds) and the b-e-b-c# chirps
            e5:1/8 f#5 e5 f#5 e5 f#5 e5 f#5 e5 f#5 e5:1/4 e6:1/2' e6' e6' e6' e6' |
            e5:1/8 f#5 e5 f#5 e5 f#5 e5:1/4 r:1 b5:1~ b5:1/4 e6 b5 c#6 |
            b5:1~ b5:1/4 e6 b5 c#6 b5 e6 b5 c#6 b5 e6 b5 c#6 |
            b5:1/4 e6 b5 c#6 b5 e6 b5 c#6 b5:1/2 e5 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 |
            % bars 24-27: trilled calls, high e6
            r:1 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 r:1 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 |
            r:1 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 e6:1 e6 |
            e6:1/8 f#6 e6 f#6 e6 f#6 e6 f#6 e6 f#6 e6 f#6 e6 f#6 e6:1/4 e6:1 e6 |
            e6:1/8 f#6 e6 f#6 e6 f#6 e6 f#6 e6 f#6 e6 f#6 e6 f#6 e6:1/4 r:1 r:1/2 !f e5:1/2 |
        """,
        "counter": """
            % second violins' bird calls (bar 13: tutti resolution to e5)
            r:2 e5:1 r:1 |
            r:1/4 a5 g#5 a5 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 r:1/4 a5 g#5 a5 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 |
            r:1/2 b5:1/8 a5 g#5 f#5 e5:1 r:1/2 b5:1/8 a5 g#5 f#5 e5:1 |
            r:1/4 a5 g#5 a5 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 r:1/4 a5 g#5 a5 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 |
            r:5/2 b5:1/8 a5 g#5 f#5 e5:1 |
            r:1/2 b5:1/8 a5 g#5 f#5 e5:1 r:1/2 b5:1/8 a5 g#5 f#5 e5:1 |
            r:2 g#5:3/4 a5:1/4 g#5:3/4 a5:1/4 |
            g#5:3/4 a5:1/4 g#5:3/4 a5:1/4 g#5 a5 g#5 a5 g#5 a5 g#5 a5 |
            g#5:1/8 a5 g#5 a5 g#5 a5 g#5 a5 g#5 a5 g#5 a5 g#5 a5 g#5 a5 g#5 a5 g#5 a5 g#5 a5 g#5:1/4 r:1 |
            g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 r:1 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 r:1 |
            g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 r:1 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 r:1 |
            g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 r:1 g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 r:1 |
            g#5:1/8 a5 g#5 a5 g#5 a5 g#5:1/4 r:1 e5:1~ e5:1/4 b5 e5 f#5 |
            e5:1~ e5:1/4 b5 e5 f#5 e5 b5 e5 f#5 e5 b5 e5 f#5 |
            e5:1/4 b5 e5 f#5 e5 b5 e5 f#5 e5:1 r:1/2 e5:1/2 |
        """,
        "chords": """
            E:1 B7:1 E:2 |
            E | E | E | E | E | E | E | E | E | E | E | E | E | E |
        """,
        "left": """
            e3:1/2 e2 b3 b2 e2:1 r |
            r:4 | r:4 | r:4 | r:4 | r:4 | r:4 | r:4 | r:4 | r:4 | r:4 | r:4 | r:4 | r:4 |
            r:2 r:1 r:1/2 e3:1/2 |
        """,
    },
    "Rit2": {
        "melody": """
            % bars 28-31: tutti ritornello returns and closes on the tonic
            b5:1/2 a5:1/4 g#5 a5:1/2 b5 c#6 b5:1 e5:1/2 |
            b5:1/2 a5:1/4 g#5 a5:1/2 b5 c#6 b5:1 e5:1/2 |
            c#6:1/2 b5:1 a5:1/2 g#5 f#5:1/4 e5 g#5 f#5 g#5 f#5 |
            e5:2 r:2 |
        """,
        "chords": """
            E E A/E E | E E A/E E | A/E E E B7 | E |
        """,
        "left": """
            e3:1 e3 e3 e3 |
            e3:1 e3 e3 e3 |
            e3:1 e3:1/2 b2 e3 e2 b3 b2 |
            e2:2 r:2 |
        """,
    },
}
FORM = ["Rit1", "Birds", "Rit2"]
SOURCES = [
    "https://www.mutopiaproject.org/ftp/VivaldiA/O8/spring/spring-lys.zip (Mutopia edition of Op. 8 No. 1; parts read via https://github.com/MutopiaProject/MutopiaProject/tree/master/ftp/VivaldiA/O8/spring/spring-lys : spring1.ly violino principale, spring1a.ly violino primo, spring1b.ly violino secondo, spring1d.ly violoncello)",
]
NOTES = """All pitches/rhythms come from the Mutopia parts of the 1st movement (bars 1-31): melody = violino principale,
except bar 17 beats 3-4, bar 18 and bar 19 beats 1-2, where the soloist rests and the melody follows the first
violins' answering bird figure (b5 repeated eighths and the e6-e5 scale); counter = second violins' bird calls
(bars 14-27, plus the tutti e5 resolution in bar 13); left = the cello part (tonic pedal; tacet during the birds).
Simplifications: all trills/mordents are written out simply (cadential f#5 trills as four 16ths g#-f#-g#-f#;
bird trills as 32nd-note alternations ending on the main note; the solo's mordent quarters on b5 as b-a-b).
Chords are my reading of the parts (E pedal; A/E and B7 on the cadences; the bird episode is a pure E major
prolongation over a silent bass). The arrangement stops at bar 31 (start of the "brook" episode) with the bar-31
tonic held as a final chord. Bar 13 contains both the ritornello cadence and the start of the birdsong (as in
the score). No bars reconstructed; the ritornello and birdsong were verified in both the principale and primo parts."""

# Solo violin over the orchestra; the second violins' bird calls on a flute; cello and harpsichord continuo.
LAYERS = (
    dict(part="melody", voice="strings", gain=0.34, pan=0.44, send=0.35),
    dict(part="counter", voice="flute", gain=0.2, pan=0.66, send=0.45),
    dict(part="left", voice="strings", gain=0.24, pan=0.54, send=0.25),
    dict(part="left", voice="bass", gain=0.12, pan=0.5, send=0.1, octave=-1),
    dict(part="chords", voice="harpsichord", pattern="pulse", unit=1, gain=0.06, pan=0.3, send=0.3, center=64),
    dict(part="chords", voice="pad", pattern="block", gain=0.03, center=62),
)
