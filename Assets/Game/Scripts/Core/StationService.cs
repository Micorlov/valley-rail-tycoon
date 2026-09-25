using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>A priced station proposal: where its platforms and building go, the track it lays and who it can serve.</summary>
    public sealed class StationPlan
    {
        public bool valid; public string reason; public int cost, axis, side = -1, length, platforms, producerId; public Cell center;
        /// <summary>Straight platform track to lay on open ground. It has no changes when straight track already runs there.</summary>
        public BuildPlan track = new BuildPlan();
        /// <summary>Every platform cell and the building strip, for the map preview (the first side tried when nothing fits).</summary>
        public List<Cell> platformCells = new List<Cell>(), strip = new List<Cell>();
        /// <summary>Towns and industries within three cells of the platforms.</summary>
        public List<ProducerState> nearby = new List<ProducerState>();
    }
    public sealed class StationService
    {
        public const int MaxStations = 24;
        readonly WorldState w; readonly RailNetwork net; readonly Balance b; readonly CitySimulation cities; readonly GameEvents events; readonly BuildService build;
        /// <summary>Save validation builds one only to call <see cref="Nearby(StationState)"/>, so <paramref name="events"/> and <paramref name="build"/> are optional; planning needs the session's BuildService.</summary>
        public StationService(WorldState w, RailNetwork net, Balance b, CitySimulation cities, GameEvents events = null, BuildService build = null)
        {
            this.w = w;
            this.net = net;
            this.b = b;
            this.cities = cities;
            this.events = events;
            this.build = build;
        }
        /// <summary>Towns and industries in reach of a standard 3-cell platform centred on <paramref name="center"/>.</summary>
        public List<ProducerState> Nearby(Cell center, int axis) => Nearby(StationLayout.Cells(center, axis, axis == 1 ? 0 : 1, StationLayout.MinLength, 1));
        public List<ProducerState> Nearby(StationState s) => Nearby(StationLayout.Cells(s));
        public List<ProducerState> Nearby(List<Cell> platformCells)
        {
            var list = new List<ProducerState>();
            foreach (var p in w.producers)
            {
                // A town counts as nearby when any platform cell is within three cells of its plaza or of any of its buildings and streets.
                var city = p.kind == ProducerKind.Town ? cities.CityFor(p.id) : null;
                foreach (var platform in platformCells)
                    if (platform.Distance(p.cell) <= 3 || (city != null && cities.Distance(city, platform) <= 3) ||
                        (p.kind == ProducerKind.SkiResort && SkiResorts.Distance(p, platform) <= SkiResorts.Catchment))
                    {
                        list.Add(p);
                        break;
                    }
            }
            return list;
        }
        /// <summary>
        /// Plans a station whose track 0 is centred on <paramref name="center"/>. Each platform cell may be open ground (the station
        /// lays straight track there) or straight track along <paramref name="axis"/>. <paramref name="producerId"/> 0 accepts any town
        /// or industry in reach. Sides are tried in the original order, so a 3 × 1 station on existing track lands where it always did.
        /// With <paramref name="quote"/> the player's funds are not checked, so the rail fixer can price a station together with its route.
        /// </summary>
        public StationPlan Plan(Cell center, int axis, int length, int platforms, int producerId = 0, bool quote = false)
        {
            var plan = new StationPlan { center = center, axis = axis, length = length, platforms = platforms, producerId = producerId };
            if (axis < 0 || axis > 1 || !StationLayout.ValidSize(length, platforms))
                return Refuse(plan, $"Stations are {StationLayout.MinLength}-{StationLayout.MaxLength} cells long with 1-{StationLayout.MaxPlatforms} platforms.");
            if (w.stations.Count >= MaxStations)
                return Refuse(plan, $"Station limit reached ({MaxStations}).");
            if (build == null)
                return Refuse(plan, "Stations cannot be planned without the railway builder.");
            // Keep the reason of the side that got furthest: platforms, catchment, the building strip, the railway rules, then funds.
            // With three or more platforms the sides lay different amounts of track, so an unaffordable side must not stop the search.
            int bestStage = -1;
            for (int d = 0; d < 4; d++)
            {
                if (d % 2 == axis)
                    continue;
                var cells = StationLayout.Cells(center, axis, d, length, platforms);
                var strip = StationLayout.Strip(center, axis, d, length);
                if (plan.platformCells.Count == 0)
                {
                    plan.platformCells = cells;
                    plan.strip = strip;
                }
                int stage = 0;
                string why = CheckPlatforms(cells, axis);
                List<ProducerState> nearby = null;
                if (why == null)
                {
                    stage = 1;
                    nearby = Nearby(cells);
                    if (nearby.Count == 0 || (producerId != 0 && !nearby.Exists(p => p.id == producerId)))
                        why = "No eligible industry or town within three cells.";
                }
                if (why == null)
                {
                    stage = 2;
                    why = CheckStrip(strip);
                }
                BuildPlan track = null;
                if (why == null)
                {
                    stage = 3;
                    var fresh = cells.FindAll(c => net.At(c) == null);
                    track = fresh.Count == 0 ? new BuildPlan { valid = true, reason = "Tracks already exist." } : build.ValidatePlatforms(fresh, StationLayout.Mask(axis));
                    if (!track.valid && !track.unaffordable)
                        why = track.reason;
                }
                int cost = track == null ? 0 : track.cost + StationLayout.Cost(b, length, platforms);
                if (why == null && cost > w.money && !quote)
                {
                    stage = 4;
                    why = "Not enough money for this station.";
                }
                if (why != null)
                {
                    // Among unaffordable sides, quote the cheaper one.
                    if (stage > bestStage || (stage == 4 && cost < plan.cost))
                    {
                        bestStage = stage;
                        plan.reason = why;
                        plan.platformCells = cells;
                        plan.strip = strip;
                        plan.nearby = nearby ?? plan.nearby;
                        plan.track = stage == 4 ? track : new BuildPlan();
                        plan.cost = stage == 4 ? cost : 0;
                    }
                    continue;
                }
                plan.side = d;
                plan.platformCells = cells;
                plan.strip = strip;
                plan.nearby = nearby;
                plan.track = track;
                plan.cost = cost;
                plan.valid = true;
                plan.reason = "Ready to build";
                return plan;
            }
            return plan;
        }
        static StationPlan Refuse(StationPlan plan, string reason)
        {
            plan.reason = reason;
            return plan;
        }
        string CheckPlatforms(List<Cell> cells, int axis)
        {
            int mask = StationLayout.Mask(axis);
            foreach (var c in cells)
            {
                var track = net.At(c);
                if (track == null)
                {
                    // Open ground only: new platforms never sit on water, hills, industries, town buildings or streets.
                    if (!MapDefinition.InBounds(c) || MapDefinition.Water(c) || !build.Placeable(c) || cities.Occupied(c))
                        return "Platforms need open ground or straight track. Hills, water, industries and town streets are in the way.";
                    continue;
                }
                if (track.mask != mask || track.bridge != 0)
                    return "Existing track must run straight along the platforms (no curves, junctions or bridges).";
                foreach (var s in w.stations)
                    if (StationLayout.PlatformAt(s, c) >= 0)
                        return "Another station already uses this track.";
                if (BuildService.OnRoute(w, track.id))
                    return "Park and clear the train route before adding a platform here.";
            }
            return null;
        }
        string CheckStrip(List<Cell> strip)
        {
            foreach (var c in strip)
                if (MapDefinition.Blocked(c, w) || MapDefinition.Water(c) || net.At(c) != null || cities.Occupied(c) || BuildService.StationFootprint(w, c))
                    return "Station needs a clear strip beside its tracks.";
            return null;
        }
        /// <summary>A standard 3 × 1 station on existing straight track; its axis follows the track.</summary>
        public Result Place(Cell center, int producerId)
        {
            var t = net.At(center);
            if (t == null || !Directions.Straight(t.mask))
                return Result.Fail("Place on the middle of three straight tracks.");
            return Place(Plan(center, t.mask == 10 ? 1 : 0, StationLayout.MinLength, 1, producerId), producerId);
        }
        /// <summary>Builds a planned station for <paramref name="producerId"/>, re-planned against the current state: its platform track and the station in one step.</summary>
        public Result Place(StationPlan quoted, int producerId)
        {
            var plan = Plan(quoted.center, quoted.axis, quoted.length, quoted.platforms, producerId);
            if (!plan.valid)
                return Result.Fail(plan.reason);
            var producer = plan.nearby.Find(p => p.id == producerId);
            if (producer == null)
                return Result.Fail("No eligible industry or town within three cells.");
            if (plan.track.changes.Count > 0)
            {
                var laid = build.CommitValidated(plan.track);
                if (!laid.ok)
                    return laid;
            }
            int id = w.nextId++, price = StationLayout.Cost(b, plan.length, plan.platforms);
            w.stations.Add(new StationState { id = id, cell = plan.center, axis = plan.axis, side = plan.side, length = plan.length, platforms = plan.platforms, producerId = producerId, name = producer.name + " Station", paid = price });
            EconomyService.Spend(w, price);
            w.revision++;
            net.Rebuild(w);
            events?.Push(GameEventKind.StationBuilt, id, plan.center);
            return Result.Good(plan.track.changes.Count > 0 ? "Station built with its own platform track." : "Station built.", id);
        }
    }
}
