using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>A filling station with a tyre shop and a garage beside an open highway: built in stages, then cars fill up.</summary>
    public class RoadsideTests
    {
        const float Step = .05f;
        GameBootstrap app;
        IntercityRoadState road;

        /// <summary>A new game with an open highway running straight east from Oakridge, left to plan its service area.</summary>
        IEnumerator BuildHighway()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var game = app.Game;
            var town = game.Cities.CityFor(5);
            Assert.That(town.name, Is.EqualTo("Oakridge"));
            int east = town.center.x;
            foreach (var r in town.roads)
                if (r.cell.z == town.center.z)
                    east = Mathf.Max(east, r.cell.x);
            road = new IntercityRoadState { a = town.producerId, b = 14 };
            for (int x = east; x <= east + 20; x++)
                road.path.Add(new Cell(x, town.center.z));
            road.built = road.path.Count;
            game.World.intercityRoads.Add(road);
            game.Cities.Rebuild();
            game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
        }
        /// <summary>Runs the simulation until the service area reaches <paramref name="stage"/>, then redraws.</summary>
        void RunUntil(int stage)
        {
            for (int i = 0; i < 2000 && road.service < stage; i++)
                app.Game.Step();
            Assert.That(road.service, Is.EqualTo(stage), "the service area goes up a stage every 200 ticks");
            app.World.Refresh();
        }
        RoadsideLife Life => app.World.GetComponentInChildren<RoadsideLife>(true);

        [UnityTest]
        public IEnumerator AServiceAreaIsBuiltBesideTheHighwayAndCarsFillUp()
        {
            yield return BuildHighway();
            RunUntil(1);
            Assert.That(Roadside.Shaped(road) && road.serviceAt > 2 && road.serviceAt < road.path.Count - 3, "planned beside a straight stretch near the middle");
            for (int i = 0; i < Roadside.Cells; i++)
                Assert.That(app.Game.Build.Placeable(Roadside.SiteCell(road, i)), Is.False, "track may not cross the service area");
            for (int stage = 2; stage < Roadside.Steps; stage++)
            {
                RunUntil(stage);
                Assert.That(Life.Sites, Is.EqualTo(0), "no customers while it is being built");
            }
            RunUntil(Roadside.Steps);
            var life = Life;
            Assert.That(life.Sites, Is.EqualTo(1));
            Assert.That(life.Cars(0), Is.GreaterThanOrEqualTo(2), "it opens with cars already at the pumps");
            yield return null;
            Assert.That(life.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
            // Three minutes of trade.
            float lowest = 1, highest = 0;
            for (int i = 0; i < 3600; i++)
            {
                life.Animate(Step, 1, true);
                Assert.That(life.Fuelling(0), Is.LessThanOrEqualTo(4), "four pumps");
                lowest = Mathf.Min(lowest, life.LiftRaise(0));
                highest = Mathf.Max(highest, life.LiftRaise(0));
            }
            Assert.That(life.Arrivals, Is.GreaterThanOrEqualTo(4), "cars pull in from the highway");
            Assert.That(life.Departures, Is.GreaterThanOrEqualTo(3), "and drive back onto it");
            Assert.That(highest - lowest, Is.GreaterThan(.15f), "the garage lift goes up and down");
            foreach (var t in life.GetComponentsInChildren<Transform>(true))
                Assert.That(float.IsNaN(t.position.x) || float.IsNaN(t.position.z), Is.False, t.name);
            // Any town change redraws the towns; the station carries on with the same cars.
            int arrivals = life.Arrivals, cars = life.Cars(0);
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(life.Sites, Is.EqualTo(1));
            Assert.That(life.Arrivals, Is.EqualTo(arrivals));
            Assert.That(life.Cars(0), Is.EqualTo(cars));
            // Every station style opens with trade of its own; the eco station's wind turbine turns.
            Assert.That(life.Kind(0), Is.EqualTo(0), "the first service area is a highway filling station");
            for (int kind = 1; kind < Roadside.Kinds; kind++)
            {
                road.serviceKind = kind;
                app.Game.World.cityRevision++;
                app.World.Refresh();
                Assert.That(life.Sites, Is.EqualTo(1));
                Assert.That(life.Kind(0), Is.EqualTo(kind));
                Assert.That(life.Cars(0), Is.GreaterThanOrEqualTo(2), Roadside.KindName(kind) + " opens with vehicles at the pumps");
                Assert.That(life.Rotor(0) != null, Is.EqualTo(kind == 3), "only the eco station has a wind turbine");
                var blades = life.Rotor(0) ? life.Rotor(0).localRotation : Quaternion.identity;
                int departed = life.Departures;
                for (int i = 0; i < 1200; i++)
                    life.Animate(Step, 1, true);
                Assert.That(life.Departures, Is.GreaterThan(departed), Roadside.KindName(kind) + " serves customers");
                // A few more steps, so the check never lands on a whole number of turns.
                for (int i = 0; i < 3; i++)
                    life.Animate(Step, 1, true);
                if (life.Rotor(0))
                    Assert.That(Quaternion.Angle(blades, life.Rotor(0).localRotation), Is.GreaterThan(1f), "the turbine turns");
            }
            yield return null;
            Assert.That(life.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics in any style");
            // Zoomed out, the people hide; nothing moves while paused.
            life.Animate(Step, 1, false);
            Transform people = null;
            foreach (Transform child in life.transform)
                if (child.name.StartsWith("Service area "))
                    people = child.Find("Service area people");
            Assert.That(people.gameObject.activeSelf, Is.False);
            var before = life.transform.GetChild(0).GetChild(life.transform.GetChild(0).childCount - 1).position;
            life.Animate(Step, 0, false);
            Assert.That(life.transform.GetChild(0).GetChild(life.transform.GetChild(0).childCount - 1).position, Is.EqualTo(before));
        }

        /// <summary>
        /// Renders a service area for review: Logs/roadside-kind-0..3.png (each station style), roadside-open.png (close up), roadside-back.png (the other way round),
        /// roadside-wide.png (with its highway) and roadside-stage-1..3.png (under construction).
        /// </summary>
        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowServiceArea()
        {
            yield return BuildHighway();
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            app.Camera.enabled = false;
            var f = default(RoadsideLayout);
            for (int stage = 1; stage < Roadside.Steps; stage++)
            {
                RunUntil(stage);
                f = new RoadsideLayout(road);
                yield return Shoot(camera, f.At(0, 1.5f, .2f), 2.4f, $"Logs/roadside-stage-{stage}.png");
            }
            RunUntil(Roadside.Steps);
            var life = Life;
            for (int i = 0; i < 700; i++)
                life.Animate(Step, 1, true);
            var centre = f.At(0, 1.4f, .2f);
            for (int kind = 0; kind < Roadside.Kinds; kind++)
            {
                road.serviceKind = kind;
                app.Game.World.cityRevision++;
                app.World.Refresh();
                for (int i = 0; i < 700; i++)
                    life.Animate(Step, 1, true);
                yield return Shoot(camera, centre, 2.1f, $"Logs/roadside-kind-{kind}.png");
            }
            road.serviceKind = 0;
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return Shoot(camera, centre, 2.1f, "Logs/roadside-open.png");
            yield return Shoot(camera, centre, 7f, "Logs/roadside-wide.png");
            var turn = camera.transform.rotation;
            camera.transform.rotation = Quaternion.Euler(0, 180, 0) * turn;
            yield return Shoot(camera, centre, 2.1f, "Logs/roadside-back.png");
            camera.transform.rotation = Quaternion.Euler(0, 90, 0) * turn;
            yield return Shoot(camera, centre, 2.1f, "Logs/roadside-side.png");
            camera.transform.rotation = turn;
            app.Camera.enabled = true;
            Assert.That(File.Exists("Logs/roadside-open.png"));
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
