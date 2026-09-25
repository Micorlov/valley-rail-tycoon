using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    public struct Notification
    {
        // CityFounded: settlers founded the city; value is the id of the nearest other city, or 0.
        public const int LevelChanged = 1, CivicBuilt = 2, BeachRoad = 3, BeachOpened = 4, ServiceOpened = 5, CityFounded = 6, SkiRoad = 7, SkiOpened = 8, TownTramOpened = 9, CampOpened = 10;
        public int kind, cityId, value;
    }
    /// <summary>
    /// Aggregate town growth. Each city evaluates once per game minute on its own tick slot: railway service earns
    /// growth points and points buy one building action at a time (a municipal service, a new building beside a street,
    /// a new street on the block grid, or an upgrade). Streets only ever extend from streets, so every town is one
    /// connected network around its plaza. Everything is integer and reads only WorldState, so a saved game replays
    /// identically, and every scratch collection is pre-sized so steady-state growth allocates nothing.
    /// </summary>
    public sealed partial class CitySimulation
    {
        // Resort: a beach car park's cells, held from the day its road is planned (see Coast).
        // Service: a roadside service area's cells, held from the day it is planned (see Roadside).
        // Camp: a campsite's cells, held from the day it is planned (see Campsites).
        public const byte Building = 1, Road = 2, Plaza = 4, Highway = 8, Resort = 16, Service = 32, Camp = 64;
        // Cleared cells hold a demolished 8×8 landmark's whole footprint.
        const int Slots = 60, ClearedCapacity = 80, NotificationCapacity = 16, ScratchCapacity = 2048;
        readonly WorldState w; readonly RailNetwork net; readonly Balance b; readonly CityBalance cb;
        readonly byte[] grid = new byte[MapDefinition.Size * MapDefinition.Size];
        /// <summary>Highway route generation. Saves with a lower WorldState.roadLayout are straightened once when a session starts.</summary>
        public const int RoadLayout = 1; // 1: fewest bends among the shortest routes
        const int States = MapDefinition.Size * MapDefinition.Size * 4;
        // Highway planning buffers, kept so a new road allocates only its own path. A search state is a cell times the
        // heading the road arrived with (key * 4 + heading), so the planner can count the road's bends.
        readonly int[] steps = new int[MapDefinition.Size * MapDefinition.Size];
        readonly int[] bends = new int[States], cameFrom = new int[States], states = new int[States];
        // Per cell: whether a road may be built there, worked out once per search (0 unknown, 1 yes, 2 no).
        readonly byte[] goal = new byte[MapDefinition.Size * MapDefinition.Size], site = new byte[MapDefinition.Size * MapDefinition.Size];
        readonly int[] heading = new int[4];
        // Summed-area table over a town's reach, for landmark placement.
        readonly int[] area;
        // The same over the homes and businesses a landmark may replace when a packed town has no free site (ClearedSite).
        readonly int[] homeArea;
        readonly HashSet<int> smallLots = new HashSet<int>(ScratchCapacity);
        /// <summary>The homes and businesses the landmark site LargeSite last found replaces; empty when it takes only free land.</summary>
        readonly List<Cell> siteClearance = new List<Cell>(MaxCleared);
        /// <summary>Most homes or businesses a landmark replaces, and what each costs a site in distance from the plaza.</summary>
        const int MaxCleared = 3, ClearCost = 12;
        /// <summary>The new street cells the landmark site LargeSite last found lays to reach the town's streets.</summary>
        readonly List<Cell> siteLink = new List<Cell>(MaxLink);
        /// <summary>Longest link street a landmark lays to reach its site, and what each cell costs a site in distance from the plaza.</summary>
        const int MaxLink = 3, LinkCost = 6;
        readonly Dictionary<int, CityState> byProducer = new Dictionary<int, CityState>(16);
        readonly List<int> candidates = new List<int>(ScratchCapacity), scores = new List<int>(ScratchCapacity), scratch = new List<int>(32);
        readonly HashSet<int> seen = new HashSet<int>(ScratchCapacity);
        // What Search returns when no route exists: shared and never written to, so a failed search allocates nothing.
        static readonly List<Cell> NoPath = new List<Cell>(0);
        public readonly Queue<Notification> Notifications = new Queue<Notification>(NotificationCapacity);
        public CitySimulation(WorldState w, RailNetwork net, Balance b)
        {
            this.w = w;
            this.net = net;
            this.b = b;
            cb = b.city;
            int reach = 0;
            foreach (int r in cb.influenceRadius)
                reach = Math.Max(reach, r);
            reach += BuildingCatalog.MaxSize;
            area = new int[(2 * reach + 2) * (2 * reach + 2)];
            homeArea = new int[area.Length];
            Rebuild();
        }
        /// <summary>Rebuilds the occupancy grid and indexes from the world; also reserves list capacities lost by JSON round trips.</summary>
        public void Rebuild()
        {
            w.intercityRoads = w.intercityRoads ?? new List<IntercityRoadState>();
            Array.Clear(grid, 0, grid.Length);
            foreach (var road in w.intercityRoads)
            {
                foreach (var cell in road.path)
                    if (MapDefinition.InBounds(cell)) grid[cell.Key] |= Highway;
                if (road.ToBeach)
                    ReservePark(road);
                if (Roadside.Planned(road) && Roadside.Shaped(road))
                    ReserveService(road);
                if (Campsites.Planned(road) && Campsites.Shaped(road))
                    ReserveCamp(road);
            }
            ReserveTrams();
            byProducer.Clear();
            foreach (var city in w.cities)
            {
                byProducer[city.producerId] = city;
                if (city.buildings.Capacity < cb.maxBuildings)
                    city.buildings.Capacity = cb.maxBuildings;
                if (city.roads.Capacity < cb.maxRoads)
                    city.roads.Capacity = cb.maxRoads;
                if (city.cleared.Capacity < ClearedCapacity)
                    city.cleared.Capacity = ClearedCapacity;
                foreach (var bs in city.buildings)
                    for (int i = 0; i < CityLayout.FootprintCells(bs); i++)
                    {
                        var cell = CityLayout.FootprintCell(bs, i);
                        if (MapDefinition.InBounds(cell))
                            grid[cell.Key] |= Building;
                    }
                foreach (var r in city.roads)
                    if (MapDefinition.InBounds(r.cell))
                        grid[r.cell.Key] |= r.cell.Equals(city.center) ? Plaza : Road;
            }
        }
        public CityState CityFor(int producerId) => byProducer.TryGetValue(producerId, out var c) ? c : null;
        public bool Occupied(Cell c) => MapDefinition.InBounds(c) && grid[c.Key] != 0;
        public bool HasBuilding(Cell c) => MapDefinition.InBounds(c) && (grid[c.Key] & Building) != 0;
        /// <summary>A plain town street: not the plaza, a highway, a beach car park or a roadside service area.</summary>
        public bool IsStreet(Cell c) => MapDefinition.InBounds(c) && (grid[c.Key] & Road) != 0 && (grid[c.Key] & (Plaza | Highway | Resort | Service)) == 0;
        /// <summary>Town ground nothing may clear, named for the player ("The town square"), or null.</summary>
        public string Landmark(Cell c)
        {
            if (!MapDefinition.InBounds(c))
                return null;
            int g = grid[c.Key];
            if ((g & Plaza) != 0)
                return "The town square";
            if ((g & Highway) != 0)
                return "A highway";
            if ((g & Resort) != 0)
                return "A beach car park";
            return (g & Service) != 0 ? "A roadside service area" : null;
        }
        /// <summary>Buildings, the town plaza, beach car parks and roadside service areas block tracks; ordinary streets may be crossed.</summary>
        public bool BlocksTrack(Cell c) => MapDefinition.InBounds(c) && (grid[c.Key] & (Building | Plaza | Resort | Service | Camp)) != 0;
        public CityState CityAt(Cell c)
        {
            if (!Occupied(c))
                return null;
            foreach (var city in w.cities)
            {
                foreach (var bs in city.buildings)
                    if (CityLayout.Covers(bs, c))
                        return city;
                foreach (var r in city.roads)
                    if (r.cell.Equals(c))
                        return city;
            }
            return null;
        }
        /// <summary>Manhattan distance from a cell to the nearest building or street of the city.</summary>
        public int Distance(CityState city, Cell c)
        {
            int best = int.MaxValue;
            foreach (var bs in city.buildings)
                for (int i = 0; i < CityLayout.FootprintCells(bs); i++)
                    best = Math.Min(best, CityLayout.FootprintCell(bs, i).Distance(c));
            foreach (var r in city.roads)
                best = Math.Min(best, r.cell.Distance(c));
            return best;
        }
        /// <summary>Creates a town's day-0 city: the plaza on the producer cell with a main street through it, five homes and the town hall around it.</summary>
        public static CityState Found(WorldState w, ProducerState p, Balance b) => Found(w, p, b, CityLayout.DayZero);
        /// <summary>Creates a town's city: the plaza on the producer cell with a main street through it and the given lots around it.</summary>
        public static CityState Found(WorldState w, ProducerState p, Balance b, CityLayout.Lot[] lots)
        {
            var city = new CityState { id = w.nextId++, producerId = p.id, name = p.name, center = p.cell, rng = Seed(p.id) };
            city.roads.Add(new RoadState { cell = p.cell });
            foreach (int dx in CityLayout.DayZeroStreets)
                city.roads.Add(new RoadState { cell = new Cell(p.cell.x + dx, p.cell.z) });
            foreach (var lot in lots)
                city.buildings.Add(new BuildingState { cell = new Cell(p.cell.x + lot.dx, p.cell.z + lot.dz), def = lot.def });
            w.cities.Add(city);
            Recount(city, p, b);
            return city;
        }
        /// <summary>
        /// Rebuilds every town once on the connected street grid (saves made before <see cref="CityLayout.Current"/>):
        /// the day-0 town where its cells are still free, then growth without points until the old population is back.
        /// Tracks, stations and highways stay; the town grows around them.
        /// </summary>
        public void Relayout()
        {
            foreach (var city in w.cities)
            {
                var p = Producer(city.producerId);
                int target = city.population, guard = cb.maxBuildings + cb.maxRoads;
                city.buildings.Clear();
                city.roads.Clear();
                Rebuild();
                city.roads.Add(new RoadState { cell = city.center });
                grid[city.center.Key] |= Plaza;
                for (int i = 0; i < CityLayout.DayZeroStreets.Length; i++)
                {
                    int dx = CityLayout.DayZeroStreets[i];
                    var c = new Cell(city.center.x + dx, city.center.z);
                    if (StreetSite(city, c, dx > 0 ? 1 : 3))
                        AddStreet(city, c);
                }
                foreach (var lot in CityLayout.DayZero)
                {
                    var c = new Cell(city.center.x + lot.dx, city.center.z + lot.dz);
                    if (net.At(c) == null && Claimable(city, c))
                        Place(city, c, lot.def);
                }
                Recount(city, p, b);
                while (city.population < target && guard-- > 0 && Act(city, p)) { }
            }
            Notifications.Clear();
            w.cityLayout = CityLayout.Current;
            w.cityRevision++;
        }
        public static void Derive(CityState city, Balance b, out int population, out int jobs, out CityLevel level, out int production, out int storage)
        {
            population = jobs = 0;
            foreach (var bs in city.buildings)
            {
                var d = BuildingCatalog.Get(bs.def);
                population += d.population;
                jobs += d.jobs;
            }
            int l = 0;
            foreach (int threshold in b.city.levelThresholds)
                if (population >= threshold)
                    l++;
            level = (CityLevel)l;
            production = b.city.passengerBase + (population + jobs) / b.city.residentsPerPassenger;
            if (city.tram != 0)
                production += production * b.city.tramPassengerPercent / 100;
            storage = b.storage + population / b.city.storagePerResident;
        }
        public static void Recount(CityState city, ProducerState p, Balance b)
        {
            Derive(city, b, out city.population, out city.jobs, out city.level, out p.production, out p.storage);
            if (p.inventory > p.storage)
                p.inventory = p.storage;
        }
        public void Step()
        {
            // Owned filling stations pay whether or not the towns grow.
            if (w.tick % ServiceSales.PayPeriod == ServiceSales.PayPhase) PayServices();
            if (!cb.growthEnabled || w.tick % 20 != 0)
                return;
            if (w.tick % 200 == 0) DevelopRoads();
            if (w.tick % 200 == 100) DevelopServices();
            if (w.tick % 200 == 160) DevelopCamps();
            if (w.tick % FoundingPeriod == FoundingPhase) DevelopSettlements();
            if (w.tick % TramPeriod == TramPhase) DevelopTrams();
            int slot = (int)(w.tick / 20 % Slots);
            for (int i = 0; i < w.cities.Count; i++)
            {
                if (i % Slots == slot)
                    Evaluate(w.cities[i]);
                StepFund(w.cities[i], slot, i);
            }
        }
        /// <summary>The highway linking two towns, or null. A plain loop: DevelopRoads asks every 200 ticks and must not allocate.</summary>
        public IntercityRoadState RoadBetween(int a, int b)
        {
            foreach (var r in w.intercityRoads)
                if ((r.a == a && r.b == b) || (r.a == b && r.b == a))
                    return r;
            return null;
        }
        public string RoadStatus(CityState city)
        {
            int complete = 0; IntercityRoadState building = null, beach = null;
            foreach (var road in w.intercityRoads)
                if (road.a == city.producerId || road.b == city.producerId)
                {
                    if (road.ToBeach) beach = road;
                    else if (road.ToSki && road.Complete) continue; // an open ski road is not a link to another town
                    else if (road.Complete) complete++;
                    else building = road;
                }
            if (building != null)
                return $"Road construction: {building.built * 100 / building.path.Count}% · {complete} open";
            if (beach != null && !beach.Complete)
                return $"Beach road construction: {beach.built * 100 / beach.path.Count}% · {complete} open";
            if (beach != null && beach.park < Coast.ParkSteps)
                return $"Beach car park: stage {beach.park + 1} of {Coast.ParkSteps} · {complete} roads open";
            return complete > 0 ? $"Roads: {complete} open{(beach != null ? " + beach" : "")} · 35% fewer rail passengers on linked routes"
                : beach != null ? "Beach open · roads to towns develop when both reach Village"
                : "Roads develop when both towns reach Village";
        }
        void DevelopRoads()
        {
            foreach (var road in w.intercityRoads)
                if (!road.Complete)
                {
                    road.built++;
                    SkiRoadOpened(road);
                    w.cityRevision++;
                    return;
                }
            // A finished beach road gets its car park next, a stage at a time; the last stage opens the beach.
            foreach (var road in w.intercityRoads)
                if (road.ToBeach && road.park < Coast.ParkSteps)
                {
                    road.park++;
                    var town = CityFor(road.a);
                    if (road.park == Coast.ParkSteps && town != null)
                        Notify(Notification.BeachOpened, town.id, 0);
                    w.cityRevision++;
                    return;
                }
            CityState from = null, to = null; int distance = int.MaxValue;
            for (int i = 0; i < w.cities.Count; i++)
                for (int j = i + 1; j < w.cities.Count; j++)
                {
                    var a = w.cities[i]; var z = w.cities[j];
                    int d = a.center.Distance(z.center);
                    if (a.level < CityLevel.Village || z.level < CityLevel.Village ||
                        RoadBetween(a.producerId, z.producerId) != null || d >= distance || !Links(i, j)) continue;
                    from = a; to = z; distance = d;
                }
            // Towns link up first; a beach road waits until no pair of towns has a road to build.
            var path = from == null ? null : PlanRoad(from, to);
            if (path == null || path.Count == 0)
            {
                // Then beach roads, then the roads to the ski resorts (CitySimulation.Ski).
                int roads = w.intercityRoads.Count;
                PlanBeachRoad();
                if (w.intercityRoads.Count == roads)
                    PlanSkiRoad();
                return;
            }
            w.intercityRoads.Add(new IntercityRoadState { a = from.producerId, b = to.producerId, path = path, built = 1 });
            // Reserve the corridor against future buildings while retaining railway level crossings.
            foreach (var cell in path) grid[cell.Key] |= Highway;
            w.cityRevision++;
        }
        /// <summary>The town's road to its beach car park, or null. A plain loop: DevelopRoads asks every 200 ticks.</summary>
        public IntercityRoadState BeachRoadOf(int producerId)
        {
            foreach (var r in w.intercityRoads)
                if (r.ToBeach && r.a == producerId)
                    return r;
            return null;
        }
        /// <summary>
        /// Starts a beach road for the Village nearest the coast (within Coast.Reach) that has none yet. When a town finds
        /// no free car park site or no route, the next nearest one tries. Allocates only for the new road's path.
        /// </summary>
        void PlanBeachRoad()
        {
            int lastDistance = -1, last = -1;
            for (int attempt = 0; attempt < w.cities.Count; attempt++)
            {
                int best = -1, distance = int.MaxValue;
                for (int i = 0; i < w.cities.Count; i++)
                {
                    var city = w.cities[i];
                    int d = Coast.EntranceX - city.center.x;
                    bool later = d > lastDistance || (d == lastDistance && i > last);
                    if (!later || d < 1 || d > Coast.Reach || d >= distance || city.level < CityLevel.Village || BeachRoadOf(city.producerId) != null)
                        continue;
                    best = i; distance = d;
                }
                if (best < 0 || BuildBeachRoad(w.cities[best]))
                    return;
                lastDistance = distance; last = best;
            }
        }
        /// <summary>Plans the town's road from its streets to a free car park site on the coast facing it.</summary>
        bool BuildBeachRoad(CityState town)
        {
            if (!BeachSite(town.center.z, out var entrance))
                return false;
            BeginSearch();
            // The road reaches its car park only through the entrance, never across the car park's own ground.
            for (int i = 0; i < Coast.ParkCells; i++)
                site[Coast.ParkCell(entrance, i).Key] = 2;
            int count = 0;
            foreach (var r in town.roads) count = SeedSearch(r.cell, count);
            goal[entrance.Key] = 1;
            var path = Search(count, entrance.x - town.center.x, entrance.z - town.center.z);
            goal[entrance.Key] = 0;
            if (path.Count < 2)
                return false;
            var road = new IntercityRoadState { a = town.producerId, b = Coast.Resort, path = path, built = 1 };
            w.intercityRoads.Add(road);
            foreach (var cell in path) grid[cell.Key] |= Highway;
            ReservePark(road);
            Notify(Notification.BeachRoad, town.id, 0);
            w.cityRevision++;
            return true;
        }
        /// <summary>The car park entrance nearest the given row whose car park fits on free, flat, open ground.</summary>
        bool BeachSite(int row, out Cell entrance)
        {
            for (int k = 0; k <= 2 * Coast.SiteSearch; k++)
            {
                // Rows 0, +1, -1, +2, -2 ... away from the town's own row.
                entrance = new Cell(Coast.EntranceX, row + (k + 1) / 2 * (k % 2 == 1 ? 1 : -1));
                if (Coast.IsEntrance(entrance) && RoadSite(entrance) && ParkFree(entrance))
                    return true;
            }
            entrance = default;
            return false;
        }
        bool ParkFree(Cell entrance)
        {
            for (int i = 0; i < Coast.ParkCells; i++)
            {
                var c = Coast.ParkCell(entrance, i);
                if (!MapDefinition.InBounds(c) || grid[c.Key] != 0 || MapDefinition.Raised(c) || MapDefinition.Water(c) ||
                    MapDefinition.Blocked(c, w) || net.At(c) != null || BuildService.StationFootprint(w, c))
                    return false;
            }
            return true;
        }
        /// <summary>Holds a beach car park's cells from the day its road is planned, so towns, tracks and stations stay off them.</summary>
        void ReservePark(IntercityRoadState road)
        {
            var entrance = Coast.Entrance(road);
            for (int i = 0; i < Coast.ParkCells; i++)
            {
                var cell = Coast.ParkCell(entrance, i);
                if (MapDefinition.InBounds(cell)) grid[cell.Key] |= Resort;
            }
        }
        /// <summary>
        /// The route between two towns' streets: the shortest one and, among those, the one with the fewest bends, so a
        /// highway runs in long straight lines instead of a staircase. Reuses preallocated buffers; only the path allocates.
        /// </summary>
        List<Cell> PlanRoad(CityState from, CityState to)
        {
            BeginSearch();
            int count = 0;
            // Leave from the town's own streets and stop on the other town's streets, so the highway joins both grids.
            foreach (var r in from.roads) count = SeedSearch(r.cell, count);
            foreach (var r in to.roads) goal[r.cell.Key] = 1;
            var path = Search(count, to.center.x - from.center.x, to.center.z - from.center.z);
            foreach (var r in to.roads) goal[r.cell.Key] = 0;
            return path;
        }
        /// <summary>
        /// Re-routes every highway of an older save between its own two ends with the planner above, so staircase
        /// highways become straight runs. A road keeps its route when no route is as short (the land may have changed).
        /// </summary>
        public void StraightenRoads()
        {
            foreach (var road in w.intercityRoads)
            {
                Cell start = road.path[0], end = road.path[road.path.Count - 1];
                BeginSearch();
                int count = SeedSearch(start, 0);
                goal[end.Key] = 1;
                var path = Search(count, end.x - start.x, end.z - start.z);
                goal[end.Key] = 0;
                if (path.Count < 2 || path.Count > road.path.Count)
                    continue;
                road.built = road.Complete ? path.Count : Math.Min(road.built, path.Count - 1);
                // A service area belongs to its stretch of road: a re-routed road plans a new one.
                if (!SameRoute(road.path, path))
                {
                    road.service = road.serviceAt = road.serviceSide = 0;
                    road.serviceOwned = false;
                    road.camp = road.campAt = road.campSide = 0;
                }
                road.path = path;
            }
            Rebuild();
            w.roadLayout = RoadLayout;
            w.cityRevision++;
        }
        void BeginSearch()
        {
            Array.Fill(steps, -1);
            Array.Clear(site, 0, site.Length);
        }
        bool Site(Cell c)
        {
            if (!MapDefinition.InBounds(c)) return false;
            if (site[c.Key] == 0) site[c.Key] = RoadSite(c) ? (byte)1 : (byte)2;
            return site[c.Key] == 1;
        }
        /// <summary>Starts the search on a cell in every heading, so the first stretch of road counts no bend.</summary>
        int SeedSearch(Cell c, int count)
        {
            if (!Site(c) || steps[c.Key] != -1) return count;
            steps[c.Key] = 0;
            for (int h = 0; h < 4; h++)
            {
                int s = c.Key * 4 + h;
                bends[s] = 0; cameFrom[s] = -1; states[count++] = s;
            }
            return count;
        }
        /// <summary>
        /// Breadth-first over (cell, heading), one road length per layer. Each layer is finished before the next one
        /// starts, so every state holds the fewest bends of any shortest route to it; the straightest goal wins.
        /// </summary>
        List<Cell> Search(int count, int dx, int dz)
        {
            // Try the headings towards the goal first, so equally straight routes keep the old preference.
            int along = dx >= 0 ? 1 : 3, across = dz >= 0 ? 0 : 2;
            heading[0] = Math.Abs(dx) >= Math.Abs(dz) ? along : across;
            heading[1] = heading[0] == along ? across : along;
            heading[2] = Directions.Opp(heading[1]);
            heading[3] = Directions.Opp(heading[0]);
            for (int start = 0; start < count;)
            {
                int end = count, best = -1;
                for (int i = start; i < end; i++)
                    if (goal[states[i] >> 2] != 0 && (best < 0 || bends[states[i]] < bends[best]))
                        best = states[i];
                if (best >= 0)
                    return Trace(best);
                for (int i = start; i < end; i++)
                    count = Expand(states[i], count);
                start = end;
            }
            return NoPath;
        }
        int Expand(int state, int count)
        {
            var c = Cell.FromKey(state >> 2);
            int arrived = state & 3, length = steps[c.Key] + 1;
            var track = net.At(c);
            foreach (int d in heading)
            {
                // Cars cross rails only straight over at right angles, so a road never turns on or runs along a track.
                if (track != null && d != arrived)
                    continue;
                var n = c.Move(d);
                if (!Site(n) || !RoadLanes.Crosses(track, d) || !RoadLanes.Crosses(net.At(n), d))
                    continue;
                if (steps[n.Key] == -1)
                {
                    steps[n.Key] = length;
                    for (int h = 0; h < 4; h++) bends[n.Key * 4 + h] = int.MaxValue;
                }
                else if (steps[n.Key] != length)
                    continue;
                int next = n.Key * 4 + d, turns = bends[state] + (d == arrived ? 0 : 1);
                if (turns >= bends[next])
                    continue;
                if (bends[next] == int.MaxValue) states[count++] = next;
                bends[next] = turns; cameFrom[next] = state;
            }
            return count;
        }
        List<Cell> Trace(int state)
        {
            var path = new List<Cell>(steps[state >> 2] + 1);
            for (int s = state; s >= 0; s = cameFrom[s]) path.Add(Cell.FromKey(s >> 2));
            path.Reverse();
            return path;
        }
        bool RoadSite(Cell c) => MapDefinition.InBounds(c) && !MapDefinition.Raised(c) &&
            (!MapDefinition.Water(c) || MapDefinition.Bridge(c) != 0) && !MapDefinition.Blocked(c, w) &&
            !HasBuilding(c) && (grid[c.Key] & (Plaza | Resort | Service | Camp)) == 0 && !BuildService.StationFootprint(w, c);
        void Evaluate(CityState city)
        {
            var p = Producer(city.producerId);
            int cost = cb.actionCost[(int)city.level], station = ServedStationLevel(city);
            // A big station a train serves lets the town put up more buildings a minute, and keeps the points to pay for them.
            int maxActions = cb.maxActions + StationCatalog.ExtraBuildings(station);
            city.growthPoints = Math.Min(city.growthPoints + Rate(city, station), (2 + StationCatalog.ExtraBuildings(station)) * cost);
            city.bucket = (city.bucket + 1) % 4;
            city.paxIn[city.bucket] = city.paxOut[city.bucket] = city.goodsIn[city.bucket] = 0;
            for (int i = city.cleared.Count - 1; i >= 0; i--)
                if (city.cleared[i].untilTick <= w.tick)
                    city.cleared.RemoveAt(i);
            int actions = 0;
            while (actions < maxActions && city.growthPoints >= cost)
            {
                if (!Act(city, p))
                {
                    city.blockedEvaluations++;
                    city.growthPoints = Math.Min(city.growthPoints, cost);
                    return;
                }
                city.growthPoints -= cost;
                actions++;
                cost = cb.actionCost[(int)city.level]; // a level-up raises the price of the next action
            }
            if (actions > 0)
                city.blockedEvaluations = 0;
        }
        public int Rate(CityState city) => Rate(city, ServedStationLevel(city));
        /// <summary>Growth points per minute. A big station a train serves speeds the whole rate up (StationCatalog.GrowthPercent).</summary>
        int Rate(CityState city, int station)
        {
            int rate = cb.basePoints;
            rate += Math.Min(Sum(city.paxIn) / cb.paxDivisor, cb.paxCap);
            rate += Math.Min(Sum(city.paxOut) / cb.paxDivisor, cb.paxCap);
            rate += Math.Min(Sum(city.goodsIn) / cb.goodsDivisor, cb.goodsCap);
            rate += Math.Min(Connections(city), cb.connectionCap) * cb.connectionPoints;
            rate += Math.Min(ServedStations(city), cb.stationCap) * cb.stationPoints;
            rate += StationCatalog.GrowthBonus(station);
            rate += Math.Min(CivicCount(city), cb.civicCap) * cb.civicPoints;
            if (city.tram != 0)
                rate += cb.tramPoints;
            return rate * (100 + StationCatalog.GrowthPercent(station)) / 100;
        }
        public int ActionCost(CityState city) => cb.actionCost[(int)city.level];
        public int RecentPassengers(CityState city) => Sum(city.paxIn) + Sum(city.paxOut);
        static int Sum(int[] a)
        {
            int s = 0;
            foreach (int v in a)
                s += v;
            return s;
        }
        /// <summary>Distinct other producers that trains routed at this city's stations travel to.</summary>
        public int Connections(CityState city)
        {
            scratch.Clear();
            foreach (var t in w.trains)
            {
                if (t.a == 0)
                    continue;
                var sa = Station(t.a);
                var sb = Station(t.b);
                if (sa == null || sb == null)
                    continue;
                int other = sa.producerId == city.producerId ? sb.producerId : sb.producerId == city.producerId ? sa.producerId : 0;
                if (other == 0 || other == city.producerId || scratch.Contains(other))
                    continue;
                scratch.Add(other);
            }
            return scratch.Count;
        }
        /// <summary>Growth points from the town's best-upgraded station that a routed train uses (StationCatalog.GrowthBonus).</summary>
        public int StationGrowthBonus(CityState city) => StationCatalog.GrowthBonus(ServedStationLevel(city));
        /// <summary>The rung of the town's best-upgraded station that a routed train uses; 0 when none is served.</summary>
        public int ServedStationLevel(CityState city)
        {
            int best = 0;
            foreach (var s in w.stations)
            {
                if (s.producerId != city.producerId || s.level <= best)
                    continue;
                foreach (var t in w.trains)
                    if (t.a == s.id || t.b == s.id)
                    {
                        best = s.level;
                        break;
                    }
            }
            return best;
        }
        public int ServedStations(CityState city)
        {
            int n = 0;
            foreach (var s in w.stations)
            {
                if (s.producerId != city.producerId)
                    continue;
                foreach (var t in w.trains)
                    if (t.a == s.id || t.b == s.id)
                    {
                        n++;
                        break;
                    }
            }
            return n;
        }
        StationState Station(int id)
        {
            foreach (var s in w.stations)
                if (s.id == id)
                    return s;
            return null;
        }
        ProducerState Producer(int id)
        {
            foreach (var p in w.producers)
                if (p.id == id)
                    return p;
            return null;
        }
        bool Act(CityState city, ProducerState p)
        {
            var before = city.level;
            int civic = -1;
            bool upgradeFirst = city.level >= CityLevel.SmallTown && Next(ref city.rng) % (uint)cb.upgradeEveryN == 0;
            bool done = (upgradeFirst && Upgrade(city)) || PlaceCivic(city, out civic) || Expand(city) || ExtendRoad(city) || Upgrade(city);
            if (!done)
                return false;
            Recount(city, p, b);
            w.cityRevision++;
            if (civic >= 0)
                Notify(Notification.CivicBuilt, city.id, civic);
            if (city.level != before)
                Notify(Notification.LevelChanged, city.id, (int)city.level);
            return true;
        }
        void Notify(int kind, int cityId, int value)
        {
            if (Notifications.Count >= NotificationCapacity)
                Notifications.Dequeue();
            Notifications.Enqueue(new Notification { kind = kind, cityId = cityId, value = value });
        }
        /// <summary>A free cell the town may claim: inside its influence radius, nearer its plaza than any other, not recently cleared. Grid bits in <paramref name="shared"/> may already be set.</summary>
        bool Claimable(CityState city, Cell c, byte shared = 0, int slack = 0)
        {
            // Cheap grid and distance tests first: the terrain and footprint tests scan hills, industries and stations.
            if (!MapDefinition.InBounds(c) || (grid[c.Key] & ~shared) != 0)
                return false;
            int mine = Chebyshev(c, city.center);
            if (mine > cb.influenceRadius[(int)city.level] + slack)
                return false;
            foreach (var other in w.cities)
            {
                if (other == city)
                    continue;
                int d = Chebyshev(c, other.center);
                if (d < mine || (d == mine && other.id < city.id))
                    return false;
            }
            foreach (var cl in city.cleared)
                if (cl.cell.Equals(c) && cl.untilTick > w.tick)
                    return false;
            return !MapDefinition.Water(c) && !MapDefinition.Blocked(c, w) && !BuildService.StationFootprint(w, c);
        }
        /// <summary>A building lot: a claimable cell between the street lines, off the railway.</summary>
        bool LotSite(CityState city, Cell c) => !CityLayout.StreetLine(city.center, c, cb) && net.At(c) == null && Claimable(city, c);
        /// <summary>
        /// A street cell entered from a neighbouring street in direction d. A highway cell on the grid joins the town's
        /// streets, and a straight track crossed at right angles becomes a level crossing.
        /// </summary>
        bool StreetSite(CityState city, Cell c, int d)
        {
            if (!CityLayout.StreetLine(city.center, c, cb) || !Claimable(city, c, (byte)(Highway | Tram)) || OnPlatform(c))
                return false;
            var track = net.At(c);
            return track == null || (track.bridge == 0 && track.mask == (d % 2 == 0 ? 10 : 5));
        }
        static int Chebyshev(Cell a, Cell b) => Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.z - b.z));
        /// <summary>New streets never cross a station's platforms, so a street a station upgrade took up stays gone.</summary>
        bool OnPlatform(Cell c)
        {
            foreach (var s in w.stations)
                if (StationLayout.PlatformAt(s, c) >= 0)
                    return true;
            return false;
        }
        void Place(CityState city, Cell cell, int def)
        {
            var bs = new BuildingState { cell = cell, def = def };
            city.buildings.Add(bs);
            for (int i = 0; i < CityLayout.FootprintCells(bs); i++)
                grid[CityLayout.FootprintCell(bs, i).Key] |= Building;
        }
        void AddStreet(CityState city, Cell cell)
        {
            city.roads.Add(new RoadState { cell = cell });
            grid[cell.Key] |= Road;
        }
        static int Pick(int[] options, ref uint rng) => options[(int)(Next(ref rng) % (uint)options.Length)];
        bool Expand(CityState city)
        {
            if (city.buildings.Count >= cb.maxBuildings || !CollectLots(city, false))
                return false;
            var cell = Cell.FromKey(candidates[Best()]);
            int commercial = 0;
            foreach (var bs in city.buildings)
                if (BuildingCatalog.Get(bs.def).category == BuildingCategory.Commercial)
                    commercial++;
            bool wantsCommercial = commercial * 100 < cb.commercialShare[(int)city.level] * city.buildings.Count && (Chebyshev(cell, city.center) <= 3 || NearStation(city, cell, 4));
            Place(city, cell, Pick(BuildingCatalog.Variants(wantsCommercial ? BuildingCategory.Commercial : BuildingCategory.Residential, 1), ref city.rng));
            return true;
        }
        /// <summary>Builds the first unlocked municipal service the town lacks, on the free lot nearest the plaza.</summary>
        bool PlaceCivic(CityState city, out int def)
        {
            def = MissingCivic(city);
            if (def < 0 || city.buildings.Count >= cb.maxBuildings)
            {
                def = -1;
                return false;
            }
            int size = BuildingCatalog.Size(def);
            int anchor = size > 1 ? LargeSite(city, size) : CollectLots(city, true) ? candidates[Best()] : -1;
            if (anchor < 0)
            {
                def = -1;
                return false;
            }
            foreach (var cell in siteClearance)
                Replace(city, cell);
            siteClearance.Clear();
            Place(city, Cell.FromKey(anchor), def);
            foreach (var cell in siteLink)
                AddStreet(city, cell);
            siteLink.Clear();
            return true;
        }
        /// <summary>
        /// The south-west corner of the free size × size square nearest the plaza that touches one of the town's streets,
        /// or -1. Landmarks may cover street lines that have no street yet (the grid then routes around them) and may reach
        /// size - 1 cells past the influence radius. A summed-area table makes each candidate square a constant-time test.
        /// A town packed so tight that no free square fronts a street reaches one down a short new street (<see cref="LinkedSite"/>)
        /// or, failing that, makes room by replacing a few homes (<see cref="ClearedSite"/>).
        /// </summary>
        int LargeSite(CityState city, int size)
        {
            siteClearance.Clear();
            siteLink.Clear();
            int reach = cb.influenceRadius[(int)city.level] + size - 1, side = 2 * reach + 1, stride = side + 1;
            if (stride * stride > area.Length)
                return -1;
            int x0 = city.center.x - reach, z0 = city.center.z - reach;
            for (int i = 0; i < stride; i++)
                area[i] = 0;
            for (int j = 0; j < side; j++)
            {
                area[(j + 1) * stride] = 0;
                for (int i = 0; i < side; i++)
                {
                    var c = new Cell(x0 + i, z0 + j);
                    int free = net.At(c) == null && Claimable(city, c, 0, size - 1) ? 1 : 0;
                    area[(j + 1) * stride + i + 1] = free + area[j * stride + i + 1] + area[(j + 1) * stride + i] - area[j * stride + i];
                }
            }
            int best = -1, bestScore = int.MaxValue;
            // Rows then columns in ascending order, so ties keep the lowest cell key.
            for (int j = 0; j + size <= side; j++)
                for (int i = 0; i + size <= side; i++)
                {
                    int free = area[(j + size) * stride + i + size] - area[j * stride + i + size] - area[(j + size) * stride + i] + area[j * stride + i];
                    if (free != size * size)
                        continue;
                    var anchor = new Cell(x0 + i, z0 + j);
                    int score = Math.Abs(2 * anchor.x + size - 1 - 2 * city.center.x) + Math.Abs(2 * anchor.z + size - 1 - 2 * city.center.z);
                    if (score >= bestScore || !TouchesStreet(anchor, size))
                        continue;
                    best = anchor.Key;
                    bestScore = score;
                }
            if (best < 0)
                best = LinkedSite(city, size, x0, z0, side, stride);
            return best >= 0 ? best : ClearedSite(city, size, x0, z0, side, stride);
        }
        /// <summary>
        /// The free square nearest the plaza that a straight new street of at most <see cref="MaxLink"/> cells, running
        /// along one of the town's street lines from one of its sides, joins to the town's streets; or -1. Each link cell
        /// costs <see cref="LinkCost"/>, so a short link wins over a slightly nearer square. The link goes in siteLink.
        /// Reads the summed-area table of free cells LargeSite just filled.
        /// </summary>
        int LinkedSite(CityState city, int size, int x0, int z0, int side, int stride)
        {
            int best = -1, bestScore = int.MaxValue, bestSide = 0, bestAt = 0, bestLength = 0;
            for (int j = 0; j + size <= side; j++)
                for (int i = 0; i + size <= side; i++)
                {
                    if (Square(area, stride, i, j, size) != size * size)
                        continue;
                    var anchor = new Cell(x0 + i, z0 + j);
                    int score = Math.Abs(2 * anchor.x + size - 1 - 2 * city.center.x) + Math.Abs(2 * anchor.z + size - 1 - 2 * city.center.z);
                    for (int d = 0; d < 4 && score + LinkCost < bestScore; d++)
                        for (int k = 0; k < size; k++)
                        {
                            int length = LinkLength(city, anchor, size, d, k);
                            if (length == 0 || score + LinkCost * length >= bestScore)
                                continue;
                            best = anchor.Key;
                            bestScore = score + LinkCost * length;
                            bestSide = d;
                            bestAt = k;
                            bestLength = length;
                        }
                }
            if (best >= 0)
            {
                var c = SideCell(Cell.FromKey(best), size, bestSide, bestAt);
                for (int n = 0; n < bestLength; n++, c = c.Move(bestSide))
                    siteLink.Add(c);
            }
            return best;
        }
        /// <summary>The cell just outside side <paramref name="d"/> of a square, <paramref name="k"/> cells along that side.</summary>
        static Cell SideCell(Cell anchor, int size, int d, int k) =>
            d == 0 ? new Cell(anchor.x + k, anchor.z + size) : d == 1 ? new Cell(anchor.x + size, anchor.z + k) : d == 2 ? new Cell(anchor.x + k, anchor.z - 1) : new Cell(anchor.x - 1, anchor.z + k);
        /// <summary>
        /// How many cells of new street run straight out from side <paramref name="d"/> of a square, at <paramref name="k"/>
        /// along it, to one of the town's streets; 0 when there is no such link of at most <see cref="MaxLink"/> cells.
        /// The link keeps to a street line, so every cell of it is a street site as <see cref="ExtendRoad"/> would lay it.
        /// </summary>
        int LinkLength(CityState city, Cell anchor, int size, int d, int k)
        {
            var c = SideCell(anchor, size, d, k);
            bool alongZ = d % 2 == 0;
            int offset = alongZ ? c.x - city.center.x : c.z - city.center.z, spacing = Math.Max(1, alongZ ? cb.blockWidth : cb.blockDepth) + 1;
            if ((offset % spacing + spacing) % spacing != 0)
                return 0;
            for (int length = 0; length <= MaxLink; length++, c = c.Move(d))
            {
                if (!MapDefinition.InBounds(c))
                    return 0;
                if ((grid[c.Key] & (Road | Plaza)) != 0)
                    return length > 0 && OwnStreet(city, c) ? length : 0;
                if (length == MaxLink || !StreetSite(city, c, d))
                    return 0;
            }
            return 0;
        }
        static bool OwnStreet(CityState city, Cell c)
        {
            if (c.Equals(city.center))
                return true;
            foreach (var road in city.roads)
                if (road.cell.Equals(c))
                    return true;
            return false;
        }
        /// <summary>
        /// For a town packed so tight that no free square fronts a street: the street-fronting square that replaces the
        /// fewest homes and businesses (at most <see cref="MaxCleared"/>, never a street, track, landmark or civic building),
        /// nearest the plaza among equals; or -1. The cells to clear go in siteClearance.
        /// </summary>
        int ClearedSite(CityState city, int size, int x0, int z0, int side, int stride)
        {
            smallLots.Clear();
            foreach (var bs in city.buildings)
                if (BuildingCatalog.Size(bs.def) == 1 && !BuildingCatalog.IsCivic(bs.def))
                    smallLots.Add(bs.cell.Key);
            for (int i = 0; i < stride; i++)
                area[i] = homeArea[i] = 0;
            for (int j = 0; j < side; j++)
            {
                area[(j + 1) * stride] = homeArea[(j + 1) * stride] = 0;
                for (int i = 0; i < side; i++)
                {
                    var c = new Cell(x0 + i, z0 + j);
                    bool open = MapDefinition.InBounds(c) && net.At(c) == null;
                    bool home = open && smallLots.Contains(c.Key) && Claimable(city, c, Building, size - 1);
                    int usable = home || (open && Claimable(city, c, 0, size - 1)) ? 1 : 0, at = (j + 1) * stride + i + 1;
                    area[at] = usable + area[at - stride] + area[at - 1] - area[at - stride - 1];
                    homeArea[at] = (home ? 1 : 0) + homeArea[at - stride] + homeArea[at - 1] - homeArea[at - stride - 1];
                }
            }
            int best = -1, bestScore = int.MaxValue;
            for (int j = 0; j + size <= side; j++)
                for (int i = 0; i + size <= side; i++)
                {
                    int homes = Square(homeArea, stride, i, j, size);
                    if (homes > MaxCleared || Square(area, stride, i, j, size) != size * size)
                        continue;
                    var anchor = new Cell(x0 + i, z0 + j);
                    int score = ClearCost * homes + Math.Abs(2 * anchor.x + size - 1 - 2 * city.center.x) + Math.Abs(2 * anchor.z + size - 1 - 2 * city.center.z);
                    if (score >= bestScore || !TouchesStreet(anchor, size))
                        continue;
                    best = anchor.Key;
                    bestScore = score;
                }
            if (best >= 0)
            {
                var anchor = Cell.FromKey(best);
                for (int j = 0; j < size; j++)
                    for (int i = 0; i < size; i++)
                        if (smallLots.Contains(new Cell(anchor.x + i, anchor.z + j).Key))
                            siteClearance.Add(new Cell(anchor.x + i, anchor.z + j));
            }
            return best;
        }
        /// <summary>How many cells of a summed-area table the size × size square at column i, row j covers.</summary>
        static int Square(int[] table, int stride, int i, int j, int size) =>
            table[(j + size) * stride + i + size] - table[j * stride + i + size] - table[(j + size) * stride + i] + table[j * stride + i];
        /// <summary>Takes down the home or business on <paramref name="cell"/> to make room for a landmark.</summary>
        void Replace(CityState city, Cell cell)
        {
            for (int i = 0; i < city.buildings.Count; i++)
                if (city.buildings[i].cell.Equals(cell) && BuildingCatalog.Size(city.buildings[i].def) == 1)
                {
                    city.buildings.RemoveAt(i);
                    grid[cell.Key] = (byte)(grid[cell.Key] & ~Building);
                    return;
                }
        }
        bool TouchesStreet(Cell anchor, int size)
        {
            for (int k = 0; k < size; k++)
                if (Street(anchor.x + k, anchor.z - 1) || Street(anchor.x + k, anchor.z + size) || Street(anchor.x - 1, anchor.z + k) || Street(anchor.x + size, anchor.z + k))
                    return true;
            return false;
        }
        bool Street(int x, int z)
        {
            var c = new Cell(x, z);
            return MapDefinition.InBounds(c) && (grid[c.Key] & (Road | Plaza)) != 0;
        }
        /// <summary>The next municipal service this town has unlocked but not built, or -1.</summary>
        public int MissingCivic(CityState city)
        {
            int have = CivicMask(city);
            foreach (int def in BuildingCatalog.CivicOrder)
                if ((have & 1 << BuildingCatalog.CivicSlot(def)) == 0 && BuildingCatalog.Get(def).unlock <= city.level)
                    return def;
            return -1;
        }
        /// <summary>Bit i is set when the town has the civic building CivicOrder[i].</summary>
        public static int CivicMask(CityState city)
        {
            int mask = 0;
            foreach (var bs in city.buildings)
            {
                int slot = BuildingCatalog.CivicSlot(bs.def);
                if (slot >= 0)
                    mask |= 1 << slot;
            }
            return mask;
        }
        public static int CivicCount(CityState city)
        {
            int n = 0;
            for (int mask = CivicMask(city); mask != 0; mask &= mask - 1)
                n++;
            return n;
        }
        /// <summary>Candidate lots are the free block cells beside the city's streets. Never a map scan.</summary>
        bool CollectLots(CityState city, bool central)
        {
            candidates.Clear();
            scores.Clear();
            seen.Clear();
            foreach (var r in city.roads)
                for (int d = 0; d < 4; d++)
                {
                    var n = r.cell.Move(d);
                    if (!LotSite(city, n) || !seen.Add(n.Key))
                        continue;
                    candidates.Add(n.Key);
                    scores.Add(central ? 100 - 4 * Chebyshev(n, city.center) + (int)(Next(ref city.rng) & 3) : SiteScore(city, n));
                }
            return candidates.Count > 0;
        }
        int SiteScore(CityState city, Cell c)
        {
            int adjacent = 0;
            for (int d = 0; d < 4; d++)
            {
                var n = c.Move(d);
                if (MapDefinition.InBounds(n) && (grid[n.Key] & Building) != 0)
                    adjacent++;
            }
            return 40 - 2 * Chebyshev(c, city.center) + 6 * adjacent + (NearStation(city, c, 4) ? 10 : 0) + (int)(Next(ref city.rng) & 7);
        }
        int Best()
        {
            int best = 0;
            for (int i = 1; i < candidates.Count; i++)
                if (scores[i] > scores[best] || (scores[i] == scores[best] && candidates[i] < candidates[best]))
                    best = i;
            return best;
        }
        bool NearStation(CityState city, Cell c, int radius)
        {
            foreach (var s in w.stations)
            {
                if (s.producerId != city.producerId)
                    continue;
                int length = StationLayout.Length(s);
                for (int k = 0; k < StationLayout.Platforms(s); k++)
                    for (int i = StationLayout.First(length); i <= StationLayout.Last(length); i++)
                        if (Chebyshev(StationLayout.Along(StationLayout.Center(s, k), s.axis, i), c) <= radius)
                            return true;
            }
            return false;
        }
        /// <summary>Lays one street cell on the grid lines, always touching an existing street, so the network stays connected to the plaza.</summary>
        bool ExtendRoad(CityState city)
        {
            if (city.roads.Count >= cb.maxRoads)
                return false;
            candidates.Clear();
            scores.Clear();
            seen.Clear();
            foreach (var r in city.roads)
                for (int d = 0; d < 4; d++)
                {
                    var n = r.cell.Move(d);
                    if (!StreetSite(city, n, d) || !seen.Add(n.Key))
                        continue;
                    candidates.Add(n.Key);
                    scores.Add(RoadScore(city, r.cell, d, n));
                }
            if (candidates.Count == 0)
                return false;
            AddStreet(city, Cell.FromKey(candidates[Best()]));
            return true;
        }
        int RoadScore(CityState city, Cell from, int direction, Cell n)
        {
            int score = 3 * OpenLots(city, n) - 2 * Chebyshev(n, city.center) + (int)(Next(ref city.rng) & 3);
            var behind = from.Move(Directions.Opp(direction));
            if (MapDefinition.InBounds(behind) && (grid[behind.Key] & (Road | Plaza)) != 0)
                score += 4; // carry a street straight on
            return score;
        }
        /// <summary>Building lots a new street at c would front.</summary>
        int OpenLots(CityState city, Cell c)
        {
            int n = 0;
            for (int d = 0; d < 4; d++)
                if (LotSite(city, c.Move(d)))
                    n++;
            return n;
        }
        /// <summary>Raises the most central home or business one level to a random variant of the next level.</summary>
        bool Upgrade(CityState city)
        {
            int maxLevel = cb.maxBuildingLevel[(int)city.level], best = -1, bestScore = int.MaxValue;
            for (int i = 0; i < city.buildings.Count; i++)
            {
                var def = BuildingCatalog.Get(city.buildings[i].def);
                if (def.category == BuildingCategory.Civic || def.level >= maxLevel)
                    continue;
                // Central and station-side buildings densify first, so a skyline rises in the middle and suburbs stay low.
                int score = def.level * 20 + Chebyshev(city.buildings[i].cell, city.center) * 30 - (NearStation(city, city.buildings[i].cell, 4) ? 25 : 0);
                if (score < bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            if (best < 0)
                return false;
            var bs = city.buildings[best];
            var current = BuildingCatalog.Get(bs.def);
            bs.def = Pick(BuildingCatalog.Variants(current.category, current.level + 1), ref city.rng);
            city.buildings[best] = bs;
            return true;
        }
        public void RecordArrival(int producerId, Cargo cargo, int units)
        {
            if (!byProducer.TryGetValue(producerId, out var city))
                return;
            if (cargo == Cargo.Passengers)
                city.paxIn[city.bucket] += units;
            else if (cargo == Cargo.Goods)
                city.goodsIn[city.bucket] += units;
        }
        public void RecordBoarding(int producerId, Cargo cargo, int units)
        {
            if (cargo == Cargo.Passengers && byProducer.TryGetValue(producerId, out var city))
                city.paxOut[city.bucket] += units;
        }
        /// <summary>Demolishes a city building at the player's expense; null when the cell holds no building.</summary>
        public Result Bulldoze(Cell c)
        {
            if (!HasBuilding(c))
                return null;
            foreach (var city in w.cities)
                for (int i = 0; i < city.buildings.Count; i++)
                {
                    var bs = city.buildings[i];
                    if (!CityLayout.Covers(bs, c))
                        continue;
                    var def = BuildingCatalog.Get(bs.def);
                    int cells = CityLayout.FootprintCells(bs), cost = DemolitionCost(bs);
                    if (w.money < cost)
                        return Result.Fail($"Demolition costs ${cost:N0}.");
                    EconomyService.Spend(w, cost);
                    city.buildings.RemoveAt(i);
                    for (int k = 0; k < cells; k++)
                    {
                        var cell = CityLayout.FootprintCell(bs, k);
                        grid[cell.Key] = (byte)(grid[cell.Key] & ~Building);
                        if (city.cleared.Count >= ClearedCapacity)
                            city.cleared.RemoveAt(0);
                        city.cleared.Add(new ClearedCell { cell = cell, untilTick = w.tick + cb.demolitionCooldown });
                    }
                    Recount(city, Producer(city.producerId), b);
                    w.cityRevision++;
                    return Result.Good($"{def.name} demolished for ${cost:N0}. The town leaves the site alone for five minutes.");
                }
            return null;
        }
        /// <summary>
        /// What knocking a building down costs: demolitionCost per level for every footprint cell, and a skyscraper's
        /// controlled demolition a further skyscraperStoreyCost for every storey (about $60,000–$80,000 a tower).
        /// </summary>
        public int DemolitionCost(BuildingState bs)
        {
            var def = BuildingCatalog.Get(bs.def);
            int cost = cb.demolitionCost * def.level * CityLayout.FootprintCells(bs);
            return BuildingCatalog.IsSkyscraper(bs.def) ? cost + cb.skyscraperStoreyCost * BuildingCatalog.Storeys(bs.def) : cost;
        }
        /// <summary>Takes up a plain town street at the player's expense (a station upgrade growing over it); null when there is none.</summary>
        public Result ClearStreet(Cell c)
        {
            if (!IsStreet(c))
                return null;
            if (w.money < cb.streetClearCost)
                return Result.Fail($"Clearing a street costs ${cb.streetClearCost:N0}.");
            foreach (var city in w.cities)
                for (int i = 0; i < city.roads.Count; i++)
                {
                    if (!city.roads[i].cell.Equals(c))
                        continue;
                    EconomyService.Spend(w, cb.streetClearCost);
                    city.roads.RemoveAt(i);
                    grid[c.Key] = (byte)(grid[c.Key] & ~Road);
                    w.cityRevision++;
                    return Result.Good($"Street cleared for ${cb.streetClearCost:N0}.");
                }
            return null;
        }
        static uint Seed(int id) => unchecked((uint)id * 2654435761u ^ 0x9E3779B9u) | 1u;
        static uint Next(ref uint x)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x;
        }
    }
}
