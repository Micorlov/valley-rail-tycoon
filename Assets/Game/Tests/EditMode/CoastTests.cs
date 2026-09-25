using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>The east coast: a sand beach, beach roads that towns build over time, and the car parks at their ends.</summary>
    public class CoastTests
    {
        const int Limit = 400000;

        [Test]
        public void TheEastEdgeIsASandBeach()
        {
            Assert.That(MapDefinition.SurfaceNames[MapDefinition.Surface(new Cell(MapDefinition.Size - 1, 30))], Is.EqualTo("Beach"));
            Assert.That(MapDefinition.Surface(new Cell(Coast.SandFrom, 90)), Is.EqualTo(8));
            Assert.That(MapDefinition.Surface(new Cell(Coast.SandFrom - 1, 30)), Is.EqualTo(2), "dry plains run right up to the sand");
            Assert.That(MapDefinition.Water(new Cell(MapDefinition.Size - 1, 30)), Is.False, "the sand is land: the sea lies past the map edge");
            var scenery = new GameSession(WorldState.New(new Balance()), new Balance()).Scenery;
            foreach (var tree in scenery.Trees)
                Assert.That(Coast.Beach(tree.cell), Is.False, "no pines on the sand");
        }

        [Test]
        public void TownsNearTheSeaBuildBeachRoadsThenCarParksOverTime()
        {
            var fast = new Balance();
            fast.city.basePoints = 400;
            // The map's own towns: villages founded near the coast would take the beach roads (CoreChecks covers those).
            fast.city.foundingEnabled = false;
            var game = new GameSession(WorldState.New(fast), fast);
            var notes = new List<int>();
            var stages = new Dictionary<int, int>();
            int ticks = 0;
            for (; ticks < Limit && game.World.intercityRoads.FindAll(Coast.Open).Count < 2; ticks++)
            {
                game.Step();
                while (game.Cities.Notifications.Count > 0)
                    notes.Add(game.Cities.Notifications.Dequeue().kind);
                foreach (var road in game.World.intercityRoads)
                {
                    if (!road.ToBeach)
                        continue;
                    int before = stages.TryGetValue(road.a, out int s) ? s : 0;
                    Assert.That(road.park == before || road.park == before + 1, Is.True, "a car park goes up one stage at a time");
                    Assert.That(road.park == 0 || road.Complete, Is.True, "the car park waits for its road");
                    stages[road.a] = road.park;
                }
            }
            var beaches = game.World.intercityRoads.FindAll(r => r.ToBeach);
            Assert.That(beaches.Count, Is.EqualTo(2), "Sunvale and Frostford are near enough to the sea; the western towns are not");
            int firstBeach = game.World.intercityRoads.FindIndex(r => r.ToBeach);
            Assert.That(game.World.intercityRoads.GetRange(0, firstBeach).TrueForAll(r => !r.ToBeach) && firstBeach >= 10, Is.True,
                "towns link up with highways first; the beach roads come after");
            foreach (var road in beaches)
            {
                var town = game.Cities.CityFor(road.a);
                Assert.That(town.level, Is.GreaterThanOrEqualTo(CityLevel.Village));
                Assert.That(town.roads.Exists(r => r.cell.Equals(road.path[0])), Is.True, town.name + "'s beach road leaves from a street");
                Assert.That(Coast.IsEntrance(Coast.Entrance(road)), Is.True, town.name + "'s beach road ends at the coast");
                Assert.That(Coast.Open(road), Is.True);
                for (int i = 0; i < Coast.ParkCells; i++)
                {
                    var cell = Coast.ParkCell(Coast.Entrance(road), i);
                    Assert.That(game.Build.Placeable(cell), Is.False, "tracks stay off the car park");
                    Assert.That(game.Cities.HasBuilding(cell), Is.False);
                }
                Assert.That(game.Cities.RoadStatus(town), Does.Contain("beach"));
            }
            Assert.That(notes.FindAll(k => k == Notification.BeachRoad).Count, Is.EqualTo(2), "news when a town starts its beach road");
            Assert.That(notes.FindAll(k => k == Notification.BeachOpened).Count, Is.EqualTo(2), "news when a beach opens");
            SaveService.Validate(game.World, fast);
            // A save keeps the beach roads and their car parks.
            var codec = new JsonSnapshotCodec();
            var loaded = codec.Decode<WorldState>(codec.Encode(game.World));
            SaveService.Validate(loaded, fast);
            Assert.That(loaded.intercityRoads.FindAll(Coast.Open).Count, Is.EqualTo(2));
            // Nothing further to build: the car parks stay finished.
            for (int i = 0; i < 2000; i++)
                game.Step();
            Assert.That(game.World.intercityRoads.FindAll(r => r.ToBeach).Count, Is.EqualTo(2));
            Assert.That(beaches.TrueForAll(r => r.park == Coast.ParkSteps), Is.True);
        }

        [Test]
        public void AnInvalidBeachIsRejected()
        {
            var game = new GameSession(WorldState.New(new Balance()), new Balance());
            var town = game.Cities.CityFor(14);
            var start = town.roads.FindAll(r => r.cell.z == town.center.z).ConvertAll(r => r.cell);
            start.Sort((a, b) => b.x.CompareTo(a.x));
            var road = new IntercityRoadState { a = town.producerId, b = Coast.Resort };
            for (int x = start[0].x; x <= Coast.EntranceX; x++)
                road.path.Add(new Cell(x, town.center.z));
            road.built = road.path.Count;
            road.park = Coast.ParkSteps;
            game.World.intercityRoads.Add(road);
            SaveService.Validate(game.World, game.Balance);
            road.park = Coast.ParkSteps + 1;
            Assert.Throws<InvalidDataException>(() => SaveService.Validate(game.World, game.Balance), "too many car park stages");
            road.park = 1;
            road.built--;
            Assert.Throws<InvalidDataException>(() => SaveService.Validate(game.World, game.Balance), "a car park before its road is finished");
            road.built++;
            road.path.RemoveAt(road.path.Count - 1);
            road.built--;
            road.park = 0;
            Assert.Throws<InvalidDataException>(() => SaveService.Validate(game.World, game.Balance), "a beach road must reach the coast");
        }
    }
}
