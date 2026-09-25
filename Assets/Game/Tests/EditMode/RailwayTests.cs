using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    public class RailwayTests
    {
        GameSession game;
        [SetUp] public void Setup() => game = new GameSession(WorldState.New(new Balance()), new Balance());
        static int OK(Result r)
        {
            Assert.That(r.ok, Is.True, r.message);
            return r.id;
        }
        void Track(Cell a, Cell b)
        {
            var p = game.Build.Preview(a, b);
            Assert.That(p.valid, Is.True, p.reason);
            OK(game.Build.CommitBuild(p));
        }
        int Coal()
        {
            Track(new Cell(8, 15), new Cell(50, 15));
            int a = OK(game.Stations.Place(new Cell(10, 15), 1)), b = OK(game.Stations.Place(new Cell(48, 15), 2));
            int t = OK(game.Trains.Buy(a, 0, Cargo.Coal));
            OK(game.Trains.AssignRoute(t, a, b));
            return t;
        }
        [TestCase(7, 1)]
        [TestCase(11, 0)]
        [TestCase(13, 3)]
        [TestCase(14, 2)]
        public void TurnoutsPermitOnlyStemTransitions(int mask, int stem)
        {
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                    Assert.That(Directions.Allows(mask, a, b), Is.EqualTo(a != b && (mask & (1 << a)) != 0 && (mask & (1 << b)) != 0 && (a == stem || b == stem)));
        }
        [Test]
        public void DisconnectedPortsDoNotJoin()
        {
            game.World.tracks.Add(new TrackPieceState { id = 10, cell = new Cell(4, 4), mask = 5 });
            game.World.tracks.Add(new TrackPieceState { id = 11, cell = new Cell(5, 4), mask = 5 });
            game.Network.Rebuild(game.World);
            Assert.That(game.Network.components[10], Is.Not.EqualTo(game.Network.components[11]));
            Assert.That(game.Pathfinder.FindPath(new Cell(4, 4), new Cell(5, 4)), Is.Null);
        }
        [Test]
        public void FailedBuildDoesNotChargeOrMutate()
        {
            game.World.money = 1;
            var p = game.Build.Preview(new Cell(4, 4), new Cell(8, 4));
            Assert.That(p.valid, Is.False);
            Assert.That(game.Build.CommitBuild(p).ok, Is.False);
            Assert.That(game.World.money, Is.EqualTo(1));
            Assert.That(game.World.tracks, Is.Empty);
        }
        [Test]
        public void BridgeRemovalIsAtomicAndRefundsOnce()
        {
            Track(new Cell(28, 15), new Cell(34, 15));
            int money = game.World.money;
            OK(game.Build.Bulldoze(new Cell(31, 15)));
            Assert.That(game.World.money, Is.EqualTo(money + 750));
            for (int x = 30; x <= 32; x++)
                Assert.That(game.Network.At(new Cell(x, 15)), Is.Null);
        }
        [Test]
        public void IncompleteBridgeCannotBePlaced()
        {
            var p = game.Build.ValidateBuild(new List<Cell> { new Cell(29, 15), new Cell(30, 15) });
            Assert.That(p.valid, Is.False);
        }
        [Test]
        public void CoalEarnsMoneyAndActiveRouteCannotBeRemoved()
        {
            Coal();
            int money = game.World.money;
            Assert.That(game.Build.Bulldoze(new Cell(25, 15)).ok, Is.False);
            for (int i = 0; i < 1200; i++)
                game.Step();
            Assert.That(game.World.delivered, Is.GreaterThanOrEqualTo(30));
            Assert.That(game.World.money, Is.GreaterThan(money));
        }
        [Test]
        public void NetworkOnlyAllowsOneTrain()
        {
            Coal();
            Assert.That(game.Trains.Buy(game.World.stations[1].id, 0, Cargo.Coal).ok, Is.False);
        }
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void SpeedChangesSchedulingNotSimulation(int speed)
        {
            Coal();
            var codec = new JsonSnapshotCodec();
            var reference = new GameSession(codec.Decode<WorldState>(codec.Encode(game.World)), game.Balance);
            var clock = new SimulationClock();
            for (int i = 0; i < 600 / speed; i++)
                clock.Advance(1d / 30, speed, game.Step);
            for (int i = 0; i < 400; i++)
                reference.Step();
            Assert.That(codec.Encode(game.World), Is.EqualTo(codec.Encode(reference.World)));
        }
        [Test]
        public void PauseHasNoEconomicEffects()
        {
            Coal();
            var codec = new JsonSnapshotCodec();
            string before = codec.Encode(game.World);
            new SimulationClock().Advance(30, 0, game.Step);
            Assert.That(codec.Encode(game.World), Is.EqualTo(before));
        }
        [Test]
        public void SaveDuringTravelContinuesExactly()
        {
            Coal();
            for (int i = 0; i < 200; i++)
                game.Step();
            var save = new SaveService("unused", new JsonSnapshotCodec(), game.Balance);
            var restored = new GameSession(save.RestoreSnapshot(save.CaptureSnapshot(game.World)), game.Balance);
            for (int i = 0; i < 2400; i++)
            {
                game.Step();
                restored.Step();
            }
            Assert.That(save.CaptureSnapshot(game.World), Is.EqualTo(save.CaptureSnapshot(restored.World)));
        }
        [Test]
        public void SaveDuringDwellContinuesExactly()
        {
            Coal();
            for (int i = 0; i < 25; i++)
                game.Step();
            var save = new SaveService("unused", new JsonSnapshotCodec(), game.Balance);
            var restored = new GameSession(save.RestoreSnapshot(save.CaptureSnapshot(game.World)), game.Balance);
            for (int i = 0; i < 400; i++)
            {
                game.Step();
                restored.Step();
            }
            Assert.That(save.CaptureSnapshot(game.World), Is.EqualTo(save.CaptureSnapshot(restored.World)));
        }
        [Test]
        public void CorruptPrimaryRetainsUsableBackup()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ValleyRail-" + Guid.NewGuid());
            try
            {
                Coal();
                var save = new SaveService(dir, new JsonSnapshotCodec(), game.Balance);
                save.Save(game.World, false);
                long tick = game.World.tick;
                game.Step();
                save.Save(game.World, false);
                File.WriteAllText(Path.Combine(dir, "manual.json"), "broken");
                Assert.Catch(() => save.Load(false));
                Assert.That(save.Load(false, true).tick, Is.EqualTo(tick));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
        [Test]
        public void InvalidSchemaIsNotLoaded()
        {
            var save = new SaveService("unused", new JsonSnapshotCodec(), game.Balance);
            Assert.Throws<InvalidDataException>(() => save.RestoreSnapshot("{\"schemaVersion\":9}"));
        }
        [Test]
        public void InsolvencyCanRecoverBySelling()
        {
            int id = Coal();
            game.World.money = 0;
            game.Step();
            Assert.That(game.Trains.Train(id).state, Is.EqualTo(ServiceState.InsufficientFunds));
            OK(game.Trains.Sell(id));
            Assert.That(game.World.money, Is.EqualTo(4000));
        }
        [Test]
        public void SharedProducerDoesNotDuplicateInventory()
        {
            Track(new Cell(8, 15), new Cell(14, 15));
            int a = OK(game.Stations.Place(new Cell(10, 15), 1));
            Track(new Cell(8, 9), new Cell(14, 9));
            int b = OK(game.Stations.Place(new Cell(10, 9), 1));
            Assert.That(game.World.stations.Find(s => s.id == a).producerId, Is.EqualTo(game.World.stations.Find(s => s.id == b).producerId));
            for (int i = 0; i < 1200; i++)
                game.Step();
            Assert.That(game.Cargo.Producer(1).inventory, Is.EqualTo(80));
        }
        [Test]
        public void RouteCacheInvalidatesAfterDeletion()
        {
            Track(new Cell(4, 4), new Cell(10, 4));
            Assert.That(game.Pathfinder.FindPath(new Cell(4, 4), new Cell(10, 4)), Is.Not.Null);
            OK(game.Build.Bulldoze(new Cell(7, 4)));
            Assert.That(game.Pathfinder.FindPath(new Cell(4, 4), new Cell(10, 4)), Is.Null);
        }
        [Test]
        public void SavingOverCorruptPrimaryPreservesLastGoodBackup()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ValleyRail-" + Guid.NewGuid());
            try
            {
                Coal();
                var save = new SaveService(dir, new JsonSnapshotCodec(), game.Balance);
                save.Save(game.World, false);
                game.Step();
                save.Save(game.World, false);
                File.WriteAllText(Path.Combine(dir, "manual.json"), "corrupt");
                game.Step();
                save.Save(game.World, false);
                Assert.That(save.Load(false, true).tick, Is.EqualTo(0));
                Assert.That(save.Load(false).tick, Is.EqualTo(2));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
        [Test]
        public void SavingDoesNotOverwriteNewerSchema()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ValleyRail-" + Guid.NewGuid());
            try
            {
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "manual.json");
                File.WriteAllText(file, "{\"schemaVersion\":99}");
                var save = new SaveService(dir, new JsonSnapshotCodec(), game.Balance);
                Assert.Throws<NotSupportedException>(() => save.Save(game.World, false));
                Assert.That(File.ReadAllText(file), Is.EqualTo("{\"schemaVersion\":99}"));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
        [Test]
        public void NonAdjacentBuildCellsAreRejected()
        {
            Assert.That(game.Build.ValidateBuild(new List<Cell> { new Cell(4, 4), new Cell(6, 4) }).valid, Is.False);
        }
        [Test]
        public void SimulationSoakHasNoRecurringManagedAllocations()
        {
            Coal();
            for (int i = 0; i < 2400; i++)
                game.Step();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 36000; i++)
                game.Step();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
            SaveService.Validate(game.World, game.Balance);
        }
        [Test]
        public void CapitalSpendingIsNotARunningExpense()
        {
            Coal();
            EconomyService.Recent(game.World, out int income, out int expense);
            Assert.That(income, Is.EqualTo(0));
            Assert.That(expense, Is.EqualTo(0), "construction and purchases must not read as per-minute expenses");
            for (int i = 0; i < 1200; i++)
                game.Step();
            EconomyService.Recent(game.World, out income, out expense);
            Assert.That(expense, Is.EqualTo(game.Balance.runningCost[0]));
            Assert.That(income, Is.GreaterThan(0));
        }
        [Test]
        public void ReturnLegIsProtected()
        {
            int id = Coal();
            var t = game.Trains.Train(id);
            t.path = new List<RailStep>();
            Assert.That(game.Build.Bulldoze(new Cell(25, 15)).ok, Is.False);
            var branch = game.Build.Preview(new Cell(25, 15), new Cell(25, 18));
            Assert.That(branch.valid, Is.False);
            Assert.That(branch.reason, Does.StartWith("Park"));
        }
        [Test]
        public void FleetNumbersAreSequentialAndStable()
        {
            Track(new Cell(8, 15), new Cell(14, 15));
            int a = OK(game.Stations.Place(new Cell(10, 15), 1));
            Track(new Cell(8, 9), new Cell(14, 9));
            int b = OK(game.Stations.Place(new Cell(10, 9), 1));
            int first = OK(game.Trains.Buy(a, 0, Cargo.Coal)), second = OK(game.Trains.Buy(b, 0, Cargo.Coal));
            Assert.That(game.Trains.Train(first).number, Is.EqualTo(1));
            Assert.That(game.Trains.Train(second).number, Is.EqualTo(2));
            OK(game.Trains.Sell(first));
            Assert.That(game.Trains.Train(second).number, Is.EqualTo(2));
            var legacy = WorldState.New(new Balance());
            legacy.trains.Add(new TrainState { id = 40 });
            Assert.That(new GameSession(legacy, new Balance()).Trains.Train(40).number, Is.EqualTo(1));
        }
        [Test]
        public void TutorialFollowsWorldStateAndEndsAfterFirstDeliveries()
        {
            Assert.That(game.World.tutorialStep, Is.EqualTo(1));
            Assert.That(Tutorial.Text(game.World), Does.StartWith("1/5"));
            Coal();
            Tutorial.Advance(game.World);
            Assert.That(game.World.tutorialStep, Is.EqualTo(5), "steps already done are skipped");
            for (int i = 0; i < 1200; i++)
                game.Step();
            Tutorial.Advance(game.World);
            Assert.That(game.World.tutorialStep, Is.EqualTo(6));
            game.World.delivered = 100;
            Tutorial.Advance(game.World);
            Assert.That(game.World.tutorialStep, Is.EqualTo(0));
            Assert.That(Tutorial.Text(game.World), Is.Null);
        }
        [Test]
        public void InvalidStationPlacementsAreRejectedWithoutSideEffects()
        {
            Track(new Cell(8, 15), new Cell(50, 15));
            OK(game.Build.CommitBuild(game.Build.ValidateBuild(new List<Cell> { new Cell(6, 13), new Cell(7, 13), new Cell(7, 14), new Cell(7, 15) })));
            int money = game.World.money;
            Assert.That(game.Stations.Place(new Cell(31, 15), 2).ok, Is.False, "bridge");
            Assert.That(game.Stations.Place(new Cell(25, 15), 1).ok, Is.False, "outside catchment");
            Assert.That(game.Stations.Place(new Cell(9, 20), 1).ok, Is.False, "no track");
            Assert.That(game.Stations.Place(new Cell(7, 13), 1).ok, Is.False, "curve");
            Assert.That(game.World.money, Is.EqualTo(money));
            Assert.That(game.World.stations, Is.Empty);
        }
        [Test]
        public void InvalidRouteAssignmentsAreRejected()
        {
            game.World.money = 100000;
            Track(new Cell(8, 15), new Cell(50, 15));
            int a = OK(game.Stations.Place(new Cell(10, 15), 1)), b = OK(game.Stations.Place(new Cell(48, 15), 2));
            int coal = OK(game.Trains.Buy(a, 0, Cargo.Coal));
            Assert.That(game.Trains.AssignRoute(coal, a, a).ok, Is.False, "same stop");
            Track(new Cell(8, 9), new Cell(14, 9));
            int c = OK(game.Stations.Place(new Cell(10, 9), 1));
            Assert.That(game.Trains.AssignRoute(coal, a, c).ok, Is.False, "mine to mine");
            OK(game.Trains.AssignRoute(coal, a, b));
            Assert.That(game.Trains.AssignRoute(coal, a, b).ok, Is.False, "change while loading");
            Track(new Cell(14, 46), new Cell(20, 46));
            Track(new Cell(46, 46), new Cell(50, 46));
            int p = OK(game.Stations.Place(new Cell(16, 46), 4)), q = OK(game.Stations.Place(new Cell(48, 46), 5));
            int passenger = OK(game.Trains.Buy(p, 2, Cargo.Passengers));
            Assert.That(game.Trains.AssignRoute(passenger, p, q).ok, Is.False, "unreachable stop");
        }
        [Test]
        public void CargoIsConservedAndPayoutIgnoresTrackLength()
        {
            Coal();
            for (int i = 0; i < 1200 && game.World.delivered == 0; i++)
                game.Step();
            Assert.That(game.World.delivered, Is.EqualTo(30));
            Assert.That(game.World.totalIncome, Is.EqualTo(30 * game.Balance.rate[0] * 38), "units × rate × Manhattan distance between producers");
            var fresh = new GameSession(WorldState.New(new Balance()), new Balance());
            game = fresh;
            Coal();
            for (int i = 0; i < 1200; i++)
                game.Step();
            Assert.That(game.Cargo.Producer(1).inventory + game.World.trains[0].units + game.World.delivered, Is.EqualTo(80), "60 initial plus 20 produced");
        }
        [Test]
        public void PassengerRouteIsProfitable()
        {
            game.World.money = 100000;
            Track(new Cell(14, 46), new Cell(50, 46));
            int a = OK(game.Stations.Place(new Cell(16, 46), 4)), b = OK(game.Stations.Place(new Cell(48, 46), 5));
            int t = OK(game.Trains.Buy(a, 2, Cargo.Passengers));
            OK(game.Trains.AssignRoute(t, a, b));
            int money = game.World.money;
            for (int i = 0; i < 2400; i++)
                game.Step();
            Assert.That(game.World.delivered, Is.GreaterThan(0));
            Assert.That(game.World.money, Is.GreaterThan(money));
        }
        [Test]
        public void ManualAndAutosaveSlotsAreIndependent()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ValleyRail-" + Guid.NewGuid());
            try
            {
                var save = new SaveService(dir, new JsonSnapshotCodec(), game.Balance);
                save.Save(game.World, false);
                game.Step();
                game.Step();
                save.Save(game.World, true);
                Assert.That(save.Load(false).tick, Is.EqualTo(0));
                Assert.That(save.Load(true).tick, Is.EqualTo(2));
                Assert.That(save.Exists(false, true), Is.False, "no backup until a slot is overwritten");
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
        [Test]
        public void SaveValidationRejectsStructuralCorruption()
        {
            Coal();
            for (int i = 0; i < 100; i++)
                game.Step();
            var codec = new JsonSnapshotCodec();
            bool Rejects(Action<WorldState> mutate)
            {
                var copy = codec.Decode<WorldState>(codec.Encode(game.World));
                mutate(copy);
                try
                {
                    SaveService.Validate(copy, game.Balance);
                    return false;
                }
                catch (InvalidDataException) { return true; }
            }
            Assert.That(Rejects(w => { }), Is.False, "untouched snapshot validates");
            Assert.That(Rejects(w => w.tracks[5].mask = 16), Is.True, "invalid port mask");
            Assert.That(Rejects(w => w.tracks.RemoveAll(t => t.cell.x == 31 && t.cell.z == 15)), Is.True, "incomplete bridge");
            Assert.That(Rejects(w => w.stations[0].cell = new Cell(25, 15)), Is.True, "station outside catchment");
            Assert.That(Rejects(w => { var extra = codec.Decode<TrainState>(codec.Encode(w.trains[0])); extra.id = w.nextId++; w.trains.Add(extra); }), Is.True, "two trains in one network");
            Assert.That(Rejects(w => w.trains[0].path[2].trackId = w.trains[0].path[4].trackId), Is.True, "disconnected route");
            Assert.That(Rejects(w => w.trains[0].units = 999), Is.True, "over-capacity cargo");
        }
        int Passengers()
        {
            game.World.money = 100000;
            Track(new Cell(14, 46), new Cell(50, 46));
            int a = OK(game.Stations.Place(new Cell(16, 46), 4)), b = OK(game.Stations.Place(new Cell(48, 46), 5));
            int t = OK(game.Trains.Buy(a, 2, Cargo.Passengers));
            OK(game.Trains.AssignRoute(t, a, b));
            return t;
        }
        [Test]
        public void TownsStartAsVillagesAndGrowWithPassengerService()
        {
            var willow = game.Cities.CityFor(4);
            Assert.That(willow.population, Is.EqualTo(420));
            Assert.That(willow.level, Is.EqualTo(CityLevel.SmallVillage));
            Assert.That(game.Cargo.Producer(4).production, Is.EqualTo(20), "day-0 towns keep the original 20 passengers per minute");
            Passengers();
            var idle = new GameSession(WorldState.New(new Balance()), new Balance());
            for (int i = 0; i < 12000; i++)
            {
                game.Step();
                idle.Step();
            }
            int grown = willow.buildings.Count + willow.roads.Count - 9, still = idle.Cities.CityFor(4).buildings.Count + idle.Cities.CityFor(4).roads.Count - 9;
            Assert.That(grown, Is.GreaterThanOrEqualTo(8));
            Assert.That(grown, Is.GreaterThanOrEqualTo(4 * Math.Max(1, still)), "service must outgrow isolation");
            Assert.That(willow.level, Is.GreaterThanOrEqualTo(CityLevel.Village));
            Assert.That(game.Cargo.Producer(4).production, Is.GreaterThan(20));
            Assert.That(game.Cities.Notifications.Count, Is.GreaterThan(0), "a level change notifies");
            SaveService.Validate(game.World, game.Balance);
        }
        [Test]
        public void CityGrowthReplaysExactlyFromASave()
        {
            Passengers();
            for (int i = 0; i < 6000; i++)
                game.Step();
            var save = new SaveService("unused", new JsonSnapshotCodec(), game.Balance);
            var restored = new GameSession(save.RestoreSnapshot(save.CaptureSnapshot(game.World)), game.Balance);
            for (int i = 0; i < 6000; i++)
            {
                game.Step();
                restored.Step();
            }
            Assert.That(save.CaptureSnapshot(game.World), Is.EqualTo(save.CaptureSnapshot(restored.World)));
            foreach (var city in game.World.cities)
                foreach (var bs in city.buildings)
                    Assert.That(game.Network.At(bs.cell), Is.Null, "buildings never sit on tracks");
        }
        [Test]
        public void LegacySaveWithoutCitiesMigratesToDayZeroTowns()
        {
            var codec = new JsonSnapshotCodec();
            var legacy = codec.Decode<WorldState>(codec.Encode(game.World));
            legacy.cities.Clear();
            legacy.producers.RemoveAll(p => p.id > 5);
            legacy.mapVersion = 1;
            foreach (var p in legacy.producers)
                p.production = p.storage = 0;
            string payload = codec.Encode(legacy);
            var save = new SaveService("unused", codec, game.Balance);
            var migrated = save.RestoreSnapshot(codec.Encode(new SaveEnvelope { payload = payload, checksum = SaveService.Hash(payload) }));
            Assert.That(migrated.mapVersion, Is.EqualTo(2));
            Assert.That(migrated.cities.Count, Is.EqualTo(2));
            Assert.That(migrated.cities[0].population, Is.EqualTo(420));
            Assert.That(migrated.producers[3].production, Is.EqualTo(20));
        }
        [Test]
        public void DemolishingAHouseCostsMoneyAndFreesTheCell()
        {
            var house = game.Cities.CityFor(4).buildings[0].cell;
            Assert.That(game.Build.ValidateBuild(new List<Cell> { house.Move(3), house, house.Move(1) }).valid, Is.False, "houses block track");
            int money = game.World.money;
            OK(game.Build.Bulldoze(house));
            Assert.That(game.World.money, Is.EqualTo(money - 500));
            Assert.That(game.Cities.CityFor(4).population, Is.EqualTo(380));
            Assert.That(game.Build.ValidateBuild(new List<Cell> { house, house.Move(0) }).valid, Is.True, "the cleared cell accepts track");
            for (int i = 0; i < 3600; i++)
                game.Step();
            Assert.That(game.Cities.HasBuilding(house), Is.False, "the town leaves a demolished cell alone for a while");
        }
        static bool Connected(CityState city)
        {
            var streets = new HashSet<int>();
            foreach (var r in city.roads)
                streets.Add(r.cell.Key);
            var reached = new HashSet<int> { city.center.Key };
            var queue = new Queue<Cell>();
            queue.Enqueue(city.center);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                    if (streets.Contains(c.Move(d).Key) && reached.Add(c.Move(d).Key))
                        queue.Enqueue(c.Move(d));
            }
            return reached.Count == streets.Count;
        }
        static bool FacesStreet(CityState city, BuildingState bs)
        {
            for (int i = 0; i < CityLayout.FootprintCells(bs); i++)
                for (int d = 0; d < 4; d++)
                {
                    var n = CityLayout.FootprintCell(bs, i).Move(d);
                    if (city.roads.Exists(r => r.cell.Equals(n)))
                        return true;
                }
            return false;
        }
        [Test]
        public void TownStreetsStayConnectedAndServicesUnlock()
        {
            var day0 = game.Cities.CityFor(4);
            Assert.That(day0.buildings.Count, Is.EqualTo(6), "five homes and the town hall");
            Assert.That(day0.roads.Count, Is.EqualTo(3), "the plaza on a main street");
            Passengers();
            for (int i = 0; i < 60000; i++)
                game.Step();
            foreach (var city in game.World.cities)
            {
                Assert.That(Connected(city), Is.True, city.name + " streets form one network around the plaza");
                foreach (var bs in city.buildings)
                    Assert.That(FacesStreet(city, bs), Is.True, city.name + " building faces a street");
            }
            var willow = game.Cities.CityFor(4);
            Assert.That(CitySimulation.CivicMask(willow) & 0b111, Is.EqualTo(0b111), "a Village has a town hall, chapel and school");
            foreach (var road in game.World.intercityRoads)
                Assert.That(game.Cities.CityFor(road.a).roads.Exists(r => r.cell.Equals(road.path[0])), Is.True, "highways leave from a town street");
            SaveService.Validate(game.World, game.Balance);
        }
        [Test]
        public void OldSavesWithoutTownLayoutAreRebuiltConnected()
        {
            var town = game.Cities.CityFor(4);
            var c0 = town.center;
            int[] ringX = { 0, 1, 0, -1, 1, 1, -1, -1 }, ringZ = { 1, 0, -1, 0, 1, -1, -1, 1 };
            town.buildings.Clear();
            town.roads.Clear();
            town.roads.Add(new RoadState { cell = c0 });
            for (int i = 0; i < 8; i++)
                town.buildings.Add(new BuildingState { cell = new Cell(c0.x + ringX[i], c0.z + ringZ[i]), def = i == 1 || i == 3 ? 1 : 0 });
            town.roads.Add(new RoadState { cell = new Cell(c0.x - 2, c0.z - 2) });
            CitySimulation.Recount(town, game.Cargo.Producer(4), game.Balance);
            var codec = new JsonSnapshotCodec();
            string payload = codec.Encode(game.World);
            string layoutField = "\"cityLayout\":" + CityLayout.Current + ",";
            Assert.That(payload, Does.Contain(layoutField));
            payload = payload.Replace(layoutField, ""); // written before the field existed
            var save = new SaveService("unused", codec, game.Balance);
            var rebuilt = new GameSession(save.RestoreSnapshot(codec.Encode(new SaveEnvelope { payload = payload, checksum = SaveService.Hash(payload) })), game.Balance);
            var fresh = rebuilt.Cities.CityFor(4);
            Assert.That(rebuilt.World.cityLayout, Is.EqualTo(CityLayout.Current));
            Assert.That(Connected(fresh), Is.True, "the rebuilt town is one street network");
            Assert.That(fresh.population, Is.GreaterThanOrEqualTo(420));
            SaveService.Validate(rebuilt.World, rebuilt.Balance);
        }
        [Test]
        public void BuildCommandsExecuteWithoutSubscribers()
        {
            game.Enqueue(new BuildCommand(game.Build.Preview(new Cell(4, 4), new Cell(8, 4))));
            game.FlushCommands();
            Assert.That(game.World.tracks.Count, Is.EqualTo(5));
        }
    }
}
