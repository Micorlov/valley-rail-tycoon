using System;
using UnityEngine;
namespace ValleyRail
{
    /// <summary>Plays the classical playlist on the persistent bootstrap object so the music carries across scene loads:
    /// the opening piece first, then a shuffle with a short silence between pieces. Full level on the title screen,
    /// lowered during play and faded out, then paused, when muted. The pieces are public-domain compositions
    /// synthesized by Tools/compose_music.py, which also writes the playlist.</summary>
    public sealed class ThemeMusic : MonoBehaviour
    {
        public const string Folder = "Audio/Music/", PlaylistPath = Folder + "playlist";
        public const float TitleVolume = .6f, GameVolume = .25f, FadePerSecond = .4f, GapSeconds = 2;
        public GameBootstrap app;
        public AudioSource Source
        {
            get; private set;
        }
        public MusicTrack[] Tracks
        {
            get; private set;
        }
        public MusicTrack CurrentTrack => current >= 0 ? Tracks[current] : null;
        public float TargetVolume => !app || !app.MusicEnabled ? 0 : app.InMenu ? TitleVolume : GameVolume;
        MusicShuffle shuffle;
        int current = -1;
        bool paused;
        float silence;
        void Awake()
        {
            Tracks = LoadPlaylist();
            if (Tracks.Length == 0)
            {
                Debug.LogWarning($"The music playlist is missing from Resources/{PlaylistPath}. Render it with python3 Tools/compose_music.py.");
                enabled = false;
                return;
            }
            shuffle = new MusicShuffle(Tracks.Length, Environment.TickCount);
            Source = gameObject.AddComponent<AudioSource>();
            Source.loop = false;
            Source.playOnAwake = false;
            Source.priority = 0;
            Source.volume = 0;
        }
        public static MusicTrack[] LoadPlaylist()
        {
            var asset = Resources.Load<TextAsset>(PlaylistPath);
            var playlist = asset ? JsonUtility.FromJson<MusicPlaylist>(asset.text) : null;
            return playlist?.tracks ?? Array.Empty<MusicTrack>();
        }
        void Update()
        {
            float target = TargetVolume;
            Source.volume = Mathf.MoveTowards(Source.volume, target, FadePerSecond * Time.unscaledDeltaTime);
            if (target == 0)
            {
                if (Source.volume == 0 && Source.isPlaying)
                {
                    Source.Pause();
                    paused = true;
                }
                return;
            }
            if (paused)
            {
                // Resume where the music paused rather than restarting the piece.
                Source.UnPause();
                paused = false;
            }
            else if (!Source.isPlaying && (current < 0 || (silence += Time.unscaledDeltaTime) >= GapSeconds))
                Next();
        }
        /// <summary>Starts the next piece of the shuffle and releases the previous one.</summary>
        public void Next()
        {
            var previous = Source.clip;
            current = shuffle.Next();
            Source.clip = Resources.Load<AudioClip>(Folder + Tracks[current].file);
            if (previous && previous != Source.clip)
                Resources.UnloadAsset(previous);
            silence = 0;
            paused = false;
            if (Source.clip)
                Source.Play();
            else
                Debug.LogWarning($"Music track {Tracks[current].file} is missing from Resources/{Folder}.");
        }
    }
    [Serializable]
    public sealed class MusicTrack
    {
        public string file, title, composer;
        public int year;
    }
    [Serializable]
    sealed class MusicPlaylist
    {
        public MusicTrack[] tracks;
    }
    /// <summary>Order of play: track 0 opens, then every track once per cycle in random order, and never the same track
    /// twice in a row across a reshuffle.</summary>
    public sealed class MusicShuffle
    {
        readonly int[] order;
        readonly System.Random random;
        int position = -1, last = -1;
        public MusicShuffle(int count, int seed)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "The playlist needs at least one track.");
            random = new System.Random(seed);
            order = new int[count];
            for (int i = 0; i < count; i++)
                order[i] = i;
            Shuffle(1);
        }
        public int Next()
        {
            if (++position == order.Length)
            {
                Shuffle(0);
                if (order.Length > 1 && order[0] == last)
                    Swap(0, random.Next(1, order.Length));
                position = 0;
            }
            return last = order[position];
        }
        void Shuffle(int from)
        {
            for (int i = order.Length - 1; i > from; i--)
                Swap(i, random.Next(from, i + 1));
        }
        void Swap(int a, int b) => (order[a], order[b]) = (order[b], order[a]);
    }
}
