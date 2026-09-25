using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    public enum SoundCue
    {
        Coins, CoinsBig, StationBuilt, TrackBuilt, Bulldoze, TrainBought, RouteStart, TrainDepart, TrainArrive, TownLevelUp, Error
    }
    /// <summary>
    /// Plays the synthesized effects in Resources/Audio/Sfx (rendered by Tools/compose_sfx.py). Delivery income is
    /// audible at any zoom; every other sound needs the camera zoomed in (<see cref="ZoomGain"/>), and a sound with a
    /// place on the map also needs that place on screen, panned to where it is. Runs after every other script, so a
    /// frame's button callbacks, simulation ticks and camera move have all happened before the events are drained.
    /// Never logs: PlayMode tests fail on any unexpected log.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class SoundEffects : MonoBehaviour
    {
        /// <summary>Orthographic sizes: full volume at or below FullZoom, silent at or above SilentZoom (the camera spans CameraController.MinZoom..MaxZoom).</summary>
        public const float FullZoom = 16, SilentZoom = 26;
        /// <summary>Sirens are heard only with the camera zoomed all the way in (<see cref="CameraController.MinZoom"/>): full volume at or below SirenFullZoom, silent at or above SirenSilentZoom.</summary>
        public const float SirenFullZoom = CameraController.MinZoom + .5f, SirenSilentZoom = CameraController.MinZoom + 2;
        /// <summary>Revenue from which one delivery plays the coin cascade instead of a single cha-ching.</summary>
        public const int BigDelivery = 3000;
        public const string Folder = "Audio/Sfx/", LoopClip = "rail_loop", SirenClip = "siren_loop";
        public static readonly string[] Clips = { "coins", "coins_big", "station_built", "track_built", "bulldoze", "train_bought", "route_start", "train_depart", "train_arrive", "town_level_up", "error" };
        // Seconds before the same cue may sound again; all exceed one frame, so a cue plays at most once per frame.
        static readonly float[] Cooldown = { .3f, .3f, .1f, .1f, .1f, .2f, .2f, .4f, .4f, .5f, .15f };
        const int Voices = 8, LoopTrains = 3;
        const float ScreenMargin = .1f, PanWidth = 1.2f, MaxPan = .6f, LoopFadePerSecond = 1.5f;
        // A siren at the screen's edge plays at SirenAtEdge of its volume in the middle; its pan glides at PanPerSecond.
        const float SirenAtEdge = .35f, PanPerSecond = 2f;
        public GameBootstrap app;
        public SoundCue LastCue { get; private set; }
        public AudioSource Loop { get; private set; }
        public AudioSource Siren { get; private set; }
        readonly int[] played = new int[Clips.Length];
        readonly float[] lastPlayed = new float[Clips.Length];
        AudioClip[] clips; AudioSource[] voices; int nextVoice;
        /// <summary>How many times a cue has sounded; tests read this because a short clip's isPlaying depends on timing.</summary>
        public int Played(SoundCue cue) => played[(int)cue];
        public static float ZoomGain(float zoom) => Mathf.InverseLerp(SilentZoom, FullZoom, zoom);
        public static float SirenGain(float zoom) => Mathf.InverseLerp(SirenSilentZoom, SirenFullZoom, zoom);
        /// <summary>Volume for interface sounds: menus have no map to zoom, so they are always audible there.</summary>
        public float UiGain => !app || !app.Camera || app.InMenu || app.MenuOpen ? 1 : ZoomGain(app.Camera.zoom);
        bool WorldAudible => app && app.SoundEnabled && !app.InMenu && !app.MenuOpen && app.Camera;
        void Awake()
        {
            clips = new AudioClip[Clips.Length];
            for (int i = 0; i < Clips.Length; i++)
            {
                clips[i] = Resources.Load<AudioClip>(Folder + Clips[i]);
                lastPlayed[i] = float.NegativeInfinity;
            }
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
                voices[i] = NewSource(128);
            Loop = NewSource(160);
            Loop.clip = Resources.Load<AudioClip>(Folder + LoopClip);
            Loop.loop = true;
            Loop.volume = 0;
            Siren = NewSource(150);
            Siren.clip = Resources.Load<AudioClip>(Folder + SirenClip);
            Siren.loop = true;
            Siren.volume = 0;
        }
        AudioSource NewSource(int priority)
        {
            // Sources sit on the persistent bootstrap object: BuildSession destroys its children on every new session.
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0; // 2D: the listener sits 180 units behind the orthographic focus, so distance means nothing
            source.priority = priority;
            return source;
        }
        /// <summary>An interface sound (the error buzz): zoom-gated over the map, always audible in menus.</summary>
        public void Ui(SoundCue cue)
        {
            if (app && app.SoundEnabled)
                Play(cue, UiGain, 0);
        }
        /// <summary>A sound at a map cell: needs the camera zoomed in and the cell on screen.</summary>
        public void World(SoundCue cue, Cell at)
        {
            if (WorldAudible && OnScreen(Ground(at), out float pan))
                Play(cue, ZoomGain(app.Camera.zoom), pan);
        }
        void World(SoundCue cue, Cell a, Cell b)
        {
            if (!WorldAudible)
                return;
            if (OnScreen(Ground(a), out float pan) || OnScreen(Ground(b), out pan))
                Play(cue, ZoomGain(app.Camera.zoom), pan);
        }
        void Play(SoundCue cue, float volume, float pan)
        {
            int i = (int)cue;
            if (!clips[i] || volume <= 0 || Time.unscaledTime - lastPlayed[i] < Cooldown[i])
                return;
            lastPlayed[i] = Time.unscaledTime;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            voice.panStereo = pan;
            voice.PlayOneShot(clips[i], volume);
            played[i]++;
            LastCue = cue;
        }
        void LateUpdate()
        {
            var game = app ? app.Game : null;
            if (game == null)
                return;
            // Drain everything even when silent, so events from a muted or paused moment never play later.
            bool audible = WorldAudible;
            int income = 0;
            while (game.Events.TryDequeue(out var e))
            {
                if (!audible)
                    continue;
                switch (e.kind)
                {
                    case GameEventKind.CargoDelivered:
                        income = Mathf.Max(income, e.value);
                        break;
                    case GameEventKind.StationBuilt:
                        World(SoundCue.StationBuilt, e.cell);
                        break;
                    case GameEventKind.TrackBuilt:
                        World(SoundCue.TrackBuilt, e.cell, Cell.FromKey(e.aux));
                        break;
                    case GameEventKind.Bulldozed:
                        World(SoundCue.Bulldoze, e.cell);
                        break;
                    case GameEventKind.TrainPurchased:
                        World(SoundCue.TrainBought, e.cell);
                        break;
                    case GameEventKind.RouteCreated:
                        World(SoundCue.RouteStart, e.cell);
                        break;
                    case GameEventKind.TrainDeparted:
                        World(SoundCue.TrainDepart, e.cell);
                        break;
                    case GameEventKind.TrainArrived:
                        World(SoundCue.TrainArrive, e.cell);
                        break;
                }
            }
            // Money is the one sound heard at every zoom level and anywhere on the map.
            if (income > 0)
                Play(income >= BigDelivery ? SoundCue.CoinsBig : SoundCue.Coins, 1, 0);
            UpdateLoop(game, audible);
            UpdateSiren(audible);
        }
        /// <summary>Wheels on rail joints, louder as more moving trains are on screen; silent when paused or zoomed out.</summary>
        void UpdateLoop(GameSession game, bool audible)
        {
            if (!Loop.clip)
                return;
            float target = 0;
            int speed = game.World.speed;
            if (audible && speed > 0)
            {
                float gain = ZoomGain(app.Camera.zoom);
                int visible = gain > 0 ? MovingTrainsOnScreen(game) : 0;
                if (visible > 0)
                    target = gain * Mathf.Min(1, .4f + .2f * Mathf.Min(visible, LoopTrains));
            }
            Loop.volume = Mathf.MoveTowards(Loop.volume, target, LoopFadePerSecond * Time.unscaledDeltaTime);
            if (Loop.volume > 0)
            {
                Loop.pitch = speed >= 4 ? 1.12f : speed >= 2 ? 1.06f : 1;
                if (!Loop.isPlaying)
                    Loop.Play();
            }
            else if (Loop.isPlaying)
                Loop.Stop();
        }
        /// <summary>
        /// The siren of the police car, ambulance or fire engine out on a call (one trip in twenty) nearest the middle of the
        /// screen, panned to it and louder the nearer it is to the middle. Heard only zoomed all the way in; like the
        /// beacons, it keeps sounding while the game is paused.
        /// </summary>
        void UpdateSiren(bool audible)
        {
            if (!Siren.clip)
                return;
            float target = 0, pan = Siren.panStereo, gain = audible ? SirenGain(app.Camera.zoom) : 0;
            var traffic = gain > 0 && app.World ? app.World.Traffic : null;
            if (traffic && NearestOnScreen(traffic.Emergency.Sirens, out float offCentre, out float at))
            {
                target = gain * Mathf.Lerp(1, SirenAtEdge, offCentre);
                pan = at;
            }
            Siren.volume = Mathf.MoveTowards(Siren.volume, target, LoopFadePerSecond * Time.unscaledDeltaTime);
            Siren.panStereo = Mathf.MoveTowards(Siren.panStereo, pan, PanPerSecond * Time.unscaledDeltaTime);
            if (Siren.volume > 0)
            {
                if (!Siren.isPlaying)
                    Siren.Play();
            }
            else if (Siren.isPlaying)
                Siren.Stop();
        }
        /// <summary>The vehicle on screen nearest its middle: how far off centre it is (0 middle, 1 edge) and its stereo pan.</summary>
        bool NearestOnScreen(IReadOnlyList<Transform> vehicles, out float offCentre, out float pan)
        {
            offCentre = float.MaxValue;
            pan = 0;
            foreach (var vehicle in vehicles)
            {
                if (!vehicle || !vehicle.gameObject.activeInHierarchy)
                    continue;
                var p = app.Camera.view.WorldToViewportPoint(vehicle.position);
                float off = Mathf.Max(Mathf.Abs(p.x - .5f), Mathf.Abs(p.y - .5f)) * 2;
                if (p.z <= 0 || off > 1 || off >= offCentre)
                    continue;
                offCentre = off;
                pan = Mathf.Clamp((p.x - .5f) * PanWidth, -MaxPan, MaxPan);
            }
            return offCentre <= 1;
        }
        int MovingTrainsOnScreen(GameSession game)
        {
            int count = 0;
            foreach (var t in game.World.trains)
                // TrainPosition throws past the end of a path, which an InvalidRoute train can be.
                if (t.state == ServiceState.Travelling && t.step < t.path.Count && OnScreen(RailGeometry.TrainPosition(game, t, 0, out _), out _))
                    count++;
            return count;
        }
        static Vector3 Ground(Cell c) => new Vector3(c.x, MapDefinition.Height(c.x, c.z), c.z);
        bool OnScreen(Vector3 world, out float pan)
        {
            var p = app.Camera.view.WorldToViewportPoint(world);
            pan = Mathf.Clamp((p.x - .5f) * PanWidth, -MaxPan, MaxPan);
            return p.z > 0 && p.x >= -ScreenMargin && p.x <= 1 + ScreenMargin && p.y >= -ScreenMargin && p.y <= 1 + ScreenMargin;
        }
    }
}
