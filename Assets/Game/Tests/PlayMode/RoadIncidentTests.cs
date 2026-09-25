using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>Closer zoom, traffic lights and roundabouts, and highway breakdowns with a tow truck (CityTraffic).</summary>
    public class RoadIncidentTests
    {
        static GameBootstrap Boot()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }

        [UnityTest]
        public IEnumerator CameraZoomsInCloseEnoughForOneCar()
        {
            var app = Boot();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            Assert.That(CameraController.MinZoom, Is.LessThanOrEqualTo(1.5f), "all the way in, one car fills a good part of the screen");
            app.Camera.zoom = .1f;
            yield return null;
            Assert.That(app.Camera.zoom, Is.EqualTo(CameraController.MinZoom));
            Assert.That(app.Camera.view.orthographicSize, Is.EqualTo(CameraController.MinZoom));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TowTruckCarriesACarOnItsFlatbedUnderAmberBeacons()
        {
            var model = VehicleCatalog.TowTruck;
            Assert.That(VehicleCatalog.Models, Has.No.Member(model), "the tow truck only comes out for breakdowns");
            Assert.That(model.kind, Is.EqualTo(VehicleKind.Service));
            Assert.That(model.beacons.Select(b => b.beat).Distinct().OrderBy(b => b), Is.EqualTo(new[] { 0, 1 }), "two beacons take turns");
            Assert.That(model.beacons.All(b => b.flash.r > .9f && b.flash.g > .5f && b.flash.b < .3f), Is.True, "amber, not police blue");
            for (int livery = 0; livery < model.liveries.Length; livery++)
            {
                var parts = model.Parts(livery);
                Vector3 min = Vector3.one * 9, max = -Vector3.one * 9;
                foreach (var part in parts)
                {
                    min = Vector3.Min(min, part.center - part.size / 2);
                    max = Vector3.Max(max, part.center + part.size / 2);
                }
                Assert.That(min.y, Is.EqualTo(0).Within(.001f), "wheels on the road");
                Assert.That(max.z - min.z, Is.EqualTo(model.length).Within(.02f), "as long as the catalogue says");
                Assert.That((max.z + min.z) / 2, Is.EqualTo(0).Within(.01f), "centred on its lane position");
                Assert.That(Mathf.Max(-min.x, max.x), Is.LessThan(.13f), "fits its lane");
                Assert.That(max.y, Is.LessThan(.55f));
                var bed = parts.Where(p => Mathf.Abs(p.center.y + p.size.y / 2 - VehicleCatalog.TowBedTop) < .002f).OrderByDescending(p => p.size.x).First();
                // Every car and van that can break down fits between the cab and the tail of the bed.
                foreach (var carried in VehicleCatalog.Models.Where(m => (m.kind == VehicleKind.Car || m.kind == VehicleKind.Truck) && m.length <= .42f))
                {
                    Assert.That(VehicleCatalog.TowBedMiddle - carried.length / 2, Is.GreaterThanOrEqualTo(bed.center.z - bed.size.z / 2 - .01f), carried.name);
                    Assert.That(VehicleCatalog.TowBedMiddle + carried.length / 2, Is.LessThanOrEqualTo(bed.center.z + bed.size.z / 2 + .01f), carried.name);
                }
            }
        }

        [UnityTest]
        public IEnumerator TownJunctionsGetWorkingLightsAndRoundabouts()
        {
            var app = Boot();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var world = app.Game.World;
            var city = world.cities[0];
            var c = city.center;
            // A street grid of blocks around the plaza, as towns grow: streets every fourth column and third row.
            city.buildings.Clear();
            var streets = new HashSet<Cell>();
            for (int x = c.x - 8; x <= c.x + 8; x++)
                for (int z = c.z - 6; z <= c.z + 6; z++)
                    if ((x - c.x) % 4 == 0 || (z - c.z) % 3 == 0)
                        streets.Add(new Cell(x, z));
            city.roads.Clear();
            foreach (var cell in streets.Where(MapDefinition.InBounds))
                city.roads.Add(new RoadState { cell = cell });
            world.cityRevision++;
            app.World.Refresh();
            yield return null;
            // The rules decide in advance which junctions get what.
            var lanes = RoadLanes.Build(world, app.Game.Network);
            var expected = new Dictionary<JunctionKind, int> { [JunctionKind.GiveWay] = 0, [JunctionKind.Lights] = 0, [JunctionKind.Roundabout] = 0 };
            foreach (var cell in streets)
                if (Directions.Count(lanes.Exits(cell)) >= 3 && !MapDefinition.Water(cell))
                    expected[JunctionRules.Kind(cell, lanes.Exits(cell), true, cell.Equals(c))]++;
            Assert.That(expected[JunctionKind.Lights], Is.GreaterThan(0), "the grid has traffic lights");
            Assert.That(expected[JunctionKind.Roundabout], Is.GreaterThan(0), "the grid has a roundabout");
            var junctions = app.World.transform.Find("Road junctions");
            Assert.That(junctions, Is.Not.Null);
            var roundabouts = junctions.Cast<Transform>().Where(t => t.name == "Roundabout").ToList();
            var signals = junctions.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Traffic lights" && t.GetComponent<MeshRenderer>()).ToList();
            var lamps = junctions.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Signal lamp").ToList();
            Assert.That(roundabouts.Count, Is.EqualTo(expected[JunctionKind.Roundabout]));
            Assert.That(signals.Count, Is.EqualTo(expected[JunctionKind.Lights]));
            Assert.That(lamps.Count, Is.EqualTo(expected[JunctionKind.Lights] * 6), "red, amber and green for both roads");
            var traffic = app.World.transform.Find("City traffic");
            Assert.That(traffic.Cast<Transform>().All(t => VehicleCatalog.Models.Any(m => m.name == t.name)), Is.True, "the traffic's children are all vehicles");

            // Drive: cars go round the islands, never through them, and nothing collides.
            var lampsBefore = lamps.Select(r => r.enabled).ToList();
            int roundTrips = 0;
            world.speed = 4;
            for (int frame = 0; frame < 1500; frame++)
            {
                app.World.Animate(.05f, false);
                foreach (Transform vehicle in traffic)
                    foreach (var island in roundabouts)
                    {
                        var offset = vehicle.position - island.position;
                        float distance = new Vector2(offset.x, offset.z).magnitude;
                        if (distance < .45f)
                            roundTrips++;
                        Assert.That(distance, Is.GreaterThan(.2f), $"{vehicle.name} drives round the island, not over it");
                    }
                AssertNoCollisions(traffic);
            }
            Assert.That(roundTrips, Is.GreaterThan(0), "cars use the roundabouts");
            Assert.That(lamps.Select(r => r.enabled).ToList(), Is.Not.EqualTo(lampsBefore), "the lights change");
            Assert.That(lamps.Count(r => r.enabled), Is.EqualTo(signals.Count * 2), "one lamp lit for each road at each junction");

            app.SetSpeed(0);
            app.Camera.focus = roundabouts[0].position;
            app.Camera.zoom = 1.6f;
            yield return Capture(app, "Logs/junction-roundabout.png");
            app.Camera.focus = signals[0].position;
            app.Camera.zoom = 1.6f;
            yield return Capture(app, "Logs/junction-lights.png");
            app.Camera.focus = new Vector3(c.x, 0, c.z);
            app.Camera.zoom = 6;
            yield return Capture(app, "Logs/junction-town.png");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BrokenDownCarIsTowedAwayAndTheJamClears()
        {
            var app = Boot();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var world = app.Game.World;
            // An open highway between the two nearest towns, as GameplayTests builds one.
            CityState from = world.cities[0], to = null;
            foreach (var city in world.cities)
                if (city != from && (to == null || city.center.Distance(from.center) < to.center.Distance(from.center)))
                    to = city;
            int east = to.center.x > from.center.x ? 1 : -1;
            var start = new Cell(from.center.x + east, from.center.z);
            var end = new Cell(to.center.x - east, to.center.z);
            var road = new IntercityRoadState { a = from.producerId, b = to.producerId };
            for (var cell = start; ; cell = cell.x != end.x ? new Cell(cell.x + east, cell.z) : new Cell(cell.x, cell.z + (end.z > cell.z ? 1 : -1)))
            {
                road.path.Add(cell);
                if (cell.Equals(end))
                    break;
            }
            road.built = road.path.Count;
            world.intercityRoads.Add(road);
            world.cityRevision++;
            app.World.Refresh();
            yield return null;
            var traffic = app.World.Traffic;
            var root = traffic.transform;
            bool OnRoad(Transform vehicle)
            {
                var cell = new Cell(Mathf.RoundToInt(vehicle.position.x), Mathf.RoundToInt(vehicle.position.z));
                return road.path.Contains(cell) || world.cities.Exists(c => c.roads.Exists(r => r.cell.Equals(cell)));
            }
            void Drive(int frames)
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    app.World.Animate(.05f, false);
                    foreach (Transform vehicle in root)
                        Assert.That(OnRoad(vehicle), Is.True, $"{vehicle.name} stays on the road");
                    AssertNoCollisions(root);
                }
            }

            // Sooner or later a car breaks down on the highway, hazards blinking.
            world.speed = 4;
            for (int tries = 0; tries < 3000 && !traffic.BrokenDown; tries++)
                Drive(1);
            var broken = traffic.BrokenDown;
            Assert.That(broken, Is.Not.Null, "a car breaks down on the open highway");
            Assert.That(broken.GetComponentsInChildren<Renderer>(true).Count(r => r.name == "Hazard light"), Is.EqualTo(4));
            Assert.That(broken.Find("Warning triangle"), Is.Not.Null);
            var stoppedAt = broken.position;
            var heading = broken.forward;
            app.Camera.focus = stoppedAt;
            app.Camera.zoom = 2.4f;
            yield return null;

            // The traffic behind queues up into a jam until the tow truck comes.
            int longestJam = 0;
            bool shot = false;
            for (int frame = 0; frame < 3000 && (!traffic.TowTruck || Vector3.Distance(traffic.TowTruck.position, stoppedAt) > 1.2f); frame++)
            {
                var before = root.Cast<Transform>().ToDictionary(t => t, t => t.position);
                Drive(1);
                Assert.That(broken.position, Is.EqualTo(stoppedAt), "the broken-down car does not move");
                int jam = root.Cast<Transform>().Count(t => t != broken && before.TryGetValue(t, out var was) && was == t.position && Behind(t.position));
                longestJam = Mathf.Max(longestJam, jam);
                if (!shot && jam >= 3)
                {
                    shot = true;
                    app.SetSpeed(0);
                    yield return Capture(app, "Logs/breakdown-jam.png");
                    world.speed = 4;
                }
                Assert.That(traffic.BrokenDown, Is.SameAs(broken), "it waits for the tow truck");
            }
            bool Behind(Vector3 p)
            {
                var offset = p - stoppedAt;
                float along = Vector3.Dot(offset, heading), across = Vector3.Dot(offset, Vector3.Cross(Vector3.up, heading));
                return along < -.2f && along > -4 && Mathf.Abs(across) < .15f;
            }
            Assert.That(longestJam, Is.GreaterThanOrEqualTo(2), "cars queue behind the broken-down car");
            var truck = traffic.TowTruck;
            Assert.That(truck, Is.Not.Null, "a tow truck comes up the hard shoulder");
            Assert.That(truck.name, Is.EqualTo("Tow truck"));
            app.SetSpeed(0);
            yield return Capture(app, "Logs/breakdown-tow-arrives.png");
            world.speed = 4;

            // It pulls in ahead, backs up and winches the car onto its bed.
            for (int frame = 0; frame < 2000 && broken.parent != truck; frame++)
                Drive(1);
            Assert.That(broken.parent, Is.SameAs(truck), "the car is hooked to the tow truck");
            for (int frame = 0; frame < 2000 && broken.localPosition.y < VehicleCatalog.TowBedTop * .5f; frame++)
                Drive(1);
            app.SetSpeed(0);
            yield return Capture(app, "Logs/breakdown-winch.png");
            world.speed = 4;
            for (int frame = 0; frame < 2000 && traffic.BrokenDown; frame++)
                Drive(1);
            Assert.That(traffic.BrokenDown, Is.Null, "the breakdown is over");
            yield return null;
            Assert.That(broken.parent, Is.SameAs(truck), "the car rides on the truck");
            Assert.That(Vector3.Distance(broken.localPosition, new Vector3(0, VehicleCatalog.TowBedTop, VehicleCatalog.TowBedMiddle)), Is.LessThan(.01f), "on the flatbed");
            Assert.That(broken.GetComponentsInChildren<Renderer>(true).Count(r => r.name == "Hazard light"), Is.Zero, "hazards off");

            // The truck drives off with it and the jam clears (still in view: out of sight it leaves the road).
            var queued = root.Cast<Transform>().Where(t => t != truck && Behind(t.position)).ToDictionary(t => t, t => t.position);
            var loadedAt = truck.position;
            Drive(8);
            Assert.That(Vector3.Distance(truck.position, loadedAt), Is.GreaterThan(.3f), "the tow truck drives away");
            Assert.That(queued.Keys.Any(t => t && Vector3.Distance(t.position, queued[t]) > .3f), Is.True, "the queue moves again");
            app.SetSpeed(0);
            app.Camera.focus = truck.position;
            yield return Capture(app, "Logs/breakdown-towed.png");
            LogAssert.NoUnexpectedReceived();
        }

        static void AssertNoCollisions(Transform traffic)
        {
            for (int i = 0; i < traffic.childCount; i++)
                for (int j = i + 1; j < traffic.childCount; j++)
                {
                    Vector3 a = traffic.GetChild(i).position, b = traffic.GetChild(j).position;
                    Assert.That(new Vector2(a.x - b.x, a.z - b.z).magnitude, Is.GreaterThan(.2f),
                        $"{traffic.GetChild(i).name} and {traffic.GetChild(j).name} never drive through each other");
                }
        }

        static IEnumerator Capture(GameBootstrap app, string filename)
        {
            var camera = app.Camera.view;
            camera.aspect = 16f / 9;
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            for (int frame = 0; frame < 30; frame++)
                yield return null;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(filename, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(texture);
            Object.Destroy(image);
        }
    }
}
