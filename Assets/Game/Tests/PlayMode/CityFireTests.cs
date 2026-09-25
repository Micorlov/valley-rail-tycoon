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
    /// <summary>
    /// Now and then a house catches fire: the nearest fire station's engine races there with its siren on, the crew
    /// puts the fire out (from the ladder at a tall building) and the engine drives home. The saved game never changes.
    /// </summary>
    public class CityFireTests
    {
        const float Step = .05f;
        GameBootstrap app;
        CityState city;
        BuildingState house, flats, station;
        Cell centre;

        /// <summary>A new game with the first town cleared to one street through its plaza: a fire station, a house and walk-up flats.</summary>
        IEnumerator BuildStreet()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            city = app.Game.World.cities[0];
            centre = city.center;
            city.buildings.Clear();
            city.roads.Clear();
            for (int x = centre.x - 7; x <= centre.x + 7; x++)
                city.roads.Add(new RoadState { cell = new Cell(x, centre.z) });
            int fireStation = System.Array.FindIndex(BuildingCatalog.Defaults, d => d.key == "fire-station");
            int walkUp = System.Array.FindIndex(BuildingCatalog.Defaults, d => d.key == "walk-up");
            station = new BuildingState { cell = new Cell(centre.x - 5, centre.z - 1), def = fireStation };
            house = new BuildingState { cell = new Cell(centre.x + 4, centre.z + 1), def = 0 };
            flats = new BuildingState { cell = new Cell(centre.x + 1, centre.z - 1), def = walkUp };
            city.buildings.Add(station);
            city.buildings.Add(house);
            city.buildings.Add(flats);
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EngineRacesToTheFireAndTheCrewPutsItOut()
        {
            yield return BuildStreet();
            var fires = app.World.Fires;
            var codec = new JsonSnapshotCodec();
            string before = codec.Encode(app.Game.World);
            Assert.That(fires.Ignite(city, house), Is.True);
            Assert.That(fires.Call.station.cell, Is.EqualTo(station.cell), "the town's own fire station answers");
            Assert.That(app.Notice, Does.Contain("Fire in " + city.name));
            var route = fires.Call.route;
            var kerb = new Vector3(house.cell.x, 0, house.cell.z - 1 + .4f);
            var seen = new HashSet<CityFires.Stage>();
            float peak = 0;
            int flames = 0, drops = 0, crew = 0;
            bool sirenRacing = false, sirenParked = false;
            for (int i = 0; i < 6000 && fires.Now != CityFires.Stage.None; i++)
            {
                fires.Animate(Step, 1);
                var now = fires.Now;
                seen.Add(now);
                peak = Mathf.Max(peak, fires.Heat);
                if (fires.Scene != null)
                {
                    flames = Mathf.Max(flames, fires.Scene.Burning);
                    drops = Mathf.Max(drops, fires.Scene.Droplets);
                    crew = Mathf.Max(crew, fires.Scene.Root.Cast<Transform>().Count(t => t.name == "Firefighter" && t.gameObject.activeSelf));
                }
                var sirens = app.World.Traffic.Emergency.Sirens;
                if (now == CityFires.Stage.Racing)
                {
                    sirenRacing |= sirens.Contains(fires.Engine.Body);
                    var p = fires.Engine.Body.localPosition;
                    var cell = new Cell(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
                    Assert.That(route.Contains(cell) || cell.Equals(station.cell) || cell.Equals(house.cell), Is.True, $"the engine at {p} keeps to its route");
                    Assert.That(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(centre.x, centre.z)), Is.GreaterThan(.28f), "the engine drives round the plaza fountain");
                }
                if (now == CityFires.Stage.Fighting)
                {
                    sirenParked |= sirens.Contains(fires.Engine.Body);
                    var p = fires.Engine.Body.localPosition;
                    Assert.That(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(kerb.x, kerb.z)), Is.LessThan(.02f), "the engine parks on the kerb by the house");
                }
            }
            Assert.That(fires.Now, Is.EqualTo(CityFires.Stage.None), "the fire is over within five game minutes");
            Assert.That(seen, Is.SupersetOf(new[] { CityFires.Stage.Alarm, CityFires.Stage.Racing, CityFires.Stage.Deploying, CityFires.Stage.Fighting, CityFires.Stage.MoppingUp, CityFires.Stage.Packing, CityFires.Stage.Returning }));
            Assert.That(peak, Is.GreaterThan(.8f), "the fire takes hold before the engine gets there");
            Assert.That(flames, Is.GreaterThanOrEqualTo(4), "flames in the windows and on the roof");
            Assert.That(crew, Is.EqualTo(4), "four firefighters");
            Assert.That(drops, Is.GreaterThan(12), "two hoses spray water");
            Assert.That(sirenRacing, Is.True, "the siren sounds on the way");
            Assert.That(sirenParked, Is.False, "and stops once it is there");
            Assert.That(app.Notice, Does.Contain("put out the fire"));
            yield return null;
            Assert.That(fires.transform.childCount, Is.Zero, "engine, crew, flames and smoke are all gone");
            Assert.That(app.World.Traffic.Emergency.Sirens.Count, Is.Zero);
            Assert.That(codec.Encode(app.Game.World), Is.EqualTo(before), "a fire never changes the saved game");
        }

        [UnityTest]
        public IEnumerator LadderReachesTheUpperFloors()
        {
            yield return BuildStreet();
            var fires = app.World.Fires;
            Assert.That(fires.Ignite(city, flats), Is.True);
            for (int i = 0; i < 3000 && fires.Now != CityFires.Stage.Fighting; i++)
                fires.Animate(Step, 1);
            for (int i = 0; i < 40; i++)
                fires.Animate(Step, 1);
            Assert.That(fires.Now, Is.EqualTo(CityFires.Stage.Fighting));
            var ladder = fires.Engine.Body.Find("Turntable ladder");
            Assert.That(Quaternion.Angle(ladder.localRotation, Quaternion.identity), Is.GreaterThan(30), "the ladder swings up");
            var tip = fires.Engine.Tip;
            float front = flats.cell.z + BuildingCatalog.Get(flats.def).depth / 200f;
            Assert.That(tip.y, Is.GreaterThan(1.2f), "to the top floor");
            Assert.That(tip.z - front, Is.InRange(.05f, .4f), "just off the front wall");
            Assert.That(fires.Scene.Root.Cast<Transform>().Any(t => t.name == "Firefighter" && Vector3.Distance(t.localPosition, tip) < .01f), Is.True, "a firefighter stands at the top");
            Assert.That(fires.Scene.Droplets, Is.GreaterThan(20), "three jets, one from the ladder");
            bool rested = false;
            for (int i = 0; i < 6000 && fires.Now != CityFires.Stage.None; i++)
            {
                fires.Animate(Step, 1);
                if (fires.Now == CityFires.Stage.Returning && !rested)
                {
                    rested = true;
                    Assert.That(Quaternion.Angle(ladder.localRotation, Quaternion.identity), Is.LessThan(1), "the ladder is down before the engine leaves");
                }
            }
            Assert.That(rested && fires.Now == CityFires.Stage.None, Is.True);
        }

        [UnityTest]
        public IEnumerator FiresAreRareAndOneAtATime()
        {
            yield return BuildStreet();
            var fires = app.World.Fires;
            Assert.That(fires.UntilNext, Is.InRange(CityFires.FirstFire * .5f, CityFires.FirstFire * 1.5f));
            int count = 0;
            float quiet = 0, shortestGap = float.MaxValue;
            // Two game hours in one-second steps.
            for (int t = 0; t < 7200; t++)
            {
                bool was = fires.Call != null;
                fires.Animate(1, 1);
                if (fires.Call != null && !was)
                {
                    count++;
                    if (count > 1)
                        shortestGap = Mathf.Min(shortestGap, quiet);
                }
                quiet = fires.Call != null ? 0 : quiet + 1;
            }
            Assert.That(count, Is.InRange(8, 20), "about one fire every ten game minutes");
            Assert.That(shortestGap, Is.GreaterThanOrEqualTo(CityFires.SoonestFire - 1), "never one straight after another");
        }

        [UnityTest]
        public IEnumerator BulldozedHouseTakesItsFireWithIt()
        {
            yield return BuildStreet();
            var fires = app.World.Fires;
            Assert.That(fires.Ignite(city, house), Is.True);
            Assert.That(fires.Ignite(city, flats), Is.False, "one fire at a time");
            for (int i = 0; i < 150; i++)
                fires.Animate(Step, 1);
            Assert.That(fires.Engine, Is.Not.Null);
            city.buildings.Remove(house);
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(fires.Call, Is.Null);
            yield return null;
            Assert.That(fires.transform.childCount, Is.Zero);
            Assert.That(app.World.Traffic.Emergency.Sirens.Count, Is.Zero);
            // Without a fire station nothing burns.
            city.buildings.Remove(station);
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(fires.Ignite(city, flats), Is.False);
            Assert.That(fires.Ignite(), Is.False);
        }

        /// <summary>Renders a fire for review: Logs/fire-{blaze,racing,fight,steam,uturn,ladder}.png.</summary>
        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowFire()
        {
            yield return BuildStreet();
            var fires = app.World.Fires;
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            app.Camera.enabled = false;
            var street = new Vector3(house.cell.x, .3f, house.cell.z - .5f);
            Assert.That(fires.Ignite(city, house), Is.True);
            Run(fires, 60);
            yield return Shoot(camera, street, 2.4f, "Logs/fire-blaze.png");
            while (fires.Now == CityFires.Stage.Alarm || fires.Engine.Remaining > 4)
                Run(fires, 1);
            yield return Shoot(camera, new Vector3(centre.x + 1, .3f, centre.z), 4.2f, "Logs/fire-racing.png");
            while (fires.Now != CityFires.Stage.Fighting)
                Run(fires, 1);
            Run(fires, 60);
            yield return Shoot(camera, street, 1.5f, "Logs/fire-fight.png");
            while (fires.Now != CityFires.Stage.MoppingUp)
                Run(fires, 1);
            yield return Shoot(camera, street, 2.4f, "Logs/fire-steam.png");
            while (fires.Now != CityFires.Stage.Returning)
                Run(fires, 1);
            Run(fires, 16);
            yield return Shoot(camera, street, 1.5f, "Logs/fire-uturn.png");
            while (fires.Now != CityFires.Stage.None)
                Run(fires, 1);
            Assert.That(fires.Ignite(city, flats), Is.True);
            while (fires.Now != CityFires.Stage.Fighting)
                Run(fires, 1);
            Run(fires, 60);
            yield return Shoot(camera, new Vector3(flats.cell.x, .6f, flats.cell.z + .5f), 2f, "Logs/fire-ladder.png");
            app.Camera.enabled = true;
            Assert.That(File.Exists("Logs/fire-ladder.png"));
        }
        static void Run(CityFires fires, int steps)
        {
            for (int i = 0; i < steps; i++)
                fires.Animate(Step, 1);
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
