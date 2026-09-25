using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>The city zoo: every pen has its animals and keeps them, visitors keep to the paths, and the zoo train shuttles between its platforms on its rails.</summary>
    public class ZooTests
    {
        const float Step = .05f;
        GameBootstrap app;
        Vector3 middle;

        /// <summary>A new game with the first town cleared down to one zoo beside one street.</summary>
        IEnumerator BuildZoo()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var city = app.Game.World.cities[0];
            int def = System.Array.FindIndex(BuildingCatalog.Defaults, d => d.key == "zoo");
            Assert.That(def, Is.GreaterThanOrEqualTo(0), "the zoo is in the catalog");
            Assert.That(BuildingCatalog.Size(def), Is.EqualTo(ZooLayout.Size), "the catalog's zoo matches its plan");
            Assert.That(BuildingCatalog.Get(def).unlock, Is.EqualTo(CityLevel.LargeCity), "big cities build zoos");
            var anchor = new Cell(city.center.x - 3, city.center.z - 10);
            city.buildings.Clear();
            city.roads.Clear();
            city.roads.Add(new RoadState { cell = city.center });
            city.buildings.Add(new BuildingState { cell = anchor, def = def });
            for (int x = anchor.x - 2; x <= anchor.x + ZooLayout.Size + 1; x++)
                city.roads.Add(new RoadState { cell = new Cell(x, anchor.z - 1) });
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
            middle = new Vector3(anchor.x + (ZooLayout.Size - 1) / 2f, 0, anchor.z + (ZooLayout.Size - 1) / 2f);
            app.Camera.enabled = false;
            var camera = app.Camera.view;
            camera.orthographicSize = 6;
            camera.transform.position = middle - camera.transform.forward * 180;
        }
        CityLife Life => app.World.GetComponentInChildren<CityLife>(true);
        [TearDown]
        public void GiveBackTheCamera()
        {
            if (app)
                app.Camera.enabled = true;
        }
        static Vector3 Local(ZooLife zoo, Transform t) => zoo.Root.InverseTransformPoint(t.position);

        [UnityTest]
        public IEnumerator TheZooOpensFullOfAnimalsAndVisitors()
        {
            yield return BuildZoo();
            var life = Life;
            Assert.That(life.Zoos, Is.EqualTo(1));
            var zoo = life.Zoo(0);
            Assert.That(zoo.Count(ZooLife.Species.Elephant), Is.EqualTo(3), "two elephants and a calf");
            Assert.That(zoo.Count(ZooLife.Species.Giraffe), Is.EqualTo(3));
            Assert.That(zoo.Count(ZooLife.Species.Zebra), Is.EqualTo(5));
            Assert.That(zoo.Count(ZooLife.Species.Lion), Is.EqualTo(5), "a pacing lion, three lionesses and a cub");
            Assert.That(zoo.Count(ZooLife.Species.Penguin), Is.EqualTo(9));
            Assert.That(zoo.Count(ZooLife.Species.Flamingo), Is.EqualTo(8));
            Assert.That(zoo.Count(ZooLife.Species.Monkey), Is.EqualTo(7));
            Assert.That(zoo.People, Is.GreaterThanOrEqualTo(45), "the zoo is busy");
            Assert.That(zoo.Walkers, Is.GreaterThanOrEqualTo(15));
            Assert.That(zoo.Keepers, Is.EqualTo(2));
            Assert.That(zoo.Riders, Is.GreaterThanOrEqualTo(2), "people ride the train");
            Assert.That(zoo.TrainCars.Length, Is.EqualTo(4), "loco, two coaches, loco");
            Assert.That(zoo.Root.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
            foreach (var r in zoo.Residents)
                Assert.That(Inside(ZooLayout.Plan.Pen(r.pen), Local(zoo, r.view)), $"{r.view.name} starts in the {r.pen} pen");
        }

        [UnityTest]
        public IEnumerator AnimalsStayPenned_VisitorsKeepToThePaths_TheTrainRunsBothWays()
        {
            yield return BuildZoo();
            var zoo = Life.Zoo(0);
            var plan = ZooLayout.Plan;
            var start = new Dictionary<Transform, Vector3>();
            foreach (var r in zoo.Residents)
                start[r.view] = Local(zoo, r.view);
            float westmost = float.MaxValue, eastmost = float.MinValue;
            // Four game minutes at the zoo.
            for (int step = 0; step < 4800; step++)
            {
                Life.Animate(Step, 1, true);
                westmost = Mathf.Min(westmost, zoo.TrainAt);
                eastmost = Mathf.Max(eastmost, zoo.TrainAt);
                if (step % 20 != 0)
                    continue;
                foreach (var r in zoo.Residents)
                {
                    var at = Local(zoo, r.view);
                    Assert.That(float.IsNaN(at.x) || float.IsNaN(at.z), Is.False, r.view.name);
                    Assert.That(Inside(plan.Pen(r.pen), at), $"{r.view.name} at {at} stays in the {r.pen} pen");
                }
                foreach (var walker in zoo.Strollers)
                    Assert.That(plan.OnPath(walker.localPosition), $"visitor at {walker.localPosition} keeps to the paths");
                foreach (var car in zoo.TrainCars)
                    Assert.That(plan.FromTrack(car.localPosition), Is.LessThan(.03f), $"{car.name} at {car.localPosition} stays on its rails");
            }
            Assert.That(westmost, Is.EqualTo(plan.WestStop).Within(.01f), "the train pulls into the west platform");
            Assert.That(eastmost, Is.EqualTo(plan.EastStop).Within(.01f), "and the east one");
            Assert.That(zoo.TrainStops, Is.GreaterThanOrEqualTo(3), "and keeps shuttling");
            int roamers = 0, moved = 0;
            foreach (var r in zoo.Residents)
                if (r.species == ZooLife.Species.Elephant || r.species == ZooLife.Species.Giraffe || r.species == ZooLife.Species.Zebra)
                {
                    roamers++;
                    if (Vector3.Distance(start[r.view], Local(zoo, r.view)) > .1f)
                        moved++;
                }
            Assert.That(moved, Is.GreaterThanOrEqualTo(roamers - 2), "elephants, giraffes and zebras wander their pens");
        }

        [UnityTest]
        public IEnumerator DemolishingTheZooClearsItsLife()
        {
            yield return BuildZoo();
            Assert.That(Life.Zoos, Is.EqualTo(1));
            app.Game.World.cities[0].buildings.Clear();
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
            Assert.That(Life.Zoos, Is.EqualTo(0));
        }

        static bool Inside(Rect pen, Vector3 at) => at.x > pen.xMin && at.x < pen.xMax && at.z > pen.yMin && at.z < pen.yMax;

        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowZoo()
        {
            yield return BuildZoo();
            var life = Life;
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            for (int i = 0; i < 600; i++)
                life.Animate(Step, 1, true);
            var zoo = life.Zoo(0).Root;
            var plan = ZooLayout.Plan;
            Vector3 At(float x, float z) => zoo.TransformPoint(new Vector3(x, .15f, z));
            yield return Shoot(life, camera, At(0, 0), 4.2f, "Logs/zoo-all.png");
            yield return Shoot(life, camera, At(0, 2.2f), 1.6f, "Logs/zoo-gate.png");
            yield return Shoot(life, camera, At(-1.75f, 1.75f), 1f, "Logs/zoo-penguins.png");
            yield return Shoot(life, camera, At(1.75f, 1.75f), 1f, "Logs/zoo-flamingos.png");
            yield return Shoot(life, camera, At(-1.75f, 0), 1.1f, "Logs/zoo-elephants.png");
            yield return Shoot(life, camera, At(0, 0), 1.1f, "Logs/zoo-monkeys.png");
            yield return Shoot(life, camera, At(1.75f, 0), 1.1f, "Logs/zoo-lions.png");
            yield return Shoot(life, camera, At(0, -1.75f), 2f, "Logs/zoo-savanna.png");
            var loco = life.Zoo(0).TrainCars[1].position;
            yield return Shoot(life, camera, loco + Vector3.up * .1f, 1f, "Logs/zoo-train.png");
            yield return Shoot(life, camera, middle, 12f, "Logs/zoo-town-zoom.png");
            camera.transform.rotation = Quaternion.Euler(35.264f, 225, 0);
            yield return Shoot(life, camera, At(0, 0), 4.2f, "Logs/zoo-other-side.png");
            Assert.That(File.Exists("Logs/zoo-other-side.png"));
        }
        static IEnumerator Shoot(CityLife life, Camera camera, Vector3 target, float zoom, string file)
        {
            camera.orthographicSize = zoom;
            camera.transform.position = target - camera.transform.forward * 180;
            camera.aspect = 16f / 9;
            // One tiny step wakes the zoo the camera now looks at.
            life.Animate(.001f, 1, true);
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
