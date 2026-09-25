"""Checks for the music tools: notation, chords, patterns, tempo, every piece, and a short render.

    python3 -m unittest discover -s Tools/music/tests -t Tools
"""
import array
import math
import unittest
from fractions import Fraction

from music import arrange, beat808, notation, synth
from music.pieces import PLAYLIST

MIN_SECONDS, MAX_SECONDS = 55, 120


class PitchTests(unittest.TestCase):
    def test_spelling(self):
        self.assertEqual(notation.pitch("c4"), 60)
        self.assertEqual(notation.pitch("a4"), 69)
        self.assertEqual(notation.pitch("bb3"), 58)
        self.assertEqual(notation.pitch("b3"), 59)
        self.assertEqual(notation.pitch("f##4"), 67)
        self.assertEqual(notation.pitch("cb4"), 59)

    def test_rejects_nonsense(self):
        with self.assertRaises(ValueError):
            notation.pitch("h4")


class NoteParsingTests(unittest.TestCase):
    def test_durations_carry_over_and_rests_advance(self):
        line = notation.parse_notes("d4:1 f#4 a4 | a4:2 r:1 |", 3)
        self.assertEqual([n.pitch for n in line.notes], [62, 66, 69, 69])
        self.assertEqual([n.start for n in line.notes], [0, 1, 2, 3])
        self.assertEqual(line.notes[-1].beats, 2)
        self.assertEqual((line.beats, line.bars), (6, 2))

    def test_chords_ties_staccato_and_dynamics(self):
        line = notation.parse_notes("!p a3+c#4:1/2 e4:1/2' g4:1~ | g4:1/2 r:3/2 |", 2)
        a, c, e, g = line.notes
        self.assertEqual((a.start, c.start, a.pitch, c.pitch), (0, 0, 57, 61))
        self.assertEqual(e.beats, Fraction(1, 4), "staccato sounds for half its length")
        self.assertEqual((g.start, g.beats), (1, Fraction(3, 2)), "the tie joins across the bar line")
        self.assertTrue(all(n.velocity == notation.DYNAMICS["p"] for n in line.notes))

    def test_pickup_and_triplets(self):
        line = notation.parse_notes("e5:1/2 d#5 | e5:1/3 d#5 e5 b4:1 d5:1 |", 3, pickup=1)
        self.assertEqual(line.beats, 4)

    def test_bar_that_does_not_fit_names_the_bar(self):
        with self.assertRaisesRegex(ValueError, "bar 2 lasts 5/2 beats"):
            notation.parse_notes("c4:3 | d4:1 e4:3/2 |", 3)

    def test_comments_are_ignored(self):
        line = notation.parse_notes("% opening\nc4:4 | % held\n", 4)
        self.assertEqual(len(line.notes), 1)


class ChordTests(unittest.TestCase):
    def test_symbols(self):
        self.assertEqual(notation.parse_chord("D").classes, (2, 6, 9))
        self.assertEqual(notation.parse_chord("Bm").classes, (2, 6, 11))
        seventh = notation.parse_chord("A7/C#")
        self.assertEqual((seventh.root, seventh.bass, seventh.classes), (9, 1, (1, 4, 7, 9)))
        self.assertEqual(notation.parse_chord("F#m7b5").classes, (0, 4, 6, 9))
        self.assertEqual(notation.parse_chord("Dbmaj7").root, 1)
        self.assertIsNone(notation.parse_chord("N"))

    def test_bars_split_evenly_unless_timed(self):
        spans, beats = notation.parse_chords("D | Em A7 | G:2 D/F#:1 |", 3)
        self.assertEqual(beats, 9)
        self.assertEqual([s.beats for s in spans], [3, Fraction(3, 2), Fraction(3, 2), 2, 1])

    def test_chord_bar_must_fit(self):
        with self.assertRaises(ValueError):
            notation.parse_chords("D:2 A:2 |", 3)


