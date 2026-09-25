using System.Collections.Generic;
using NUnit.Framework;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>Stations on open ground, station sizes, and platforms that let separate railways share one station.</summary>
    public class StationSizeTests
    {
        GameSession game;
        [SetUp] public void Setup() => game = new GameSession(WorldState.New(new Balance()), new Balance());
        static int OK(Result r)
        {
            Assert.That(r.ok, Is.True, r.message);
            return r.id;
        }
        void Line(List<Cell> cells)
        {
            var plan = game.Build.ValidateBuild(cells);
            Assert.That(plan.valid, Is.True, plan.reason);
            OK(game.Build.CommitBuild(plan));
        }

        [Test]
        public void StationOnOpenGroundLaysItsOwnPlatformTrack()
        {
            int money = game.World.money, tracks = game.World.tracks.Count;
            var plan = game.Stations.Plan(new Cell(10, 15), 1, 3, 1);
            Assert.That(plan.valid, Is.True, plan.reason);
            Assert.That(plan.cost, Is.EqualTo(2300), "station $2,000 + three straights");
            var s = game.Trains.Station(OK(game.Stations.Place(plan, 1)));
            Assert.That(game.World.money, Is.EqualTo(money - 2300));
            Assert.That(game.World.tracks.Count, Is.EqualTo(tracks + 3));
            foreach (var c in StationLayout.Cells(s))
                Assert.That(game.Network.At(c).mask, Is.EqualTo(10), "straight platform track at " + c);
            var removed = game.Build.Bulldoze(new Cell(10, 15));
            Assert.That(removed.ok && removed.message.Contains("track remains"), Is.True, removed.message);
            Assert.That(game.World.tracks.Count, Is.EqualTo(tracks + 3), "the platform track stays as ordinary track");
        }

        [Test]
        public void BigStationCoversItsLengthAndPlatforms()
        {
            var plan = game.Stations.Plan(new Cell(10, 15), 1, 5, 2);
            Assert.That(plan.valid, Is.True, plan.reason);
            Assert.That(plan.cost, Is.EqualTo(StationLayout.Cost(game.Balance, 5, 2) + 1000), "ten new straights");
            var s = game.Trains.Station(OK(game.Stations.Place(plan, 1)));
            Assert.That(StationLayout.Cells(s).Count, Is.EqualTo(10));
            Assert.That(StationLayout.PlatformAt(s, new Cell(12, 14)), Is.EqualTo(1), "the second track lies away from the building");
            Assert.That(BuildService.StationFootprint(game.World, new Cell(8, 16)), Is.True, "the building strip is as long as the station");
            Assert.That(game.Build.Protected(game.Network.At(new Cell(8, 14)).id), Is.True, "every platform cell is protected");
            var saves = new SaveService(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ValleyRailStationSize-" + System.Guid.NewGuid()), new JsonSnapshotCodec(), game.Balance);
            var copy = saves.RestoreSnapshot(saves.CaptureSnapshot(game.World));
            Assert.That(copy.stations[0].length == 5 && copy.stations[0].platforms == 2, Is.True, "size survives a save");
        }

        [Test]
        public void RefusedStationsChangeNothing()
        {
            int money = game.World.money, tracks = game.World.tracks.Count;
            Assert.That(game.Stations.Plan(new Cell(20, 15), 1, 7, 1).valid, Is.False, "too long");
            Assert.That(game.Stations.Plan(new Cell(20, 15), 1, 3, 5).valid, Is.False, "too many platforms");
            Assert.That(game.Stations.Plan(new Cell(31, 20), 0, 3, 1).valid, Is.False, "water");
            Assert.That(game.Stations.Plan(new Cell(25, 20), 1, 3, 1).valid, Is.False, "nothing in reach");
            game.World.money = 500;
            Assert.That(game.Stations.Place(game.Stations.Plan(new Cell(10, 15), 1, 3, 1), 1).ok, Is.False, "unaffordable");
            Assert.That(game.World.tracks.Count, Is.EqualTo(tracks));
            Assert.That(game.World.stations, Is.Empty);
            Assert.That(game.World.money, Is.EqualTo(500));
            game.World.money = money;
        }

        [Test]
        public void TwoRailwaysShareATwoPlatformStation()
        {
            game.World.money = 200000;
            var hub = game.Stations.Plan(new Cell(13, 12), 0, 3, 2, 1);
            Assert.That(hub.valid, Is.True, hub.reason);
            int mine = OK(game.Stations.Place(hub, 1));
            int west = OK(game.Stations.Place(game.Stations.Plan(new Cell(17, 23), 0, 3, 1, 6), 6));
            // Platform 0 runs north to the bridge row and east to Eastbank Power; platform 1 loops west and north to Westvale.
            var east = new List<Cell>();
            for (int z = 13; z <= 15; z++)
                east.Add(new Cell(13, z));
            for (int x = 14; x <= 50; x++)
                east.Add(new Cell(x, 15));
            Line(east);
            int plant = OK(game.Stations.Place(new Cell(48, 15), 2));
            var westward = new List<Cell> { new Cell(12, 13), new Cell(12, 14) };
            for (int x = 11; x >= 8; x--)
                westward.Add(new Cell(x, 14));
            for (int z = 15; z <= 21; z++)
                westward.Add(new Cell(8, z));
            for (int x = 9; x <= 17; x++)
                westward.Add(new Cell(x, 21));
            westward.Add(new Cell(17, 22));
            Line(westward);
            int first = OK(game.Trains.Buy(mine, 0, Cargo.Coal)), second = OK(game.Trains.Buy(mine, 0, Cargo.Coal));
            Assert.That(game.Trains.Train(first).platform, Is.EqualTo(0));
            Assert.That(game.Trains.Train(second).platform, Is.EqualTo(1), "the second train takes the other platform's railway");
            Assert.That(game.Trains.Buy(mine, 0, Cargo.Coal).ok, Is.False, "no third train without a free platform");
            OK(game.Trains.AssignRoute(first, mine, plant));
            OK(game.Trains.AssignRoute(second, mine, west));
            bool eastReached = false, westReached = false;
            for (int i = 0; i < 4000; i++)
            {
                game.Step();
                eastReached |= game.Trains.Train(first).stationId == plant;
                westReached |= game.Trains.Train(second).stationId == west;
            }
            Assert.That(eastReached && westReached, Is.True, "both trains run from the shared station");
            Assert.That(game.World.delivered, Is.GreaterThan(0));
        }
    }
}
