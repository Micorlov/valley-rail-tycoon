using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    public static class EconomyService
    {
        /// <summary>
        /// Debits money. Only recurring costs (train running costs) enter the rolling per-minute ledger;
        /// one-off construction and purchase spending is tracked in totalExpenses but must not read as "per min".
        /// </summary>
        public static void Spend(WorldState w, int amount, bool recurring = false)
        {
            if (amount < 0 || amount > w.money)
                throw new InvalidOperationException("Invalid debit");
            w.money -= amount;
            w.totalExpenses += amount;
            if (recurring)
                Record(w, 0, amount);
        }
        public static void Credit(WorldState w, int amount, bool delivery)
        {
            w.money += amount;
            if (delivery)
            {
                w.totalIncome += amount;
                Record(w, amount, 0);
            }
        }
        static void Record(WorldState w, int income, int expense)
        {
            long bucket = w.tick / 20 * 20;
            if (w.ledger.Count > 0 && w.ledger[w.ledger.Count - 1].tick == bucket)
            {
                var e = w.ledger[w.ledger.Count - 1];
                e.income += income;
                e.expense += expense;
                w.ledger[w.ledger.Count - 1] = e;
            }
            else
                w.ledger.Add(new LedgerEntry { tick = bucket, income = income, expense = expense });
        }
        public static void Recent(WorldState w, out int income, out int expense)
        {
            income = expense = 0;
            foreach (var e in w.ledger)
                if (e.tick > w.tick - 1200)
                {
                    income += e.income;
                    expense += e.expense;
                }
        }
    }
    public sealed class CargoService
    {
        readonly WorldState w; readonly Balance b; readonly GameEvents events; public readonly CitySimulation Cities;
        public CargoService(WorldState w, Balance b, CitySimulation cities, GameEvents events)
        {
            this.w = w;
            this.b = b;
            Cities = cities;
            this.events = events;
        }
        public ProducerState Producer(int id)
        {
            for (int i = 0; i < w.producers.Count; i++)
                if (w.producers[i].id == id)
                    return w.producers[i];
            return null;
        }
        public void Step()
        {
            foreach (var p in w.producers)
            {
                if (!IndustryCatalog.Output(p.kind).HasValue || IndustryCatalog.Processing(p.kind))
                    continue;
                bool town = p.kind == ProducerKind.Town;
                p.remainder += town ? p.production : b.CargoProduction(IndustryCatalog.Output(p.kind).Value);
                int storage = town ? p.storage : b.storage;
                while (p.remainder >= 1200)
                {
                    p.remainder -= 1200;
                    if (p.inventory < storage)
                        p.inventory++;
                }
            }
        }
        public void Receive(ProducerState producer, Cargo type, int units)
        {
            if (units <= 0 || !MapDefinition.Accepts(producer.kind, type)) return;
            // One unit delivered becomes one unit of output, capped by industry storage.
            if (IndustryCatalog.Processing(producer.kind) || producer.kind == ProducerKind.Factory)
                producer.inventory += Math.Min(units, Math.Max(0, b.storage - producer.inventory));
            Cities.RecordArrival(producer.id, type, units);
        }
        /// <summary>
        /// Pays for and unloads cargo when <paramref name="at"/> is its destination. Both the normal stop
        /// (<see cref="Service"/>) and a return-to-station stop use this, so every delivery credits and reports alike.
        /// </summary>
        public void Unload(TrainState t, StationState at, ProducerState p)
        {
            if (t.units <= 0 || t.cargoDestination != p.id || t.origin == p.id)
                return;
            if (CargoTransfer.Holds(p.kind, t.cargo))
            {
                Transfer(t, at, p);
                return;
            }
            if (!MapDefinition.Accepts(p.kind, t.cargo))
                return;
            var origin = Producer(t.origin);
            // Freight forwarded from a city was partly paid on drop-off; this pays the rest of the fare from its source.
            int units = t.units, revenue = Math.Max(0, units * b.CargoRate(t.cargo) * Math.Max(1, origin.cell.Distance(p.cell)) - t.prepaid);
            t.prepaid = 0;
            EconomyService.Credit(w, revenue, true);
            t.accounts.Record(w.tick, revenue, 0);
            w.delivered += units;
            Receive(p, t.cargo, units);
            // Some of the passengers change here to the station's light rail lines.
            TramLines.Arrive(w, at, t.cargo, units);
            t.units = 0;
            t.origin = 0;
            t.cargoDestination = 0;
            events.Push(GameEventKind.CargoDelivered, t.id, at.cell, units, revenue);
        }
        /// <summary>Leaves freight at a city transfer station and pays the first leg's share of its fare (CargoTransfer).</summary>
        void Transfer(TrainState t, StationState at, ProducerState town)
        {
            var origin = Producer(t.origin);
            int kept = CargoTransfer.Store(town, t.cargo, t.origin, t.units, b.storage, out var stock);
            int pay = kept * b.CargoRate(t.cargo) * Math.Max(1, origin.cell.Distance(town.cell)) * CargoTransfer.Share / 100;
            stock.credit += pay + t.prepaid;
            EconomyService.Credit(w, pay, true);
            t.accounts.Record(w.tick, pay, 0);
            t.units = 0;
            t.origin = 0;
            t.cargoDestination = 0;
            t.prepaid = 0;
            events.Push(GameEventKind.CargoDelivered, t.id, at.cell, kept, pay);
        }
        /// <summary>Boards the freight a city holds for a train bound to a stop that uses it.</summary>
        void Forward(TrainState t, ProducerState town, ProducerState destination)
        {
            int units = CargoTransfer.Take(town, t.cargo, b.Capacity(t), out int origin, out int credit);
            if (units == 0)
                return;
            t.units = units;
            t.origin = origin;
            t.prepaid = credit;
            t.cargoDestination = destination.id;
        }
        public void Service(TrainState t, StationState current, StationState next)
        {
            var p = Producer(current.producerId);
            var destination = Producer(next.producerId);
            Unload(t, current, p);
            if (t.units == 0 && p.id != destination.id && CargoTransfer.Holds(p.kind, t.cargo) && MapDefinition.Accepts(destination.kind, t.cargo))
                Forward(t, p, destination);
            else if (t.units == 0 && MapDefinition.Produces(p.kind, t.cargo) && (MapDefinition.Accepts(destination.kind, t.cargo) || CargoTransfer.Holds(destination.kind, t.cargo)) && p.id != destination.id)
            {
                t.units = Math.Min(b.Capacity(t), p.inventory);
                p.inventory -= t.units;
                if (t.cargo == Cargo.Passengers)
                {
                    var road = Cities.RoadBetween(p.id, destination.id);
                    if (road != null && road.Complete)
                    {
                        int share = t.units * 35 + road.diversionRemainder;
                        t.units -= share / 100;
                        road.diversionRemainder = share % 100;
                    }
                }
                Cities.RecordBoarding(p.id, t.cargo, t.units);
                t.origin = p.id;
                t.cargoDestination = destination.id;
            }
        }
    }
    public sealed class TrainSimulation
    {
        readonly WorldState w; readonly RailNetwork net; readonly RailPathfinder paths; readonly Balance b; readonly CargoService cargo; readonly GameEvents events;
        readonly IRailAccessPolicy access = new ExclusiveNetworkPolicy();
        public TrainSimulation(WorldState w, RailNetwork n, RailPathfinder p, Balance b, CargoService c, GameEvents events)
        {
            this.w = w;
            net = n;
            paths = p;
            this.b = b;
            cargo = c;
            this.events = events;
        }
        public StationState Station(int id)
        {
            for (int i = 0; i < w.stations.Count; i++)
                if (w.stations[i].id == id)
                    return w.stations[i];
            return null;
        }
        public TrainState Train(int id)
        {
            for (int i = 0; i < w.trains.Count; i++)
                if (w.trains[i].id == id)
                    return w.trains[i];
            return null;
        }
        /// <summary>Buys the model with its standard consist.</summary>
        public Result Buy(int stationId, int model, Cargo type) => Buy(stationId, model, type, TrainCatalog.DefaultWagons(model));
        public Result Buy(int stationId, int model, Cargo type, int wagons)
        {
            var s = Station(stationId);
            if (s == null)
                return Result.Fail("Select a station first.");
            if (!TrainCatalog.SupportsCargo(model, type))
                return Result.Fail("This train cannot carry that cargo.");
            if (wagons < TrainCatalog.MinWagons || wagons > TrainCatalog.MaxWagons)
                return Result.Fail($"A train takes {TrainCatalog.MinWagons} to {TrainCatalog.MaxWagons} wagons.");
            if (w.trains.Count >= 12)
                return Result.Fail("Train limit reached (12).");
            int platform = FreePlatform(s);
            if (platform < 0)
                return Result.Fail(StationLayout.Platforms(s) > 1 ? "Every platform's railway already has a train. Build a separate network." : "This railway already has a train. Build a separate network.");
            if (w.money < b.TrainPrice(model, wagons))
                return Result.Fail("Not enough money.");
            var t = new TrainState { id = w.nextId++, number = w.nextTrainNumber++, stationId = stationId, model = model, wagons = wagons, cargo = type, state = ServiceState.Parked };
            t.platform = platform;
            w.trains.Add(t);
            EconomyService.Spend(w, b.TrainPrice(model, wagons));
            events.Push(GameEventKind.TrainPurchased, t.id, s.cell, model);
            return Result.Good("Train purchased. Choose its destination.", t.id);
        }
        /// <summary>The first platform whose railway has no train yet, or -1. Separate railways may share one station this way.</summary>
        int FreePlatform(StationState s)
        {
            for (int k = 0; k < StationLayout.Platforms(s); k++)
            {
                var piece = net.At(StationLayout.Center(s, k));
                if (piece != null && access.CanOccupy(w, net, piece.id))
                    return k;
            }
            return -1;
        }
        /// <summary>
        /// The shortest leg from the platform <paramref name="t"/> stands at to any platform of <paramref name="there"/>, with its
        /// way back to the same platform. False when no platform is reachable both ways.
        /// </summary>
        bool Legs(TrainState t, StationState here, StationState there, out List<RailStep> go, out List<RailStep> back, out int distance)
        {
            go = back = null;
            distance = int.MaxValue;
            var from = StationLayout.Center(here, t.platform);
            for (int k = 0; k < StationLayout.Platforms(there); k++)
            {
                var to = StationLayout.Center(there, k);
                var forward = paths.FindPath(from, to);
                if (forward == null)
                    continue;
                int length = 0;
                foreach (var step in forward)
                    length += step.length;
                if (length >= distance)
                    continue;
                var backward = paths.FindPath(to, from);
                if (backward == null)
                    continue;
                go = forward;
                back = backward;
                distance = length;
            }
            return go != null;
        }
        /// <summary>True when a train can carry <paramref name="type"/> between the stops: a full delivery, or a first leg to a city transfer station.</summary>
        public bool CanDeliver(StationState a, StationState z, Cargo type) => Delivers(a, z, type) || Feeds(a, z, type);
        /// <summary>True when the leg ends the cargo's journey at a stop that uses it (not at a city transfer station).</summary>
        public bool Delivers(StationState a, StationState z, Cargo type)
        {
            var pa = cargo.Producer(a.producerId);
            var pb = cargo.Producer(z.producerId);
            return pa.id != pb.id && CargoTransfer.Delivers(pa.kind, pb.kind, type);
        }
        /// <summary>True when the leg leaves the cargo at a city transfer station for a later train.</summary>
        public bool Feeds(StationState a, StationState z, Cargo type)
        {
            var pa = cargo.Producer(a.producerId);
            var pb = cargo.Producer(z.producerId);
            return pa.id != pb.id && CargoTransfer.Feeds(pa.kind, pb.kind, type);
        }
        public Result AutoDestination(int trainId)
        {
            var t = Train(trainId);
            if (t == null)
                return Result.Fail("Train not found.");
            if (t.state != ServiceState.Parked)
                return Result.Fail("Return to a station before changing the route.");
            if (t.units > 0)
                return Result.Fail("Deliver the current cargo or sell this train first.");
            var current = Station(t.stationId);
            if (current == null)
                return Result.Fail("Select a station first.");
            StationState best = null;
            int bestDistance = int.MaxValue;
            bool bestDelivers = false;
            foreach (var candidate in w.stations)
            {
                if (candidate.id == current.id || !CanDeliver(current, candidate, t.cargo))
                    continue;
                if (!Legs(t, current, candidate, out _, out _, out int distance))
                    continue;
                // A stop that uses the cargo beats a city transfer station at any distance.
                bool delivers = Delivers(current, candidate, t.cargo);
                if ((delivers && !bestDelivers) || (delivers == bestDelivers && (distance < bestDistance || (distance == bestDistance && (best == null || candidate.id < best.id)))))
                {
                    best = candidate;
                    bestDistance = distance;
                    bestDelivers = delivers;
                }
            }
            if (best == null)
                return Result.Fail("No reachable delivery station for " + t.cargo + ". Connect a compatible station, or tap AI FIX RAILS.");
            var result = AssignRoute(t.id, current.id, best.id);
            return result.ok ? Result.Good("Auto destination: " + best.name + ". Service started.") : result;
        }
        public Result AssignRoute(int trainId, int firstId, int secondId)
        {
            var t = Train(trainId);
            var a = Station(firstId);
            var z = Station(secondId);
            if (t == null || a == null || z == null || a.id == z.id)
                return Result.Fail("Choose two different stations.");
            if (t.state != ServiceState.Parked)
                return Result.Fail("Return to a station before changing the route.");
            if (t.stationId != a.id && t.stationId != z.id)
                return Result.Fail("One stop must be the train's current station.");
            if (t.units > 0)
                return Result.Fail("Deliver the current cargo or sell this train first.");
            if (!CanDeliver(a, z, t.cargo))
                return Result.Fail("These stops do not form a delivery route for " + t.cargo + ".");
            bool atA = t.stationId == a.id;
            if (!Legs(t, atA ? a : z, atA ? z : a, out var go, out var back, out _))
                return Result.Fail("Both stations need a connected, traversable railway.");
            t.a = a.id;
            t.b = z.id;
            t.returnToStation = false;
            t.destination = atA ? z.id : a.id;
            t.path = go;
            t.returnPath = back;
            t.step = 0;
            t.distance = 0;
            t.dwell = StationCatalog.DwellTicks(b, Station(t.stationId));
            t.state = ServiceState.Loading;
            var here = Station(t.stationId);
            cargo.Service(t, here, Station(t.destination));
            events.Push(GameEventKind.RouteCreated, t.id, here.cell);
            return Result.Good("Route assigned. All aboard!");
        }
        public Result ReturnToStation(int id)
        {
            var t = Train(id);
            if (t == null)
                return Result.Fail("Train not found.");
            if (t.state == ServiceState.Loading)
            {
                if(t.units>0)t.returnToStation=true;
                else {t.state=ServiceState.Parked;t.returnToStation=false;}
            }
            else if (t.state == ServiceState.Travelling)
                t.returnToStation = true;
            else
                return Result.Fail("Train is already parked or needs operating funds.");
            return Result.Good("Train will park at the next station.");
        }
        public Result ClearRoute(int id)
        {
            var t = Train(id);
            if (t == null || t.state != ServiceState.Parked)
                return Result.Fail("Park the train first.");
            if (t.units > 0)
                return Result.Fail("Cargo remains aboard. Resume its route to deliver or sell the train.");
            t.a = t.b = t.destination = 0;
            t.path.Clear();
            t.returnPath.Clear();
            t.step = t.distance = 0;
            return Result.Good("Route cleared.");
        }
        public Result Resume(int id)
        {
            var t = Train(id);
            if (t == null || w.money == 0)
                return Result.Fail("Operating funds are required.");
            if (t.state == ServiceState.InsufficientFunds)
            {
                t.state = t.dwell > 0 ? ServiceState.Loading : ServiceState.Travelling;
                return Result.Good("Service resumed.");
            }
            if (t.state != ServiceState.Parked || t.path.Count == 0)
                return Result.Fail("Assign a route first.");
            // Arrival leaves the completed path installed. Depart on the opposite leg.
            if (t.stationId == t.destination)
                SwapLeg(t);
            t.returnToStation = false;
            t.dwell = StationCatalog.DwellTicks(b, Station(t.stationId));
            t.state = ServiceState.Loading;
            cargo.Service(t, Station(t.stationId), Station(t.destination));
            return Result.Good("Service resumed.");
        }
        public Result Sell(int id)
        {
            var t = Train(id);
            if (t == null)
                return Result.Fail("Train not found.");
            w.trains.Remove(t);
            EconomyService.Credit(w, b.TrainPrice(t) / 2, false);
            var at = Station(t.stationId);
            events.Push(GameEventKind.TrainSold, t.id, at != null ? at.cell : default);
            return Result.Good("Train sold; any onboard cargo was discarded.");
        }
        /// <summary>
        /// Adds or removes wagons while the train stands at a station. Each added wagon costs the full wagon price and
        /// each removed one refunds half of it, as a sold train does. Cargo already aboard must still fit.
        /// </summary>
        public Result SetWagons(int id, int wagons)
        {
            var t = Train(id);
            if (t == null)
                return Result.Fail("Train not found.");
            if (wagons < TrainCatalog.MinWagons || wagons > TrainCatalog.MaxWagons)
                return Result.Fail($"A train takes {TrainCatalog.MinWagons} to {TrainCatalog.MaxWagons} wagons.");
            int current = TrainCatalog.Wagons(t);
            if (wagons == current)
                return Result.Fail($"The train already has {wagons} wagons.");
            if (t.state != ServiceState.Parked && t.state != ServiceState.Loading)
                return Result.Fail("Wagons are changed at a station. Tap RETURN TO STATION first.");
            if (t.units > b.Capacity(t.model, wagons))
                return Result.Fail("The cargo aboard needs more wagons. Deliver it first.");
            int change = wagons - current, price = Math.Abs(change) * b.WagonPrice(t.model);
            if (change > 0)
            {
                if (w.money < price)
                    return Result.Fail("Not enough money.");
                EconomyService.Spend(w, price);
            }
            else
                EconomyService.Credit(w, price / 2, false);
            t.wagons = wagons;
            string count = Math.Abs(change) == 1 ? "1 wagon" : Math.Abs(change) + " wagons";
            return Result.Good(change > 0 ? $"Added {count} for ${price:N0}." : $"Removed {count}; ${price / 2:N0} refunded.");
        }
        void SwapLeg(TrainState t)
        {
            var old = t.path;
            t.path = t.returnPath;
            t.returnPath = old;
            t.destination = t.stationId == t.a ? t.b : t.a;
            t.step = 0;
            t.distance = 0;
        }
        public void Step()
        {
            foreach (var t in w.trains)
            {
                if (t.state != ServiceState.Loading && t.state != ServiceState.Travelling)
                    continue;
                if (w.money == 0)
                {
                    t.state = ServiceState.InsufficientFunds;
                    continue;
                }
                if (t.state == ServiceState.Loading)
                {
                    if (--t.dwell <= 0)
                    {
                        t.state = ServiceState.Travelling;
                        // stationId still names the stop being left; it changes on arrival.
                        var leaving = Station(t.stationId);
                        if (leaving != null)
                            events.Push(GameEventKind.TrainDeparted, t.id, leaving.cell);
                    }
                }
                else
                {
                    t.moveRemainder += b.speed[t.model];
                    int remaining = t.moveRemainder / 20;
                    t.moveRemainder %= 20;
                    while (remaining > 0 && t.state == ServiceState.Travelling)
                    {
                        if (t.step >= t.path.Count)
                        {
                            t.state = ServiceState.InvalidRoute;
                            break;
                        }
                        var step = t.path[t.step];
                        int travel = Math.Min(remaining, step.length - t.distance);
                        t.distance += travel;
                        remaining -= travel;
                        if (t.distance == step.length)
                        {
                            if (t.step + 1 < t.path.Count)
                            {
                                TurnAside(t);
                                var nextStep = t.path[t.step + 1];
                                if (net.ids[nextStep.trackId].mask == 15 && !CrossingSignals.MayEnter(w, nextStep.trackId, nextStep.entry, nextStep.exit, t.id))
                                    break; // Another train still covers the crossing: discard this tick's movement.
                                t.step++;
                                t.distance = 0;
                            }
                            else
                            {
                                t.stationId = t.destination;
                                int next = t.stationId == t.a ? t.b : t.a;
                                var arrived = Station(t.stationId);
                                if (arrived != null)
                                    t.platform = Math.Max(0, StationLayout.PlatformAt(arrived, net.ids[step.trackId].cell));
                                if (arrived != null)
                                    events.Push(GameEventKind.TrainArrived, t.id, arrived.cell);
                                // A return-to-station request unloads without boarding new cargo.
                                if (t.returnToStation)
                                {
                                    UnloadOnly(t);
                                    t.state = ServiceState.Parked;
                                    t.returnToStation = false;
                                }
                                else
                                {
                                    cargo.Service(t, Station(t.stationId), Station(next));
                                    SwapLeg(t);
                                    t.dwell = StationCatalog.DwellTicks(b, Station(t.stationId));
                                    t.state = ServiceState.Loading;
                                }
                            }
                        }
                    }
                }
                t.costRemainder += b.RunningCost(t);
                int charge = t.costRemainder / 1200;
                t.costRemainder %= 1200;
                if (charge > 0)
                {
                    int paid = Math.Min(w.money, charge);
                    EconomyService.Spend(w, paid, recurring: true);
                    t.accounts.Record(w.tick, 0, paid);
                }
                if (w.money == 0 && (t.state == ServiceState.Loading || t.state == ServiceState.Travelling))
                    t.state = ServiceState.InsufficientFunds;
            }
        }
        /// <summary>How far past its next piece a train looks for a crossing another train holds.</summary>
        public const int DetourLookahead = 4000;
        /// <summary>The most extra track a train runs to turn aside rather than wait: the longest train that could hold a crossing.</summary>
        public const int DetourSlack = CrossingSignals.Reach;
        /// <summary>
        /// Turns a train onto a side line when a crossing a few pieces ahead is held by another train: the rest of its route is
        /// replanned from its next piece around every crossing other trains cover, when that costs at most
        /// <see cref="DetourSlack"/> more track. It keeps the same platform, so the planned way back still starts there. A
        /// train already at the held crossing's signal has no piece left to turn at, so it waits.
        /// </summary>
        void TurnAside(TrainState t)
        {
            int from = t.step + 1, held = -1;
            for (int i = from, ahead = 0; i < t.path.Count && ahead <= DetourLookahead; ahead += t.path[i++].length)
            {
                var s = t.path[i];
                if (net.ids[s.trackId].mask == 15 && !CrossingSignals.MayEnter(w, s.trackId, s.entry, s.exit, t.id))
                {
                    held = i;
                    break;
                }
            }
            if (held <= from)
                return;
            var start = t.path[from];
            var detour = paths.FindDetour(start.trackId, start.entry, t.path[t.path.Count - 1].trackId, CrossingSignals.Covered(w, net, t.id));
            if (detour == null)
                return;
            int length = 0, remaining = 0;
            foreach (var s in detour)
                length += s.length;
            for (int i = from; i < t.path.Count; i++)
                remaining += t.path[i].length;
            if (length > remaining + DetourSlack)
                return;
            t.path.RemoveRange(from, t.path.Count - from);
            t.path.AddRange(detour);
        }
        void UnloadOnly(TrainState t)
        {
            var s = Station(t.stationId);
            cargo.Unload(t, s, cargo.Producer(s.producerId));
        }
    }
    public interface IGameCommand
    {
        Result Execute(GameSession game);
    }
    public sealed class BuildCommand : IGameCommand
    {
        readonly BuildPlan plan; public BuildCommand(BuildPlan p)
        {
            plan = p;
        }
        public Result Execute(GameSession g) => g.Build.CommitBuild(plan);
    }
    public sealed class GameSession
    {
        public readonly WorldState World; public readonly Balance Balance; public readonly RailNetwork Network; public readonly RailPathfinder Pathfinder; public readonly BuildService Build; public readonly StationService Stations; public readonly CargoService Cargo; public readonly TrainSimulation Trains; public readonly CitySimulation Cities; public readonly RailFixPlanner Fixer; public readonly Scenery Scenery; public readonly StationUpgradeService Upgrades;
        /// <summary>AI FIX in the track tool: repairs gaps, loose ends and cut junctions across the whole network.</summary>
        public readonly RailRepairPlanner Repairs;
        /// <summary>Light rail lines from railway stations to the stadiums, beaches and ski resorts.</summary>
        public readonly TramService Trams;
        readonly Queue<IGameCommand> commands = new Queue<IGameCommand>(); public event Action<Result> CommandCompleted;
        /// <summary>Gameplay facts for the presentation (sounds) to drain each frame. Never saved.</summary>
        public readonly GameEvents Events = new GameEvents();
        public GameSession(WorldState w, Balance b)
        {
            World = w;
            Balance = b;
            Network = new RailNetwork(w);
            Pathfinder = new RailPathfinder(Network);
            Cities = new CitySimulation(w, Network, b);
            // Every snowy peak gets its ski resort, in new games and older saves alike, before the pines and builders look at the land.
            int resorts = SkiResorts.Establish(w, Cities, Network);
            Scenery = new Scenery(w);
            // Pines on a new resort's base are gone for good, as if the resort had always stood there.
            if (resorts > 0)
                Scenery.Sync();
            Build = new BuildService(w, Network, b, Cities, Events, Scenery);
            Stations = new StationService(w, Network, b, Cities, Events, Build);
            Cargo = new CargoService(w, b, Cities, Events);
            Trains = new TrainSimulation(w, Network, Pathfinder, b, Cargo, Events);
            Fixer = new RailFixPlanner(w, Network, Pathfinder, Build, Trains, b, Stations, Cities, Scenery);
            Upgrades = new StationUpgradeService(w, Network, b, Cities, Build, Scenery, Events);
            Repairs = new RailRepairPlanner(w, Network, Build, b, Cities);
            Trams = new TramService(w, Network, b, Cities, Scenery, Events);
            AssignMissingTrainNumbers(w);
            if (w.cityLayout < CityLayout.Current)
                Cities.Relayout();
            if (w.roadLayout < CitySimulation.RoadLayout)
                Cities.StraightenRoads();
        }
        /// <summary>Saves written before fleet numbers existed carry number 0; give those trains stable numbers once.</summary>
        static void AssignMissingTrainNumbers(WorldState w)
        {
            foreach (var t in w.trains)
                if (t.number <= 0)
                    t.number = w.nextTrainNumber++;
        }
        public void Enqueue(IGameCommand c) => commands.Enqueue(c);
        public void FlushCommands()
        {
            while (commands.Count > 0)
            {
                var result = commands.Dequeue().Execute(this);
                CommandCompleted?.Invoke(result);
            }
        }
        public void Step()
        {
            FlushCommands();
            World.tick++;
            Cargo.Step();
            Trains.Step();
            Trams.Step();
            Cities.Step();
            while (World.ledger.Count > 0 && World.ledger[0].tick < World.tick - 1200)
                World.ledger.RemoveAt(0);
        }
    }
    public sealed class SimulationClock
    {
        public double PendingSeconds
        {
            get; private set;
        }
        public float Alpha => (float)Math.Min(1, PendingSeconds / .05);
        public int Advance(double realSeconds, int speed, Action step, int maxTicks = 32)
        {
            if (speed != 0 && speed != 1 && speed != 2 && speed != 4)
                throw new ArgumentOutOfRangeException(nameof(speed));
            if (speed == 0)
                return 0;
            PendingSeconds += Math.Max(0, realSeconds) * speed;
            int ticks = 0;
            while (PendingSeconds + 1e-10 >= .05 && ticks < maxTicks)
            {
                step();
                PendingSeconds -= .05;
                ticks++;
            }
            return ticks;
        }
        public void Reset() => PendingSeconds = 0;
    }
}
