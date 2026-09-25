using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    public sealed class RailNetwork
    {
        public readonly Dictionary<int, TrackPieceState> cells = new Dictionary<int, TrackPieceState>();
        public readonly Dictionary<int, TrackPieceState> ids = new Dictionary<int, TrackPieceState>();
        public readonly Dictionary<int, int> components = new Dictionary<int, int>();
        public int Revision
        {
            get; private set;
        }
        public RailNetwork(WorldState w)
        {
            Rebuild(w);
        }
        public void Rebuild(WorldState w)
        {
            cells.Clear();
            ids.Clear();
            components.Clear();
            Revision = w.revision;
            foreach (var t in w.tracks)
            {
                cells.Add(t.cell.Key, t);
                ids.Add(t.id, t);
            }
            var queue = new Queue<TrackPieceState>();
            foreach (var t in w.tracks)
            {
                if (components.ContainsKey(t.id))
                    continue;
                int root = t.id;
                components[t.id] = root;
                queue.Enqueue(t);
                while (queue.Count > 0)
                {
                    var a = queue.Dequeue();
                    for (int d = 0; d < 4; d++)
                        if (Neighbor(a, d, out var b) && !components.ContainsKey(b.id))
                        {
                            components[b.id] = root;
                            queue.Enqueue(b);
                        }
                }
            }
        }
        public TrackPieceState At(Cell c) => MapDefinition.InBounds(c) && cells.TryGetValue(c.Key, out var t) ? t : null;
        public bool Neighbor(TrackPieceState a, int d, out TrackPieceState b)
        {
            b = At(a.cell.Move(d));
            return (a.mask & (1 << d)) != 0 && b != null && (b.mask & (1 << Directions.Opp(d))) != 0;
        }
    }
    /// <summary>
    /// Signals at a four-way crossing (mask 15), where a train may run straight across or turn onto the other line. A train
    /// is held only while another train's wagons still cover the crossing on a conflicting move: straight runs along the
    /// same line share it; the other line, or any turn, does not. Each railway carries one train, so a lone train never
    /// waits: holding it on a timer made it stop mid-route on every trip whose length was a multiple of the old six-second cycle.
    /// </summary>
    public static class CrossingSignals
    {
        /// <summary>How far behind its front a train still covers a crossing: the longest train.</summary>
        public const int Reach = 4500;
        /// <summary>How far ahead of a train its crossing lights already show green.</summary>
        public const int Approach = 2000;
        /// <summary>True when <paramref name="trainId"/> may run straight across the crossing from <paramref name="entry"/> now.</summary>
        public static bool MayEnter(WorldState world, int trackId, int entry, int trainId) => MayEnter(world, trackId, entry, Directions.Opp(entry), trainId);
        /// <summary>True when <paramref name="trainId"/> may pass the crossing from <paramref name="entry"/> to <paramref name="exit"/> now.</summary>
        public static bool MayEnter(WorldState world, int trackId, int entry, int exit, int trainId)
        {
            foreach (var train in world.trains)
                if (train.id != trainId && Occupies(train, trackId, out var move) && Conflict(move, entry, exit))
                    return false;
            return true;
        }
        /// <summary>
        /// What the lights show: green towards a train on or approaching the crossing, so a train always meets green, and on a
        /// straight run at the far end of its line too. With no train near, the lines alternate every six simulation seconds.
        /// </summary>
        public static bool IsGreen(WorldState world, int trackId, int entry)
        {
            foreach (var train in world.trains)
                if (Occupies(train, trackId, out var move))
                    return move.entry == entry || !Conflict(move, entry, Directions.Opp(entry));
            foreach (var train in world.trains)
                if (Approaches(train, trackId, out var move))
                    return move.entry == entry || !Conflict(move, entry, Directions.Opp(entry));
            return (world.tick / 120) % 2 == entry % 2;
        }
        /// <summary>Crossings that the wagons of trains other than <paramref name="trainId"/> cover now.</summary>
        public static HashSet<int> Covered(WorldState world, RailNetwork network, int trainId)
        {
            var covered = new HashSet<int>();
            foreach (var train in world.trains)
            {
                if (train.id == trainId)
                    continue;
                for (int k = 0, behind = 0; behind < Reach; k++)
                {
                    var step = Behind(train, k);
                    if (step == null)
                        break;
                    if (network.ids.TryGetValue(step.trackId, out var piece) && piece.mask == 15)
                        covered.Add(piece.id);
                    behind += k == 0 ? train.distance : step.length;
                }
            }
            return covered;
        }
        /// <summary>True when two moves cannot share a crossing: only straight runs along the same line can.</summary>
        static bool Conflict(RailStep move, int entry, int exit) =>
            Directions.Opp(move.entry) != move.exit || Directions.Opp(entry) != exit || move.entry % 2 != entry % 2;
        /// <summary>
        /// The <paramref name="k"/>th route step back from a train's front, or null past its last one. Near the start of a leg
        /// the cars still stand on the end of the leg the train came in on, whichever way it leaves (as in RoadLanes).
        /// </summary>
        static RailStep Behind(TrainState train, int k)
        {
            if (train.path.Count == 0)
                return null;
            int step = Math.Min(train.step, train.path.Count - 1);
            if (k <= step)
                return train.path[step - k];
            int back = train.returnPath.Count - 1 - (k - step - 1);
            return back >= 0 ? train.returnPath[back] : null;
        }
        /// <summary>True when the train's wagons may cover the crossing: a step counts once its near end is within <see cref="Reach"/>.</summary>
        static bool Occupies(TrainState train, int trackId, out RailStep move)
        {
            for (int k = 0, behind = 0; behind < Reach; k++)
            {
                move = Behind(train, k);
                if (move == null)
                    break;
                if (move.trackId == trackId)
                    return true;
                behind += k == 0 ? train.distance : move.length;
            }
            move = null;
            return false;
        }
        static bool Approaches(TrainState train, int trackId, out RailStep move)
        {
            move = null;
            if ((train.state != ServiceState.Travelling && train.state != ServiceState.Loading) || train.step >= train.path.Count)
                return false;
            int ahead = train.path[train.step].length - train.distance;
            for (int i = train.step + 1; i < train.path.Count && ahead <= Approach; i++)
            {
                if (train.path[i].trackId == trackId)
                {
                    move = train.path[i];
                    return true;
                }
                ahead += train.path[i].length;
            }
            return false;
        }
    }
    public interface IRailAccessPolicy
    {
        bool CanOccupy(WorldState w, RailNetwork network, int trackId, int exceptTrain = 0);
    }
    public sealed class ExclusiveNetworkPolicy : IRailAccessPolicy
    {
        public bool CanOccupy(WorldState w, RailNetwork n, int trackId, int exceptTrain = 0)
        {
            if (!n.components.TryGetValue(trackId, out int component))
                return false;
            foreach (var t in w.trains)
            {
                if (t.id == exceptTrain)
                    continue;
                var station = w.stations.Find(s => s.id == t.stationId);
                int occupied = t.path.Count > 0 ? t.path[Math.Min(t.step, t.path.Count - 1)].trackId : n.At(StationLayout.Center(station, t.platform)).id;
                if (n.components[occupied] == component)
                    return false;
            }
            return true;
        }
    }
    public sealed class RailPathfinder
    {
        readonly RailNetwork network;
        readonly Dictionary<string, List<RailStep>> cache = new Dictionary<string, List<RailStep>>(); int revision = -1;
        public RailPathfinder(RailNetwork n)
        {
            network = n;
        }
        public List<RailStep> FindPath(Cell start, Cell end)
        {
            if (revision != network.Revision)
            {
                cache.Clear();
                revision = network.Revision;
            }
            string key = start.Key + ":" + end.Key;
            if (cache.TryGetValue(key, out var saved))
                return Clone(saved);
            var first = network.At(start);
            var last = network.At(end);
            if (first == null || last == null || first.id == last.id)
                return null;
            var open = new List<int>();
            var cost = new Dictionary<int, int>();
            var parent = new Dictionary<int, int>();
            // Search states identify a piece and its entry port. Source is the station center.
            for (int exit = 0; exit < 4; exit++)
                if (network.Neighbor(first, exit, out var next))
                {
                    int s = next.id * 4 + Directions.Opp(exit);
                    open.Add(s);
                    cost[s] = 500;
                    parent[s] = -1 - exit;
                }
            int goal = Search(open, cost, parent, last, null);
            if (goal < 0)
                return null;
            var states = Trace(goal, parent, out int cursor);
            int firstExit = -cursor - 1;
            var path = new List<RailStep> { new RailStep { trackId = first.id, entry = Directions.Opp(firstExit), exit = firstExit, length = 500, fromCenter = true } };
            path.AddRange(Steps(states));
            cache[key] = path;
            return Clone(path);
        }
        /// <summary>
        /// The shortest way on for a train entering piece <paramref name="trackId"/> through <paramref name="entry"/> to the
        /// station piece <paramref name="goalId"/>, passing no piece in <paramref name="avoid"/>. The first step keeps that
        /// piece and entry, so the result can replace the rest of a route. Null when there is none. Not cached: what to
        /// avoid changes from tick to tick.
        /// </summary>
        public List<RailStep> FindDetour(int trackId, int entry, int goalId, ICollection<int> avoid)
        {
            if (trackId == goalId || !network.ids.ContainsKey(trackId) || !network.ids.TryGetValue(goalId, out var last))
                return null;
            int start = trackId * 4 + entry;
            var open = new List<int> { start };
            var cost = new Dictionary<int, int> { [start] = 0 };
            var parent = new Dictionary<int, int> { [start] = -1 };
            int goal = Search(open, cost, parent, last, avoid);
            return goal < 0 ? null : Steps(Trace(goal, parent, out _));
        }
        /// <summary>A* over (piece, entry port) states from the seeded ones to <paramref name="last"/>; -1 when it is out of reach.</summary>
        int Search(List<int> open, Dictionary<int, int> cost, Dictionary<int, int> parent, TrackPieceState last, ICollection<int> avoid)
        {
            var end = last.cell;
            while (open.Count > 0)
            {
                int best = 0, bestF = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    var c = network.ids[open[i] / 4].cell;
                    int dx = c.x - end.x, dz = c.z - end.z;
                    int f = cost[open[i]] + (int)(Math.Sqrt(dx * dx + dz * dz) * 500);
                    if (f < bestF || (f == bestF && open[i] < open[best]))
                    {
                        best = i;
                        bestF = f;
                    }
                }
                int current = open[best];
                open.RemoveAt(best);
                var piece = network.ids[current / 4];
                int entry = current % 4;
                if (piece.id == last.id)
                    return current;
                for (int exit = 0; exit < 4; exit++)
                {
                    if (!Directions.Allows(piece.mask, entry, exit) || !network.Neighbor(piece, exit, out var next) || (avoid != null && avoid.Contains(next.id)))
                        continue;
                    int ns = next.id * 4 + Directions.Opp(exit), nc = cost[current] + Length(entry, exit);
                    if (cost.TryGetValue(ns, out int old) && old <= nc)
                        continue;
                    cost[ns] = nc;
                    parent[ns] = current;
                    if (!open.Contains(ns))
                        open.Add(ns);
                }
            }
            return -1;
        }
        /// <summary>The searched states from the seed to <paramref name="goal"/>; <paramref name="seed"/> is the seed's parent mark.</summary>
        static List<int> Trace(int goal, Dictionary<int, int> parent, out int seed)
        {
            var states = new List<int>();
            int cursor = goal;
            while (cursor >= 0)
            {
                states.Add(cursor);
                cursor = parent[cursor];
            }
            states.Reverse();
            seed = cursor;
            return states;
        }
        /// <summary>Route steps through searched states; the last one stops at its piece's center.</summary>
        static List<RailStep> Steps(List<int> states)
        {
            var path = new List<RailStep>();
            for (int i = 0; i < states.Count; i++)
            {
                int entry = states[i] % 4, exit = i + 1 < states.Count ? Directions.Opp(states[i + 1] % 4) : Directions.Opp(entry);
                path.Add(new RailStep { trackId = states[i] / 4, entry = entry, exit = exit, length = i == states.Count - 1 ? 500 : Length(entry, exit), toCenter = i == states.Count - 1 });
            }
            return path;
        }
        static List<RailStep> Clone(List<RailStep> p)
        {
            var n = new List<RailStep>();
            foreach (var s in p)
                n.Add(new RailStep { trackId = s.trackId, entry = s.entry, exit = s.exit, length = s.length, fromCenter = s.fromCenter, toCenter = s.toCenter });
            return n;
        }
        public static int Length(int entry, int exit) => Directions.Opp(entry) == exit ? 1000 : 785;
    }
}
