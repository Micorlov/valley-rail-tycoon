using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace ValleyRail.Tests
{
    public class AudioTests
    {
        const string MusicFolder = "Assets/Game/Resources/Audio/Music/", SfxFolder = "Assets/Game/Resources/Audio/Sfx/";
        [Test]
        public void PlaylistListsSixteenPublicDomainClassicsOpeningWithTheBlueDanube()
        {
            var tracks = ThemeMusic.LoadPlaylist();
            Assert.That(tracks.Length, Is.EqualTo(16), "Render the music with python3 Tools/compose_music.py");
            Assert.That(tracks[0].title, Is.EqualTo("The Blue Danube"));
            Assert.That(tracks.Select(t => t.file).Distinct().Count(), Is.EqualTo(tracks.Length));
            foreach (var track in tracks)
            {
                Assert.That(track.title, Is.Not.Empty, track.file);
                Assert.That(track.composer, Is.Not.Empty, track.file);
                Assert.That(track.year, Is.InRange(1600, 1929), track.file + " must be long out of copyright");
            }
        }
        [Test]
        public void EveryMusicTrackStreamsAsVorbisAndLoadsAsStereo()
        {
            foreach (var track in ThemeMusic.LoadPlaylist())
            {
                // Decompress-on-load would hold about 16 MB of PCM per piece in memory on the phone.
                var importer = AssetImporter.GetAtPath($"{MusicFolder}{track.file}.wav") as AudioImporter;
                Assert.That(importer, Is.Not.Null, track.file);
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), track.file);
                Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis), track.file);
                var clip = Resources.Load<AudioClip>(ThemeMusic.Folder + track.file);
                Assert.That(clip, Is.Not.Null, track.file);
                Assert.That(clip.length, Is.InRange(55f, 120f), track.file);
                Assert.That(clip.channels, Is.EqualTo(2), track.file);
            }
        }
        [Test]
        public void RetiredValleyThemeNoLongerShips()
        {
            Assert.That(Resources.Load<AudioClip>("Audio/ValleyTheme"), Is.Null);
        }
        [Test]
        public void ShuffleOpensWithTheFirstTrackThenPlaysEachOncePerCycleNeverTwiceInARow()
        {
            const int count = 16;
            var shuffle = new MusicShuffle(count, 1234);
            int previous = -1;
            for (int cycle = 0; cycle < 50; cycle++)
            {
                var seen = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < count; i++)
                {
                    int track = shuffle.Next();
                    if (cycle == 0 && i == 0)
                        Assert.That(track, Is.Zero, "The opening piece plays first");
                    Assert.That(track, Is.Not.EqualTo(previous), $"cycle {cycle} step {i}");
                    Assert.That(seen.Add(track), Is.True, $"cycle {cycle} repeats track {track}");
                    previous = track;
                }
            }
            var single = new MusicShuffle(1, 7);
            Assert.That(new[] { single.Next(), single.Next() }, Is.EqualTo(new[] { 0, 0 }));
        }
        [Test]
        public void ZoomGainIsFullZoomedInAndSilentZoomedOut()
        {
            Assert.That(SoundEffects.ZoomGain(7), Is.EqualTo(1));
            Assert.That(SoundEffects.ZoomGain(SoundEffects.FullZoom), Is.EqualTo(1));
            Assert.That(SoundEffects.ZoomGain(21), Is.EqualTo(.5f).Within(1e-5f));
            Assert.That(SoundEffects.ZoomGain(SoundEffects.SilentZoom), Is.Zero);
            Assert.That(SoundEffects.ZoomGain(58), Is.Zero, "The new-game overview only hears delivery income");
            Assert.That(SoundEffects.ZoomGain(82), Is.Zero);
            for (float zoom = 7; zoom < 82; zoom += .5f)
                Assert.That(SoundEffects.ZoomGain(zoom + .5f), Is.LessThanOrEqualTo(SoundEffects.ZoomGain(zoom)));
            Assert.That(SoundEffects.SirenGain(SoundEffects.FullZoom), Is.Zero, "Sirens need the camera closer in than other sounds");
            Assert.That(SoundEffects.SirenGain(CameraController.MinZoom), Is.EqualTo(1), "Zoomed all the way in: the siren at full volume");
            Assert.That(SoundEffects.SirenGain(CameraController.MinZoom + 2), Is.Zero, "Only the closest zoom hears sirens");
        }
        [Test]
        public void SoundEffectsImportPreloadedMonoAndDecompressed()
        {
            // Streaming would delay every effect; the loops stay PCM because ADPCM padding can click at the loop point.
            foreach (var name in SoundEffects.Clips.Concat(new[] { SoundEffects.LoopClip, SoundEffects.SirenClip }))
            {
                var importer = AssetImporter.GetAtPath($"{SfxFolder}{name}.wav") as AudioImporter;
                Assert.That(importer, Is.Not.Null, "Render the effects with python3 Tools/compose_sfx.py: " + name);
                var settings = importer.defaultSampleSettings;
                Assert.That(importer.forceToMono, Is.True, name);
                Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), name);
                Assert.That(settings.preloadAudioData, Is.True, name);
                Assert.That(settings.compressionFormat, Is.EqualTo(name.EndsWith("_loop") ? AudioCompressionFormat.PCM : AudioCompressionFormat.ADPCM), name);
            }
        }
        [Test]
        public void EverySoundCueHasItsOwnShortMonoClip()
        {
            var cues = (SoundCue[])Enum.GetValues(typeof(SoundCue));
            Assert.That(SoundEffects.Clips.Length, Is.EqualTo(cues.Length));
            foreach (var cue in cues)
            {
                // Each clip is named after its cue in snake case, which catches a reordered table.
                string name = SoundEffects.Clips[(int)cue];
                Assert.That(name, Is.EqualTo(Regex.Replace(cue.ToString(), "(?<=[a-z])([A-Z])", "_$1").ToLowerInvariant()));
                var clip = Resources.Load<AudioClip>(SoundEffects.Folder + name);
                Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.channels, Is.EqualTo(1), name);
                Assert.That(clip.length, Is.InRange(.1f, 3f), name);
            }
            foreach (var name in new[] { SoundEffects.LoopClip, SoundEffects.SirenClip })
            {
                var loop = Resources.Load<AudioClip>(SoundEffects.Folder + name);
                Assert.That(loop, Is.Not.Null, name);
                Assert.That(loop.channels, Is.EqualTo(1), name);
                Assert.That(loop.length, Is.InRange(1f, 5f), name);
            }
        }
    }
}
