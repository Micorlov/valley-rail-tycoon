using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Little passengers waiting on town platforms. The crowd grows with the town's queue (shared by all its stations), so a
    /// busy station reads at a glance at any zoom. Cosmetic only: never reads or changes simulation randomness.
    /// </summary>
    public sealed class StationCrowds : MonoBehaviour
    {
        sealed class Crowd
        {
            public int stationId;
            public (int x, int z, int axis, int side, int length, int platforms) shape;
            public Transform root;
            public Transform[] people;
            public Quaternion[] facing;
            // Shirt colour of each person, so the one who walks to a train keeps their look.
            public int[] shirts;
            // People who already boarded in the simulation but stand here until their walk to the doors starts.
            public int held;
            public float[] phase;
            public int showing = -1;
        }
        const int Columns = StationLoad.MaxFigures / 2;
        // Figures are a little larger than pavement walkers so a queue still reads at the default zoom.
        internal const float FigureScale = 1.6f, PlatformTop = .3f, FrontRow = -.2f, BackRow = .16f, Jitter = .05f;
        const float TurnDegrees = 30f, SwayDegrees = 18f, SwayRate = .6f;
        static readonly Color Calm = new Color(.71f, .95f, .74f), Busy = new Color(1f, .82f, .45f), Crowded = new Color(1f, .45f, .4f);
        readonly List<Crowd> crowds = new List<Crowd>();
        float time;

        /// <summary>Label and list colour for a queue: mint while calm, amber when busy, red when crowded.</summary>
        public static Color LoadColor(LoadLevel level) => level == LoadLevel.Crowded ? Crowded : level == LoadLevel.Busy ? Busy : Calm;
        /// <summary>People currently standing on the station's platform (0 for freight stations).</summary>
        public int Showing(int stationId)
        {
            foreach (var crowd in crowds)
                if (crowd.stationId == stationId)
                    return Mathf.Max(0, crowd.showing);
            return 0;
        }

        /// <summary>
        /// Keeps <paramref name="count"/> people who already boarded in the simulation standing until their walk to the train
        /// starts; a negative count releases them. <see cref="StationBoarding"/> takes them one at a time.
        /// </summary>
        public void Hold(int stationId, int count)
        {
            var crowd = crowds.Find(c => c.stationId == stationId);
            if (crowd != null)
                crowd.held = Mathf.Max(0, crowd.held + count);
        }
        /// <summary>Hides the last held person on the platform and says where they stood and what they wore.</summary>
        public bool TakeBoarder(int stationId, out Vector3 standing, out int shirt)
        {
            standing = default;
            shirt = 0;
            var crowd = crowds.Find(c => c.stationId == stationId);
            if (crowd == null || crowd.held <= 0 || crowd.showing <= 0)
                return false;
            int i = --crowd.showing;
            crowd.held--;
            crowd.people[i].gameObject.SetActive(false);
            standing = crowd.people[i].position;
            shirt = crowd.shirts[i];
            return true;
        }

        /// <summary>
        /// Matches crowds to the passenger platforms after the railway was redrawn. Any track edit bumps the revision, so
        /// a platform that did not move keeps its crowd; only new or moved stations build one.
        /// </summary>
        public void Rebuild(WorldView world, GameSession game)
        {
            var kept = new List<Crowd>();
            foreach (var s in game.World.stations)
            {
                if (!StationLoad.Of(game.World, game.Balance, s).Passengers)
                    continue;
                var shape = Shape(s);
                kept.Add(crowds.Find(c => c.stationId == s.id && c.shape == shape) ?? Build(world, s));
            }
            foreach (var crowd in crowds)
                if (!kept.Contains(crowd))
                {
                    crowd.root.gameObject.SetActive(false);
                    Destroy(crowd.root.gameObject);
                }
            crowds.Clear();
            crowds.AddRange(kept);
            Sync(game);
        }
        static (int x, int z, int axis, int side, int length, int platforms) Shape(StationState s) => (s.cell.x, s.cell.z, s.axis, s.side, StationLayout.Length(s), StationLayout.Platforms(s));
        Crowd Build(WorldView world, StationState s)
        {
            var root = new GameObject("Crowd " + s.id).transform;
            root.SetParent(transform, false);
            var across = new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]);
            var along = s.axis == 1 ? Vector3.right : Vector3.forward;
            int length = StationLayout.Length(s);
            // The crowd stands on the main platform and spreads along its whole length, .15 short of each end.
            float reach = length / 2f - .15f;
            root.localPosition = new Vector3(s.cell.x, PlatformTop, s.cell.z) + across + along * StationLayout.Middle(length);
            // Seeded per station, so a platform keeps the same faces and spots between redraws.
            var random = new System.Random(s.id * 7919 + 17);
            var slots = new List<Vector3>(StationLoad.MaxFigures);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < Columns; i++)
                    slots.Add(along * (Mathf.Lerp(-reach, reach, i / (Columns - 1f)) + Spread(random)) + across * ((row == 0 ? FrontRow : BackRow) + Spread(random)));
            // Fill spots in random order so a growing queue spreads along the platform instead of filling it end to end.
            for (int i = slots.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (slots[i], slots[j]) = (slots[j], slots[i]);
            }
            var toTrack = Quaternion.LookRotation(-across);
            var crowd = new Crowd { stationId = s.id, shape = Shape(s), root = root, people = new Transform[slots.Count], facing = new Quaternion[slots.Count], phase = new float[slots.Count], shirts = new int[slots.Count] };
            for (int i = 0; i < slots.Count; i++)
            {
                crowd.shirts[i] = random.Next(CityLife.Shirts.Length);
                var person = CityLife.Figure(world, root, "Passenger", CityLife.Shirts[crowd.shirts[i]], FigureScale);
                person.localPosition = slots[i];
                crowd.facing[i] = toTrack * Quaternion.Euler(0, ((float)random.NextDouble() * 2 - 1) * TurnDegrees, 0);
                person.localRotation = crowd.facing[i];
                crowd.phase[i] = (float)random.NextDouble() * Mathf.PI * 2;
                person.gameObject.SetActive(false);
                crowd.people[i] = person;
            }
            return crowd;
        }
        static float Spread(System.Random random) => ((float)random.NextDouble() * 2 - 1) * Jitter;
        /// <summary>Shows as many people as each town queue calls for; cheap when nothing changed.</summary>
        public void Sync(GameSession game)
        {
            foreach (var crowd in crowds)
            {
                var station = game.Trains.Station(crowd.stationId);
                int want = station == null ? 0 : Mathf.Min(crowd.people.Length, StationLoad.Of(game.World, game.Balance, station).Figures + crowd.held);
                if (want == crowd.showing)
                    continue;
                for (int i = 0; i < crowd.people.Length; i++)
                    crowd.people[i].gameObject.SetActive(i < want);
                crowd.showing = want;
            }
        }
        /// <summary>Waiting people glance around while the game runs; only worth doing when zoomed in enough to see it.</summary>
        public void Animate(float dt, float speed, bool near)
        {
            if (!near || speed <= 0)
                return;
            time += dt;
            foreach (var crowd in crowds)
                for (int i = 0; i < crowd.showing; i++)
                    crowd.people[i].localRotation = crowd.facing[i] * Quaternion.Euler(0, Mathf.Sin(time * SwayRate + crowd.phase[i]) * SwayDegrees, 0);
        }
    }
}
