using System.Collections.Generic;

namespace ValleyRail.Core
{
    /// <summary>
    /// The drivable road graph for cosmetic traffic: every town street plus every open highway, joined where a highway
    /// meets a street, so cars can leave one town and drive to the next. Cars cross a straight track only at right
    /// angles (a level crossing) and never drive along rails, over a rail bridge or through a station's platform strip.
    /// </summary>
    public sealed class RoadLanes
    {
        /// <summary>How far behind its front a train still blocks a crossing (the longest train, as CrossingSignals).</summary>
        public const int TrainReach = 4500;
        /// <summary>How far ahead of a moving train a crossing closes: a car needs about 1.5 game seconds per cell.</summary>
        public const int Approach = 8000;
        // Per cell key: bit d is set when a car may drive on in direction d.
        readonly Dictionary<int, int> exits = new Dictionary<int, int>();
        // The subset of exits that runs along a highway rather than between two town streets.
        readonly Dictionary<int, int> highwayExits = new Dictionary<int, int>();
        readonly HashSet<int> townStreets = new HashSet<int>();
        public int Count => exits.Count;
        public int Exits(Cell c) => exits.TryGetValue(c.Key, out int mask) ? mask : 0;
        public bool IsHighway(Cell c, int d) => highwayExits.TryGetValue(c.Key, out int mask) && (mask & 1 << d) != 0;
        public bool IsTownStreet(Cell c) => townStreets.Contains(c.Key);

        public static RoadLanes Build(WorldState w, RailNetwork net)
        {
            var lanes = new RoadLanes();
            foreach (var city in w.cities)
                foreach (var road in city.roads)
                    lanes.townStreets.Add(road.cell.Key);
            foreach (var city in w.cities)
                foreach (var road in city.roads)
                    for (int d = 0; d < 2; d++) // north and east: every pair of streets once
                    {
                        var next = road.cell.Move(d);
                        if (lanes.townStreets.Contains(next.Key))
                            lanes.Join(w, net, road.cell, next, d, false);
                    }
            foreach (var road in w.intercityRoads)
            {
                if (!road.Complete)
                    continue; // traffic waits until the whole highway opens
                for (int i = 0; i + 1 < road.path.Count; i++)
                {
                    Cell a = road.path[i], b = road.path[i + 1];
                    if (a.Distance(b) != 1)
                        continue;
                    bool open = !(lanes.IsTownStreet(a) && lanes.IsTownStreet(b));
                    lanes.Join(w, net, a, b, Directions.Between(a, b), open);
                }
            }
            return lanes;
        }
        void Join(WorldState w, RailNetwork net, Cell a, Cell b, int d, bool highway)
        {
            if (!Passable(w, net, a, d) || !Passable(w, net, b, d))
                return;
            Add(exits, a, d);
            Add(exits, b, Directions.Opp(d));
            if (!highway)
                return;
            Add(highwayExits, a, d);
            Add(highwayExits, b, Directions.Opp(d));
        }
        static void Add(Dictionary<int, int> masks, Cell c, int d) =>
            masks[c.Key] = (masks.TryGetValue(c.Key, out int mask) ? mask : 0) | 1 << d;
        /// <summary>A car may drive through c heading along axis d: open ground, or rails crossed at right angles.</summary>
        public static bool Passable(WorldState w, RailNetwork net, Cell c, int d)
        {
            return !BuildService.StationFootprint(w, c) && Crosses(net.At(c), d);
        }
        /// <summary>A road along axis d may cross this piece: no track, or a straight one at right angles off a bridge.</summary>
        public static bool Crosses(TrackPieceState track, int d) =>
            track == null || (track.bridge == 0 && track.mask == (d % 2 == 0 ? 10 : 5));
        public bool SameAs(RoadLanes other) =>
            other != null && Equal(exits, other.exits) && Equal(highwayExits, other.highwayExits);
        static bool Equal(Dictionary<int, int> a, Dictionary<int, int> b)
        {
            if (a.Count != b.Count)
                return false;
            foreach (var pair in a)
                if (!b.TryGetValue(pair.Key, out int mask) || mask != pair.Value)
                    return false;
            return true;
        }
        /// <summary>
        /// Collects the track pieces a train covers or will reach soon; road traffic waits before those level crossings.
        /// Only reads the world, so the cosmetic traffic never changes the simulation.
        /// </summary>
        public static void TracksNearTrains(WorldState w, HashSet<int> tracks)
        {
            tracks.Clear();
            foreach (var t in w.trains)
            {
                if (t.path.Count == 0)
                    continue;
                int step = System.Math.Min(t.step, t.path.Count - 1), behind = t.distance;
                tracks.Add(t.path[step].trackId);
                // Each step whose near end lies within reach holds some of the train. Near the start of a leg the cars still
                // stand where the train came in, on the end of the route's other leg, whichever way it leaves (TrainConsist).
                for (int i = step - 1; i >= 0 && behind < TrainReach; i--)
                {
                    tracks.Add(t.path[i].trackId);
                    behind += t.path[i].length;
                }
                for (int i = t.returnPath.Count - 1; i >= 0 && behind < TrainReach; i--)
                {
                    tracks.Add(t.returnPath[i].trackId);
                    behind += t.returnPath[i].length;
                }
                if (t.state != ServiceState.Travelling)
                    continue;
                for (int i = step + 1, ahead = t.path[step].length - t.distance; i < t.path.Count && ahead <= Approach; i++)
                {
                    tracks.Add(t.path[i].trackId);
                    ahead += t.path[i].length;
                }
            }
        }
    }
}
