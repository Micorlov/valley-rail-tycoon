using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    public enum TramLineStatus
    {
        Running, StationGone, VenueGone, NoFunds, HeldAtCrossing
    }
    /// <summary>
    /// Builds, changes and runs the light rail lines. Each tick, after the trains, a running line gathers riders (locals from
    /// the town's station queue, fans and visitors heading home from the venue, and the share of train passengers who change at
    /// the transfer stop, TramLines.Arrive), moves its trams round the loop a safe distance apart, stops them at either end to
    /// pay fares and board, and holds them before a railway crossing while a train is near. Riders a tram brings back to the
    /// station join the town's queue for trains. A line whose station or venue is gone idles and costs nothing.
    /// </summary>
    public sealed class TramService
    {
        /// <summary>How far ahead of a train's front a crossing counts as closed for trams, in simulation units.</summary>
        public const int TrainApproach = 6000;
        const int CarLength = 560;
        struct CrossingSpan
        {
            public int enter, leave, trackId;
        }
        readonly WorldState w; readonly RailNetwork net; readonly Balance b; readonly CitySimulation cities; readonly Scenery scenery; readonly GameEvents events;
        public readonly TramPlanner Planner;
        readonly Dictionary<int, TramRoute> routes = new Dictionary<int, TramRoute>();
        readonly Dictionary<int, List<CrossingSpan>> crossings = new Dictionary<int, List<CrossingSpan>>();
        readonly Dictionary<int, TramLineStatus> statuses = new Dictionary<int, TramLineStatus>();
        readonly HashSet<int> held = new HashSet<int>();
        int routeRevision = -1, statusRevision = -1, statusCityRevision = -1, statusLines = -1;
        public TramService(WorldState w, RailNetwork net, Balance b, CitySimulation cities, Scenery scenery, GameEvents events)
        {
            this.w = w;
            this.net = net;
            this.b = b;
            this.cities = cities;
            this.scenery = scenery;
            this.events = events;
            w.tramLines = w.tramLines ?? new List<TramLineState>();
            Planner = new TramPlanner(w, net, b, cities, scenery);
        }
        public TramLineState Line(int id) => TramLines.Line(w, id);
        public StationState Station(TramLineState line) => w.stations.Find(s => s.id == line.stationId);
        ProducerState Town(TramLineState line)
        {
            var s = Station(line);
            return s == null ? null : w.producers.Find(p => p.id == s.producerId);
        }
        /// <summary>The line's loop geometry, cached until the railway changes.</summary>
        public TramRoute Route(TramLineState line)
        {
            if (routeRevision != w.revision)
            {
                routes.Clear();
                crossings.Clear();
                routeRevision = w.revision;
            }
            if (routes.TryGetValue(line.id, out var route) && route.cells == line.cells)
                return route;
            route = new TramRoute(line.cells);
            routes[line.id] = route;
            var spans = new List<CrossingSpan>();
            for (int i = 0; i < line.cells.Count; i++)
            {
                var track = net.At(line.cells[i]);
                if (track == null)
                    continue;
                for (int back = 0; back < 2; back++)
                {
                    route.Span(i, back == 1, out int enter, out int leave);
                    spans.Add(new CrossingSpan { enter = enter, leave = leave, trackId = track.id });
                }
            }
            crossings[line.id] = spans;
            return route;
        }
        public TramLineStatus Status(TramLineState line)
        {
            if (statusRevision != w.revision || statusCityRevision != w.cityRevision || statusLines != w.tramLines.Count)
            {
                statuses.Clear();
                statusRevision = w.revision;
                statusCityRevision = w.cityRevision;
                statusLines = w.tramLines.Count;
            }
            if (!statuses.TryGetValue(line.id, out var status))
            {
                var town = Town(line);
                status = town == null || town.kind != ProducerKind.Town ? TramLineStatus.StationGone
                    : !TramVenues.Resolve(w, line, out _) ? TramLineStatus.VenueGone : TramLineStatus.Running;
                statuses[line.id] = status;
            }
            if (status == TramLineStatus.Running && w.money == 0)
                return TramLineStatus.NoFunds;
            if (status == TramLineStatus.Running && held.Contains(line.id))
                return TramLineStatus.HeldAtCrossing;
            return status;
        }
        public static string Describe(TramLineStatus status)
        {
            switch (status)
            {
                case TramLineStatus.StationGone: return "Idle: its railway station is gone";
                case TramLineStatus.VenueGone: return "Idle: the venue is gone or closed";
                case TramLineStatus.NoFunds: return "Stopped: operating funds are required";
                case TramLineStatus.HeldAtCrossing: return "Running · a tram waits for a train at a crossing";
                default: return "Running";
            }
        }
        /// <summary>Builds a quoted plan, re-planned against the current state; the route must still be the one the player saw.</summary>
        public Result Build(TramPlan quoted)
        {
            var plan = Planner.Plan(quoted.venue, quoted.stationId);
            if (!plan.valid)
                return Result.Fail(plan.reason);
            if (quoted.cells.Count > 0 && !Same(quoted.cells, plan.cells))
                return Result.Fail("The land changed since the plan was drawn. Check the new route and price.");
            foreach (var c in plan.trees)
                scenery.Fell(c);
            EconomyService.Spend(w, plan.cost);
            var line = new TramLineState
            {
                id = w.nextId++, stationId = plan.stationId, venueKind = (int)plan.venue.kind, venueId = plan.venue.id, venueCell = plan.venue.cell,
                paid = plan.trackCost + plan.stopCost, cells = new List<Cell>(plan.cells)
            };
            line.trams.Add(new TramState { id = w.nextId++, position = -1 });
            w.tramLines.Add(line);
            cities.Rebuild();
            w.revision++;
            events.Push(GameEventKind.TrackBuilt, 0, plan.cells[plan.cells.Count - 1], plan.cells[0].Key, plan.cost);
            return Result.Good("Light rail to " + plan.venue.name + " opened. Its first tram is on the way.", line.id);
        }
        static bool Same(List<Cell> a, List<Cell> z)
        {
            if (a.Count != z.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
                if (!a[i].Equals(z[i]))
                    return false;
            return true;
        }
        /// <summary>Removes a line and its trams for half of what they cost.</summary>
        public Result Remove(int lineId)
        {
            var line = Line(lineId);
            if (line == null)
                return Result.Fail("Light rail line not found.");
            int refund = line.paid / 2 + line.trams.Count * (TramCatalog.Price / 2);
            w.tramLines.Remove(line);
            EconomyService.Credit(w, refund, false);
            cities.Rebuild();
            w.revision++;
            events.Push(GameEventKind.Bulldozed, line.id, line.cells[0], 0);
            return Result.Good($"Light rail line removed; ${refund:N0} refunded.");
        }
        public Result AddTram(int lineId)
        {
            var line = Line(lineId);
            if (line == null)
                return Result.Fail("Light rail line not found.");
            int max = TramCatalog.TramsFor(line.cells.Count);
            if (line.trams.Count >= max)
                return Result.Fail(max == TramCatalog.MaxTrams ? $"A line runs at most {max} trams." : $"This line is long enough for {max} tram{(max == 1 ? "" : "s")}. Longer lines take more.");
            if (w.money < TramCatalog.Price)
                return Result.Fail("Not enough money.");
            EconomyService.Spend(w, TramCatalog.Price);
            line.trams.Add(new TramState { id = w.nextId++, position = -1 });
            events.Push(GameEventKind.TrainPurchased, line.id, line.cells[0], -1);
            return Result.Good($"Tram added for ${TramCatalog.Price:N0}. It enters service at the transfer stop.");
        }
        public Result SellTram(int lineId)
        {
            var line = Line(lineId);
            if (line == null)
                return Result.Fail("Light rail line not found.");
            if (line.trams.Count <= 1)
                return Result.Fail("A line needs one tram. Remove the line instead.");
            int index = line.trams.FindIndex(t => t.position < 0);
            line.trams.RemoveAt(index >= 0 ? index : line.trams.Count - 1);
            EconomyService.Credit(w, TramCatalog.Price / 2, false);
            return Result.Good($"Tram sold for ${TramCatalog.Price / 2:N0}.");
        }
        public void Step()
        {
            if (w.tramLines == null || w.tramLines.Count == 0)
                return;
            held.Clear();
            foreach (var line in w.tramLines)
            {
                var status = Status(line);
                if (status != TramLineStatus.Running && status != TramLineStatus.NoFunds && status != TramLineStatus.HeldAtCrossing)
                    continue;
                Accrue(line);
                if (w.money == 0)
                    continue;
                Move(line, Route(line));
                Charge(line);
            }
        }
        void Accrue(TramLineState line)
        {
            var town = Town(line);
            line.localRemainder += TramCatalog.LocalRate(line.venueKind);
            while (line.localRemainder >= 1200)
            {
                line.localRemainder -= 1200;
                if (town.inventory > 0 && line.waitingOut < TramCatalog.QueueCap)
                {
                    town.inventory--;
                    line.waitingOut++;
                }
            }
            line.drawRemainder += TramCatalog.DrawRate(line.venueKind);
            while (line.drawRemainder >= 1200)
            {
                line.drawRemainder -= 1200;
                line.waitingBack = Math.Min(TramCatalog.QueueCap, line.waitingBack + 1);
            }
            if (w.tick % TramCatalog.HomeEvery == 0 && line.visitors > 0)
            {
                int home = Math.Max(1, line.visitors * TramCatalog.HomePercent / 100);
                line.visitors -= home;
                line.waitingBack = Math.Min(TramCatalog.QueueCap, line.waitingBack + home);
            }
        }
        void Move(TramLineState line, TramRoute route)
        {
            var spans = crossings[line.id];
            foreach (var t in line.trams)
            {
                if (t.position < 0)
                {
                    if (CanEnter(line, route, t))
                    {
                        t.position = 0;
                        Arrive(line, route, t);
                    }
                    continue;
                }
                if (t.dwell > 0)
                {
                    t.dwell--;
                    continue;
                }
                t.moveRemainder += TramCatalog.Speed;
                int advance = t.moveRemainder / 20;
                t.moveRemainder %= 20;
                int next = t.position < route.Half ? route.Half : route.Loop;
                int gap = GapAhead(line, route, t);
                int room = next - t.position;
                if (gap != int.MaxValue)
                    room = Math.Min(room, gap - TramCatalog.Gap);
                int move = Math.Max(0, Math.Min(advance, room));
                foreach (var x in spans)
                {
                    if (t.position > x.enter || t.position + move <= x.enter)
                        continue;
                    // A tram crosses only when no train is near and it can clear the crossing without stopping on it.
                    bool clear = !TrainNear(x.trackId) && (gap == int.MaxValue || t.position + gap - TramCatalog.Gap >= x.leave + TramCatalog.Length);
                    if (!clear)
                    {
                        move = x.enter - t.position;
                        held.Add(line.id);
                    }
                }
                t.position += move;
                if (t.position == next)
                {
                    if (next == route.Loop)
                        t.position = 0;
                    Arrive(line, route, t);
                }
            }
        }
        /// <summary>A waiting tram enters at the transfer stop once no other tram is within a safe gap either side of it.</summary>
        bool CanEnter(TramLineState line, TramRoute route, TramState tram)
        {
            foreach (var o in line.trams)
                if (o != tram && o.position >= 0 && (route.Ahead(0, o.position) < TramCatalog.Gap || route.Ahead(o.position, 0) < TramCatalog.Gap))
                    return false;
            return true;
        }
        static int GapAhead(TramLineState line, TramRoute route, TramState tram)
        {
            int gap = int.MaxValue;
            foreach (var o in line.trams)
                if (o != tram && o.position >= 0)
                    gap = Math.Min(gap, route.Ahead(tram.position, o.position));
            return gap;
        }
        /// <summary>Pays for the riders aboard, lets them off and boards the next ones: at the venue, and at the transfer stop.</summary>
        void Arrive(TramLineState line, TramRoute route, TramState t)
        {
            bool venue = t.position == route.Half;
            if (t.units > 0)
            {
                int pay = t.units * TramCatalog.Fare(b, line.cells.Count);
                EconomyService.Credit(w, pay, true);
                line.accounts.Record(w.tick, pay, 0);
                if (venue)
                    line.visitors = Math.Min(TramCatalog.VisitorsCap, line.visitors + t.units);
                else
                {
                    // Back at the station, riders join the town's queue for trains.
                    var town = Town(line);
                    town.inventory = Math.Min(town.storage, town.inventory + t.units);
                }
                t.units = 0;
            }
            if (venue)
            {
                t.units = Math.Min(TramCatalog.Capacity, line.waitingBack);
                line.waitingBack -= t.units;
            }
            else
            {
                t.units = Math.Min(TramCatalog.Capacity, line.waitingOut);
                line.waitingOut -= t.units;
            }
            t.dwell = TramCatalog.Dwell;
        }
        void Charge(TramLineState line)
        {
            foreach (var t in line.trams)
            {
                t.costRemainder += TramCatalog.RunningCost;
                int charge = t.costRemainder / 1200;
                t.costRemainder %= 1200;
                if (charge <= 0)
                    continue;
                int paid = Math.Min(w.money, charge);
                EconomyService.Spend(w, paid, recurring: true);
                line.accounts.Record(w.tick, 0, paid);
            }
        }
        /// <summary>
        /// True while a running train covers the piece or will reach it soon: its body from the front back (through the leg it
        /// arrived on), and <see cref="TrainApproach"/> ahead of it.
        /// </summary>
        public bool TrainNear(int trackId)
        {
            foreach (var t in w.trains)
            {
                if ((t.state != ServiceState.Travelling && t.state != ServiceState.Loading) || t.path.Count == 0 || t.step >= t.path.Count)
                    continue;
                for (int i = t.step, ahead = -t.distance; i < t.path.Count && ahead <= TrainApproach; ahead += t.path[i++].length)
                    if (t.path[i].trackId == trackId)
                        return true;
                int body = (1 + TrainCatalog.Wagons(t)) * CarLength, behind = t.distance;
                for (int i = t.step - 1; i >= 0 && behind < body; behind += t.path[i--].length)
                    if (t.path[i].trackId == trackId)
                        return true;
                for (int i = t.returnPath.Count - 1; i >= 0 && behind < body; behind += t.returnPath[i--].length)
                    if (t.returnPath[i].trackId == trackId)
                        return true;
            }
            return false;
        }
    }
}