class PatternTests(unittest.TestCase):
    def spans(self, text, meter):
        return notation.parse_chords(text, meter)[0]

    def test_waltz_has_bass_on_one_and_chords_after(self):
        notes = notation.accompany(self.spans("D |", 3), "waltz", 3, bass_low=38)
        bass = [n for n in notes if n.role == "bass"]
        self.assertEqual([(n.start, n.pitch) for n in bass], [(0, 38)])
        self.assertEqual(sorted({n.start for n in notes if n.role == "chord"}), [1, 2])

    def test_waltz_follows_the_pickup(self):
        notes = notation.accompany(notation.parse_chords("D | D |", 3, pickup=1)[0], "waltz", 3, pickup=1)
        self.assertEqual([n.start for n in notes if n.role == "bass"], [1])

    def test_oompah_and_stride_alternate_root_and_fifth(self):
        notes = notation.accompany(self.spans("C |", 2), "oompah", 2, bass_low=36)
        self.assertEqual([n.pitch for n in notes if n.role == "bass"], [36, 43])
        stride = notation.accompany(self.spans("C |", 2), "stride", 2, bass_low=36)
        self.assertEqual(sorted(n.pitch for n in stride if n.role == "bass" and n.start == 0), [24, 36])

    def test_alberti_is_low_high_middle_high(self):
        notes = notation.accompany(self.spans("C |", 2), "alberti", 2, center=60)
        self.assertEqual([n.pitch for n in notes], [60, 67, 64, 67])

    def test_block_and_bass_hold_the_span(self):
        notes = notation.accompany(self.spans("G/B |", 4), "block", 4)
        self.assertTrue(all(n.beats == 4 for n in notes))
        bass = notation.accompany(self.spans("G/B |", 4), "bass", 4, bass_low=36)
        self.assertEqual([n.pitch for n in bass], [47])

    def test_no_chord_is_silent(self):
        self.assertEqual(notation.accompany(self.spans("N |", 3), "waltz", 3), ())


class TempoTests(unittest.TestCase):
    def test_constant_tempo(self):
        self.assertAlmostEqual(arrange.TempoMap([(Fraction(0), 120.0)]).seconds(8), 4.0)

    def test_linear_slowdown_takes_longer_and_stays_monotonic(self):
        tempo = arrange.TempoMap([(Fraction(0), 120.0), (Fraction(4), 120.0), (Fraction(8), 60.0)])
        self.assertAlmostEqual(tempo.seconds(4), 2.0)
        self.assertGreater(tempo.seconds(8), 4.0)
        self.assertLess(tempo.seconds(8), 6.0)
        times = [tempo.seconds(Fraction(b, 4)) for b in range(0, 49)]
        self.assertEqual(times, sorted(times))


class PieceTests(unittest.TestCase):
    """Every piece parses, fits the meter, stays in its instruments' ranges and lasts a sensible time."""

    @classmethod
    def setUpClass(cls):
        cls.pieces = [arrange.load(key) for key in PLAYLIST]

    def test_playlist_is_sixteen_distinct_pieces_starting_with_the_blue_danube(self):
        self.assertEqual(len(self.pieces), 16)
        self.assertEqual(len({p.file for p in self.pieces}), 16)
        self.assertEqual(self.pieces[0].title, "The Blue Danube")
        for piece in self.pieces:
            self.assertTrue(piece.title and piece.composer, piece.key)
            self.assertLess(piece.year, 1930, f"{piece.key} must be long out of copyright")

    def test_durations(self):
        for piece in self.pieces:
            with self.subTest(piece=piece.key):
                self.assertTrue(MIN_SECONDS <= arrange.duration(piece) <= MAX_SECONDS, f"{arrange.duration(piece):.1f} s")

    def test_notes_stay_in_range(self):
        for piece in self.pieces:
            notes, _ = arrange.events(piece)
            self.assertTrue(notes, piece.key)
            for event in notes:
                voice = piece.layers[event.layer].voice
                low, high = synth.RANGES[voice]
                self.assertTrue(low <= event.pitch <= high, f"{piece.key}: {voice} note {event.pitch} at beat {event.start}")

    def test_layers_only_name_form_entries_that_exist(self):
        for piece in self.pieces:
            for layer in piece.layers:
                for index in layer.form or ():
                    self.assertLess(index, len(piece.form), f"{piece.key}: {layer}")


