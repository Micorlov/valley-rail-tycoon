"""Humoresque, Op. 101 No. 7: public-domain score transcribed into Tools/music/notation.py notation, with the orchestration in LAYERS."""
TITLE = "Humoresque, Op. 101 No. 7"
COMPOSER = "Antonin Dvorak"
YEAR = 1894
KEY = "G-flat major"
METER = 2
BEAT = "quarter"
BPM = 69
PICKUP = "0"
SECTIONS = {
    "A": {
        "melody": """
            % antecedent: bars 1-6 verified, bars 7-8 half cadence (held note length assumed)
            !p gb4:3/4 ab4:1/4 gb4:3/4 ab4:1/4 |
            bb4:3/4 db5:1/4 eb5:3/4 db5:1/4 |
            gb5:3/4 f5:1/4 ab5:3/4 gb5:1/4 |
            f5:3/4 ab5:1/4 gb5:3/4 eb5:1/4 |
            db5:3/4 db5:1/4 eb5:3/4 db5:1/4 |
            gb5:3/4 eb5:1/4 db5:3/4 bb4:1/4 |
            ab4:2~ |
            ab4:1 r:1 |
        """,
        "chords": """
            Gb | Gb | Gb Db7 | Db7 Cb | Gb/Db | Cb Gb | Db7 | Db7 |
        """,
    },
    "A2": {
        "melody": """
            % consequent: bars 9-12 = bars 1-4, then the tonic close (bars 13-14 reconstructed)
            gb4:3/4 ab4:1/4 gb4:3/4 ab4:1/4 |
            bb4:3/4 db5:1/4 eb5:3/4 db5:1/4 |
            gb5:3/4 f5:1/4 ab5:3/4 gb5:1/4 |
            f5:3/4 ab5:1/4 gb5:3/4 eb5:1/4 |
            db5:3/4 gb5:1/4 bb4:3/4 ab4:1/4 |
            gb4:1 r:1 |
        """,
        "chords": """
            Gb | Gb | Gb Db7 | Db7 Cb | Gb/Db Db7 | Gb |
        """,
    },
    "A2_end": {
        "melody": """
            gb4:3/4 ab4:1/4 gb4:3/4 ab4:1/4 |
            bb4:3/4 db5:1/4 eb5:3/4 db5:1/4 |
            gb5:3/4 f5:1/4 ab5:3/4 gb5:1/4 |
            f5:3/4 ab5:1/4 gb5:3/4 eb5:1/4 |
            db5:3/4 gb5:1/4 bb4:3/4 ab4:1/4 |
            gb4:2 |
        """,
        "chords": """
            Gb | Gb | Gb Db7 | Db7 Cb | Gb/Db Db7 | Gb |
        """,
    },
}
FORM = ["A", "A2", "A", "A2", "A", "A2_end"]
SOURCES = [
    "https://hymnary.org/tune/humoresque_dvorak (tune HUMORESQUE, incipit scale degrees 12123 56517 21721)",
    "https://pianoletternotes.blogspot.com/2018/04/humoreske-by-antonin-dvorak.html (letter-note transcription in C; pitch order and the 3:1 dotted rhythm of the main theme)",
    "https://imslp.org/wiki/8_Humoresques,_Op.101_(Dvo%C5%99%C3%A1k,_Anton%C3%ADn) (key G-flat major, 2/4, Poco lento e grazioso)",
]
NOTES = """LOW-MEDIUM CONFIDENCE - no readable full score (LilyPond/ABC/MusicXML) of Op. 101 No. 7 could be found;
IMSLP only has scans, which the spec says not to download.
Verified: key G-flat major, 2/4, Poco lento e grazioso (IMSLP, Wikipedia). Bars 1-6 pitch order
(1 2 1 2 | 3 5 6 5 | 1' 7 2' 1' | 7 2' 1' 6 | 5 5 6 5 | 1' 6 5 3 | 2) matches two independent sources: the hymnary.org
incipit covers the first 15 notes, and the letter-note transcription covers the whole phrase. The dotted
eighth + sixteenth rhythm comes from the 3:1 spacing in the letter-note timing. The register (theme starting on gb4)
is my choice.
Reconstructed/unverified: the length of the held ab4 in bars 7-8 (the note itself is verified); the consequent's close
in bars 13-14 (db5 db5 -> gb4) is the tune's well-known leap 5-1'-3-2-1 (Db-Gb-Bb-Ab-Gb, corrected on review) in the dotted rhythm;
all chord symbols are my own harmonisation (Gb / Db7 / Cb), not taken from Dvorak's left hand. No "left" part is
given because the original accompaniment could not be verified. The minor middle section (and the major-key
second strain) is omitted because I could not verify it. The main theme is stated three times to reach ~73 s."""

# The violin-and-piano setting: violin tune, lilting piano accompaniment, a flute an octave up in the middle.
LAYERS = (
    dict(part="melody", voice="strings", gain=0.34, pan=0.46, send=0.4),
    dict(part="melody", voice="flute", gain=0.12, pan=0.62, send=0.45, octave=1, form=(2, 3)),
    dict(part="chords", voice="piano", pattern="oompah", unit="1/2", gain=0.26, pan=0.52, send=0.3, center=58, bass_low=39),
    dict(part="chords", voice="pad", pattern="block", gain=0.025, center=62),
)
