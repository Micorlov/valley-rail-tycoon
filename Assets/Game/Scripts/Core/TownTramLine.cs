using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>
    /// A town's surface light-rail line, Tel Aviv style but street level only: a simple path along the town's own
    /// streets from one end of town, through the plaza, to the other, with stops along it. Trams share the ordinary
    /// traffic lanes, so the path only takes moves the car graph (<see cref="RoadLanes"/>) allows, and it crosses rails
    /// only where cars do. Planned from the world alone and never saved: the simulation only records that a town has
    /// opened its line (<see cref="CityState.tram"/>), and the view plans the same path to draw and run it.
    /// Positions along the line are in cells: cells[i] covers [i, i + 1), so the line runs from 0 to Count.
    /// </summary>
    public sealed class TownTramLine
    {
        public const int MinCells = 8, MaxArm = 16;
        /// <summary>The cells at each end where a tram reverses: cars take turns with it there.</summary>
        public const int EndCells = 3;
        /// <summary>Half a tram's length in cells: a stop needs this much clear straight street either side.</summary>
        public const float HalfTram = .75f;
        /// <summary>How far from each end of the line a tram stops before it reverses.</summary>
        public const float EndStop = .9f;
        public const float StopSpacing = 4.5f;
        /// <summary>Lines are coloured in the order towns open them, like Gush Dan's Red, Purple and Green lines.</summary>
        public static readonly string[] ColourNames = { "Red", "Purple", "Green", "Blue", "Orange", "Brown" };
        public readonly int cityId;
        /// <summary>The line's street cells in order, each next to the one before.</summary>
        public readonly List<Cell> cells;
        /// <summary>Stop positions along the line, ascending. The first and last are the ends, where trams reverse.</summary>
        public readonly List<float> stops;
        public int Count => cells.Count;

        TownTramLine(int cityId, List<Cell> cells, List<float> stops)
        {
            this.cityId = cityId;
            this.cells = cells;
            this.stops = stops;
        }
        /// <summary>The side a tram running towards the end of the line enters cell i by.</summary>
        public int Entry(int i) => i > 0 ? Directions.Between(cells[i], cells[i - 1]) : Directions.Opp(Directions.Between(cells[0], cells[1]));
        /// <summary>The side a tram running towards the end of the line leaves cell i by; the ends run straight on.</summary>
        public int Exit(int i) => i + 1 < cells.Count ? Directions.Between(cells[i], cells[i + 1]) : Directions.Opp(Entry(i));
        public int IndexOf(Cell c) => cells.IndexOf(c);
        public bool SameAs(TownTramLine other)
        {
            if (other == null || other.cityId != cityId || other.cells.Count != cells.Count || other.stops.Count != stops.Count)
                return false;
            for (int i = 0; i < cells.Count; i++)
                if (!cells[i].Equals(other.cells[i]))
                    return false;
            for (int i = 0; i < stops.Count; i++)
                if (Math.Abs(stops[i] - other.stops[i]) > .001f)
                    return false;
            return true;
        }

        /// <summary>The line's colour: its place among the towns' lines in the order they opened (ties by city id).</summary>
        public static int ColourIndex(WorldState w, CityState city)
        {
            int rank = 0;
            foreach (var other in w.cities)
                if (other != city && other.tram != 0 && (other.tram < city.tram || (other.tram == city.tram && other.id < city.id)))
                    rank++;
            return rank;
        }
        public static string ColourName(int index) => ColourNames[index % ColourNames.Length];
        /// <summary>The cells the player's light-rail lines (TramLines) hold, which town lines keep off; null when there are none.</summary>
        public static Predicate<Cell> PlayerLines(WorldState w) =>
            w.tramLines == null || w.tramLines.Count == 0 ? null : c => TramLines.LineAt(w, c) != null;

        /// <summary>
        /// Plans the town's line, or null when its streets cannot hold one of at least <see cref="MinCells"/> cells.
        /// Arm A runs from the plaza to the best end (long, few bends, beside the town's railway station if it can);
        /// arm B leaves the plaza another way, ideally straight on. Both are shortest street routes with the fewest bends.
        /// Cells <paramref name="avoid"/> rejects (another light-rail network) are left alone.
        /// </summary>
        public static TownTramLine Plan(WorldState w, CityState city, RoadLanes lanes, RailNetwork net, Predicate<Cell> avoid = null)
        {
            var streets = Streets(city, lanes, avoid);
            if (!streets.Contains(city.center.Key))
                return null;
            var armA = Arm(w, city, lanes, streets, null, -1);
            if (armA.Count < 2)
                return null;
            var taken = new HashSet<int>();
            for (int i = 1; i < armA.Count; i++)
                taken.Add(armA[i].Key);
            var armB = Arm(w, city, lanes, streets, taken, Directions.Between(city.center, armA[1]));
            var cells = new List<Cell>(armA.Count + armB.Count);
            for (int i = armA.Count - 1; i >= 0; i--)
                cells.Add(armA[i]);
            for (int i = 1; i < armB.Count; i++)
                cells.Add(armB[i]);
            while (cells.Count >= MinCells && !EndClear(cells, lanes, net, false))
                cells.RemoveAt(0);
            while (cells.Count >= MinCells && !EndClear(cells, lanes, net, true))
                cells.RemoveAt(cells.Count - 1);
            if (cells.Count < MinCells)
                return null;
            return new TownTramLine(city.id, cells, Stops(cells, lanes, net, cells.IndexOf(city.center)));
        }

        /// <summary>
        /// True while every cell is still one of the town's streets, every step still drivable, and the ends and stops
        /// still clear (no track laid across them since). The view keeps a line while it is valid, so it does not
        /// wander every time the town adds a street.
        /// </summary>
        public bool StillValid(CityState city, RoadLanes lanes, RailNetwork net, Predicate<Cell> avoid = null)
        {
            if (city.id != cityId || cells.Count < MinCells)
                return false;
            var streets = Streets(city, lanes, avoid);
            for (int i = 0; i < cells.Count; i++)
            {
                if (!streets.Contains(cells[i].Key))
                    return false;
                if (i > 0 && (cells[i - 1].Distance(cells[i]) != 1 || (lanes.Exits(cells[i - 1]) & 1 << Directions.Between(cells[i - 1], cells[i])) == 0))
                    return false;
            }
            if (!EndClear(cells, lanes, net, false) || !EndClear(cells, lanes, net, true))
                return false;
            for (int k = 1; k + 1 < stops.Count; k++)
                if (!Clear(cells, lanes, net, stops[k]))
                    return false;
            return true;
        }

        static HashSet<int> Streets(CityState city, RoadLanes lanes, Predicate<Cell> avoid)
        {
            var streets = new HashSet<int>();
            foreach (var r in city.roads)
                if (lanes.IsTownStreet(r.cell) && lanes.Exits(r.cell) != 0 && (avoid == null || !avoid(r.cell)))
                    streets.Add(r.cell.Key);
            return streets;
        }

        /// <summary>
        /// The best arm from the plaza: breadth first by steps, keeping the fewest bends among equally short routes
        /// (a search state is a cell and the heading it was entered with), up to <see cref="MaxArm"/> cells. Returns
        /// the path starting at the plaza; just the plaza when no street leads away.
        /// </summary>
        static List<Cell> Arm(WorldState w, CityState city, RoadLanes lanes, HashSet<int> streets, HashSet<int> taken, int otherArm)
        {
            var plaza = city.center;
            var depth = new Dictionary<int, int> { [plaza.Key] = 0 };
            var bends = new Dictionary<int, int>();
            var from = new Dictionary<int, int>();
            var first = new Dictionary<int, int>();
            var frontier = new List<int>();
            int exits = lanes.Exits(plaza);
            for (int d = 0; d < 4; d++)
            {
                var n = plaza.Move(d);
                if (d == otherArm || (exits & 1 << d) == 0 || !streets.Contains(n.Key) || (taken != null && taken.Contains(n.Key)))
                    continue;
                int state = n.Key * 4 + d;
                depth[n.Key] = 1;
                bends[state] = 0;
                from[state] = -1;
                first[state] = d;
                frontier.Add(state);
            }
            int best = -1, bestScore = int.MinValue;
            for (int step = 1; frontier.Count > 0; step++)
            {
                foreach (int state in frontier)
                {
                    int score = Score(w, city, state, step, bends[state], first[state], from, otherArm);
                    int key = state / 4;
                    if (score > bestScore || (score == bestScore && key < best / 4))
                    {
                        best = state;
                        bestScore = score;
                    }
                }
                if (step >= MaxArm)
                    break;
                var next = new List<int>();
                foreach (int state in frontier)
                {
                    var cell = Cell.FromKey(state / 4);
                    int heading = state % 4, mask = lanes.Exits(cell);
                    for (int d = 0; d < 4; d++)
                    {
                        var n = cell.Move(d);
                        if (d == Directions.Opp(heading) || (mask & 1 << d) == 0 || !streets.Contains(n.Key) || (taken != null && taken.Contains(n.Key)))
                            continue;
                        if (depth.TryGetValue(n.Key, out int seen) && seen <= step)
                            continue;
                        depth[n.Key] = step + 1;
                        int ns = n.Key * 4 + d, nb = bends[state] + (d != heading ? 1 : 0);
                        if (bends.TryGetValue(ns, out int old) && old <= nb)
                            continue;
                        if (!bends.ContainsKey(ns))
                            next.Add(ns);
                        bends[ns] = nb;
                        from[ns] = state;
                        first[ns] = first[state];
                    }
                }
                frontier = next;
            }
            var arm = new List<Cell>();
            for (int s = best; s >= 0; s = from[s])
                arm.Add(Cell.FromKey(s / 4));
            arm.Add(plaza);
            arm.Reverse();
            return arm;
        }

        /// <summary>Long arms with few bends, a straight final stretch, ending by the town's station, and arm B straight across the plaza from arm A.</summary>
        static int Score(WorldState w, CityState city, int state, int steps, int bends, int firstStep, Dictionary<int, int> from, int otherArm)
        {
            int score = 2 * steps - 3 * bends;
            int before = from[state];
            if (before >= 0 && before % 4 == state % 4)
                score += 3;
            if (otherArm >= 0 && firstStep == Directions.Opp(otherArm))
                score += 6;
            var end = Cell.FromKey(state / 4);
            foreach (var s in w.stations)
                if (s.producerId == city.producerId && Math.Max(Math.Abs(s.cell.x - end.x), Math.Abs(s.cell.z - end.z)) <= 3)
                {
                    score += 8;
                    break;
                }
            return score;
        }

        /// <summary>
        /// Whether one end of the line leaves room to reverse: its last three cells in a straight row with no track,
        /// and the two where the tram stops free of side streets, so both kerbs can take a platform.
        /// </summary>
        static bool EndClear(List<Cell> cells, RoadLanes lanes, RailNetwork net, bool atEnd)
        {
            int n = cells.Count;
            if (n < EndCells)
                return false;
            Cell At(int k) => cells[atEnd ? n - 1 - k : k];
            if (Directions.Between(At(0), At(1)) != Directions.Between(At(1), At(2)))
                return false;
            for (int k = 0; k < EndCells; k++)
                if (net.At(At(k)) != null)
                    return false;
            return Directions.Count(lanes.Exits(At(0))) <= 2 && Directions.Count(lanes.Exits(At(1))) <= 2;
        }

        /// <summary>A plain stretch of street for a stop: straight through, two ways only, no track.</summary>
        static bool StopCell(List<Cell> cells, RoadLanes lanes, RailNetwork net, int i)
        {
            if (i <= 0 || i >= cells.Count - 1)
                return false;
            if (Directions.Between(cells[i - 1], cells[i]) != Directions.Between(cells[i], cells[i + 1]))
                return false;
            return net.At(cells[i]) == null && Directions.Count(lanes.Exits(cells[i])) == 2;
        }

        /// <summary>A tram standing with its middle at s covers only plain street, away from both reversing ends.</summary>
        static bool Clear(List<Cell> cells, RoadLanes lanes, RailNetwork net, float s)
        {
            if (s - HalfTram < EndCells || s + HalfTram > cells.Count - EndCells)
                return false;
            int from = (int)Math.Floor(s - HalfTram), to = (int)Math.Ceiling(s + HalfTram) - 1;
            for (int i = from; i <= to; i++)
                if (!StopCell(cells, lanes, net, i))
                    return false;
            return true;
        }

        /// <summary>
        /// Both ends, a central stop by the plaza when one fits, and stops about every <see cref="StopSpacing"/> cells out
        /// from it. A stop sits mid-cell or on a cell edge, wherever a whole tram fits on plain street.
        /// </summary>
        static List<float> Stops(List<Cell> cells, RoadLanes lanes, RailNetwork net, int plaza)
        {
            int n = cells.Count;
            var candidates = new List<float>();
            for (int i = 0; i < n; i++)
                foreach (float s in new[] { (float)i, i + .5f })
                    if (Clear(cells, lanes, net, s))
                        candidates.Add(s);
            var mids = new List<float>();
            float centre = -1;
            if (plaza >= 0)
            {
                float best = 3.01f;
                foreach (float s in candidates)
                    if (Math.Abs(s - (plaza + .5f)) < best)
                    {
                        best = Math.Abs(s - (plaza + .5f));
                        centre = s;
                    }
            }
            if (centre >= 0)
            {
                mids.Add(centre);
                float last = centre;
                foreach (float s in candidates)
                    if (s - last >= StopSpacing)
                    {
                        mids.Add(s);
                        last = s;
                    }
                last = centre;
                for (int k = candidates.Count - 1; k >= 0; k--)
                    if (last - candidates[k] >= StopSpacing)
                    {
                        mids.Add(candidates[k]);
                        last = candidates[k];
                    }
            }
            else
            {
                float last = EndStop;
                foreach (float s in candidates)
                    if (s - last >= StopSpacing)
                    {
                        mids.Add(s);
                        last = s;
                    }
            }
            mids.Sort();
            var stops = new List<float>(mids.Count + 2) { EndStop };
            stops.AddRange(mids);
            stops.Add(n - EndStop);
            return stops;
        }
    }
}
