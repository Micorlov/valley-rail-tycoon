using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>The four open-air pools: cars come and go in their car parks, people swim, sunbathe, dive and slide.</summary>
    public class SwimmingPoolTests
    {
        const float Step = .05f;
        static readonly string[] Keys = { "splash-pool", "community-pool", "lido", "water-park" };
        GameBootstrap app;
        Cell first;

        /// <summary>A new game with the first town cleared down to the four pools side by side along one street, smallest first.</summary>
        IEnumerator BuildPools()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var city = app.Game.World.cities[0];
            first = new Cell(city.center.x - 14, city.center.z - 9);
            city.buildings.Clear();
            city.roads.Clear();
            city.roads.Add(new RoadState { cell = city.center });
            int x = first.x;
            for (int i = 0; i < Keys.Length; i++)
            {
                int def = System.Array.FindIndex(BuildingCatalog.Defaults, d => d.key == Keys[i]);
                Assert.That(def, Is.GreaterThanOrEqualTo(0), Keys[i] + " is in the catalog");
                Assert.That(BuildingCatalog.Size(def), Is.EqualTo(i + PoolLayout.MinSize), Keys[i] + " has its own plan");
                Assert.That(PoolLayout.For(BuildingCatalog.Size(def)).Size, Is.EqualTo(BuildingCatalog.Size(def)));
                city.buildings.Add(new BuildingState { cell = new Cell(x, first.z), def = def });
                x += BuildingCatalog.Size(def) + 1;
            }
            for (int sx = first.x - 2; sx <= x + 1; sx++)
                city.roads.Add(new RoadState { cell = new Cell(sx, first.z - 1) });
            for (int z = first.z; z < first.z + PoolLayout.MaxSize; z++)
                city.roads.Add(new RoadState { cell = new Cell(first.x - 1, z) });
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
        }
        PoolLife Life => app.World.GetComponentInChildren<PoolLife>(true);

        [UnityTest]
        public IEnumerator EveryPoolOpensWithSwimmersSunbathersAndParkedCars()
        {
            yield return BuildPools();
            var life = Life;
            Assert.That(life.Pools, Is.EqualTo(Keys.Length));
            for (int i = 0; i < life.Pools; i++)
            {
                var plan = PoolLayout.For(life.Size(i));
                Assert.That(life.Size(i), Is.EqualTo(i + PoolLayout.MinSize));
                int bays = plan.BaysPerRow - 1 + (plan.TwoRows ? plan.BaysPerRow - 2 : 0);
                Assert.That(life.Bays(i), Is.EqualTo(bays), $"{Keys[i]}: bays minus the zebra walkway (and the driveway on two rows)");
                Assert.That(life.ParkedCars(i), Is.GreaterThanOrEqualTo(2), Keys[i] + " opens with cars already parked");
                Assert.That(life.Swimmers(i), Is.GreaterThanOrEqualTo(2), Keys[i] + ": regulars swim lengths");
                Assert.That(life.Sunbathers(i), Is.GreaterThanOrEqualTo(1), Keys[i] + ": someone sunbathing");
                if (plan.HasPaddling)
                    Assert.That(life.Paddlers(i), Is.GreaterThanOrEqualTo(2), Keys[i] + ": children in the paddling pool");
            }
            Assert.That(life.People(3), Is.GreaterThan(life.People(0)), "the water park is busier than the splash pool");
            Assert.That(life.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
        }

        [UnityTest]
        public IEnumerator CarsComeAndGoWhileSwimmersStayInTheWater()
        {
            yield return BuildPools();
            var life = Life;
            int peak = 0;
            // Five minutes at the pools.
            for (int step = 0; step < 6000; step++)
            {
                life.Animate(Step, 1, true);
                if (step % 20 != 0)
                    continue;
                for (int i = 0; i < life.Pools; i++)
                {
                    var plan = PoolLayout.For(life.Size(i));
                    foreach (var swimmer in life.SwimmerSpots(i))
                    {
                        Assert.That(float.IsNaN(swimmer.x) || float.IsNaN(swimmer.z), Is.False);
                        bool inPool = Inside(plan.Pool, swimmer) || plan.HasSlide && Inside(plan.Leisure, swimmer);
                        Assert.That(inPool, $"{Keys[i]}: swimmer at {swimmer} stays in the water");
                        Assert.That(swimmer.y, Is.LessThan(0), "only a swimmer's head clears the water");
                    }
                    foreach (var car in life.ParkedSpots(i))
                    {
                        Assert.That(car.z, Is.InRange(plan.Fence, plan.Hedge), Keys[i] + ": parked cars stand in the car park");
                        Assert.That(Mathf.Abs(car.x), Is.LessThan(plan.Half));
                    }
                    peak = Mathf.Max(peak, life.People(i));
                }
            }
            Assert.That(life.Arrivals, Is.GreaterThanOrEqualTo(6), "cars turn in from the street");
            Assert.That(life.Departures, Is.GreaterThanOrEqualTo(2), "families drive home again");
            Assert.That(life.Dives, Is.GreaterThanOrEqualTo(3), "divers keep jumping off the boards");
            Assert.That(life.Slides, Is.GreaterThanOrEqualTo(3), "the water park's slide is in use");
            for (int i = 0; i < life.Pools; i++)
                Assert.That(life.Cars(i), Is.LessThanOrEqualTo(life.Bays(i)));
            Assert.That(peak, Is.LessThan(90), "the crowd stays a sensible size");
        }
        static bool Inside(Rect water, Vector3 at) =>
            at.x > water.xMin - .02f && at.x < water.xMax + .02f && at.z > water.yMin - .02f && at.z < water.yMax + .02f;

        [UnityTest]
        public IEnumerator DemolishingAPoolClearsItsLife()
        {
            yield return BuildPools();
            Assert.That(Life.Pools, Is.EqualTo(Keys.Length));
            app.Game.World.cities[0].buildings.RemoveAt(0);
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
            Assert.That(Life.Pools, Is.EqualTo(Keys.Length - 1));
        }

        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowPool()
        {
            yield return BuildPools();
            var life = Life;
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            app.Camera.enabled = false;
            for (int i = 0; i < 700; i++)
                life.Animate(Step, 1, true);
            var middle = (life.Root(1).position + life.Root(2).position) / 2 + Vector3.up * .2f;
            yield return Shoot(camera, middle, 6.5f, "Logs/pool-all.png");
            string[] names = { "splash", "community", "lido", "waterpark" };
            float[] zooms = { 1.6f, 2.3f, 3f, 3.6f };
            for (int i = 0; i < life.Pools; i++)
                yield return Shoot(camera, life.Root(i).TransformPoint(new Vector3(0, .2f, 0)), zooms[i], $"Logs/pool-{names[i]}.png");
            yield return Shoot(camera, life.Root(3).TransformPoint(new Vector3(1.4f, .4f, -1.4f)), 1.5f, "Logs/pool-slide.png");
            yield return Shoot(camera, middle, 14f, "Logs/pool-town.png");
            camera.transform.rotation = Quaternion.Euler(35.264f, 225, 0);
            yield return Shoot(camera, middle, 6.5f, "Logs/pool-other-side.png");
            app.Camera.enabled = true;
            Assert.That(File.Exists("Logs/pool-other-side.png"));
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
