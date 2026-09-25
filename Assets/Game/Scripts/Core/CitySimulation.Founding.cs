using System;
using System.Collections.Generic;

namespace ValleyRail.Core
{
    /// <summary>
    /// New towns. As the valley prospers, settlers found a village on the best open site left on the map: flat, roomy land
    /// clear of other towns and industry yards, drawn to the railway, highways and water. A founding is due when the
    /// valley's population passes the next threshold (CityBalance.foundingPopulation, plus foundingStep for every town
    /// already founded) and foundingInterval ticks have passed since the last one. The village is an ordinary Town producer
    /// with its city, so stations, trains, growth, highways and saves treat it like the map's own towns.
    /// Everything is integer and reads only WorldState, so a saved game founds the same towns on the same ticks.
    /// </summary>
    public sealed partial class CitySimulation
    {
        /// <summary>Producers 1 to MapProducers are the map's own; settlers' towns take later ids (WorldState.nextId starts above them).</summary>
        public const int MapProducers = 22;
        /// <summary>The most founded towns a save may hold, whatever the balance asks for.</summary>
        public const int FoundedLimit = 12;
        // Checked every five game minutes, between the road and service steps, so a valley with no site left costs one
        // map scan per period. foundingInterval should be a multiple of the period.
        const int FoundingPeriod = 6000, FoundingPhase = 660;
        /// <summary>
        /// Only the full valley (map version 5) founds towns. Older saves keep their smaller producer set for good (nothing
        /// migrates them past 4), and their validation allows no extra producers.
        /// </summary>
        const int FoundingMapVersion = 5;
        // A site needs a free 7×7 square for the day-0 village and most of the 13×13 around it free to grow into, at least
        // Edge cells in from the map edge. Other towns keep their full reach plus GrowthRoom for the newcomer.
        const int CoreReach = 3, RoomReach = 6, MinRoom = 110, Edge = 6, GrowthRoom = 10, IndustryClearance = 7;
        // What draws settlers: a station within StationReach, a served industry within JobsReach (work), rails or a highway
        // within RoomReach, water within WaterReach. Distance to the nearest town counts up to SpreadCap, so towns spread out.
        const int StationReach = 10, JobsReach = 16, WaterReach = 5, SpreadCap = 36;
        // A station pulls StationPull, plus StationLevelPull per rung of the biggest one within reach.
        const int StationPull = 50, StationLevelPull = 15, JobsPull = 25, RoutePull = 20, WaterPull = 15;
        // Summed-area layers over the whole map, so every window count is four reads.
        const int OpenLayer = 0, WaterLayer = 1, RouteLayer = 2, Layers = 3;
        const int SiteSide = MapDefinition.Size + 1, SiteLayer = SiteSide * SiteSide;
        // Allocated on the first site search, so worlds that never found a town (and save validation) do not carry them.
        // ground: per cell Dry (open terrain), Wet (water) or 0 (hill, sand, industry yard); platforms: station strips.
        const byte Dry = 1, Wet = 2;
        int[] siteArea;
        byte[] ground;
        bool[] platforms;
        readonly List<int> servedIndustries = new List<int>(32);
        static readonly string[] NameStems =
        {
            "Amber", "Ash", "Birch", "Bramble", "Cedar", "Clay", "Elder", "Fern", "Fox", "Glen", "Hazel", "Heather",
            "Holly", "Kings", "Linden", "Maple", "Marsh", "Mill", "Rose", "Rowan", "Silver", "Stone", "Thorn", "Wren",
        };
        static readonly string[][] NameEndings =
        {
            new[] { "ton", "field", "bury", "wick", "ham", "by", "worth", "stead" }, // open country
            new[] { "ford", "mere", "brook", "bridge", "haven", "well" },           // by a river or the lake
            new[] { "wood", "hurst", "grove", "holt" },                             // woodland
            new[] { "ridge", "dale", "fell", "crest" },                             // the alpine north
        };

