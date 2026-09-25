using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>A beach car park by the sea: cars come and go, people stroll on the sand and lie on sun loungers, waves roll in.</summary>
    public class BeachTests
    {
        const float Step = .05f;
        GameBootstrap app;
        IntercityRoadState road;

        /// <summary>A new game with Sunvale's beach road laid straight east to the coast and its car park at <paramref name="stage"/>.</summary>
        IEnumerator BuildBeach(int stage)
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var game = app.Game;
            var town = game.Cities.CityFor(14);
            Assert.That(town.name, Is.EqualTo("Sunvale"));
            int east = town.center.x;
            foreach (var r in town.roads)
                if (r.cell.z == town.center.z)
                    east = Mathf.Max(east, r.cell.x);
            road = new IntercityRoadState { a = town.producerId, b = Coast.Resort };
            for (int x = east; x <= Coast.EntranceX; x++)
                road.path.Add(new Cell(x, town.center.z));
            road.built = road.path.Count;
            road.park = stage;
            game.World.intercityRoads.Add(road);
            game.Cities.Rebuild();
            SaveService.Validate(game.World, game.Balance);
            game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
        }
        BeachLife Life => app.World.GetComponentInChildren<BeachLife>(true);

        [UnityTest]
        public IEnumerator AnOpenBeachHasCarsComingAndGoingAndPeopleOnTheSand()
        {
            yield return BuildBeach(Coast.ParkSteps);
            var life = Life;
            Assert.That(life.Beaches, Is.EqualTo(1));
            Assert.That(life.Bays(0), Is.EqualTo(14), "two rows of bays, with a gap where the road comes in");
            Assert.That(life.Cars(0), Is.GreaterThanOrEqualTo(2), "a beach opens with a few cars already parked");
            Assert.That(life.People(0), Is.GreaterThanOrEqualTo(15), "strollers, sunbathers, swimmers, the ice-cream seller and visitors");
            Assert.That(life.Loungers(0), Is.EqualTo(16), "four rows of two umbrellas, a sun lounger either side of each");
            Assert.That(life.LoungersTaken(0), Is.GreaterThanOrEqualTo(3), "some loungers already have sunbathers");
            Assert.That(life.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
            // Four minutes at the seaside.
            for (int i = 0; i < 4800; i++)
            {
                life.Animate(Step, 1, true);
                Assert.That(life.Cars(0), Is.LessThanOrEqualTo(life.Bays(0)));
            }
            Assert.That(life.Arrivals, Is.GreaterThanOrEqualTo(3), "cars drive in from the beach road");
            Assert.That(life.Departures, Is.GreaterThanOrEqualTo(1), "cars leave once their passengers are back");
            Assert.That(life.OnSand(0), Is.GreaterThanOrEqualTo(8), "people walk on the beach");
            Assert.That(life.LoungerVisits, Is.GreaterThanOrEqualTo(1), "visitors lie down on free sun loungers");
            Assert.That(life.LoungersTaken(0), Is.LessThanOrEqualTo(life.Loungers(0)));
            foreach (var t in life.GetComponentsInChildren<Transform>(true))
                Assert.That(float.IsNaN(t.position.x) || float.IsNaN(t.position.z), Is.False, t.name);
            // Any town change redraws the towns; the beach carries on with the same people and cars.
            int people = life.People(0), arrivals = life.Arrivals;
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(life.Beaches, Is.EqualTo(1));
            Assert.That(life.People(0), Is.EqualTo(people));
            Assert.That(life.Arrivals, Is.EqualTo(arrivals));
            // Zoomed out, the people hide.
            life.Animate(Step, 1, false);
            Transform crowd = null;
            foreach (Transform child in life.transform)
                if (child.name.StartsWith("Beach "))
                    crowd = child.Find("Beach people");
            Assert.That(crowd.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TheBeachOpensWhenItsCarParkIsFinished()
        {
            yield return BuildBeach(3);
            Assert.That(app.World.transform.Find("Ground/Shallows"), Is.Not.Null, "shallow water with waves runs along the east edge");
            Assert.That(app.World.transform.Find("Ground/Sea"), Is.Null, "no open sea beyond the shoreline");
            Assert.That(Life.Beaches, Is.EqualTo(0), "no visitors while the car park is being built");
            road.park = Coast.ParkSteps;
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(Life.Beaches, Is.EqualTo(1));
        }

        /// <summary>Renders the beach for review: Logs/beach-coast.png, beach-park.png, beach-sand.png, beach-building.png.</summary>
        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowBeach()
        {
            yield return BuildBeach(Coast.ParkSteps);
            var life = Life;
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            app.Camera.enabled = false;
            for (int i = 0; i < 1600; i++)
                life.Animate(Step, 1, true);
            var entrance = Coast.Entrance(road);
            var park = new Vector3(BeachLayout.Aisle + .8f, .2f, entrance.z + .5f);
            yield return Shoot(camera, park + new Vector3(-6, 0, 0), 9f, "Logs/beach-coast.png");
            yield return Shoot(camera, park, 2.6f, "Logs/beach-park.png");
            yield return Shoot(camera, park + new Vector3(1.6f, 0, 3f), 2.4f, "Logs/beach-sand.png");
            road.park = 2;
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return Shoot(camera, park, 2.6f, "Logs/beach-building.png");
            app.Camera.enabled = true;
            Assert.That(File.Exists("Logs/beach-park.png"));
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
