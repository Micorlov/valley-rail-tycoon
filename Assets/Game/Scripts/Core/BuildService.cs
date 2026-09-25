using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    public sealed class BuildPlan
    {
        public bool valid; public string reason; public int cost; public List<Cell> path = new List<Cell>(); public List<TrackPieceState> changes = new List<TrackPieceState>();
        /// <summary>Refused only because it costs more than the player has; the rail fixer still quotes such a plan.</summary>
        public bool unaffordable;
        /// <summary>The style the plan prices its bridge in; -1 uses each site's current style (<see cref="BridgeCatalog.StyleAt"/>).</summary>
        public int bridgeStyle = -1;
    }
    public sealed class BuildService
    {
        readonly WorldState w; readonly RailNetwork net; readonly Balance balance; readonly CitySimulation cities; readonly GameEvents events; readonly Scenery scenery;
        /// <summary>Helpers built only for placement checks may leave <paramref name="scenery"/> out; they get their own view of the forest.</summary>
        public BuildService(WorldState w, RailNetwork net, Balance balance, CitySimulation cities, GameEvents events, Scenery scenery = null)
        {
            this.w = w;
            this.net = net;
            this.balance = balance;
            this.cities = cities;
            this.events = events;
            this.scenery = scenery ?? new Scenery(w);
        }
        /// <summary>True on the clear building strip beside a station platform.</summary>
        public static bool StationFootprint(WorldState w, Cell c)
        {
            foreach (var s in w.stations)
                if (StationLayout.OnStrip(s, c))
                    return true;
            return false;
        }
        // City streets may be crossed (a level crossing); buildings and the town plaza may not.
        public bool Placeable(Cell c) => !MapDefinition.Blocked(c, w) && !StationFootprint(w, c) && !cities.BlocksTrack(c) && (!MapDefinition.Water(c) || MapDefinition.Bridge(c) != 0);
        public BuildPlan Preview(Cell start, Cell end, int bridgeStyle = -1)
        {
            if (start.Equals(end))
                return Invalid("Choose a different end cell.");
            if (!Placeable(start) || !Placeable(end))
                return Invalid("Buildings, hills and open water block tracks.");
            var open = new List<int>();
            var cost = new Dictionary<int, int>();
            var parent = new Dictionary<int, int>();
            for (int d = 0; d < 4; d++)
            {
                int key = start.Key * 4 + d;
                open.Add(key);
                cost[key] = 0;
                parent[key] = -1;
            }
            int goal = -1;
            while (open.Count > 0)
            {
                int best = 0, score = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    int ck = open[i] / 4;
                    var c = Cell.FromKey(ck);
                    int f = cost[open[i]] + c.Distance(end) * 100;
                    if (f < score || (f == score && open[i] < open[best]))
                    {
                        score = f;
                        best = i;
                    }
                }
                int cur = open[best];
                open.RemoveAt(best);
                int cellKey = cur / 4;
                var cell = Cell.FromKey(cellKey);
                int incoming = cur % 4;
                if (cell.Equals(end))
                {
                    goal = cur;
                    break;
                }
                for (int d = 0; d < 4; d++)
                {
                    if (parent[cur] != -1 && d == Directions.Opp(incoming))
                        continue;
                    var next = cell.Move(d);
                    if (!Placeable(next))
                        continue;
                    // Along a light rail line only a straight crossing at right angles, with no turn on its cell.
                    if (!TramRules.TrackMayEnter(cities.TramMask(next), d) || (cities.TramMask(cell) != 0 && parent[cur] != -1 && d != incoming))
                        continue;
                    if ((MapDefinition.Water(cell) || MapDefinition.Water(next)) && d % 2 != 1)
                        continue;
                    int desired = (1 << Directions.Opp(incoming)) | (1 << d);
                    var existing = net.At(cell);
                    if (existing != null && parent[cur] != -1)
                    {
                        int merged = existing.mask | desired;
                        if (!Directions.Allows(merged, Directions.Opp(incoming), d))
                            continue;
                    }
                    int ns = next.Key * 4 + d, nc = cost[cur] + 100 + (d == incoming ? 0 : 25);
                    if (cost.TryGetValue(ns, out int old) && old <= nc)
                        continue;
                    cost[ns] = nc;
                    parent[ns] = cur;
                    if (!open.Contains(ns))
                        open.Add(ns);
                }
            }
            if (goal < 0)
                return Invalid("No valid track path. Use a marked bridge corridor.");
            var path = new List<Cell>();
            while (goal >= 0)
            {
                int k = goal / 4;
                path.Add(Cell.FromKey(k));
                goal = parent[goal];
            }
            path.Reverse();
            return ValidateBuild(path, bridgeStyle);
        }
        BuildPlan Invalid(string reason) => new BuildPlan { reason = reason };
        /// <summary>Prices straight platform track on open cells with the checks of every build: track cap, funds and one train per railway.</summary>
        /// <summary>Prices straight platform track on <paramref name="cells"/>. Cells in <paramref name="clearing"/> hold only town
        /// buildings that the caller demolishes before committing (a station upgrade), so those buildings do not block.</summary>
        public BuildPlan ValidatePlatforms(List<Cell> cells, int mask, ICollection<int> clearing = null)
        {
            var plan = new BuildPlan { path = new List<Cell>(cells) };
            var seen = new HashSet<int>();
            foreach (var c in cells)
            {
                bool cleared = clearing != null && clearing.Contains(c.Key) && !MapDefinition.Blocked(c, w) && !StationFootprint(w, c);
                if (!(cleared || Placeable(c)) || MapDefinition.Water(c) || net.At(c) != null || !seen.Add(c.Key))
                {
                    plan.reason = "Platform track is blocked.";
                    return plan;
                }
                int paid = balance.PieceCost(mask);
                plan.cost += paid;
                plan.changes.Add(new TrackPieceState { cell = c, mask = mask, paid = paid });
            }
            return Finish(plan, seen, new HashSet<int>());
        }
        public BuildPlan ValidateBuild(List<Cell> path, int bridgeStyle = -1)
        {
            var plan = new BuildPlan { path = new List<Cell>(path), bridgeStyle = bridgeStyle };
            var seen = new HashSet<int>();
            var bridges = new HashSet<int>();
            if (path.Count < 2)
            {
                plan.reason = "Choose at least two cells.";
                return plan;
            }
            for (int i = 0; i < path.Count; i++)
            {
                var c = path[i];
                if ((i > 0 && path[i - 1].Distance(c) != 1) || (i + 1 < path.Count && c.Distance(path[i + 1]) != 1))
                {
                    plan.reason = "Track cells must be adjacent.";
                    return plan;
                }
                if (!Placeable(c) || !seen.Add(c.Key))
                {
                    plan.reason = "Track path is blocked or overlaps itself.";
                    return plan;
                }
                var old = net.At(c);
                int mask = 0;
                if (i > 0)
                    mask |= 1 << Directions.Between(c, path[i - 1]);
                if (i + 1 < path.Count)
                    mask |= 1 << Directions.Between(c, path[i + 1]);
                if ((i == 0 || i == path.Count - 1) && old == null)
                {
                    int d = i == 0 ? Directions.Between(c, path[1]) : Directions.Between(c, path[i - 1]);
                    mask |= 1 << Directions.Opp(d);
                }
                mask |= old?.mask ?? 0;
                if (Directions.Count(mask) < 2 || Directions.Count(mask) > 4)
                {
                    plan.reason = "Track needs at least two connected directions.";
                    return plan;
                }
                if (i > 0 && i + 1 < path.Count && !Directions.Allows(mask, Directions.Between(c, path[i - 1]), Directions.Between(c, path[i + 1])))
                {
                    plan.reason = "This turnout would require an impossible turn.";
                    return plan;
                }
                int bridge = MapDefinition.Bridge(c);
                if (bridge != 0 && mask != 10)
                {
                    plan.reason = "Bridges must cross straight east–west.";
                    return plan;
                }
                if (old != null && old.mask == mask)
                    continue;
                if (old != null && Protected(old.id))
                {
                    plan.reason = "Park the train and clear its route before changing this track.";
                    return plan;
                }
                int paid = bridge != 0 ? (c.x == 30 ? BridgeCost(bridge, bridgeStyle) : 0) : balance.PieceCost(mask);
                plan.cost += paid - (old?.paid ?? 0);
                plan.changes.Add(new TrackPieceState { id = old?.id ?? 0, cell = c, mask = mask, paid = paid, bridge = bridge });
                if (bridge != 0)
                    bridges.Add(bridge);
            }
            return Finish(plan, seen, bridges);
        }
        /// <summary>
        /// Validates a train route between two existing pieces, such as a rail-fix route, with the rules of a dragged line.
        /// Unlike <see cref="ValidateBuild"/> the route may pass a cell twice: each visit adds its ports, and every visit must
        /// still be a legal turn through the merged piece. Unchanged pieces cost nothing. The plan's path lists each cell once.
        /// </summary>
        public BuildPlan ValidateRoute(List<Cell> route)
        {
            var plan = new BuildPlan();
            var masks = new Dictionary<int, int>();
            var seen = new HashSet<int>();
            var bridges = new HashSet<int>();
            if (route.Count < 2)
            {
                plan.reason = "Choose at least two cells.";
                return plan;
            }
            for (int i = 0; i < route.Count; i++)
            {
                if (i + 1 < route.Count && route[i].Distance(route[i + 1]) != 1)
                {
                    plan.reason = "Track cells must be adjacent.";
                    return plan;
                }
                int ports = (i > 0 ? 1 << Directions.Between(route[i], route[i - 1]) : 0) | (i + 1 < route.Count ? 1 << Directions.Between(route[i], route[i + 1]) : 0);
                masks[route[i].Key] = (masks.TryGetValue(route[i].Key, out int mask) ? mask : net.At(route[i])?.mask ?? 0) | ports;
                if (seen.Add(route[i].Key))
                    plan.path.Add(route[i]);
            }
            for (int i = 1; i + 1 < route.Count; i++)
                if (!Directions.Allows(masks[route[i].Key], Directions.Between(route[i], route[i - 1]), Directions.Between(route[i], route[i + 1])))
                {
                    plan.reason = "This turnout would require an impossible turn.";
                    return plan;
                }
            foreach (var c in plan.path)
            {
                if (!Placeable(c))
                {
                    plan.reason = "Track path is blocked.";
                    return plan;
                }
                var old = net.At(c);
                int mask = masks[c.Key];
                if (Directions.Count(mask) < 2)
                {
                    plan.reason = "Track needs at least two connected directions.";
                    return plan;
                }
                int bridge = MapDefinition.Bridge(c);
                if (bridge != 0 && mask != 10)
                {
                    plan.reason = "Bridges must cross straight east–west.";
                    return plan;
                }
                if (old != null && old.mask == mask)
                    continue;
                if (old != null && Protected(old.id))
                {
                    plan.reason = "Park the train and clear its route before changing this track.";
                    return plan;
                }
                int paid = bridge != 0 ? (c.x == 30 ? BridgeCost(bridge, -1) : 0) : balance.PieceCost(mask);
                plan.cost += paid - (old?.paid ?? 0);
                plan.changes.Add(new TrackPieceState { id = old?.id ?? 0, cell = c, mask = mask, paid = paid, bridge = bridge });
                if (bridge != 0)
                    bridges.Add(bridge);
            }
            return Finish(plan, seen, bridges);
        }
        /// <summary>
        /// Validates an AI track repair: each change sets one cell's piece, and mask 0 removes it. Removed or simplified
        /// pieces are free, with no refund. Guarded track, bridges, water and town ground (roads keep their level
        /// crossings) are never changed. <paramref name="checkFunds"/> false checks everything but the price.
        /// </summary>
        public BuildPlan ValidateRepair(IReadOnlyList<TrackPieceState> changes, bool checkFunds = true)
        {
            var plan = new BuildPlan();
            var seen = new HashSet<int>();
            foreach (var t in changes)
            {
                var c = t.cell;
                if (!seen.Add(c.Key))
                {
                    plan.reason = "A repair changes one cell twice.";
                    return plan;
                }
                if (t.mask < 0 || t.mask > 15 || Directions.Count(t.mask) == 1)
                {
                    plan.reason = "Track needs at least two connected directions.";
                    return plan;
                }
                var old = net.At(c);
                if ((old?.mask ?? 0) == t.mask)
                    continue;
                if (!Placeable(c) || MapDefinition.Water(c) || MapDefinition.Bridge(c) != 0 || cities.Occupied(c))
                {
                    plan.reason = "Track path is blocked.";
                    return plan;
                }
                if (old != null && Protected(old.id))
                {
                    plan.reason = "Park the train and clear its route before changing this track.";
                    return plan;
                }
                int paid = t.mask == 0 ? 0 : balance.PieceCost(t.mask);
                plan.cost += Math.Max(0, paid - (old?.paid ?? 0));
                plan.changes.Add(new TrackPieceState { id = old?.id ?? 0, cell = c, mask = t.mask, paid = paid });
                plan.path.Add(c);
            }
            return Finish(plan, seen, new HashSet<int>(), checkFunds);
        }
        /// <summary>Checks shared by every build: whole bridge spans, the track cap, funds and one train per railway.
        /// A change with mask 0 removes that cell's piece (repairs only).</summary>
        BuildPlan Finish(BuildPlan plan, HashSet<int> seen, HashSet<int> bridges, bool checkFunds = true)
        {
            foreach (int z in bridges)
                for (int x = 30; x <= 32; x++)
                    if (!seen.Contains(new Cell(x, z).Key) && net.At(new Cell(x, z)) == null)
                    {
                        plan.reason = "Include the entire marked bridge span.";
                        return plan;
                    }
            // Heavy track may cross a light rail line only straight over at right angles, away from its stops.
            foreach (var t in plan.changes)
                if (t.mask != 0 && !TramRules.TrackMayCross(cities.TramMask(t.cell), t.mask, t.bridge))
                {
                    plan.reason = "Track may cross a light rail line only straight over at right angles, away from its stops.";
                    return plan;
                }
            if (w.tracks.Count + plan.changes.FindAll(t => t.id == 0 && t.mask != 0).Count - plan.changes.FindAll(t => t.mask == 0).Count > 1500)
            {
                plan.reason = "Track limit reached (1,500).";
                return plan;
            }
            if (checkFunds && plan.cost > w.money)
            {
                plan.unaffordable = true;
                plan.reason = "Not enough money.";
                return plan;
            }
            // Validate connectivity on a disposable candidate world, never the live state.
            var candidate = new WorldState();
            candidate.tracks.AddRange(w.tracks);
            int next = w.nextId;
            foreach (var t in plan.changes)
            {
                candidate.tracks.RemoveAll(a => a.cell.Equals(t.cell));
                if (t.mask != 0)
                    candidate.tracks.Add(new TrackPieceState { id = t.id == 0 ? next++ : t.id, cell = t.cell, mask = t.mask });
            }
            var network = new RailNetwork(candidate);
            var occupied = new HashSet<int>();
            foreach (var train in w.trains)
            {
                int id = train.path.Count > 0 ? train.path[Math.Min(train.step, train.path.Count - 1)].trackId : net.At(StationLayout.Center(w.stations.Find(s => s.id == train.stationId), train.platform)).id;
                if (!occupied.Add(network.components[id]))
                {
                    plan.reason = "Each connected railway can contain only one train.";
                    return plan;
                }
            }
            plan.valid = true;
            plan.reason = plan.changes.Count == 0 ? "Tracks already exist." : "Ready to build";
            return plan;
        }
        public Result CommitBuild(BuildPlan preview) => CommitValidated(ValidateBuild(preview.path, preview.bridgeStyle));
        /// <summary>Builds a plan validated against the current state in this same step (never a stored preview).</summary>
        public Result CommitValidated(BuildPlan p)
        {
            if (!p.valid)
                return Result.Fail(p.reason);
            // A repair may remove pieces: trees that stood under them stay felled.
            if (p.changes.Exists(t => t.mask == 0))
                scenery.Sync();
            foreach (var t in p.changes)
            {
                w.tracks.RemoveAll(a => a.cell.Equals(t.cell));
                if (t.mask == 0)
                    continue;
                if (t.id == 0)
                    t.id = w.nextId++;
                w.tracks.Add(t);
            }
            // A new crossing keeps the style it was priced in, even if the site's default changes later.
            foreach (var t in p.changes)
                if (t.bridge != 0 && t.mask != 0)
                    BridgeCatalog.Choose(w, t.bridge, BridgeCatalog.Valid(p.bridgeStyle) ? p.bridgeStyle : BridgeCatalog.StyleAt(w, t.bridge));
            EconomyService.Spend(w, p.cost);
            w.revision++;
            net.Rebuild(w);
            // "Tracks already exist." validates with no changes and builds nothing, so it is not an event.
            if (p.changes.Count > 0)
                events.Push(GameEventKind.TrackBuilt, 0, p.path[p.path.Count - 1], p.path[0].Key, p.cost);
            return Result.Good("Railway built.");
        }
        int BridgeCost(int row, int style) => BridgeCatalog.Cost(balance, BridgeCatalog.Valid(style) ? style : BridgeCatalog.StyleAt(w, row));
        /// <summary>
        /// Rebuilds a crossing in another style for that style's full price. The railway or highway on it stays in service,
        /// so a train route over it does not block the work. An empty site just remembers the style, free, for its future bridge.
        /// </summary>
        public Result RestyleBridge(int row, int style)
        {
            if (!BridgeCatalog.Site(row) || !BridgeCatalog.Valid(style))
                return Result.Fail("Choose a bridge style.");
            if (BridgeCatalog.StyleAt(w, row) == style)
                return Result.Fail("The bridge already has that style.");
            if (!BridgeCatalog.HasRailway(w, row) && !BridgeCatalog.HasHighway(w, row))
            {
                BridgeCatalog.Choose(w, row, style);
                return Result.Good(BridgeCatalog.Name(style) + " chosen for this crossing.");
            }
            int cost = BridgeCatalog.Cost(balance, style);
            if (cost > w.money)
                return Result.Fail("Not enough money.");
            BridgeCatalog.Choose(w, row, style);
            // The span's price sits on its west piece, as on a new bridge, so bulldozing refunds half of this style.
            foreach (var t in w.tracks)
                if (t.bridge == row && t.cell.x == 30)
                    t.paid = cost;
            EconomyService.Spend(w, cost);
            w.revision++;
            w.cityRevision++;
            events.Push(GameEventKind.TrackBuilt, 0, new Cell(32, row), new Cell(30, row).Key, cost);
            return Result.Good(BridgeCatalog.Name(style) + " bridge built.");
        }
        public bool Protected(int trackId)
        {
            var track = net.ids[trackId];
            foreach (var s in w.stations)
                if (Platform(s, track.cell))
                    return true;
            return OnRoute(w, trackId);
        }
        /// <summary>True when any train's route uses the track on either leg. The legs swap at every stop, so both must be protected.</summary>
        public static bool OnRoute(WorldState w, int trackId)
        {
            foreach (var t in w.trains)
            {
                foreach (var step in t.path)
                    if (step.trackId == trackId)
                        return true;
                foreach (var step in t.returnPath)
                    if (step.trackId == trackId)
                        return true;
            }
            return false;
        }
        public static bool Platform(StationState s, Cell c) => StationLayout.PlatformAt(s, c) >= 0;
        public Result Bulldoze(Cell cell)
        {
            // Trees under what is about to go are gone for good; the cleared ground stays bare.
            scenery.Sync();
            var served = TramLines.ServingBuilding(w, cell);
            if (served != null)
                return Result.Fail("A light rail line serves this stadium. Remove the line first.");
            var demolition = cities.Bulldoze(cell);
            if (demolition != null)
            {
                if (demolition.ok)
                    events.Push(GameEventKind.Bulldozed, 0, cell, 2);
                return demolition;
            }
            var station = w.stations.Find(s => Platform(s, cell));
            if (station != null)
            {
                if (TramLines.UsesStation(w, station.id))
                    return Result.Fail("A light rail line starts at this station. Remove the line first.");
                if (w.trains.Exists(t => t.stationId == station.id || t.a == station.id || t.b == station.id))
                    return Result.Fail("Sell or reroute the train before removing its station.");
                w.stations.Remove(station);
                EconomyService.Credit(w, station.paid / 2, false);
                w.revision++;
                net.Rebuild(w);
                events.Push(GameEventKind.Bulldozed, station.id, cell, 1);
                return Result.Good("Station removed. Its platform track remains.");
            }
            var track = net.At(cell);
            if (track == null && cities.HasTram(cell))
                return Result.Fail("Light rail track: tap the line and use REMOVE LINE.");
            if (track == null)
                return FellTree(cell);
            var remove = track.bridge == 0 ? new List<TrackPieceState> { track } : w.tracks.FindAll(t => t.bridge == track.bridge);
            foreach (var t in remove)
                if (Protected(t.id))
                    return Result.Fail("Track belongs to a station or train route.");
            foreach (var t in remove)
            {
                w.tracks.Remove(t);
                EconomyService.Credit(w, t.paid / 2, false);
            }
            w.revision++;
            net.Rebuild(w);
            events.Push(GameEventKind.Bulldozed, track.id, cell, 0);
            return Result.Good("Track removed.");
        }
        Result FellTree(Cell cell)
        {
            if (!scenery.TreeAt(cell))
                return Result.Fail("Tap track, a station, a town building or a tree.");
            if (w.money < balance.treeClearCost)
                return Result.Fail($"Clearing a tree costs ${balance.treeClearCost:N0}.");
            scenery.Fell(cell);
            EconomyService.Spend(w, balance.treeClearCost);
            events.Push(GameEventKind.Bulldozed, 0, cell, 3);
            return Result.Good($"Tree cleared for ${balance.treeClearCost:N0}.");
        }
    }
}