        /// <summary>
        /// True for a town settlers founded during play. The map's own producers are never founded, and neither are other
        /// producers added past them (ski resorts use fixed ids above MapProducers), so the kind is checked too.
        /// </summary>
        public static bool Founded(ProducerState p) => p.kind == ProducerKind.Town && p.id > MapProducers;
        /// <summary>A founded town a save may hold: a named Town on open, flat, dry ground off the industry yards.</summary>
        public static bool FoundedSite(WorldState w, ProducerState p) =>
            Founded(p) && w.mapVersion >= FoundingMapVersion && !string.IsNullOrWhiteSpace(p.name) && p.name.Length <= 40 &&
            MapDefinition.InBounds(p.cell) && !MapDefinition.Water(p.cell) && !MapDefinition.Blocked(p.cell, w);
        /// <summary>The valley's population at which the next town is founded, given how many settlers already founded.</summary>
        public int FoundingPopulation(int founded) => cb.foundingPopulation + founded * cb.foundingStep;
        /// <summary>Whether a new town is due now: population, pause since the last founding and the cap all allow it.</summary>
        public bool FoundingDue(out int founded)
        {
            int population = 0;
            long last = 0;
            founded = 0;
            foreach (var city in w.cities)
            {
                population += city.population;
                if (city.founded <= 0)
                    continue;
                founded++;
                last = Math.Max(last, city.founded);
            }
            return cb.foundingEnabled && w.mapVersion >= FoundingMapVersion && founded < Math.Min(cb.maxFounded, FoundedLimit) &&
                population >= FoundingPopulation(founded) && w.tick - last >= cb.foundingInterval;
        }
        /// <summary>
        /// Whether towns i &lt; j (indices in w.cities) may be linked by a highway. The map's own towns all link up; a founded
        /// town gets one feeder highway, to a town listed before it (map towns come first), so every village hangs off the
        /// network instead of linking to every town. The feeder is the nearest earlier town that has reached Village, so
        /// a stalled neighbour never strands the newcomer; the nearest of all only while none has. Once the feeder highway
        /// is planned the village links to nothing else. Allocates nothing: DevelopRoads asks every 200 ticks.
        /// </summary>
        bool Links(int i, int j)
        {
            var town = w.cities[j];
            if (town.founded <= 0)
                return true;
            int feeder = -1, nearest = 0;
            for (int k = 0; k < j; k++)
            {
                var other = w.cities[k];
                if (RoadBetween(other.producerId, town.producerId) != null)
                    return false;
                int d = other.center.Distance(town.center);
                if (d < w.cities[nearest].center.Distance(town.center))
                    nearest = k;
                if (other.level >= CityLevel.Village && (feeder < 0 || d < w.cities[feeder].center.Distance(town.center)))
                    feeder = k;
            }
            return (feeder >= 0 ? feeder : nearest) == i;
        }
        /// <summary>Founds a village when one is due and the map still has a site for it. Scans the map only when due.</summary>
        void DevelopSettlements()
        {
            if (FoundingDue(out int founded) && FindTownSite(founded, out var site))
                Settle(site, founded);
        }
        /// <summary>
        /// The best site for a new village, or false when none is left. Every free cell far enough from the map edge is a
        /// candidate; summed-area tables make its free-land, water and route counts constant time, so a search is one pass
        /// over the map plus a short loop over towns, industries and stations per candidate. Ties keep the lowest cell key.
        /// </summary>
        public bool FindTownSite(int founded, out Cell best)
        {
            FillSiteLayers();
            CollectServedIndustries();
            int bestScore = int.MinValue, core = (2 * CoreReach + 1) * (2 * CoreReach + 1);
            best = default;
            for (int z = Edge; z < MapDefinition.Size - Edge; z++)
                for (int x = Edge; x < MapDefinition.Size - Edge; x++)
                {
                    var c = new Cell(x, z);
                    if (Window(OpenLayer, c, CoreReach) != core)
                        continue;
                    int room = Window(OpenLayer, c, RoomReach);
                    if (room < MinRoom)
                        continue;
                    int score = TownSiteScore(c, room, founded);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = c;
                    }
                }
            return bestScore != int.MinValue;
        }
        /// <summary>How much settlers like a free site, or int.MinValue when a town or an industry yard is too close.</summary>
        int TownSiteScore(Cell c, int room, int founded)
        {
            int nearestTown = SpreadCap;
            foreach (var city in w.cities)
            {
                int d = Chebyshev(c, city.center);
                // Room for both: the neighbour's full reach plus the newcomer's first growth.
                if (d < Math.Max(cb.foundingSpacing, cb.influenceRadius[(int)city.level] + GrowthRoom))
                    return int.MinValue;
                nearestTown = Math.Min(nearestTown, d);
            }
            bool jobs = false;
            foreach (var p in w.producers)
            {
                if (p.kind == ProducerKind.Town)
                    continue;
                int d = Chebyshev(c, p.cell);
                if (d < IndustryClearance)
                    return int.MinValue;
                if (d <= JobsReach && servedIndustries.Contains(p.id))
                    jobs = true;
            }
            int station = -1;
            foreach (var s in w.stations)
                if (Chebyshev(c, s.cell) <= StationReach)
                    station = Math.Max(station, s.level);
            int score = room + 2 * nearestTown + TerrainPull(MapDefinition.Terrain(c));
            if (station >= 0) score += StationPull + StationLevelPull * station; // settlers follow the trains, to a big station most
            if (jobs) score += JobsPull; // and the work at an industry the railway serves
            if (Window(RouteLayer, c, RoomReach) > 0) score += RoutePull;
            if (Window(WaterLayer, c, WaterReach) > 0) score += WaterPull;
            return score + (int)(Mix((uint)c.Key, (uint)founded) & 15);
        }
        /// <summary>Meadow suits a village best, then the shore and woodland; dry plains and the alpine north least.</summary>
        static int TerrainPull(int terrain) => terrain == 0 ? 10 : terrain == 6 ? 8 : terrain == 1 ? 6 : terrain == 2 ? 2 : 0;
        /// <summary>Industries with a station that a routed train uses: their jobs draw settlers.</summary>
        void CollectServedIndustries()
        {
            servedIndustries.Clear();
            foreach (var s in w.stations)
            {
                if (servedIndustries.Contains(s.producerId))
                    continue;
                foreach (var t in w.trains)
                    if (t.a == s.id || t.b == s.id)
                    {
                        servedIndustries.Add(s.producerId);
                        break;
                    }
            }
        }
        /// <summary>
        /// Fills the three summed-area layers: open land a village may take, water, and rails or highways. The terrain part
        /// (dry, flat, off the sand and the industry yards, none of which ever change) is worked out on the first search only.
        /// </summary>
        void FillSiteLayers()
        {
            if (ground == null)
            {
                // Row and column 0 of every layer stay zero from the allocation; every other entry is rewritten below.
                siteArea = new int[Layers * SiteLayer];
                ground = new byte[MapDefinition.Size * MapDefinition.Size];
                platforms = new bool[ground.Length];
                for (int key = 0; key < ground.Length; key++)
                {
                    var c = Cell.FromKey(key);
                    ground[key] = MapDefinition.Water(c) ? Wet : Coast.Beach(c) || MapDefinition.Blocked(c, w) ? (byte)0 : Dry;
                }
            }
            Array.Clear(platforms, 0, platforms.Length);
            foreach (var s in w.stations)
                foreach (var c in StationLayout.Strip(s))
                    if (MapDefinition.InBounds(c))
                        platforms[c.Key] = true;
            for (int z = 0; z < MapDefinition.Size; z++)
                for (int x = 0; x < MapDefinition.Size; x++)
                {
                    var c = new Cell(x, z);
                    bool water = ground[c.Key] == Wet, route = !water && (net.At(c) != null || (grid[c.Key] & Highway) != 0);
                    bool open = ground[c.Key] == Dry && !route && grid[c.Key] == 0 && !platforms[c.Key];
                    int i = (z + 1) * SiteSide + x + 1;
                    Integrate(OpenLayer * SiteLayer + i, open);
                    Integrate(WaterLayer * SiteLayer + i, water);
                    Integrate(RouteLayer * SiteLayer + i, route);
                }
        }
        void Integrate(int i, bool set) => siteArea[i] = (set ? 1 : 0) + siteArea[i - SiteSide] + siteArea[i - 1] - siteArea[i - SiteSide - 1];
        /// <summary>Cells of the layer inside the square of the given reach around c, clipped to the map.</summary>
        int Window(int layer, Cell c, int reach)
        {
            int x0 = Math.Max(0, c.x - reach), z0 = Math.Max(0, c.z - reach);
            int x1 = Math.Min(MapDefinition.Size, c.x + reach + 1), z1 = Math.Min(MapDefinition.Size, c.z + reach + 1), o = layer * SiteLayer;
            return siteArea[o + z1 * SiteSide + x1] - siteArea[o + z0 * SiteSide + x1] - siteArea[o + z1 * SiteSide + x0] + siteArea[o + z0 * SiteSide + x0];
        }
        /// <summary>Founds the village: a new Town producer and its day-0 hamlet on the site, announced like a level change.</summary>
        void Settle(Cell site, int founded)
        {
            var p = new ProducerState { id = w.nextId++, name = TownName(site, founded), kind = ProducerKind.Town, cell = site };
            w.producers.Add(p);
            var city = Found(w, p, b, CityLayout.Hamlet);
            city.founded = w.tick;
            Rebuild();
            CityState nearest = null;
            foreach (var other in w.cities)
                if (other != city && (nearest == null || Chebyshev(site, other.center) < Chebyshev(site, nearest.center)))
                    nearest = other;
            Notify(Notification.CityFounded, city.id, nearest?.id ?? 0);
            w.cityRevision++;
        }
        /// <summary>
        /// A two-part name that suits the site (a ford by water, a wood in woodland, a ridge in the alpine north) and that no
        /// producer uses yet. The same site and founding count always give the same name.
        /// </summary>
        string TownName(Cell site, int founded)
        {
            int terrain = MapDefinition.Terrain(site);
            var endings = Window(WaterLayer, site, WaterReach) > 0 ? NameEndings[1] : terrain == 1 ? NameEndings[2] : terrain == 3 ? NameEndings[3] : NameEndings[0];
            uint total = (uint)(NameStems.Length * endings.Length), start = Mix((uint)site.Key, (uint)founded + 0x51EDu) % total;
            // First a stem no town or industry starts with (no Ashford beside Ashby), then any name still free.
            for (int pass = 0; pass < 2; pass++)
                for (uint k = 0; k < total; k++)
                {
                    int i = (int)((start + k) % total);
                    string stem = NameStems[i % NameStems.Length], name = stem + endings[i / NameStems.Length];
                    if (!NameTaken(name, pass == 0 ? stem : null))
                        return name;
                }
            // Every pairing is taken (more towns than the cap allows): number the stem instead.
            return NameStems[start % NameStems.Length] + " " + (founded + 1);
        }
        /// <summary>A producer already has the name, or (when a stem is given) a name starting with that stem.</summary>
        bool NameTaken(string name, string stem)
        {
            foreach (var p in w.producers)
                if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase) ||
                    (stem != null && p.name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)))
                    return true;
            return false;
        }
        static uint Mix(uint a, uint b)
        {
            unchecked
            {
                uint x = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA6Bu;
                x ^= x >> 15;
                x *= 0x2C1B3C6Du;
                x ^= x >> 12;
                return x;
            }
        }
    }
}
