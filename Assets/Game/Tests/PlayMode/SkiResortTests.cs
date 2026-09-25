using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>The ski resorts on the snowy peaks: a running gondola, skiers on the pistes, and cars once a road reaches them.</summary>
    public class SkiResortTests
    {
        const float Step = .05f;
        GameBootstrap app;

        IEnumerator NewGame()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
        }
        SkiLife Life => app.World.GetComponentInChildren<SkiLife>(true);

        /// <summary>Lays Granite Ridge's road from the nearest street of Sunvale (a breadth-first route over open ground) and opens it.</summary>
        IntercityRoadState OpenRoad()
        {
            var game = app.Game;
            var resort = game.World.producers.Find(p => p.id == SkiResorts.FirstId);
            var town = game.Cities.CityFor(14);
            var streets = new HashSet<int>();
            foreach (var r in town.roads)
                streets.Add(r.cell.Key);
            var entrance = SkiResorts.Entrance(resort);
            var came = new Dictionary<int, int> { [entrance.Key] = -1 };
            var queue = new Queue<Cell>();
            queue.Enqueue(entrance);
            Cell end = entrance;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (streets.Contains(c.Key)) { end = c; break; }
                for (int d = 0; d < 4; d++)
                {
                    var n = c.Move(d);
                    if (came.ContainsKey(n.Key) || !MapDefinition.InBounds(n) || MapDefinition.Raised(n) || MapDefinition.Water(n) || MapDefinition.Blocked(n, game.World) ||
                        game.Cities.HasBuilding(n) || game.Network.At(n) != null || BuildService.StationFootprint(game.World, n))
                        continue;
                    came[n.Key] = c.Key;
                    queue.Enqueue(n);
                }
            }
            Assert.That(streets.Contains(end.Key), Is.True, "a route from Sunvale's streets to the resort");
            var road = new IntercityRoadState { a = town.producerId, b = resort.id };
            for (int key = end.Key; key >= 0; key = came[key])
                road.path.Add(Cell.FromKey(key));
            road.built = road.path.Count;
            game.World.intercityRoads.Add(road);
            game.Cities.Rebuild();
            SaveService.Validate(game.World, game.Balance);
            game.World.cityRevision++;
            app.World.Refresh();
            return road;
        }

        [UnityTest]
        public IEnumerator EverySnowyPeakHasAGondolaAndSkiers()
        {
            yield return NewGame();
            var life = Life;
            Assert.That(life.Resorts, Is.EqualTo(SkiResorts.Count), "one resort per snowy peak");
            Assert.That(app.World.transform.Find("Ski resorts"), Is.Not.Null);
            Assert.That(app.World.transform.Find("Ski resorts/Pistes Frostpeaks Ski Resort"), Is.Not.Null, "groomed pistes are drawn");
            var start = new List<Vector3>();
            for (int r = 0; r < life.Resorts; r++)
            {
                Assert.That(life.Cabins(r), Is.GreaterThanOrEqualTo(6));
                Assert.That(life.Skiers(r), Is.GreaterThanOrEqualTo(9));
                Assert.That(life.Open(r), Is.False, "no cars before a road reaches the resort");
                Assert.That(life.Layout(r).towers.Count, Is.GreaterThanOrEqualTo(1));
                start.Add(life.Cabin(r, 0).position);
            }
            Assert.That(life.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
            // Two minutes on the mountain: cabins circle, never touching the slope, and skiers finish runs.
            for (int i = 0; i < 2400; i++)
            {
                life.Animate(Step, 1, true);
                if (i % 20 != 0)
                    continue;
                for (int r = 0; r < life.Resorts; r++)
                    for (int k = 0; k < life.Cabins(r); k++)
                    {
                        var cabin = life.Cabin(r, k).position;
                        float bottom = cabin.y - SkiLayout.CabinDrop - .17f;
                        Assert.That(bottom, Is.GreaterThan(SkiLayout.Ground(cabin.x, cabin.z) + .05f), $"cabin {k} of resort {r} clears the slope at {cabin}");
                    }
            }
            for (int r = 0; r < life.Resorts; r++)
                Assert.That(Vector3.Distance(life.Cabin(r, 0).position, start[r]), Is.GreaterThan(.5f), "the gondola runs");
            Assert.That(life.Runs, Is.GreaterThanOrEqualTo(8), "skiers reach the bottom of the pistes");
            for (int r = 0; r < life.Resorts; r++)
            {
                Assert.That(life.Skiing(r) + life.Riding(r), Is.GreaterThanOrEqualTo(3), "skiers go round: down the piste, up the gondola");
                Assert.That(life.Skiers(r), Is.EqualTo(9), "lodge guests stay all day");
            }
            foreach (var t in life.GetComponentsInChildren<Transform>(true))
                Assert.That(float.IsNaN(t.position.x) || float.IsNaN(t.position.y) || float.IsNaN(t.position.z), Is.False, t.name);
            // Paused, nothing moves; zoomed out, the skiers hide.
            var still = life.Cabin(0, 0).position;
            life.Animate(Step, 0, true);
            Assert.That(life.Cabin(0, 0).position, Is.EqualTo(still));
            life.Animate(Step, 1, false);
            Assert.That(life.transform.GetChild(0).Find("Skiers").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator CarsParkAtTheResortOnceItsRoadIsOpen()
        {
            yield return NewGame();
            var life = Life;
            var road = OpenRoad();
            Assert.That(life.Open(0), Is.True, "Granite Ridge's road is open");
            Assert.That(life.Bays(0), Is.EqualTo(21), "two rows of bays with a gap for the lane from the gate");
            Assert.That(life.Cars(0), Is.GreaterThanOrEqualTo(2), "a resort opens with a few cars already parked");
            for (int r = 1; r < life.Resorts; r++)
                Assert.That(life.Open(r), Is.False);
            int skiers = life.Skiers(0);
            Assert.That(skiers, Is.GreaterThan(9), "the parked cars' skiers are on the mountain");
            // Five minutes: cars come up the road, park, their skiers ski and later drive home.
            for (int i = 0; i < 6000; i++)
            {
                life.Animate(Step, 1, true);
                Assert.That(life.Cars(0), Is.LessThanOrEqualTo(life.Bays(0)));
            }
            Assert.That(life.Arrivals, Is.GreaterThanOrEqualTo(3), "cars drive in from the road");
            Assert.That(life.Departures, Is.GreaterThanOrEqualTo(1), "cars leave once their skiers are back");
            Assert.That(life.ParkedCars(0), Is.GreaterThanOrEqualTo(1));
            // Any town change redraws the towns; the resort carries on with the same cars.
            int arrivals = life.Arrivals;
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(life.Resorts, Is.EqualTo(SkiResorts.Count));
            Assert.That(life.Arrivals, Is.EqualTo(arrivals));
            Assert.That(road.Complete, Is.True);
        }

        [UnityTest]
        public IEnumerator ATapOnTheLodgeOpensTheResortWindow()
        {
            yield return NewGame();
            var resort = app.Game.World.producers.Find(p => p.id == SkiResorts.FirstId);
            var layout = new SkiLayout(resort);
            app.Camera.focus = layout.center;
            app.Camera.zoom = 12;
            yield return null;
            yield return null;
            // Aim at the lodge's roof: the ray meets the ground behind the base, as a finger on the tilted view does.
            var view = app.Camera.view;
            var ray = view.ScreenPointToRay(view.WorldToScreenPoint(layout.At(-1.4f, 1.35f, 1.3f)));
            var ground = ray.GetPoint(-ray.origin.y / ray.direction.y);
            app.Tap(new Cell(Mathf.RoundToInt(ground.x), Mathf.RoundToInt(ground.z)), ray);
            yield return null;
            var body = app.UI.transform.Find("Safe area/Context/Scrollable details");
            var text = "";
            if (body && body.gameObject.activeInHierarchy)
                foreach (var t in body.GetComponentsInChildren<TMPro.TMP_Text>())
                    text += t.text + "\n";
            Assert.That(text, Does.Contain("SKI RESORT").And.Contain("TOURISTS").And.Contain("STATION").And.Contain("ROAD"));
            Assert.That(text, Does.Contain("build one within 3 cells"), "no station yet: the window says where to build one");
        }

        /// <summary>Renders the resorts for review: Logs/ski-*.png.</summary>
        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowSkiResort()
        {
            yield return NewGame();
            OpenRoad();
            var life = Life;
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            app.Camera.enabled = false;
            for (int i = 0; i < 1600; i++)
                life.Animate(Step, 1, true);
            for (int r = 0; r < life.Resorts; r++)
            {
                var l = life.Layout(r);
                var middle = Vector3.Lerp(l.valleyWheel, l.topWheel, .45f);
                string name = SkiResorts.Peaks[SkiResorts.PeakOf(l.resort.id)].name.Replace(" ", "").ToLowerInvariant();
                yield return Shoot(camera, middle, 9f, $"Logs/ski-{name}.png");
            }
            var granite = life.Layout(0);
            yield return Shoot(camera, granite.At(0, 0, .3f), 3.2f, "Logs/ski-base.png");
            yield return Shoot(camera, granite.At(0, -1.2f, .2f), 1.8f, "Logs/ski-carpark.png");
            yield return Shoot(camera, Vector3.Lerp(granite.valleyWheel, granite.topWheel, .5f), 3.2f, "Logs/ski-slope.png");
            yield return Shoot(camera, granite.topWheel, 2.6f, "Logs/ski-top.png");
            yield return Shoot(camera, Vector3.Lerp(granite.valleyWheel, granite.topWheel, .35f), 11.5f, "Logs/ski-phone-zoom.png");
            app.Camera.enabled = true;
            Assert.That(File.Exists("Logs/ski-base.png"));
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
