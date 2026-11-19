using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>An AUTO ROUTE quote: the first station's site, the train, and the price of the whole line.</summary>
    public sealed class AutoRoutePlan
    {
        public bool valid; public string reason; public int fromId, toId, model, cost; public Cargo cargo;
        public StationPlan site;
        /// <summary>The AI's track and last station from the first station, for the map preview. Null when there is none.</summary>
        public RailFix fix;
    }
    /// <summary>
    /// AUTO ROUTE: the player picks where a line starts and where it ends, and the AI builds all of it: a station at the start,
    /// the basic train for its cargo, then AI FIX RAILS (RailFixPlanner) aimed at the end, which lays the track and builds the
    /// last station. Quotes run on a planning copy of the world, so nothing live changes until <see cref="Build"/>.
    /// </summary>
    public static class AutoRoute
    {
        // Each try plans a whole line, so only the few sites nearest the destination are tried.
        const int SiteTries = 3;
        public static int Model(Cargo cargo) => cargo == Cargo.Passengers ? 2 : 0;
        /// <summary>Places that send something some other place on the map uses.</summary>
        public static List<ProducerState> Origins(WorldState w) => w.producers.FindAll(p => Targets(w, p).Count > 0);
        /// <summary>Places that use what <paramref name="from"/> sends, nearest first.</summary>
        public static List<ProducerState> Targets(WorldState w, ProducerState from)
        {
            var cargo = IndustryCatalog.Output(from.kind);
            if (!cargo.HasValue)
                return new List<ProducerState>();
            var list = w.producers.FindAll(p => p.id != from.id && CargoTransfer.Delivers(from.kind, p.kind, cargo.Value));
            list.Sort((x, y) => x.cell.Distance(from.cell) != y.cell.Distance(from.cell) ? x.cell.Distance(from.cell).CompareTo(y.cell.Distance(from.cell)) : x.id.CompareTo(y.id));
            return list;
        }
        public static AutoRoutePlan Plan(GameSession g, int fromId, int toId)
        {
            var w = g.World;
            var plan = new AutoRoutePlan { fromId = fromId, toId = toId };
            ProducerState from = g.Cargo.Producer(fromId), to = g.Cargo.Producer(toId);
            if (from == null || to == null || from.id == to.id)
                return Refuse(plan, "Choose two different places.");
            var cargo = IndustryCatalog.Output(from.kind);
            if (!cargo.HasValue || !CargoTransfer.Delivers(from.kind, to.kind, cargo.Value))
                return Refuse(plan, $"{to.name} has no use for anything from {from.name}.");
            plan.cargo = cargo.Value;
            plan.model = Model(plan.cargo);
            int train = g.Balance.TrainPrice(plan.model, TrainCatalog.DefaultWagons(plan.model));
            var sites = g.Fixer.Sites(from, to.cell);
            plan.reason = $"No room for a station at {from.name}.";
            for (int i = 0; i < sites.Count && i < SiteTries; i++)
            {
                // A what-if world with its own track, station, train and tree lists; money is unlimited so every part gets a price.
                var copy = w.PlanningCopy();
                copy.trains = new List<TrainState>(w.trains);
                copy.felledTrees = new List<Cell>(w.felledTrees);
                copy.money = int.MaxValue / 2;
                var scenery = new Scenery(copy);
                var net = new RailNetwork(copy);
                var paths = new RailPathfinder(net);
                var build = new BuildService(copy, net, g.Balance, g.Cities, new GameEvents(), scenery);
                var stations = new StationService(copy, net, g.Balance, g.Cities, null, build);
                var trains = new TrainSimulation(copy, net, paths, g.Balance, new CargoService(copy, g.Balance, g.Cities, null), new GameEvents());
                var placed = stations.Place(sites[i], from.id);
                if (!placed.ok)
                {
                    plan.reason = placed.message;
                    continue;
                }
                var bought = trains.Buy(placed.id, plan.model, plan.cargo);
                if (!bought.ok)
                    return Refuse(plan, bought.message);
                var fix = new RailFixPlanner(copy, net, paths, build, trains, g.Balance, stations, g.Cities, scenery).Plan(bought.id, to.id);
                if (!fix.valid)
                {
                    plan.reason = fix.reason;
                    continue;
                }
                plan.site = sites[i];
                plan.fix = fix;
                plan.cost = sites[i].cost + train + fix.cost;
                plan.valid = plan.cost <= w.money;
                plan.reason = plan.valid ? "Ready to build" : $"This line costs ${plan.cost:N0}; you have ${w.money:N0}.";
                return plan;
            }
            return plan;
        }
        /// <summary>Plans again against the live world and builds the line: station, train, then the AI's track and last station.</summary>
        public static Result Build(GameSession g, int fromId, int toId)
        {
            var plan = Plan(g, fromId, toId);
            if (!plan.valid)
                return Result.Fail(plan.reason);
            var placed = g.Stations.Place(plan.site, plan.fromId);
            if (!placed.ok)
                return placed;
            var bought = g.Trains.Buy(placed.id, plan.model, plan.cargo);
            if (!bought.ok)
                return bought;
            // The same plan on the same state: this only fails if the world changed, and the parked train keeps its AI FIX RAILS.
            return g.Fixer.Apply(bought.id, null, plan.toId);
        }
        static AutoRoutePlan Refuse(AutoRoutePlan plan, string reason)
        {
            plan.reason = reason;
            return plan;
        }
    }
}
