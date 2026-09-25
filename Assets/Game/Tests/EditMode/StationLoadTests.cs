using NUnit.Framework;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>Station load: how many people a platform shows, its colour band and the busiest-first ranking.</summary>
    public class StationLoadTests
    {
        const int TownStorage = 200;
        static WorldState World(int townWaiting, int mineStock)
        {
            var w = new WorldState();
            w.producers.Add(new ProducerState { id = 1, name = "Willowbrook", kind = ProducerKind.Town, inventory = townWaiting, storage = TownStorage });
            w.producers.Add(new ProducerState { id = 2, name = "Pit", kind = ProducerKind.Mine, inventory = mineStock });
            w.producers.Add(new ProducerState { id = 3, name = "Power", kind = ProducerKind.Plant });
            w.stations.Add(new StationState { id = 10, producerId = 1, name = "Willowbrook Station" });
            w.stations.Add(new StationState { id = 11, producerId = 1, name = "Willowbrook East" });
            w.stations.Add(new StationState { id = 12, producerId = 2, name = "Pit Station" });
            w.stations.Add(new StationState { id = 13, producerId = 3, name = "Power Station" });
            return w;
        }
        static StationLoad Load(WorldState w, int stationId) => StationLoad.Of(w, new Balance(), w.stations.Find(s => s.id == stationId));

        [TestCase(0, 0, LoadLevel.Empty)]
        [TestCase(1, 1, LoadLevel.Calm)]
        [TestCase(99, 12, LoadLevel.Calm)]
        [TestCase(100, 12, LoadLevel.Busy)]
        [TestCase(170, 21, LoadLevel.Crowded)]
        [TestCase(TownStorage, StationLoad.MaxFigures, LoadLevel.Crowded)]
        public void TownPlatformShowsPeopleInProportionToItsQueue(int waiting, int figures, LoadLevel level)
        {
            var load = Load(World(waiting, 0), 10);
            Assert.That(load.Passengers, Is.True);
            Assert.That(load.Figures, Is.EqualTo(figures));
            Assert.That(load.Level, Is.EqualTo(level));
        }
        [Test]
        public void AnOverfullQueueIsCappedAtAFullPlatform()
        {
            var load = Load(World(TownStorage * 3, 0), 10);
            Assert.That(load.Fill, Is.EqualTo(1f));
            Assert.That(load.Percent, Is.EqualTo(100));
            Assert.That(load.Figures, Is.EqualTo(StationLoad.MaxFigures));
        }
        [Test]
        public void FreightStationsReportStockButShowNoPeople()
        {
            var w = World(0, 150);
            var mine = Load(w, 12);
            Assert.That(mine.cargo, Is.EqualTo(Cargo.Coal));
            Assert.That(mine.capacity, Is.EqualTo(new Balance().storage));
            Assert.That(mine.Percent, Is.EqualTo(75));
            Assert.That(mine.Level, Is.EqualTo(LoadLevel.Busy));
            Assert.That(mine.Figures, Is.Zero);
            var plant = Load(w, 13);
            Assert.That(plant.cargo, Is.Null, "a power station only accepts deliveries");
            Assert.That(plant.Level, Is.EqualTo(LoadLevel.Empty));
        }
        [Test]
        public void BusiestStationsComeFirstAndSharedTownQueuesCountOnce()
        {
            var w = World(40, 180);
            var ranked = StationLoad.Busiest(w, new Balance());
            Assert.That(ranked.ConvertAll(s => s.id), Is.EqualTo(new[] { 12, 11, 10, 13 }), "mine at 90%, then the town at 20% in name order, then the plant");
            Assert.That(StationLoad.PassengersWaiting(w, new Balance()), Is.EqualTo(40), "two stations share one town queue");
        }
    }
}
