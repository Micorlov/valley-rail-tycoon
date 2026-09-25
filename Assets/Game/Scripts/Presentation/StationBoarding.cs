using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Passengers getting off and on while a train stands at a town platform. Each stop plays over the train's dwell:
    /// arrivals step out of the coaches and walk off the platform, then the people who were waiting walk to the doors and
    /// vanish inside before the train leaves. Cosmetic only: reads the simulation, never changes it or its randomness.
    /// </summary>
    public sealed class StationBoarding : MonoBehaviour
    {
        sealed class Walker
        {
            public bool boarding, fromCrowd, fadeIn, fadeOut, started, done;
            public float start, length, phase;
            public Vector3 from, via, to;
            public int shirt;
            public Transform view;
        }
        /// <summary>A platform seen from the train standing at it: along the track, and across towards the platform.</summary>
        struct Frame
        {
            public Vector3 origin, along, across;
            public float track, first, last;
            public bool island;
            public float Middle => (first + last) * .5f;
            public float Edge => track + (island ? IslandEdge : MainEdge);
            public Vector3 At(float a, float c) => origin + along * a + across * c;
            public float AlongOf(Vector3 p) => Vector3.Dot(p - origin, along);
        }
        sealed class Stop
        {
            public int trainId, stationId, held, dwellTicks, pass;
            public float elapsed, seconds;
            public bool timedByDwell;
            public Frame frame;
            public List<float> doors;
            public readonly List<Walker> walkers = new List<Walker>();
        }
        struct Seen
        {
            public ServiceState state;
            public int station, units, origin;
        }
        // The stop's timeline in fractions of its length: arrivals leave first, then the queue boards, all inside before departure.
        // The last arrival (.03 + .25 + .38 = .66) is gone before even a lone boarder (.32 + .36 = .68) steps aboard.
        const float AlightStart = .03f, AlightSpread = .25f, AlightWalk = .38f, BoardStart = .32f, BoardSpread = .25f, BoardWalk = .36f;
        const float FadeIn = .15f, FadeOut = .25f, TickSeconds = .05f;
        // A full train lets MaxWalkers people off and on, whatever few coaches reach the platform; a light one at least MinWalkers.
        const int MaxWalkers = 8, MinWalkers = 2;
        // Across the track from its centre: inside the coach, the platform edge by the doors, and where arrivals head off.
        const float Inside = .08f, MainEdge = .72f, IslandEdge = .45f, MainExit = 1.3f, IslandMiddle = .5f, IslandStroll = .7f;
        // Doors sit either side of a coach's middle; people spread a little so they never walk in single file.
        const float DoorOffset = .1f, DoorJitter = .06f, ExitSpread = .35f, PlatformEndMargin = .35f;
        const float StrideRate = 14f, StrideLift = .018f, SwayDegrees = 5f;
        readonly Dictionary<int, Stop> stops = new Dictionary<int, Stop>();
        readonly Dictionary<int, Seen> seen = new Dictionary<int, Seen>();
        readonly List<Transform>[] pool = new List<Transform>[CityLife.Shirts.Length];
        readonly List<Stop> ended = new List<Stop>();
        WorldView world;
        StationCrowds crowds;
        GameSession game;
        int pass, walking;

        /// <summary>People currently walking between a train and its platform.</summary>
        public int Walking => walking;
        /// <summary>How many people get off and on during the train's current stop (0, 0 when it is not at a platform).</summary>
        public (int alighting, int boarding) Planned(int trainId)
        {
            if (!stops.TryGetValue(trainId, out var stop))
                return (0, 0);
            int boarding = stop.walkers.FindAll(w => w.boarding).Count;
            return (stop.walkers.Count - boarding, boarding);
        }

        public void Initialize(WorldView view, StationCrowds platformCrowds)
        {
            world = view;
            crowds = platformCrowds;
        }

        /// <summary>Starts a stop when a passenger train pulls in and plays it along the train's dwell; drawn only when zoomed in.</summary>
        public void Animate(GameSession session, float dt, float speed, bool near)
        {
            if (session != game)
            {
                EndAll();
                seen.Clear();
                game = session;
            }
            if (gameObject.activeSelf != near)
                gameObject.SetActive(near);
            pass++;
            walking = 0;
            foreach (var t in session.World.trains)
            {
                if (t.cargo != Cargo.Passengers)
                    continue;
                bool known = seen.TryGetValue(t.id, out var before);
                if (stops.TryGetValue(t.id, out var stop) && !StillThere(stop, t))
                {
                    End(stop);
                    stop = null;
                }
                stop = stop ?? Begin(t, known, before);
                if (stop != null)
                    Play(stop, t, dt * speed, near);
                seen[t.id] = new Seen { state = t.state, station = t.stationId, units = t.units, origin = t.origin };
            }
            // A train sold, or switched to freight, mid-stop.
            ended.Clear();
            foreach (var stop in stops.Values)
                if (stop.pass != pass)
                    ended.Add(stop);
            foreach (var stop in ended)
                End(stop);
            if (seen.Count > session.World.trains.Count * 2 + 16)
                seen.Clear();
        }

        static bool StillThere(Stop stop, TrainState t) =>
            t.stationId == stop.stationId && (stop.timedByDwell ? t.state == ServiceState.Loading : t.state == ServiceState.Parked && stop.elapsed < stop.seconds);

        Stop Begin(TrainState t, bool known, Seen before)
        {
            // A normal stop dwells; a train sent back to a station unloads and parks without boarding anyone.
            bool dwelling = t.state == ServiceState.Loading && (!known || before.state != ServiceState.Loading || before.station != t.stationId);
            bool parking = known && t.state == ServiceState.Parked && before.state == ServiceState.Travelling && before.units > 0 && t.units == 0;
            var station = dwelling || parking ? game.Trains.Station(t.stationId) : null;
            if (station == null)
                return null;
            int delivered = known && before.units > 0 && (t.units == 0 || t.origin != before.origin) ? before.units : 0;
            int boarded = dwelling && t.units > 0 && t.origin == station.producerId ? t.units : 0;
            if (delivered == 0 && boarded == 0)
                return null;
            var stop = new Stop { trainId = t.id, stationId = station.id, timedByDwell = dwelling, pass = pass, frame = FrameOf(t, station) };
            stop.doors = Doors(t, stop.frame);
            if (stop.doors.Count == 0)
                return null;
            stop.dwellTicks = dwelling ? Mathf.Max(1, StationCatalog.DwellTicks(game.Balance, station)) : Mathf.Max(1, game.Balance.dwellTicks);
            stop.seconds = stop.dwellTicks * TickSeconds;
            // Seeded per stop, so the same arrival always looks the same; never the simulation's own random.
            var random = new System.Random(t.id * 7919 + (int)(game.World.tick % 100003));
            int capacity = game.Balance.Capacity(t);
            AddAlighting(stop, Figures(delivered, capacity), random);
            AddBoarding(stop, station, boarded, Figures(boarded, capacity), random);
            stops[t.id] = stop;
            return stop;
        }

        static int Figures(int units, int capacity) =>
            units <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(units / (float)Mathf.Max(1, capacity) * MaxWalkers), MinWalkers, MaxWalkers);

        static Frame FrameOf(TrainState t, StationState s)
        {
            int length = StationLayout.Length(s), platform = Mathf.Clamp(t.platform, 0, StationLayout.Platforms(s) - 1);
            return new Frame
            {
                origin = new Vector3(s.cell.x, StationCrowds.PlatformTop, s.cell.z),
                along = s.axis == 1 ? Vector3.right : Vector3.forward,
                across = new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]),
                // Track k lies k cells from the building side; tracks after the first stop beside an island platform.
                track = -platform,
                island = platform > 0,
                first = StationLayout.First(length) - .5f + PlatformEndMargin,
                last = StationLayout.Last(length) + .5f - PlatformEndMargin,
            };
        }

        /// <summary>The middles of the passenger cars that stand beside the platform, along the track.</summary>
        List<float> Doors(TrainState t, Frame frame)
        {
            var doors = new List<float>();
            int cars = WorldView.TrainCarCount(t);
            // A locomotive carries nobody; every car of a multiple unit does.
            for (int i = TrainCatalog.MultipleUnit(t.model) ? 0 : 1; i < cars; i++)
            {
                float a = frame.AlongOf(world.CarPosition(t, i));
                if (a >= frame.first && a <= frame.last)
                    doors.Add(a);
            }
            return doors;
        }

        static float Door(Stop stop, int i, System.Random random)
        {
            int count = stop.doors.Count;
            float side = i / count % 2 == 0 ? -DoorOffset : DoorOffset;
            return stop.doors[i % count] + side + Spread(random, DoorJitter);
        }
        static float Spread(System.Random random, float reach) => ((float)random.NextDouble() * 2 - 1) * reach;
        static float Stagger(int i, int count, float spread) => count <= 1 ? 0 : spread * i / (count - 1);

        /// <summary>Where people come from or go to off the platform: into the station at the back, or down an island's stairs.</summary>
        static Vector3 Away(Frame f, float door, System.Random random)
        {
            if (f.island)
                return f.At(Mathf.Clamp(door + Mathf.Sign(door - f.Middle) * IslandStroll, f.first, f.last), f.track + IslandMiddle);
            return f.At(Mathf.Clamp(door + Spread(random, ExitSpread), f.first, f.last), f.track + MainExit);
        }

        void AddAlighting(Stop stop, int count, System.Random random)
        {
            var f = stop.frame;
            for (int i = 0; i < count; i++)
            {
                float door = Door(stop, i, random);
                stop.walkers.Add(new Walker
                {
                    start = AlightStart + Stagger(i, count, AlightSpread),
                    length = AlightWalk,
                    fadeOut = true,
                    from = f.At(door, f.track + Inside),
                    via = f.At(door, f.Edge),
                    to = Away(f, door, random),
                    shirt = random.Next(CityLife.Shirts.Length),
                    phase = (float)random.NextDouble() * Mathf.PI * 2,
                });
            }
        }

        /// <summary>
        /// The people who boarded in the simulation. On the main platform they are the ones who were waiting there: the crowd
        /// keeps them standing until each one's walk starts. Anyone else (an island platform, a crowd already redrawn) walks on.
        /// </summary>
        void AddBoarding(Stop stop, StationState station, int boarded, int count, System.Random random)
        {
            if (count == 0)
                return;
            var f = stop.frame;
            var load = StationLoad.Of(game.World, game.Balance, station);
            // The town queue is shared by all its stations: take only this train's share of the people who left it.
            int share = Mathf.CeilToInt(boarded * (float)StationLoad.MaxFigures / Mathf.Max(1, load.capacity));
            int queued = f.island ? 0 : Mathf.Clamp(Mathf.Min(crowds.Showing(station.id) - load.Figures, share), 0, MaxWalkers);
            count = Mathf.Max(count, queued);
            if (queued > 0)
            {
                crowds.Hold(station.id, queued);
                stop.held = queued;
            }
            for (int j = 0; j < count; j++)
            {
                // Boarders use the other door of each coach from the one arrivals left by.
                float door = Door(stop, j + stop.doors.Count, random);
                stop.walkers.Add(new Walker
                {
                    boarding = true,
                    fromCrowd = j < queued,
                    fadeIn = j >= queued,
                    start = BoardStart + Stagger(j, count, BoardSpread),
                    length = BoardWalk,
                    from = Away(f, door, random),
                    via = f.At(door, f.Edge),
                    to = f.At(door, f.track + Inside),
                    shirt = random.Next(CityLife.Shirts.Length),
                    phase = (float)random.NextDouble() * Mathf.PI * 2,
                });
            }
        }

        void Play(Stop stop, TrainState t, float gameDt, bool near)
        {
            stop.pass = pass;
            stop.elapsed += gameDt;
            if (stop.timedByDwell)
            {
                // Follow the dwell countdown, smoothed between its 20 Hz ticks so walking stays fluid.
                float truth = (stop.dwellTicks - t.dwell) * TickSeconds;
                stop.elapsed = Mathf.Clamp(stop.elapsed, truth, truth + TickSeconds);
            }
            float progress = stop.elapsed / stop.seconds;
            foreach (var w in stop.walkers)
            {
                float u = (progress - w.start) / w.length;
                if (w.done || u < 0)
                    continue;
                if (!w.started)
                    BeginWalk(stop, w);
                if (u >= 1)
                {
                    Retire(w);
                    continue;
                }
                walking++;
                if (near)
                    Pose(w, u, stop.elapsed);
            }
        }

        void BeginWalk(Stop stop, Walker w)
        {
            w.started = true;
            if (w.fromCrowd && crowds.TakeBoarder(stop.stationId, out var standing, out int shirt))
            {
                // Step out of the queue exactly where this person stood, towards the nearest coach.
                stop.held--;
                var f = stop.frame;
                float door = Nearest(stop.doors, f.AlongOf(standing));
                w.from = standing;
                w.via = f.At(door, f.Edge);
                w.to = f.At(door, f.track + Inside);
                w.shirt = shirt;
            }
            else if (w.fromCrowd)
                w.fadeIn = true;
            w.view = Take(w.shirt, w.boarding ? "Boarding passenger" : "Alighting passenger");
        }

        static float Nearest(List<float> doors, float along)
        {
            float best = doors[0];
            foreach (float door in doors)
                if (Mathf.Abs(door - along) < Mathf.Abs(best - along))
                    best = door;
            return best + (along < best ? -DoorOffset : DoorOffset);
        }

        /// <summary>Walks at an even pace along the two legs (through the door, across the platform) with a little stride bob.</summary>
        static void Pose(Walker w, float u, float time)
        {
            float first = Vector3.Distance(w.from, w.via), total = first + Vector3.Distance(w.via, w.to);
            float d = u * total;
            bool leg1 = d < first;
            Vector3 a = leg1 ? w.from : w.via, b = leg1 ? w.via : w.to;
            float span = leg1 ? first : total - first;
            float k = span > 1e-4f ? Mathf.Clamp01((leg1 ? d : d - first) / span) : 1;
            float stride = Mathf.Sin((time + w.phase) * StrideRate);
            w.view.position = Vector3.Lerp(a, b, k) + Vector3.up * Mathf.Abs(stride) * StrideLift;
            var heading = b - a;
            heading.y = 0;
            if (heading.sqrMagnitude > 1e-6f)
                w.view.rotation = Quaternion.LookRotation(heading) * Quaternion.Euler(0, 0, stride * SwayDegrees);
            float scale = 1;
            if (w.fadeIn)
                scale = Mathf.Min(scale, u / FadeIn);
            if (w.fadeOut)
                scale = Mathf.Min(scale, (1 - u) / FadeOut);
            w.view.localScale = Vector3.one * Mathf.Clamp01(scale);
        }

        Transform Take(int shirt, string name)
        {
            var spare = pool[shirt] ?? (pool[shirt] = new List<Transform>());
            Transform view;
            if (spare.Count > 0)
            {
                view = spare[spare.Count - 1];
                spare.RemoveAt(spare.Count - 1);
            }
            else
                view = CityLife.Figure(world, transform, name, CityLife.Shirts[shirt], StationCrowds.FigureScale);
            view.name = name;
            view.localScale = Vector3.zero; // Posed on its first visible frame.
            view.gameObject.SetActive(true);
            return view;
        }

        void Retire(Walker w)
        {
            w.done = true;
            if (w.view == null)
                return;
            w.view.gameObject.SetActive(false);
            pool[w.shirt].Add(w.view);
            w.view = null;
        }

        void End(Stop stop)
        {
            foreach (var w in stop.walkers)
                Retire(w);
            // Anyone still held on the platform boarded in the simulation: let the crowd drop them now.
            if (stop.held > 0)
                crowds.Hold(stop.stationId, -stop.held);
            stops.Remove(stop.trainId);
        }

        void EndAll()
        {
            ended.Clear();
            ended.AddRange(stops.Values);
            foreach (var stop in ended)
                End(stop);
        }
    }
}
