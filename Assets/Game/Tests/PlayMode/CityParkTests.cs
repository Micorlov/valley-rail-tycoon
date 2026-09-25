using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>The three city parks: people stroll, jog and play, a band plays, ducks swim and boats row, all inside the hedge.</summary>
    public class CityParkTests
    {
        const float Step = .05f;
        static readonly string[] Keys = { "town-park", "city-park", "central-park" };
        static readonly int[] Crowds = { 18, 35, 50 };
        GameBootstrap app;
        Vector3 middle;

        /// <summary>A new game with the first town cleared down to the three parks side by side along one street, smallest first.</summary>
        IEnumerator BuildParks()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var city = app.Game.World.cities[0];
            var first = new Cell(city.center.x - 14, city.center.z - 9);
            city.buildings.Clear();
            city.roads.Clear();
            city.roads.Add(new RoadState { cell = city.center });
            int x = first.x;
            for (int i = 0; i < Keys.Length; i++)
            {
                int def = System.Array.FindIndex(BuildingCatalog.Defaults, d => d.key == Keys[i]);
                Assert.That(def, Is.GreaterThanOrEqualTo(0), Keys[i] + " is in the catalog");
                Assert.That(BuildingCatalog.Size(def), Is.EqualTo(i + ParkLayout.MinSize), Keys[i] + " has its own plan");
                Assert.That(ParkLayout.For(BuildingCatalog.Size(def)).Size, Is.EqualTo(BuildingCatalog.Size(def)));
                city.buildings.Add(new BuildingState { cell = new Cell(x, first.z), def = def });
                x += BuildingCatalog.Size(def) + 1;
            }
            for (int sx = first.x - 2; sx <= x + 1; sx++)
                city.roads.Add(new RoadState { cell = new Cell(sx, first.z - 1) });
            for (int z = first.z; z < first.z + ParkLayout.MaxSize; z++)
                city.roads.Add(new RoadState { cell = new Cell(first.x - 1, z) });
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
            // Look straight at the three parks, so none of them sleeps off the screen.
            middle = new Vector3((first.x + x - 2) / 2f, 0, first.z + 2);
            app.Camera.enabled = false;
            var camera = app.Camera.view;
            camera.orthographicSize = 12;
            camera.transform.position = middle - camera.transform.forward * 180;
        }
        CityLife Life => app.World.GetComponentInChildren<CityLife>(true);
        [TearDown]
        public void GiveBackTheCamera()
        {
            if (app)
                app.Camera.enabled = true;
        }

        [UnityTest]
        public IEnumerator EveryParkOpensFullOfPeople()
        {
            yield return BuildParks();
            var life = Life;
            Assert.That(life.Parks, Is.EqualTo(Keys.Length));
            for (int i = 0; i < life.Parks; i++)
            {
                var park = life.Park(i);
                var plan = ParkLayout.For(park.Size);
                Assert.That(park.Size, Is.EqualTo(i + ParkLayout.MinSize));
                Assert.That(park.People, Is.GreaterThanOrEqualTo(Crowds[i]), Keys[i] + " is busy");
                Assert.That(park.Joggers, Is.EqualTo(park.Size - 2), Keys[i] + ": joggers run laps");
                Assert.That(park.Dogs, Is.GreaterThanOrEqualTo(1), Keys[i] + ": someone walks a dog");
                Assert.That(park.Playing, Is.GreaterThanOrEqualTo(3), Keys[i] + ": children in the playground");
                Assert.That(park.Ducks, Is.EqualTo(plan.Ducks), Keys[i] + ": ducks on the pond");
                Assert.That(park.Boats, Is.EqualTo(plan.Boats), Keys[i] + ": boats on the lake");
                Assert.That(park.Musicians > 0, Is.EqualTo(plan.HasBandstand), Keys[i] + ": a band only where there is a bandstand");
                Assert.That(park.Listeners, Is.EqualTo(plan.Audience.Length), Keys[i] + ": the audience sits before the bandstand");
                Assert.That(park.Root.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
            }
            Assert.That(life.Park(2).People, Is.GreaterThan(life.Park(0).People), "the central park is busier than the town park");
            Assert.That(life.Park(2).Boats, Is.GreaterThanOrEqualTo(2), "rowing boats on the central park's lake");
        }

        [UnityTest]
        public IEnumerator WalkersKeepToThePathsAndDucksToTheWater()
        {
            yield return BuildParks();
            var life = Life;
            var start = new Dictionary<Transform, Vector3>();
            for (int i = 0; i < life.Parks; i++)
                foreach (Transform t in life.Park(i).Root)
                    start[t] = t.localPosition;
            var swings = new HashSet<float>();
            // Three game minutes in the parks.
            for (int step = 0; step < 3600; step++)
            {
                life.Animate(Step, 1, true);
                if (step % 20 != 0)
                    continue;
                for (int i = 0; i < life.Parks; i++)
                {
                    var park = life.Park(i);
                    var plan = ParkLayout.For(park.Size);
                    foreach (Transform t in park.Root)
                    {
                        var at = t.localPosition;
                        bool walker = t.name == "Park walker" || t.name == "Park child";
                        // Walkers turn round in the gateways; everything else stays inside the hedge.
                        float inside = walker ? plan.Half : plan.Half - plan.HedgeInset;
                        Assert.That(float.IsNaN(at.x) || float.IsNaN(at.z), Is.False, t.name);
                        Assert.That(Mathf.Abs(at.x) < inside && Mathf.Abs(at.z) < inside, $"{Keys[i]}: {t.name} at {at} stays inside the park");
                        if (walker)
                            Assert.That(OnPath(plan, at), $"{Keys[i]}: {t.name} at {at} keeps to the paths");
                        else if (t.name == "Duck" || t.name == "Rowing boat")
                            Assert.That(InWater(plan, at), $"{Keys[i]}: {t.name} at {at} stays on the water");
                        else if (t.name == "Swing" && i == 0)
                            swings.Add(Mathf.Round(t.localEulerAngles.x));
                    }
                }
            }
            for (int i = 0; i < life.Parks; i++)
            {
                int moved = 0, walkers = 0;
                foreach (Transform t in life.Park(i).Root)
                    if (t.name == "Park walker" || t.name == "Slide child" || t.name == "Duck")
                    {
                        walkers++;
                        if (Vector3.Distance(start[t], t.localPosition) > .05f)
                            moved++;
                    }
                Assert.That(moved, Is.GreaterThanOrEqualTo(walkers - 2), Keys[i] + ": walkers, sliders and ducks get about");
            }
            Assert.That(swings.Count, Is.GreaterThan(10), "the swings swing");
        }
        /// <summary>On the loop, on one of the straight paths, or on the plaza clear of the fountain basin.</summary>
        static bool OnPath(ParkLayout plan, Vector3 at)
        {
            float half = plan.PathWidth / 2 + .01f, x = Mathf.Abs(at.x), z = Mathf.Abs(at.z), r = new Vector2(at.x, at.z).magnitude;
            if (r < plan.Plaza + .02f)
                return r > plan.Basin + .01f;
            return x < half || z < half || (Mathf.Abs(x - plan.Loop) < half && z < plan.Loop + half) || (Mathf.Abs(z - plan.Loop) < half && x < plan.Loop + half);
        }
        static bool InWater(ParkLayout plan, Vector3 at)
        {
            var pond = plan.Pond;
            float dx = (at.x - pond.center.x) / (pond.width / 2), dz = (at.z - pond.center.y) / (pond.height / 2);
            return dx * dx + dz * dz < 1;
        }

        [UnityTest]
        public IEnumerator DemolishingAParkClearsItsLife()
        {
            yield return BuildParks();
            Assert.That(Life.Parks, Is.EqualTo(Keys.Length));
            app.Game.World.cities[0].buildings.RemoveAt(0);
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
            Assert.That(Life.Parks, Is.EqualTo(Keys.Length - 1));
        }

        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowParks()
        {
            yield return BuildParks();
            var life = Life;
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            for (int i = 0; i < 400; i++)
                life.Animate(Step, 1, true);
            yield return Shoot(life, camera, middle + Vector3.up * .2f, 6.5f, "Logs/park-all.png");
            string[] names = { "town", "city", "central" };
            float[] zooms = { 2f, 2.6f, 3.2f };
            for (int i = 0; i < life.Parks; i++)
                yield return Shoot(life, camera, life.Park(i).Root.TransformPoint(new Vector3(0, .2f, 0)), zooms[i], $"Logs/park-{names[i]}.png");
            var central = life.Park(2).Root;
            var plan = ParkLayout.For(5);
            yield return Shoot(life, camera, central.TransformPoint(new Vector3(plan.Playground.center.x, .2f, plan.Playground.center.y)), 1.1f, "Logs/park-playground.png");
            yield return Shoot(life, camera, central.TransformPoint(new Vector3(plan.Pond.center.x, .2f, plan.Pond.center.y)), 1.2f, "Logs/park-lake.png");
            yield return Shoot(life, camera, central.TransformPoint(plan.Bandstand + Vector3.up * .2f), 1f, "Logs/park-band.png");
            yield return Shoot(life, camera, central.TransformPoint(new Vector3(plan.Kiosk.center.x - .4f, .2f, plan.Kiosk.center.y - .4f)), 1f, "Logs/park-cafe.png");
            yield return Shoot(life, camera, middle, 12f, "Logs/park-town-zoom.png");
            camera.transform.rotation = Quaternion.Euler(35.264f, 225, 0);
            yield return Shoot(life, camera, middle + Vector3.up * .2f, 6.5f, "Logs/park-other-side.png");
            Assert.That(File.Exists("Logs/park-other-side.png"));
        }
        static IEnumerator Shoot(CityLife life, Camera camera, Vector3 target, float zoom, string file)
        {
            camera.orthographicSize = zoom;
            camera.transform.position = target - camera.transform.forward * 180;
            camera.aspect = 16f / 9;
            // One tiny step wakes the parks the camera now looks at.
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
