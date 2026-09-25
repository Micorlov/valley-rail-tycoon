using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>A priced light rail route from a station's forecourt to a venue, ready for TramService.Build.</summary>
    public sealed class TramPlan
    {
        public bool valid, unaffordable; public string reason;
        public TramVenue venue; public int stationId;
        public List<Cell> cells = new List<Cell>();
        public List<Cell> trees = new List<Cell>();
        public int trackCost, stopCost, clearingCost, tramCost, cost, streetCells, crossings;
    }
    /// <summary>
    /// Lays out a light rail line. The transfer stop stands on a station's forecourt, two cells out from the platform on the
    /// station building's side, and the line leaves it straight, away from the tracks; it ends straight on a cell beside the
    /// venue. Between them an A* search over cell and heading prefers town streets and highways (the trams run in the road),
    /// crosses open ground where it must, and keeps off buildings, squares, car parks, hills, water (except a finished road
    /// bridge), stations and their room to grow, and other lines. It may cross a straight railway at right angles, away
    /// from stations and its own stops. Plans only: nothing changes until TramService.Build.
    /// </summary>
    public sealed class TramPlanner
    {
        public const int StreetStep = 80, OpenStep = 140, BridgeStep = 120, Turn = 40, TreeStep = 30, CrossingStep = 400;
        /// <summary>A crossing stays this many cells (Chebyshev) from any platform, and this many from the line's own stops.</summary>
        public const int CrossingClearance = 4, StopClearance = 3;
        /// <summary>How far (Manhattan, forecourt to venue) a station may be to be offered for a line.</summary>
        public const int Reach = 60, StationChoices = 3;
        const int States = MapDefinition.Size * MapDefinition.Size * 4;
        readonly WorldState w; readonly RailNetwork net; readonly Balance b; readonly CitySimulation cities; readonly Scenery scenery;
        readonly int[] cost = new int[States], parent = new int[States], count = new int[States];
        readonly bool[] closed = new bool[States];
        // Town tram lines (TownTramLine): 0 off them, else 1 + the axis a straight stretch runs along, 3 where it bends or ends.
        readonly byte[] townTram = new byte[MapDefinition.Size * MapDefinition.Size];
        readonly bool[] envelope = new bool[MapDefinition.Size * MapDefinition.Size], nearPlatform = new bool[MapDefinition.Size * MapDefinition.Size], target = new bool[MapDefinition.Size * MapDefinition.Size], nearStop = new bool[MapDefinition.Size * MapDefinition.Size];
        readonly List<int> touched = new List<int>(4096);
        public TramPlanner(WorldState w, RailNetwork net, Balance b, CitySimulation cities, Scenery scenery)
        {
            this.w = w;
            this.net = net;
            this.b = b;
            this.cities = cities;
            this.scenery = scenery;
            Array.Fill(cost, int.MaxValue);
        }
        /// <summary>Town railway stations a line to the venue may start from, nearest first (at most StationChoices).</summary>
        public List<StationState> Stations(TramVenue venue)
        {
            var list = new List<StationState>();
            foreach (var s in w.stations)
            {
                var p = w.producers.Find(x => x.id == s.producerId);
                if (p != null && p.kind == ProducerKind.Town && Forecourt(s).Distance(venue.cell) <= Reach)
                    list.Add(s);
            }
            list.Sort((a, z) =>
            {
                int da = Forecourt(a).Distance(venue.cell), dz = Forecourt(z).Distance(venue.cell);
                return da != dz ? da.CompareTo(dz) : a.id.CompareTo(z.id);
            });
            if (list.Count > StationChoices)
                list.RemoveRange(StationChoices, list.Count - StationChoices);
            return list;
        }
        /// <summary>The forecourt cell in front of the station's middle: two cells out from track 0 on the building's side.</summary>
        public static Cell Forecourt(StationState s) => s.cell.Move(s.side).Move(s.side);
        public TramPlan Plan(TramVenue venue, int stationId, bool quote = false)
        {
            var plan = new TramPlan { venue = venue, stationId = stationId };
            var station = w.stations.Find(s => s.id == stationId);
            var town = station == null ? null : w.producers.Find(p => p.id == station.producerId);
            if (station == null || town == null || town.kind != ProducerKind.Town)
                return Refuse(plan, "Choose a town's railway station for the transfer stop.");
            if (!venue.open)
                return Refuse(plan, venue.name + " is not open yet: " + venue.status + ".");
            if (w.tramLines != null && w.tramLines.Exists(l => venue.Matches(l)))
                return Refuse(plan, venue.name + " already has a light rail line.");
            if (w.tramLines != null && w.tramLines.Count >= TramCatalog.MaxLines)
                return Refuse(plan, $"Light rail line limit reached ({TramCatalog.MaxLines}).");
            var targets = TramVenues.Targets(w, venue);
            if (targets.Count == 0)
                return Refuse(plan, "There is no room for a tram stop at " + venue.name + ".");
            if (!Search(station, targets, plan.cells))
                return Refuse(plan, "No clear light rail route from " + station.name + " to " + venue.name + ". Buildings, hills, water or other stations are in the way.");
            if (plan.cells.Count < TramCatalog.MinCells)
                return Refuse(plan, venue.name + " is too close to " + station.name + " for a tram line.");
            Price(plan);
            if (!quote && plan.cost > w.money)
            {
                plan.unaffordable = true;
                return Refuse(plan, "Not enough money.");
            }
            plan.valid = true;
            plan.reason = "Ready to build";
            return plan;
        }
        static TramPlan Refuse(TramPlan plan, string reason)
        {
            plan.reason = reason;
            return plan;
        }
        void Price(TramPlan plan)
        {
            plan.trackCost = plan.clearingCost = plan.streetCells = plan.crossings = 0;
            plan.trees.Clear();
            foreach (var c in plan.cells)
            {
                bool street = (cities.Bits(c) & (CitySimulation.Road | CitySimulation.Highway)) != 0;
                if (MapDefinition.Water(c))
                    plan.trackCost += TramCatalog.BridgeCell;
                else if (street)
                {
                    plan.trackCost += TramCatalog.StreetCell;
                    plan.streetCells++;
                }
                else
                    plan.trackCost += TramCatalog.OpenCell;
                if (net.At(c) != null)
                {
                    plan.trackCost += TramCatalog.Crossing;
                    plan.crossings++;
                }
                if (scenery.TreeAt(c))
                {
                    plan.trees.Add(c);
                    plan.clearingCost += b.treeClearCost;
                }
            }
            plan.stopCost = TramCatalog.TransferStop + TramCatalog.VenueStop;
            plan.tramCost = TramCatalog.Price;
            plan.cost = plan.trackCost + plan.stopCost + plan.clearingCost + plan.tramCost;
        }
        /// <summary>Ground a tram may run on. Water only on a finished road bridge, crossed east-west.</summary>
        bool Open(Cell c)
        {
            if (!MapDefinition.InBounds(c) || MapDefinition.Raised(c) || MapDefinition.Blocked(c, w) || Coast.Beach(c) || envelope[c.Key])
                return false;
            if ((cities.Bits(c) & ~(CitySimulation.Road | CitySimulation.Highway)) != 0)
                return false;
            if (MapDefinition.Water(c))
            {
                int row = MapDefinition.Bridge(c);
                return row != 0 && (cities.Bits(c) & CitySimulation.Highway) != 0 && net.At(c) == null && BridgeCatalog.HasHighway(w, row);
            }
            return true;
        }
        /// <summary>Whether the tram may move from <paramref name="c"/> (entered heading <paramref name="h"/>) on in direction <paramref name="d"/>.</summary>
        bool Step(Cell c, int h, int d, Cell n)
        {
            if (!Open(n))
                return false;
            // Over water, on railway crossings and across a town's tram line the tram runs straight through.
            if ((MapDefinition.Water(c) || net.At(c) != null || townTram[c.Key] != 0) && d != h)
                return false;
            // A town tram line may only be crossed at right angles where it runs straight.
            if (townTram[n.Key] != 0 && (townTram[n.Key] == 3 || townTram[n.Key] - 1 == d % 2 || target[n.Key]))
                return false;
            if (MapDefinition.Water(n) && d % 2 != 1)
                return false;
            var track = net.At(n);
            if (track != null && (track.bridge != 0 || !Directions.Straight(track.mask) || (track.mask & (1 << d)) != 0 || nearPlatform[n.Key] || nearStop[n.Key] || target[n.Key]))
                return false;
            return true;
        }
        int StepCost(Cell c, int h, int d, Cell n)
        {
            int bits = cities.Bits(n);
            int step = MapDefinition.Water(n) ? BridgeStep : (bits & (CitySimulation.Road | CitySimulation.Highway)) != 0 ? StreetStep : OpenStep;
            if (d != h)
                step += Turn;
            if (net.At(n) != null)
                step += CrossingStep;
            if (scenery.TreeAt(n))
                step += TreeStep;
            return step;
        }
        void Mark(Cell c, bool[] map, int radius)
        {
            for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var n = new Cell(c.x + dx, c.z + dz);
                    if (MapDefinition.InBounds(n))
                        map[n.Key] = true;
                }
        }
        void PrepareMaps(List<Cell> targets)
        {
            Array.Clear(envelope, 0, envelope.Length);
            Array.Clear(nearPlatform, 0, nearPlatform.Length);
            Array.Clear(target, 0, target.Length);
            Array.Clear(nearStop, 0, nearStop.Length);
            foreach (var s in w.stations)
            {
                // The station's room to grow: every length and platform count an upgrade may reach, with its building strip.
                int away = Directions.Opp(s.side);
                for (int a = StationLayout.First(StationLayout.MaxLength); a <= StationLayout.Last(StationLayout.MaxLength); a++)
                    for (int k = -1; k < StationLayout.MaxPlatforms; k++)
                    {
                        var c = StationLayout.Along(new Cell(s.cell.x + Directions.Dx[away] * k, s.cell.z + Directions.Dz[away] * k), s.axis, a);
                        if (MapDefinition.InBounds(c))
                            envelope[c.Key] = true;
                    }
                foreach (var c in StationLayout.Cells(s))
                    Mark(c, nearPlatform, CrossingClearance);
            }
            foreach (var c in targets)
            {
                target[c.Key] = true;
                Mark(c, nearStop, StopClearance);
            }
            MarkTownTrams();
        }
        /// <summary>The lines towns run through their own streets, planned as their view plans them: around this network's cells.</summary>
        void MarkTownTrams()
        {
            Array.Clear(townTram, 0, townTram.Length);
            RoadLanes lanes = null;
            foreach (var city in w.cities)
            {
                if (city.tram == 0)
                    continue;
                lanes = lanes ?? RoadLanes.Build(w, net);
                var line = TownTramLine.Plan(w, city, lanes, net, cities.HasTram);
                if (line == null)
                    continue;
                for (int i = 0; i < line.cells.Count; i++)
                {
                    var c = line.cells[i];
                    bool straight = i > 0 && i + 1 < line.cells.Count && Directions.Between(line.cells[i - 1], c) == Directions.Between(c, line.cells[i + 1]);
                    townTram[c.Key] = (byte)(straight ? 1 + Directions.Between(c, line.cells[i + 1]) % 2 : 3);
                }
            }
        }
        /// <summary>A* from the forecourt of <paramref name="station"/> to any target, both ends straight. Fills <paramref name="path"/>.</summary>
        bool Search(StationState station, List<Cell> targets, List<Cell> path)
        {
            PrepareMaps(targets);
            Reset();
            var forecourt = Forecourt(station);
            // Transfer stop candidates along the forecourt, a cell past each end of the longest platform.
            for (int a = StationLayout.First(StationLayout.MaxLength) - 1; a <= StationLayout.Last(StationLayout.MaxLength) + 1; a++)
            {
                var c0 = StationLayout.Along(forecourt, station.axis, a);
                foreach (int h in new[] { station.side, station.axis == 1 ? 1 : 0, station.axis == 1 ? 3 : 2 })
                {
                    var c1 = c0.Move(h);
                    var c2 = c1.Move(h);
                    if (!Open(c0) || net.At(c0) != null || MapDefinition.Water(c0) || !Step(c0, h, h, c1) || !Step(c1, h, h, c2))
                        continue;
                    if (target[c0.Key] || target[c1.Key])
                        continue;
                    // The first three cells are one straight run; the search carries on from the third.
                    int seed = c2.Key * 4 + h, g = StepCost(c0, h, h, c1) + StepCost(c1, h, h, c2) + Math.Abs(a) * 10;
                    if (g >= cost[seed])
                        continue;
                    Touch(seed);
                    cost[seed] = g;
                    parent[seed] = -1 - (c0.Key * 4 + h);
                    count[seed] = 3;
                    Push(seed, g + Heuristic(c2, targets));
                }
            }
            int goal = -1;
            while (heapKeys.Count > 0)
            {
                int state = Pop();
                if (closed[state])
                    continue;
                closed[state] = true;
                var c = Cell.FromKey(state >> 2);
                int h = state & 3;
                if (count[state] >= TramCatalog.MaxCells)
                    continue;
                for (int d = 0; d < 4; d++)
                {
                    if (d == Directions.Opp(h))
                        continue;
                    var n = c.Move(d);
                    if (!Step(c, h, d, n))
                        continue;
                    int next = n.Key * 4 + d, g = cost[state] + StepCost(c, h, d, n);
                    // A stop is reached straight on: the cell before it must continue in the same direction.
                    if (target[n.Key] && d == h && count[state] + 1 >= TramCatalog.MinCells && !OnPath(state, n))
                    {
                        Touch(next);
                        cost[next] = g;
                        parent[next] = state;
                        count[next] = count[state] + 1;
                        goal = next;
                        break;
                    }
                    if (target[n.Key] || closed[next] || g >= cost[next])
                        continue;
                    Touch(next);
                    cost[next] = g;
                    parent[next] = state;
                    count[next] = count[state] + 1;
                    Push(next, g + Heuristic(n, targets));
                }
                if (goal >= 0)
                    break;
            }
            if (goal < 0)
                return false;
            Trace(goal, path);
            // The search may, in odd corners, cross its own path; such a line cannot be built.
            var cellsSeen = new HashSet<int>();
            foreach (var c in path)
                if (!cellsSeen.Add(c.Key))
                {
                    path.Clear();
                    return false;
                }
            return true;
        }
        bool OnPath(int state, Cell c)
        {
            for (int s = state; s >= 0; s = parent[s])
                if ((s >> 2) == c.Key)
                    return true;
            return false;
        }
        void Trace(int goal, List<Cell> path)
        {
            path.Clear();
            int s = goal;
            while (s >= 0)
            {
                path.Add(Cell.FromKey(s >> 2));
                s = parent[s];
            }
            // The seed's parent encodes the first cell and the heading of the straight start.
            int origin = -1 - s;
            var c0 = Cell.FromKey(origin >> 2);
            path.Add(c0.Move(origin & 3));
            path.Add(c0);
            path.Reverse();
        }
        static int Heuristic(Cell c, List<Cell> targets)
        {
            int best = int.MaxValue;
            foreach (var t in targets)
                best = Math.Min(best, t.Distance(c));
            return best * StreetStep;
        }
        void Touch(int state) => touched.Add(state);
        void Reset()
        {
            foreach (int s in touched)
            {
                cost[s] = int.MaxValue;
                closed[s] = false;
            }
            touched.Clear();
            heapKeys.Clear();
        }
        // A binary heap of (priority << 18 | state) keys: states stay below 2^16, so the low bits never reach the priority.
        void Push(int state, int priority)
        {
            long key = ((long)priority << 18) | (uint)state;
            heapKeys.Add(key);
            int i = heapKeys.Count - 1;
            while (i > 0)
            {
                int up = (i - 1) / 2;
                if (heapKeys[up] <= heapKeys[i])
                    break;
                (heapKeys[up], heapKeys[i]) = (heapKeys[i], heapKeys[up]);
                i = up;
            }
        }
        readonly List<long> heapKeys = new List<long>(1024);
        int Pop()
        {
            long top = heapKeys[0];
            int last = heapKeys.Count - 1;
            heapKeys[0] = heapKeys[last];
            heapKeys.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < heapKeys.Count && heapKeys[l] < heapKeys[m]) m = l;
                if (r < heapKeys.Count && heapKeys[r] < heapKeys[m]) m = r;
                if (m == i)
                    break;
                (heapKeys[m], heapKeys[i]) = (heapKeys[i], heapKeys[m]);
                i = m;
            }
            return (int)(top & ((1 << 18) - 1));
        }
    }
}
