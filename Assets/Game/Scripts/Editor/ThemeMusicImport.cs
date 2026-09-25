using System;
using UnityEditor;
using UnityEngine;
namespace ValleyRail.Editor
{
    /// <summary>Music under Resources/Audio streams as Vorbis. Unity's default, decompress on load, would hold each
    /// one-to-two-minute stereo piece as 10-20 MB of PCM in memory. Sound effects under Resources/Audio/Sfx are the opposite:
    /// short clips that must start instantly, so they are preloaded and decompressed as mono ADPCM. A clip whose name
    /// ends in "_loop" stays PCM, because ADPCM block padding can click at the loop point.</summary>
    public sealed class ThemeMusicImport : AssetPostprocessor
    {
        const string MusicFolder = "Assets/Game/Resources/Audio/", SfxFolder = MusicFolder + "Sfx/";
        public override uint GetVersion() => 2;
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(MusicFolder, StringComparison.Ordinal))
                return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            if (assetPath.StartsWith(SfxFolder, StringComparison.Ordinal))
            {
                importer.forceToMono = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = true;
                settings.compressionFormat = assetPath.EndsWith("_loop.wav", StringComparison.Ordinal) ? AudioCompressionFormat.PCM : AudioCompressionFormat.ADPCM;
            }
            else
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .5f;
            }
            importer.defaultSampleSettings = settings;
        }
    }
}
