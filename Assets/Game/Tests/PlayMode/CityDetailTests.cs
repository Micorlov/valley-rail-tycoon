using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>Close-up town details, town life and construction sites.</summary>
    public class CityDetailTests
    {
        static IEnumerator Launch(System.Action<GameBootstrap> ready)
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            yield return null;
            ready(app);
        }
        [UnityTest]
        public IEnumerator DetailsAndLifeShowOnlyWhenZoomedIn()
        {
            GameBootstrap app = null;
            yield return Launch(a => app = a);
            app.SetSpeed(1);
            var town = app.Game.World.cities[0];
            var details = app.World.transform.Find("City details");
            var life = app.World.GetComponentInChildren<CityLife>(true);
            app.Camera.zoom = 40;
            yield return null;
            yield return null;
            Assert.That(details.gameObject.activeSelf, Is.False, "far view hides details");
            Assert.That(life.gameObject.activeSelf, Is.False, "far view hides town life");
            app.Camera.focus = new Vector3(town.center.x, 0, town.center.z);
            app.Camera.zoom = 9;
            yield return null;
            yield return null;
            Assert.That(details.gameObject.activeSelf, Is.True, "close view shows details");
            Assert.That(details.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(3), "details are batched meshes");
            Assert.That(life.gameObject.activeSelf, Is.True, "close view shows town life");
            Assert.That(life.Walkers, Is.GreaterThan(0), "pedestrians walk the streets");
            Assert.That(life.Motions, Is.GreaterThanOrEqualTo(app.Game.World.cities.Count), "every plaza fountain and chimney animates");
            var walker = life.transform.Find("Pedestrian");
            var before = walker.localPosition;
            for (int i = 0; i < 20; i++)
                yield return null;
            Assert.That(walker.localPosition, Is.Not.EqualTo(before), "pedestrians move while the game runs");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator NewBuildingsRiseAsConstructionSites()
        {
            GameBootstrap app = null;
            yield return Launch(a => app = a);
            Assert.That(app.World.ConstructionSites, Is.EqualTo(0), "a new game shows its towns finished");
            app.SetSpeed(1);
            var town = app.Game.World.cities[0];
            town.buildings.Add(new BuildingState { cell = new Cell(town.center.x + 2, town.center.z + 1), def = BuildingCatalog.Villa });
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(app.World.ConstructionSites, Is.EqualTo(1), "the new villa starts as a construction site");
            Assert.That(app.World.transform.Find("Construction sites").childCount, Is.EqualTo(1));
            for (int i = 0; i < 20 && app.World.ConstructionSites > 0; i++)
                app.World.Animate(1f, false);
            Assert.That(app.World.ConstructionSites, Is.EqualTo(0), "the site finishes at game speed");
            app.SetSpeed(0);
            town.buildings.Add(new BuildingState { cell = new Cell(town.center.x - 2, town.center.z + 1), def = BuildingCatalog.Duplex });
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(app.World.ConstructionSites, Is.EqualTo(0), "while paused, changes show at once");
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator LargeCityStaysWithinTheMeshBudget()
        {
            GameBootstrap app = null;
            yield return Launch(a => app = a);
            app.SetSpeed(0);
            app.World.ConstructionEnabled = false;
            // Every town at 12,000 people: the heaviest map the game can draw.
            foreach (var city in app.Game.World.cities)
                city.population = 12000;
            app.Game.Cities.Relayout();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            app.Game.World.cityRevision++;
            app.World.Refresh();
            watch.Stop();
            long vertices = 0;
            foreach (var root in new[] { "Cities", "City details" })
                foreach (var filter in app.World.transform.Find(root).GetComponentsInChildren<MeshFilter>(true))
                    vertices += filter.sharedMesh.vertexCount;
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("Five 12,000-person towns"));
            Debug.Log($"Five 12,000-person towns: {vertices:N0} vertices, redraw {watch.ElapsedMilliseconds} ms");
            Assert.That(vertices, Is.LessThan(1_000_000), "city meshes stay within a mobile budget (about 560k when written)");
            yield return null;
        }
    }
}
