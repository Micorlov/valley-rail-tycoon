using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>Light rail from the build tray: the AI plans a tram line to a stadium, it is built, and trams run on it.</summary>
    public class LightRailTests
    {
        const float Step = .05f;
        GameBootstrap app;
        int station;

        static int StadiumDef => System.Array.FindIndex(BuildingCatalog.Defaults, d => d.key == "stadium-bowl");
        Transform Safe => app.UI.transform.Find("Safe area");
        Transform Details => Safe.Find("Context/Scrollable details");

        /// <summary>A new game with a railway station at Oakridge and a stadium on the open ground to its east.</summary>
        IEnumerator Setup()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var game = app.Game;
            game.World.money = 500000;
            Assert.That(game.Build.CommitBuild(game.Build.Preview(new Cell(14, 46), new Cell(50, 46))).ok);
            var placed = game.Stations.Place(new Cell(48, 46), 5);
            Assert.That(placed.ok, placed.message);
            station = placed.id;
            var city = game.World.cities.Find(c => c.producerId == 5);
            city.buildings.Add(new BuildingState { cell = new Cell(53, 34), def = StadiumDef });
            CitySimulation.Recount(city, game.Cargo.Producer(5), game.Balance);
            game.Cities.Rebuild();
            game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
        }
        Button Find(string prefix)
        {
            foreach (var b in Details.GetComponentsInChildren<Button>(false))
                if (b.name.StartsWith(prefix))
                    return b;
            return null;
        }
        void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                app.Game.Step();
                app.World.Animate(Step, false);
            }
        }

        [UnityTest]
        public IEnumerator TheTrayPlansAndBuildsALineAndTramsRunOnIt()
        {
            yield return Setup();
            Safe.Find("Tools/BUILD").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Safe.Find("Tools/LIGHT RAIL").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(app.Mode, Is.EqualTo(ToolMode.LightRail));
            var venue = Find("OAKRIDGE STADIUM");
            Assert.That(venue, Is.Not.Null, "the list offers the stadium");
            Assert.That(Find("GRANITE RIDGE SKI RESORT"), Is.Not.Null, "and the ski resorts");
            venue.onClick.Invoke();
            yield return null;
            var build = Find("BUILD LIGHT RAIL");
            Assert.That(build, Is.Not.Null, "the AI plans a line and prices it");
            Assert.That(build.interactable, Is.True);
            int money = app.Game.World.money;
            build.onClick.Invoke();
            yield return null;
            Assert.That(app.Game.World.tramLines.Count, Is.EqualTo(1), app.Notice);
            var line = app.Game.World.tramLines[0];
            Assert.That(app.Game.World.money, Is.LessThan(money));
            Assert.That(Find("ADD TRAM"), Is.Not.Null, "the line's panel opens");
            // The loop is one smooth path: out on the right-hand track, back on the other, joined at the stops.
            var route = app.Game.Trams.Route(line);
            var previous = app.World.LightRailPoint(route, 0, out _);
            for (int p = 10; p < route.Loop; p += 10)
            {
                var point = app.World.LightRailPoint(route, p, out _);
                Assert.That(Vector3.Distance(point, previous), Is.LessThan(.06f), "no jump at loop position " + p);
                previous = point;
            }
            var outbound = app.World.LightRailPoint(route, route.Half / 2, out var forward);
            var back = app.World.LightRailPoint(route, route.Loop - route.Half / 2, out _);
            Assert.That(Vector3.Dot(Vector3.Cross(Vector3.up, forward), outbound - back), Is.GreaterThan(.3f), "trams keep to the right");
            // Trams run, never through the floor or off into NaN, and no physics components come with them.
            Run(600);
            // Figures are built from cubes whose colliders go at the end of the frame.
            yield return null;
            var trams = app.World.transform.Find("Light rail trams");
            Assert.That(trams, Is.Not.Null);
            int shown = 0;
            foreach (Transform t in trams)
                if ((t.name == "Tram cab" || t.name == "Tram middle") && t.gameObject.activeSelf)
                {
                    shown++;
                    Assert.That(float.IsNaN(t.position.x) || t.position.y < 0, Is.False, t.name);
                }
            Assert.That(shown, Is.EqualTo(3), "one tram of three sections runs");
            Assert.That(app.World.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
            Assert.That(app.World.transform.Find("Light rail").childCount, Is.GreaterThan(0), "track, wires and stops are drawn");
            bool blocks = false;
            foreach (var c in line.cells)
                blocks |= app.World.LightRailBlocks(c);
            Assert.That(blocks, Is.True, "street cars see where the tram is");
            // A tap on the track in browse mode opens the line; ADD TRAM adds one, REMOVE LINE takes it all away.
            app.ChooseTool(ToolMode.Browse);
            app.Select(line.cells[line.cells.Count / 2]);
            yield return null;
            var add = Find("ADD TRAM");
            Assert.That(add, Is.Not.Null, "tapping the track opens the line");
            add.onClick.Invoke();
            yield return null;
            Assert.That(line.trams.Count, Is.EqualTo(2), app.Notice);
            Run(400);
            Find("REMOVE LINE").onClick.Invoke();
            yield return null;
            Find("REMOVE LINE").onClick.Invoke();
            yield return null;
            Assert.That(app.Game.World.tramLines, Is.Empty);
            app.World.Refresh();
            yield return null;
            foreach (Transform t in app.World.transform.Find("Light rail trams"))
                Assert.That((t.name == "Tram cab" || t.name == "Tram middle") && t.gameObject.activeSelf, Is.False, "the trams went with the line");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowLightRail()
        {
            yield return Setup();
            var game = app.Game;
            var stadium = TramVenues.All(game.World).Find(v => v.kind == TramVenueKind.Stadium);
            var built = game.Trams.Build(game.Trams.Planner.Plan(stadium, station));
            Assert.That(built.ok, built.message);
            var line = game.Trams.Line(built.id);
            for (int i = 1; i < TramCatalog.TramsFor(line.cells.Count); i++)
                game.Trams.AddTram(line.id);
            // Sunvale gets a station and its beach, with a line down the beach road.
            var town = game.Cities.CityFor(14);
            var road = new IntercityRoadState { a = 14, b = Coast.Resort, park = Coast.ParkSteps };
            for (int x = 79; x <= Coast.EntranceX; x++)
                road.path.Add(new Cell(x, 28));
            road.built = road.path.Count;
            game.World.intercityRoads.Add(road);
            game.Cities.Rebuild();
            Assert.That(game.Build.CommitBuild(game.Build.Preview(new Cell(70, 31), new Cell(90, 31))).ok);
            int sunvale = game.Stations.Place(new Cell(80, 31), 14).id;
            var beach = TramVenues.All(game.World).Find(v => v.kind == TramVenueKind.Beach);
            var beachLine = game.Trams.Build(game.Trams.Planner.Plan(beach, sunvale));
            Assert.That(beachLine.ok, beachLine.message);
            game.Trams.AddTram(beachLine.id);
            game.Trams.AddTram(beachLine.id);
            game.World.cityRevision++;
            app.World.Refresh();
            var camera = app.Camera.view;
            app.Camera.enabled = false;
            Run(900);
            var s = game.Trains.Station(station);
            var first = line.cells[0];
            var mid = line.cells[line.cells.Count / 2];
            var last = line.cells[line.cells.Count - 1];
            yield return Shoot(camera, new Vector3(first.x, .3f, first.z), 2.2f, "Logs/tram-transfer.png");
            yield return Shoot(camera, new Vector3(mid.x, .3f, mid.z), 2.4f, "Logs/tram-open.png");
            yield return Shoot(camera, new Vector3(last.x, .3f, last.z), 2.6f, "Logs/tram-venue.png");
            yield return Shoot(camera, new Vector3((first.x + last.x) / 2f, .3f, (first.z + last.z) / 2f), 7f, "Logs/tram-wide.png");
            var bl = game.Trams.Line(beachLine.id);
            var street = bl.cells[bl.cells.Count / 2];
            yield return Shoot(camera, new Vector3(street.x, .3f, street.z), 2.4f, "Logs/tram-street.png");
            var beachEnd = bl.cells[bl.cells.Count - 1];
            yield return Shoot(camera, new Vector3(beachEnd.x, .3f, beachEnd.z), 3f, "Logs/tram-beach.png");
            // Close to a tram.
            var tram = app.World.transform.Find("Light rail trams/Tram middle");
            if (tram)
                yield return Shoot(camera, tram.position, 1.1f, "Logs/tram-close.png");
            app.Camera.enabled = true;
            Assert.That(File.Exists("Logs/tram-transfer.png"));
        }
        static IEnumerator Shoot(Camera camera, Vector3 target, float zoom, string file)
        {
            camera.orthographicSize = zoom;
            camera.transform.position = target - camera.transform.forward * 180;
            camera.aspect = 16f / 9;
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            for (int frame = 0; frame < 3; frame++)
                yield return null;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            File.WriteAllBytes(file, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(texture);
            Object.Destroy(image);
        }
    }
}