class Beat808Tests(unittest.TestCase):
    def test_groove_follows_meter_and_tempo(self):
        common = beat808.groove(4, 100)  # 2.4 s bars: backbeat on 2 and 4, kick on 1 and the and of 3
        self.assertEqual((common.kicks, common.snares, common.cycle, common.hat), ((0, Fraction(5, 2)), (1, 3), 4, Fraction(1, 2)))
        self.assertEqual(beat808.groove(4, 140).snares, (2,), "quick 4/4 falls to a half-time snare")
        waltz = beat808.groove(3, 172)  # 1.05 s bars share one kick and one snare across two bars
        self.assertEqual((waltz.kicks, waltz.snares, waltz.cycle), ((0,), (3,), 6))
        slow = beat808.groove(3, 72)
        self.assertEqual((slow.kicks, slow.snares, slow.hat), ((0,), (2,), Fraction(1, 4)))
        nocturne = beat808.groove(12, 105)  # compound: the second kick lands on the third pulse's last eighth
        self.assertEqual((nocturne.kicks, nocturne.snares), ((0, 8), (3, 9)))
        self.assertEqual(beat808.groove(9, 105).kicks, (0, 6), "long three-pulse bars get a second kick")
        with self.assertRaises(ValueError):
            beat808.groove(5, 120)

    def test_every_piece_has_a_beat_that_leaves_the_last_bar_to_the_final_chord(self):
        for key in PLAYLIST:
            piece = arrange.load(key)
            notes, total = arrange.events(piece)
            voice = lambda e: piece.layers[e.layer].voice
            kicks = [e for e in notes if voice(e) == "kick808"]
            self.assertGreater(len(kicks), 8, key)
            last_bar = total - piece.meter
            tail = [e for e in notes if e.start >= last_bar and voice(e) in ("kick808", "snare808", "clap808", "hat808", "openhat808")]
            self.assertEqual([(e.start, voice(e)) for e in tail], [(last_bar, "kick808")], key)
            self.assertTrue(any(voice(e) == "bass808" and e.start == last_bar for e in notes), key)


class RenderTests(unittest.TestCase):
    def test_balance_sets_the_chosen_layers_relative_to_the_rest(self):
        mix = synth.Mix(1000, ((0.5, 0.0), (0.5, 0.0), (0.5, 0.0)))
        mix.add(0, 0, array.array("d", [0.5] * 1000))
        mix.add(1, 0, array.array("d", [0.1] * 1000))
        mix.add(2, 0, array.array("d", [0.3] * 1000))
        mix.balance([1, 2], -6.0)
        power = [sum(v * v for v in bus) for bus in mix.buses]
        self.assertAlmostEqual(10 * math.log10((power[1] + power[2]) / power[0]), -6.0, places=6)
        self.assertAlmostEqual(power[2] / power[1], 9.0, places=6, msg="the chosen layers keep their own balance")

    def test_short_piece_renders_cleanly_with_every_voice(self):
        section = arrange.parse_section("smoke", {"melody": "c5:1 e5 g5 c6 |", "chords": "C |"}, 4, 0)
        layers = tuple(arrange.Layer(part="melody", voice=voice, gain=0.1) for voice in synth.VOICES)
        piece = arrange.Piece("smoke", "Smoke", "Test", 2026, Fraction(4), 240.0, {"A": section}, ("A",),
                              layers + (arrange.Layer(part="chords", voice="bass", pattern="bass", octave=0),), (), 1.0)
        left, right = arrange.render(piece)
        self.assertEqual(len(left), len(right))
        self.assertTrue(all(math.isfinite(v) and abs(v) <= synth.PEAK_CEILING for v in left))
        self.assertAlmostEqual(synth.rms_of(left, right), synth.TARGET_RMS, delta=0.03)
        self.assertLess(max(abs(v) for v in left[-100:]), 1e-3, "the end fades to silence")


if __name__ == "__main__":
    unittest.main()
