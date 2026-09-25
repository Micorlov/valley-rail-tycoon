using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>A priced station upgrade: the new rung and size, what must be demolished for it and the track it lays.</summary>
    public sealed class UpgradePlan
    {
        public bool valid, unaffordable; public string reason;
        public int stationId, level, length, platforms;
        /// <summary>Total price, split into the station itself, demolition and tree clearing, and new platform track.</summary>
        public int cost, stationPrice, clearingCost, trackCost;
        /// <summary>The whole footprint after the upgrade, for the map preview.</summary>
        public List<Cell> platformCells = new List<Cell>(), strip = new List<Cell>();
        /// <summary>Town buildings the bulldozer knocks down (whole buildings, even when only a corner is in the way), town street cells it takes up and standing trees it clears.</summary>
        public List<BuildingState> buildings = new List<BuildingState>();
        public List<Cell> trees = new List<Cell>(), streets = new List<Cell>();
        /// <summary>How many of the buildings are skyscrapers, and their share of the clearing cost.</summary>
        public int skyscrapers, skyscraperCost;
        public BuildPlan track = new BuildPlan();
        public int Clearings => buildings.Count + trees.Count + streets.Count;
    }
    /// <summary>What the AI suggests for a station and why, with the plan for its pick.</summary>
    public sealed class UpgradeAdvice
    {
        public int level, length, platforms; public string reason; public UpgradePlan plan;
    }
    /// <summary>What the last upgrade changed, so the presentation can replay it: a bulldozer, then the rising station.</summary>
    public sealed class UpgradeRecord
    {
        public int stationId, oldLevel, oldLength, oldPlatforms;
        public List<BuildingState> buildings = new List<BuildingState>();
        public List<Cell> trees = new List<Cell>(), cleared = new List<Cell>();
    }
    /// <summary>
    /// The AI station upgrade. A station keeps its cell, axis and side, so its first platform's stop and every train's platform
    /// stay where they were; it only grows. Town buildings (skyscrapers too, priced per storey), town streets and trees in
    /// the new footprint are cleared, however many there are. The town square, highways, car parks, water, hills,
    /// industries, bends in the line and other stations are not, and refuse that size instead. The whole transaction is
    /// priced and validated before anything changes.
    /// </summary>
    public sealed class StationUpgradeService
    {
        readonly WorldState w; readonly RailNetwork net; readonly Balance b; readonly CitySimulation cities; readonly BuildService build; readonly Scenery scenery; readonly GameEvents events;
        public StationUpgradeService(WorldState w, RailNetwork net, Balance b, CitySimulation cities, BuildService build, Scenery scenery, GameEvents events)
        {
            this.w = w;
            this.net = net;
            this.b = b;
            this.cities = cities;
            this.build = build;
            this.scenery = scenery;
            this.events = events;
        }
        /// <summary>The last applied upgrade, for the show. Never saved.</summary>
        public UpgradeRecord Last { get; private set; }
        StationState Find(int id) => w.stations.Find(s => s.id == id);
        ProducerKind KindOf(StationState s) => w.producers.Find(p => p.id == s.producerId).kind;
        public bool FullyUpgraded(StationState s) => s.level >= StationCatalog.MaxLevel(KindOf(s)) && StationLayout.Length(s) >= StationLayout.MaxLength && StationLayout.Platforms(s) >= StationLayout.MaxPlatforms;

        public UpgradePlan Plan(int stationId, int level, int length, int platforms)
        {
            var plan = new UpgradePlan { stationId = stationId, level = level, length = length, platforms = platforms };
            var s = Find(stationId);
            if (s == null)
                return Refuse(plan, "That station no longer exists.");
            plan.platformCells = StationLayout.Cells(s.cell, s.axis, s.side, length, platforms);
            plan.strip = StationLayout.Strip(s.cell, s.axis, s.side, length);
            var kind = KindOf(s);
            int oldLength = StationLayout.Length(s), oldPlatforms = StationLayout.Platforms(s);
            if (!StationCatalog.ValidLevel(kind, level) || !StationLayout.ValidSize(length, platforms))
                return Refuse(plan, $"Stations go up to {StationCatalog.Name(kind, StationCatalog.MaxLevel(kind))}, {StationLayout.Describe(StationLayout.MaxLength, StationLayout.MaxPlatforms)}.");
            if (level < s.level || length < oldLength || platforms < oldPlatforms)
                return Refuse(plan, "An upgrade never makes a station smaller.");
            if (level == s.level && length == oldLength && platforms == oldPlatforms)
                return Refuse(plan, "Choose a higher type, a longer platform or more platforms.");
            if (!StationCatalog.Fits(level, length, platforms))
                return Refuse(plan, $"A {StationCatalog.Name(kind, level)} needs at least {StationLayout.Describe(StationCatalog.MinLength(level), StationCatalog.MinPlatformCount(level))}.");
            var oldCells = new HashSet<int>();
            foreach (var c in StationLayout.Cells(s))
                oldCells.Add(c.Key);
            foreach (var c in StationLayout.Strip(s))
                oldCells.Add(c.Key);
            var clearing = new HashSet<int>();
            var fresh = new List<Cell>();
            foreach (var c in plan.platformCells)
                if (!oldCells.Contains(c.Key))
                {
                    string why = ClassifyPlatform(s, c, plan, clearing, fresh);
                    if (why != null)
                        return Refuse(plan, why);
                }
            foreach (var c in plan.strip)
                if (!oldCells.Contains(c.Key))
                {
                    string why = ClassifyStrip(c, plan, clearing);
                    if (why != null)
                        return Refuse(plan, why);
                }
            foreach (var bs in plan.buildings)
            {
                int price = cities.DemolitionCost(bs);
                plan.clearingCost += price;
                if (BuildingCatalog.IsSkyscraper(bs.def))
                {
                    plan.skyscrapers++;
                    plan.skyscraperCost += price;
                }
            }
            plan.clearingCost += plan.trees.Count * b.treeClearCost + plan.streets.Count * b.city.streetClearCost;
            plan.stationPrice = StationLayout.Cost(b, length, platforms) - StationLayout.Cost(b, oldLength, oldPlatforms) + StationCatalog.LevelPrice(level) - StationCatalog.LevelPrice(s.level);
            if (fresh.Count > 0)
            {
                plan.track = build.ValidatePlatforms(fresh, StationLayout.Mask(s.axis), clearing);
                if (!plan.track.valid && !plan.track.unaffordable)
                    return Refuse(plan, plan.track.reason);
                plan.trackCost = plan.track.cost;
            }
            plan.cost = plan.stationPrice + plan.clearingCost + plan.trackCost;
            if (plan.cost > w.money)
            {
                plan.unaffordable = true;
                return Refuse(plan, $"Not enough money: this upgrade costs ${plan.cost:N0}.");
            }
            plan.valid = true;
            plan.reason = "Ready to build";
            return plan;
        }
        static UpgradePlan Refuse(UpgradePlan plan, string reason)
        {
            plan.valid = false;
            plan.reason = reason;
            return plan;
        }
        /// <summary>A new platform cell: reuse straight track along the axis, or clear open ground (felling a tree, demolishing a building) for new track.</summary>
        string ClassifyPlatform(StationState s, Cell c, UpgradePlan plan, HashSet<int> clearing, List<Cell> fresh)
        {
            if (!MapDefinition.InBounds(c))
                return "The station would run off the map.";
            if (MapDefinition.Water(c))
                return "Water is in the way of the new platforms.";
            var track = net.At(c);
            if (track != null)
            {
                foreach (var other in w.stations)
                    if (other.id != s.id && StationLayout.PlatformAt(other, c) >= 0)
                        return "Another station is in the way.";
                if (track.mask != StationLayout.Mask(s.axis) || track.bridge != 0)
                    return $"The railway bends or crosses at {c}. Choose a shorter platform or fewer platforms.";
                return null;
            }
            string why = ClearGround(c, plan, clearing, "new platforms");
            if (why == null)
                fresh.Add(c);
            return why;
        }
        /// <summary>A new strip cell, where the station building grows: it must end up bare ground.</summary>
        string ClassifyStrip(Cell c, UpgradePlan plan, HashSet<int> clearing)
        {
            if (!MapDefinition.InBounds(c))
                return "The station would run off the map.";
            if (MapDefinition.Water(c))
                return "Water is in the way of the station building.";
            if (net.At(c) != null)
                return $"Track at {c} runs where the station building would stand.";
            return ClearGround(c, plan, clearing, "station building");
        }
        string ClearGround(Cell c, UpgradePlan plan, HashSet<int> clearing, string what)
        {
            if (MapDefinition.Blocked(c, w))
                return $"An industry or hill is in the way of the {what}.";
            if (BuildService.StationFootprint(w, c))
                return "Another station's building is in the way.";
            if (cities.IsStreet(c))
            {
                plan.streets.Add(c);
                return null;
            }
            if (cities.HasTram(c))
                return $"The light rail runs where the {what} would go.";
            if (TramLines.ServingBuilding(w, c) != null)
                return $"A light rail line serves the stadium where the {what} would go.";
            if (cities.Occupied(c) && !cities.HasBuilding(c))
                return $"{cities.Landmark(c) ?? "Town ground"} is in the way of the {what}. The bulldozer clears buildings, streets and trees, but not that.";
            if (cities.HasBuilding(c))
            {
                if (!clearing.Contains(c.Key))
                    foreach (var city in w.cities)
                        foreach (var bs in city.buildings)
                            if (CityLayout.Covers(bs, c))
                            {
                                plan.buildings.Add(bs);
                                for (int k = 0; k < CityLayout.FootprintCells(bs); k++)
                                    clearing.Add(CityLayout.FootprintCell(bs, k).Key);
                            }
                return null;
            }
            if (scenery.TreeAt(c))
                plan.trees.Add(c);
            return null;
        }

        /// <summary>Builds a planned upgrade, re-planned against the current state: demolition, streets, trees, platform track, then the station.</summary>
        public Result Apply(int stationId, int level, int length, int platforms)
        {
            var plan = Plan(stationId, level, length, platforms);
            if (!plan.valid)
                return Result.Fail(plan.reason);
            var s = Find(stationId);
            var record = new UpgradeRecord { stationId = s.id, oldLevel = s.level, oldLength = StationLayout.Length(s), oldPlatforms = StationLayout.Platforms(s) };
            foreach (var bs in plan.buildings)
            {
                var knocked = cities.Bulldoze(CityLayout.FootprintCell(bs, 0));
                if (knocked == null || !knocked.ok)
                    return knocked ?? Result.Fail("A building could not be demolished.");
                record.buildings.Add(bs);
                for (int k = 0; k < CityLayout.FootprintCells(bs); k++)
                    record.cleared.Add(CityLayout.FootprintCell(bs, k));
            }
            foreach (var c in plan.streets)
            {
                var street = cities.ClearStreet(c);
                if (street == null || !street.ok)
                    return street ?? Result.Fail("A street could not be cleared.");
                record.cleared.Add(c);
            }
            foreach (var c in plan.trees)
                if (scenery.Fell(c))
                {
                    EconomyService.Spend(w, b.treeClearCost);
                    record.trees.Add(c);
                }
            if (plan.track.changes.Count > 0)
            {
                // The buildings are gone now, so the platform track validates against the live map.
                var track = build.ValidatePlatforms(plan.track.path, StationLayout.Mask(s.axis));
                var laid = track.valid ? build.CommitValidated(track) : Result.Fail(track.reason);
                if (!laid.ok)
                    return laid;
            }
            s.level = level;
            s.length = length;
            s.platforms = platforms;
            s.paid += plan.stationPrice;
            EconomyService.Spend(w, plan.stationPrice);
            w.revision++;
            net.Rebuild(w);
            events?.Push(GameEventKind.StationUpgraded, s.id, s.cell);
            Last = record;
            return Result.Good($"{s.name} is now a {StationCatalog.Name(KindOf(s), level)}.", s.id);
        }

        /// <summary>
        /// The AI pick: how high the station should go given its queue, its town and the trains calling there, then the closest
        /// combination that actually fits and is affordable. The reason explains the pick in the player's terms.
        /// </summary>
        public UpgradeAdvice Recommend(int stationId)
        {
            var s = Find(stationId);
            if (s == null)
                return new UpgradeAdvice { reason = "That station no longer exists." };
            var kind = KindOf(s);
            int max = StationCatalog.MaxLevel(kind), trains = TrainsCalling(s);
            var load = StationLoad.Of(w, b, s);
            var city = StationCatalog.Town(kind) ? cities.CityFor(s.producerId) : null;
            int population = city?.population ?? 0;
            int want = 1;
            if (population >= 1500 || load.Percent >= 50 || trains >= 2)
                want = 2;
            if (population >= 5000 || (load.Percent >= 80 && trains >= 2))
                want = 3;
            int ideal = Math.Min(max, Math.Max(s.level + 1, want));
            int idealPlatforms = Math.Max(Math.Max(StationCatalog.MinPlatformCount(ideal), StationLayout.Platforms(s)), Math.Min(StationLayout.MaxPlatforms, trains));
            int idealLength = Math.Max(StationCatalog.MinLength(ideal), StationLayout.Length(s));
            UpgradePlan best = null, closestRefused = null, idealPlan = null;
            int bestScore = int.MaxValue, refusedScore = int.MaxValue;
            for (int level = s.level; level <= max; level++)
                for (int length = StationLayout.Length(s); length <= StationLayout.MaxLength; length++)
                    for (int platforms = StationLayout.Platforms(s); platforms <= StationLayout.MaxPlatforms; platforms++)
                    {
                        if (!StationCatalog.Fits(level, length, platforms) || (level == s.level && length == StationLayout.Length(s) && platforms == StationLayout.Platforms(s)))
                            continue;
                        int score = Math.Abs(level - ideal) * 100 + Math.Abs(platforms - idealPlatforms) * 10 + Math.Abs(length - idealLength);
                        var plan = Plan(stationId, level, length, platforms);
                        if (level == ideal && length == idealLength && platforms == idealPlatforms)
                            idealPlan = plan;
                        if (plan.valid && (score < bestScore || (score == bestScore && plan.cost < best.cost)))
                        {
                            best = plan;
                            bestScore = score;
                        }
                        else if (!plan.valid && score < refusedScore)
                        {
                            closestRefused = plan;
                            refusedScore = score;
                        }
                    }
            var pick = best ?? closestRefused;
            var advice = new UpgradeAdvice { plan = pick };
            string situation = Situation(kind, load, population, trains);
            if (pick == null)
            {
                advice.level = s.level;
                advice.length = StationLayout.Length(s);
                advice.platforms = StationLayout.Platforms(s);
                advice.reason = situation + "\nThis station is already as big as stations get.";
                return advice;
            }
            advice.level = pick.level;
            advice.length = pick.length;
            advice.platforms = pick.platforms;
            string pickName = $"{StationCatalog.Name(kind, pick.level)}, {StationLayout.Describe(pick.length, pick.platforms)}";
            if (best == null)
                advice.reason = $"{situation}\nAI pick: {pickName}, but it cannot be built yet. {pick.reason}";
            else
            {
                advice.reason = $"{situation}\nAI pick: {pickName}. {Clearing(pick)}";
                if (idealPlan != null && idealPlan != best && !idealPlan.valid)
                    advice.reason += $"\nA {StationCatalog.Name(kind, ideal)} {StationLayout.Describe(idealLength, idealPlatforms)} would be ideal: {idealPlan.reason}";
            }
            return advice;
        }
        int TrainsCalling(StationState s)
        {
            int n = 0;
            foreach (var t in w.trains)
                if (t.a == s.id || t.b == s.id || t.stationId == s.id)
                    n++;
            return n;
        }
        static string Situation(ProducerKind kind, StationLoad load, int population, int trains)
        {
            string calls = trains == 0 ? "no trains yet" : trains == 1 ? "1 train calls here" : $"{trains} trains call here";
            if (kind == ProducerKind.SkiResort)
                return $"{load.waiting:N0} tourists waiting ({load.Percent}% full) · {calls}.";
            if (StationCatalog.Town(kind))
                return $"{load.waiting:N0} passengers waiting ({load.Percent}% full) · {population:N0} people · {calls}.";
            string waiting = load.cargo.HasValue ? $"{load.waiting:N0} {IndustryCatalog.CargoName(load.cargo.Value).ToLowerInvariant()} waiting ({load.Percent}% full)" : "Takes deliveries";
            return $"{waiting} · {calls}.";
        }
        /// <summary>"The bulldozer clears 2 skyscrapers, 3 other buildings, 4 street cells and 5 trees." or "Open ground: nothing to demolish."</summary>
        public static string Clearing(UpgradePlan plan)
        {
            var parts = new List<string>(4);
            int others = plan.buildings.Count - plan.skyscrapers;
            if (plan.skyscrapers > 0)
                parts.Add(Plural(plan.skyscrapers, "skyscraper"));
            if (others > 0)
                parts.Add(Plural(others, plan.skyscrapers > 0 ? "other building" : "building"));
            if (plan.streets.Count > 0)
                parts.Add(Plural(plan.streets.Count, "street cell"));
            if (plan.trees.Count > 0)
                parts.Add(Plural(plan.trees.Count, "tree"));
            if (parts.Count == 0)
                return "Open ground: nothing to demolish.";
            string what = parts.Count == 1 ? parts[0] : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
            return $"The bulldozer clears {what}.";
        }
        static string Plural(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
    }
}
