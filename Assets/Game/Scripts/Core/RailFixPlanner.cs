using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>What the rail fixer proposes for one parked train: a priced route to its best delivery stop, or why there is none.</summary>
    public sealed class RailFix
    {
        public bool valid; public string reason; public int trainId, fromStationId, toStationId;
        /// <summary>The whole route from platform to platform; only new or altered pieces are in its changes. Null when no route exists.</summary>
        public BuildPlan build;
        /// <summary>A station the fix builds first because no existing stop can take the cargo; the route ends there. Null otherwise.</summary>
        public StationPlan newStation;
        /// <summary>True when the stop is a city transfer station that holds the cargo for a later train (CargoTransfer).</summary>
        public bool transfer;
        /// <summary>Everything the fix costs: new track plus any new station.</summary>
        public int cost;
    }
    /// <summary>
    /// The "AI FIX RAILS" helper. For a parked train it searches the cheapest route to every compatible stop: existing track
    /// wherever trains can already pass, new track across open ground, and as a last resort an extra port on an unprotected
    /// piece. BuildService prices the winner with the rules of a dragged line and the train pathfinder confirms both legs
    /// before it is offered. Other trains' railways are never entered, so the one-train-per-railway rule holds.
    /// Stops are tried in tiers: stops that use the cargo, then city transfer stations that hold it for a later train, and when
    /// neither works a new station at the nearest industry or town that uses it, priced together with its route.
    /// </summary>
    public sealed class RailFixPlanner
    {
        // Every cell travelled costs Travel, so reusing track beats building unless the detour is long;
        // altering an existing piece also pays AlterPenalty because it can change how other lines behave.
        const int Travel = 60, AlterPenalty = 400;
        // A new station goes to one of the nearest MaxTargets users of the cargo, at one of the MaxSites sites closest to the
        // train; SiteReach is how far from an industry (or a town's streets) a platform centre may sit and stay in catchment.
        const int MaxTargets = 2, MaxSites = 6, SiteReach = 4;
        /// <summary>A priced route to one platform of an existing (or planned) stop.</summary>
        sealed class Stop
        {
            public StationState station; public BuildPlan plan; public Cell end; public int score;
        }
        struct Visit
        {
            public Cell cell; public int entry, exit;
            public Visit(Cell cell, int entry, int exit)
            {
                this.cell = cell;
                this.entry = entry;
                this.exit = exit;
            }
        }
        readonly WorldState w; readonly RailNetwork net; readonly RailPathfinder paths; readonly BuildService build; readonly TrainSimulation trains; readonly Balance b;
        readonly StationService stations; readonly CitySimulation cities; readonly Scenery scenery;
        readonly StateHeap open = new StateHeap();
        readonly HashSet<int> guarded = new HashSet<int>(), blocked = new HashSet<int>();
        int[] cost, parent;
        /// <summary>Without <paramref name="stations"/> the planner never proposes a new station (the what-if planners it builds itself).</summary>
        public RailFixPlanner(WorldState w, RailNetwork net, RailPathfinder paths, BuildService build, TrainSimulation trains, Balance b,
            StationService stations = null, CitySimulation cities = null, Scenery scenery = null)
        {
            this.w = w;
            this.net = net;
            this.paths = paths;
            this.build = build;
            this.trains = trains;
            this.b = b;
            this.stations = stations;
            this.cities = cities;
            this.scenery = scenery;
        }
        public RailFix Plan(int trainId)
        {
            var fix = new RailFix { trainId = trainId };
            var t = trains.Train(trainId);
            if (t == null)
                return Refuse(fix, "Train not found.");
            if (t.state != ServiceState.Parked)
                return Refuse(fix, "Park the train first: tap RETURN TO STATION, then ask the AI again.");
            if (t.units > 0)
                return Refuse(fix, "Cargo is still aboard. Resume the route to deliver it first.");
            var origin = trains.Station(t.stationId);
            if (origin == null || net.At(StationLayout.Center(origin, t.platform)) == null)
                return Refuse(fix, "The train is not at a station.");
            var start = StationLayout.Center(origin, t.platform);
            fix.fromStationId = origin.id;
            Prepare(t);
            bool direct = false, feeder = false;
            string problem = null;
            var best = Choose(origin, start, stop => trains.Delivers(origin, stop, t.cargo), ref direct, ref problem);
            string note = null;
            if (best == null)
            {
                best = Choose(origin, start, stop => trains.Feeds(origin, stop, t.cargo), ref feeder, ref problem);
                fix.transfer = best != null;
                if (best != null)
                    note = TransferNote(best.station, t.cargo);
            }
            if (best == null)
                return stations != null ? PlanNewStation(fix, t, origin, start, direct, problem) : Refuse(fix, problem ?? "No route.");
            return Quote(fix, origin, best, note == null ? Diagnose(start, best.end) : Diagnose(start, best.end) + "\n" + note);
        }
        /// <summary>
        /// The best priced route to any platform of an eligible stop. Among those, a plan the player can pay for now beats a
        /// cheaper-to-run one they cannot. Null when none can be built; <paramref name="compatible"/> records that a stop existed.
        /// </summary>
        Stop Choose(StationState origin, Cell start, Func<StationState, bool> eligible, ref bool compatible, ref string problem)
        {
            Stop best = null, bestAffordable = null;
            foreach (var stop in w.stations)
            {
                if (stop.id == origin.id || !eligible(stop))
                    continue;
                compatible = true;
                // Any platform of the stop will do; each is its own candidate.
                for (int k = 0; k < StationLayout.Platforms(stop); k++)
                {
                    var end = StationLayout.Center(stop, k);
                    if (net.At(end) == null || Blocked(net.At(end)))
                        continue;
                    int score = Search(start, end, out var route);
                    if (route == null || (bestAffordable != null && score >= bestAffordable.score))
                        continue;
                    var plan = Price(route, start, end, ref problem);
                    if (plan == null)
                        continue;
                    var candidate = new Stop { station = stop, plan = plan, end = end, score = score };
                    if (plan.valid)
                        bestAffordable = candidate;
                    if (best == null || score < best.score)
                        best = candidate;
                }
            }
            return bestAffordable ?? best;
        }
        /// <summary>Fills in the offer for <paramref name="best"/>: the stop, what is wrong today, the work and its price.</summary>
        RailFix Quote(RailFix fix, StationState origin, Stop best, string diagnosis)
        {
            fix.toStationId = fix.newStation == null ? best.station.id : 0;
            fix.build = best.plan;
            fix.cost = best.plan.cost + (fix.newStation?.cost ?? 0);
            string stop = fix.newStation == null ? best.station.name : "new " + best.station.name;
            fix.reason = $"{origin.name} → {stop}\n{diagnosis}\n{Work(fix)}";
            if (!best.plan.valid || fix.cost > w.money)
            {
                fix.reason += $"\nThe fix costs ${fix.cost:N0}; you have ${w.money:N0}.";
                return fix;
            }
            fix.valid = true;
            fix.reason += "\nGreen marks the route the train will run.";
            return fix;
        }
        static string Work(RailFix fix)
        {
            int added = fix.build.changes.FindAll(c => c.id == 0).Count, altered = fix.build.changes.Count - added;
            var parts = new List<string>();
            if (fix.newStation != null)
                parts.Add("build the station");
            if (added > 0)
                parts.Add($"lay {added} new track piece{(added == 1 ? "" : "s")}");
            if (altered > 0)
                parts.Add($"rewire {altered} existing piece{(altered == 1 ? "" : "s")}");
            return parts.Count == 0 ? "No construction needed." : $"Fix: {string.Join(" + ", parts)} · ${fix.cost:N0}";
        }
        string TransferNote(StationState stop, Cargo cargo)
        {
            string name = IndustryCatalog.CargoName(cargo).ToLowerInvariant();
            return $"{Producer(stop.producerId).name} is a transfer station: it holds the {name} for a second train to take on. Half the fare is paid on drop-off, the rest on final delivery.";
        }
        ProducerState Producer(int id) => w.producers.Find(p => p.id == id);
        /// <summary>
        /// Plans again against the current state, builds the fix and starts the route. With <paramref name="quoted"/> (the fix the
        /// player was shown) it refuses when the railway has changed since, so the player never pays for a plan they did not see.
        /// </summary>
        public Result Apply(int trainId, RailFix quoted = null)
        {
            var fix = Plan(trainId);
            if (!fix.valid)
                return Result.Fail(fix.reason);
            if (quoted != null && Differs(fix, quoted))
                return Result.Fail("The railway changed since the AI looked. Tap AI FIX RAILS again.");
            if (fix.newStation == null)
                return Commit(fix, "AI fixed the line to");
            var placed = stations.Place(fix.newStation, fix.newStation.producerId);
            if (!placed.ok)
                return placed;
            // The station is now an ordinary stop, and the same search on the same railway finds the same route to it.
            var route = Plan(trainId);
            if (!route.valid || route.toStationId != placed.id)
                return Result.Fail($"{trains.Station(placed.id).name} is built, but its route changed. Tap AI FIX RAILS again.");
            return Commit(route, "AI built a station and fixed the line to");
        }
        static bool Differs(RailFix fix, RailFix quoted)
        {
            if (quoted.build == null || fix.toStationId != quoted.toStationId || fix.build.cost != quoted.build.cost || fix.cost != quoted.cost || fix.build.changes.Count != quoted.build.changes.Count)
                return true;
            if (fix.newStation == null || quoted.newStation == null)
                return fix.newStation != quoted.newStation;
            return !fix.newStation.center.Equals(quoted.newStation.center) || fix.newStation.axis != quoted.newStation.axis || fix.newStation.producerId != quoted.newStation.producerId;
        }
        Result Commit(RailFix fix, string done)
        {
            if (fix.build.changes.Count > 0)
            {
                var built = build.CommitValidated(fix.build);
                if (!built.ok)
                    return built;
            }
            var route = trains.AssignRoute(fix.trainId, fix.fromStationId, fix.toStationId);
            return route.ok ? Result.Good($"{done} {trains.Station(fix.toStationId).name}. All aboard!") : route;
        }
        static RailFix Refuse(RailFix fix, string reason)
        {
            fix.reason = reason;
            return fix;
        }
        string Diagnose(Cell origin, Cell stop)
        {
            if (paths.FindPath(origin, stop) != null && paths.FindPath(stop, origin) != null)
                return "The track already runs there.";
            if (net.components[net.At(origin).id] == net.components[net.At(stop).id])
                return "Problem: the tracks join, but a junction blocks the way. Trains can only turn onto or off a junction's stem, never run straight across it.";
            return "Problem: these stations are not connected by track.";
        }
        /// <summary>
        /// No existing stop works: prices a new station at the nearest industry or town that uses the cargo, together with the
        /// route to it, by planning on a copy of the world that already has the station. Nothing live changes until Apply.
        /// </summary>
        RailFix PlanNewStation(RailFix fix, TrainState t, StationState origin, Cell start, bool compatible, string problem)
        {
            var from = Producer(origin.producerId);
            var targets = Targets(from, t.cargo, start);
            string name = IndustryCatalog.CargoName(t.cargo).ToLowerInvariant();
            if (targets.Count == 0)
                return Refuse(fix, $"No other stop on the map can use {name} from {from.name}.");
            Stop best = null;
            StationPlan bestSite = null;
            bool bestAffordable = false;
            foreach (var target in targets)
                foreach (var site in Sites(target, start))
                {
                    // Travel per cell is a lower bound on any route there, so a site that cannot beat an affordable best is skipped.
                    if (bestAffordable && Travel * site.center.Distance(start) + site.cost >= best.score + bestSite.cost)
                        continue;
                    var stop = Evaluate(site, t, origin, ref problem);
                    if (stop == null)
                        continue;
                    // Station price plus route weight: running length and track both count, as in the route search.
                    bool affordable = stop.plan.valid && stop.plan.cost + site.cost <= w.money;
                    if (best == null || (affordable && !bestAffordable) || (affordable == bestAffordable && stop.score + site.cost < best.score + bestSite.cost))
                    {
                        best = stop;
                        bestSite = site;
                        bestAffordable = affordable;
                    }
                }
            if (best != null)
            {
                fix.newStation = bestSite;
                string why = compatible ? $"Every {name} stop is blocked or unreachable" : $"No station takes {name} yet";
                return Quote(fix, origin, best, $"{why}, so the AI will build one at {Producer(bestSite.producerId).name}.");
            }
            return Refuse(fix, problem ?? (compatible
                ? $"Every {name} stop is blocked by hills, water or another train's railway (one train per railway), and there is no room for a new station at {targets[0].name}. Build a station for this train elsewhere."
                : $"No other station can take {name} from here, and the AI found no room for one at {targets[0].name} that this train can reach. Build a station there, then ask the AI again."));
        }
        /// <summary>The industries or towns nearest the train that would complete this cargo's journey from <paramref name="from"/>.</summary>
        List<ProducerState> Targets(ProducerState from, Cargo cargo, Cell start)
        {
            var list = w.producers.FindAll(p => p.id != from.id && CargoTransfer.Delivers(from.kind, p.kind, cargo));
            list.Sort((x, y) => x.cell.Distance(start) != y.cell.Distance(start) ? x.cell.Distance(start).CompareTo(y.cell.Distance(start)) : x.id.CompareTo(y.id));
            return list.GetRange(0, Math.Min(MaxTargets, list.Count));
        }
        /// <summary>Buildable 3 × 1 station sites serving <paramref name="target"/>, the ones closest to the train first.</summary>
        List<StationPlan> Sites(ProducerState target, Cell start)
        {
            var city = target.kind == ProducerKind.Town ? cities.CityFor(target.id) : null;
            int minX = target.cell.x, maxX = target.cell.x, minZ = target.cell.z, maxZ = target.cell.z;
            if (target.kind == ProducerKind.SkiResort)
            {
                minX -= SkiResorts.Half; maxX += SkiResorts.Half; minZ -= SkiResorts.Half; maxZ += SkiResorts.Half;
            }
            if (city != null)
                foreach (var road in city.roads)
                {
                    minX = Math.Min(minX, road.cell.x);
                    maxX = Math.Max(maxX, road.cell.x);
                    minZ = Math.Min(minZ, road.cell.z);
                    maxZ = Math.Max(maxZ, road.cell.z);
                }
            var cells = new List<Cell>();
            for (int x = minX - SiteReach; x <= maxX + SiteReach; x++)
                for (int z = minZ - SiteReach; z <= maxZ + SiteReach; z++)
                {
                    var c = new Cell(x, z);
                    var piece = MapDefinition.InBounds(c) ? net.At(c) : null;
                    if (!MapDefinition.InBounds(c) || !build.Placeable(c) || cities.Occupied(c) || (piece != null && !Directions.Straight(piece.mask)))
                        continue;
                    if (city != null ? cities.Distance(city, c) <= SiteReach : (target.kind == ProducerKind.SkiResort ? SkiResorts.Distance(target, c) : c.Distance(target.cell)) <= SiteReach)
                        cells.Add(c);
                }
            // Closest cells first; once MaxSites sites are found, farther cells cannot make the cut, so planning stops there.
            cells.Sort((x, y) => x.Distance(start) != y.Distance(start) ? x.Distance(start).CompareTo(y.Distance(start)) : x.Key.CompareTo(y.Key));
            var sites = new List<StationPlan>();
            foreach (var c in cells)
            {
                if (sites.Count >= MaxSites && c.Distance(start) > sites[MaxSites - 1].center.Distance(start))
                    break;
                for (int axis = 0; axis < 2; axis++)
                {
                    var site = stations.Plan(c, axis, StationLayout.MinLength, 1, target.id, quote: true);
                    if (site.valid)
                        sites.Add(site);
                }
            }
            sites.Sort((x, y) => x.center.Distance(start) != y.center.Distance(start) ? x.center.Distance(start).CompareTo(y.center.Distance(start))
                : x.cost != y.cost ? x.cost.CompareTo(y.cost) : x.center.Key != y.center.Key ? x.center.Key.CompareTo(y.center.Key) : x.axis.CompareTo(y.axis));
            return sites.GetRange(0, Math.Min(MaxSites, sites.Count));
        }
        /// <summary>
        /// The route to a station that is not built yet: plans on a copy of the world with the station and its platform track
        /// already in place. The copy has its own track and station lists; nothing live is touched.
        /// </summary>
        Stop Evaluate(StationPlan site, TrainState t, StationState origin, ref string problem)
        {
            var copy = w.PlanningCopy();
            foreach (var piece in site.track.changes)
                copy.tracks.Add(new TrackPieceState { id = copy.nextId++, cell = piece.cell, mask = piece.mask, paid = piece.paid });
            var station = new StationState { id = copy.nextId++, cell = site.center, axis = site.axis, side = site.side, length = site.length, platforms = site.platforms, producerId = site.producerId, name = Producer(site.producerId).name + " Station" };
            copy.stations.Add(station);
            var network = new RailNetwork(copy);
            var finder = new RailPathfinder(network);
            var builder = new BuildService(copy, network, b, cities, null, scenery);
            var what = new TrainSimulation(copy, network, finder, b, new CargoService(copy, b, cities, null), null);
            var planner = new RailFixPlanner(copy, network, finder, builder, what, b);
            planner.Prepare(t);
            bool compatible = false;
            return planner.Choose(origin, StationLayout.Center(origin, t.platform), stop => stop.id == station.id, ref compatible, ref problem);
        }
        /// <summary>Protected pieces (platforms and every train's route legs, as in BuildService.Protected) and other trains' railways.</summary>
        void Prepare(TrainState train)
        {
            guarded.Clear();
            blocked.Clear();
            foreach (var s in w.stations)
                foreach (var c in StationLayout.Cells(s))
                {
                    var piece = net.At(c);
                    if (piece != null)
                        guarded.Add(piece.id);
                }
            foreach (var other in w.trains)
            {
                foreach (var step in other.path)
                    guarded.Add(step.trackId);
                foreach (var step in other.returnPath)
                    guarded.Add(step.trackId);
                if (other.id == train.id)
                    continue;
                // Mirrors ExclusiveNetworkPolicy: a train holds the railway of its current step, or of its station when parked.
                var station = trains.Station(other.stationId);
                int occupied = other.path.Count > 0 ? other.path[Math.Min(other.step, other.path.Count - 1)].trackId : net.At(StationLayout.Center(station, other.platform)).id;
                blocked.Add(net.components[occupied]);
            }
        }
        bool Blocked(TrackPieceState piece) => piece != null && blocked.Contains(net.components[piece.id]);
        /// <summary>A* over (cell, entry port) from the origin platform to the stop's platform. Returns the route's weight.</summary>
        int Search(Cell from, Cell to, out List<Visit> route)
        {
            route = null;
            var first = net.At(from);
            if (cost == null)
            {
                cost = new int[MapDefinition.Size * MapDefinition.Size * 4];
                parent = new int[cost.Length];
            }
            for (int i = 0; i < cost.Length; i++)
                cost[i] = int.MaxValue;
            open.Clear();
            // Leave along the platform only; the platform piece itself is never altered.
            for (int exit = 0; exit < 4; exit++)
                if ((first.mask & (1 << exit)) != 0)
                    Push(from, exit, from, to, Travel, -1 - exit);
            while (open.Count > 0)
            {
                open.Pop(out int f, out int state);
                var cell = Cell.FromKey(state >> 2);
                int g = cost[state], entry = state & 3;
                if (f != g + Heuristic(cell, to))
                    continue; // A cheaper copy of this state was already expanded.
                if (cell.Equals(to))
                {
                    route = Unwind(state, from);
                    return g;
                }
                for (int exit = 0; exit < 4; exit++)
                {
                    int step = exit == entry ? -1 : StepCost(cell, entry, exit);
                    if (step >= 0)
                        Push(cell, exit, from, to, g + step, state);
                }
            }
            return int.MaxValue;
        }
        static int Heuristic(Cell c, Cell to) => Travel * c.Distance(to);
        void Push(Cell cell, int exit, Cell from, Cell to, int g, int parentState)
        {
            var next = cell.Move(exit);
            if (!MapDefinition.InBounds(next) || next.Equals(from) || !build.Placeable(next))
                return;
            // Water is crossed only straight east–west on a marked bridge row (Placeable refuses the rest of the river).
            if ((MapDefinition.Water(cell) || MapDefinition.Water(next)) && exit % 2 != 1)
                return;
            var piece = net.At(next);
            if (Blocked(piece))
                return;
            int entry = Directions.Opp(exit);
            // Arrive along the destination platform so it is never altered.
            if (next.Equals(to) && (piece.mask & (1 << entry)) == 0)
                return;
            int state = next.Key * 4 + entry;
            if (g >= cost[state])
                return;
            cost[state] = g;
            parent[state] = parentState;
            open.Push(g + Heuristic(next, to), state);
        }
        /// <summary>Weight of passing through a cell, or -1 when that move is impossible or not allowed.</summary>
        int StepCost(Cell cell, int entry, int exit)
        {
            var piece = net.At(cell);
            int ports = (1 << entry) | (1 << exit);
            if (piece == null)
                return Travel + (MapDefinition.Bridge(cell) != 0 ? (cell.x == 30 ? b.bridgeCost : 0) : b.PieceCost(ports));
            int merged = piece.mask | ports;
            if (!Directions.Allows(merged, entry, exit))
                return -1;
            if (merged == piece.mask)
                return Travel;
            if (piece.bridge != 0 || guarded.Contains(piece.id))
                return -1;
            return Travel + AlterPenalty + Math.Max(0, b.PieceCost(merged) - piece.paid);
        }
        List<Visit> Unwind(int goal, Cell from)
        {
            var states = new List<int>();
            int state = goal;
            while (state >= 0)
            {
                states.Add(state);
                state = parent[state];
            }
            states.Reverse();
            // The search seeds the origin's exits as parent -1 - exit.
            var route = new List<Visit> { new Visit(from, -1, -1 - state) };
            for (int i = 0; i < states.Count; i++)
            {
                int exit = i + 1 < states.Count ? Directions.Opp(states[i + 1] & 3) : -1;
                route.Add(new Visit(Cell.FromKey(states[i] >> 2), states[i] & 3, exit));
            }
            return route;
        }
        /// <summary>
        /// Turns a route into final port masks, prices them and proves both legs on a candidate network.
        /// Returns null (with <paramref name="problem"/>) when the route cannot be built; an unaffordable plan is returned invalid.
        /// </summary>
        BuildPlan Price(List<Visit> route, Cell from, Cell to, ref string problem)
        {
            // BuildService merges the ports of repeated cells and checks every turn, exactly as the search walked them.
            var plan = build.ValidateRoute(route.ConvertAll(v => v.cell));
            if (!plan.valid && !plan.unaffordable)
            {
                problem = "The AI's route was refused: " + plan.reason;
                return null;
            }
            var candidate = new WorldState();
            candidate.tracks.AddRange(w.tracks);
            int next = w.nextId;
            foreach (var t in plan.changes)
            {
                candidate.tracks.RemoveAll(a => a.cell.Equals(t.cell));
                candidate.tracks.Add(new TrackPieceState { id = t.id == 0 ? next++ : t.id, cell = t.cell, mask = t.mask });
            }
            var check = new RailPathfinder(new RailNetwork(candidate));
            if (check.FindPath(from, to) == null || check.FindPath(to, from) == null)
            {
                problem = "The AI could not prove a route both ways. Remove some tangled track and try again.";
                return null;
            }
            return plan;
        }
        /// <summary>Binary min-heap of search states ordered by priority, then state, so plans are deterministic.</summary>
        sealed class StateHeap
        {
            long[] items = new long[1024];
            public int Count { get; private set; }
            public void Clear() => Count = 0;
            public void Push(int priority, int state)
            {
                if (Count == items.Length)
                    Array.Resize(ref items, Count * 2);
                long item = ((long)priority << 32) | (uint)state;
                int i = Count++;
                while (i > 0 && items[(i - 1) / 2] > item)
                {
                    items[i] = items[(i - 1) / 2];
                    i = (i - 1) / 2;
                }
                items[i] = item;
            }
            public void Pop(out int priority, out int state)
            {
                long top = items[0], last = items[--Count];
                int i = 0;
                while (true)
                {
                    int child = 2 * i + 1;
                    if (child >= Count)
                        break;
                    if (child + 1 < Count && items[child + 1] < items[child])
                        child++;
                    if (items[child] >= last)
                        break;
                    items[i] = items[child];
                    i = child;
                }
                items[i] = last;
                priority = (int)(top >> 32);
                state = (int)(top & 0xffffffff);
            }
        }
    }
}
