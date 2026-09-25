using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>AI UPGRADE STATION: the panel's pickers, the bulldozer show and the finished station building.</summary>
    public class StationUpgradeShowTests
    {
        static GameBootstrap App()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }
        static Transform Details(GameBootstrap app) => app.UI.transform.Find("Safe area/Context/Scrollable details");
        static Button Find(GameBootstrap app, string prefix)
        {
            foreach (var button in Details(app).GetComponentsInChildren<Button>())
                if (button.name.StartsWith(prefix))
                    return button;
            return null;
        }
        static void Cycle(GameBootstrap app, string prefix, string wanted)
        {
            for (int i = 0; i < 8 && Find(app, prefix).name != wanted; i++)
                Find(app, prefix).onClick.Invoke();
            Assert.That(Find(app, prefix).name, Is.EqualTo(wanted));
        }

        [UnityTest]
        public IEnumerator BulldozerClearsTheSiteThenTheNewStationRises()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var game = app.Game;
            game.World.money = 500000;
            var line = game.Build.Preview(new Cell(14, 46), new Cell(50, 46));
            Assert.That(game.Build.CommitBuild(line).ok, Is.True);
            var placed = game.Stations.Place(new Cell(16, 46), 4);
            Assert.That(placed.ok, Is.True, placed.message);
            var s = game.Trains.Station(placed.id);
            app.World.Refresh();
            app.UI.Station(s);
            var ai = Find(app, "AI UPGRADE STATION");
            Assert.That(ai, Is.Not.Null, "stations offer the AI upgrade");
            ai.onClick.Invoke();
            Assert.That(Find(app, "USE AI PICK"), Is.Not.Null, "the upgrade panel opened");
            Assert.That(Find(app, "HALT").interactable, Is.True, "the current rung stays selectable");
            Find(app, "CENTRAL").onClick.Invoke();
            Cycle(app, "PLATFORMS", "PLATFORMS 3");
            var plan = game.Upgrades.Plan(s.id, 2, 5, 3);
            Assert.That(plan.valid && plan.buildings.Count > 0, Is.True, "Willowbrook's houses stand in the way: " + plan.reason);
            Assert.That(app.World.transform.Find("Demolition pins"), Is.Not.Null, "doomed buildings are pinned on the map");
            var build = Find(app, "BULLDOZE & BUILD");
            Assert.That(build.interactable, Is.True);
            build.onClick.Invoke();
            yield return null;
            Assert.That(s.level, Is.EqualTo(2));
            Assert.That(StationLayout.Platforms(s), Is.EqualTo(3));
            Assert.That(app.World.StationUpgradeShows, Is.EqualTo(1), "the show started");
            var show = app.World.transform.Find("Station upgrades");
            Assert.That(show.GetComponentsInChildren<Transform>(true), Has.Some.Property("name").EqualTo("Bulldozer"));
            Assert.That(show.GetComponentsInChildren<Transform>(true), Has.Some.Property("name").StartsWith("Doomed"), "ghosts of the demolished houses");
            Assert.That(show.GetComponentsInChildren<Transform>(true), Has.Some.Property("name").EqualTo("Dump truck"), "a tipper hauls the rubble");
            Assert.That(show.GetComponentsInChildren<Transform>(true), Has.Some.Property("name").EqualTo("Cement mixer"), "a mixer brings the concrete");
            Assert.That(show.GetComponentsInChildren<Transform>(true), Has.Exactly(5).Property("name").EqualTo("Site worker"), "a crew of five");
            Assert.That(app.World.transform.Find("Demolition pins"), Is.Null, "pins go once the work starts");
            Assert.That(app.World.transform.Find("Stations/Station hall"), Is.Null, "the finished station waits for the show");
            app.SetSpeed(4);
            float waited = 0;
            while (app.World.StationUpgradeShows > 0 && waited < 30)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.That(app.World.StationUpgradeShows, Is.EqualTo(0), "the show finishes");
            yield return null; // Destroy takes effect at the end of the frame
            Assert.That(show.childCount, Is.EqualTo(0), "nothing of the show is left behind");
            Assert.That(app.World.transform.Find("Stations/Station hall"), Is.Not.Null, "the Central station stands");
            Assert.That(app.World.transform.Find("Stations/Glazed ridge"), Is.Not.Null, "with its train shed");
            app.SetSpeed(0);
        }

        [UnityTest, Explicit("Writes review renders of the upgrade show to Logs; run it by name.")]
        public IEnumerator ShowFramesForReview()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var game = app.Game;
            game.World.money = 500000;
            Assert.That(game.Build.CommitBuild(game.Build.Preview(new Cell(14, 46), new Cell(50, 46))).ok, Is.True);
            int town = game.Stations.Place(new Cell(16, 46), 4).id;
            var mine = game.Stations.Place(game.Stations.Plan(new Cell(7, 12), 0, 3, 1, 1), 1).id;
            app.World.Refresh();
            yield return Upgrade(app, town, 2, 5, 3, "upgrade-central", new[] { .3f, 1.6f, 3.2f, 5.5f, 8f, 13f });
            yield return Upgrade(app, town, 3, 6, 3, "upgrade-grand", new[] { 12f });
            yield return Upgrade(app, mine, 2, 6, 4, "upgrade-yard", new[] { 1.5f, 11f });
        }
        /// <summary>Applies an upgrade through the presenter's own steps and renders the map at the given seconds into the show.</summary>
        static IEnumerator Upgrade(GameBootstrap app, int stationId, int level, int length, int platforms, string name, float[] seconds)
        {
            var s = app.Game.Trains.Station(stationId);
            app.World.BeginStationUpgrade(stationId);
            var result = app.Perform(() => app.Game.Upgrades.Apply(stationId, level, length, platforms));
            Assert.That(result.ok, Is.True, result.message);
            app.World.PlayStationUpgrade(app.Game.Upgrades.Last);
            var along = s.axis == 1 ? Vector3.right : Vector3.forward;
            app.Camera.focus = new Vector3(s.cell.x, 0, s.cell.z) + along * StationLayout.Middle(length);
            app.Camera.zoom = 4.5f;
            app.SetSpeed(1);
            float at = 0;
            for (int i = 0; i < seconds.Length; i++)
            {
                yield return new WaitForSeconds(seconds[i] - at);
                at = seconds[i];
                Render(app, $"Logs/{name}-{i + 1}.png");
            }
            app.SetSpeed(0);
            while (app.World.StationUpgradeShows > 0)
                yield return null;
            yield return null;
        }
        static void Render(GameBootstrap app, string file)
        {
            var camera = app.Camera.view;
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(file, image.EncodeToPNG());
            Object.Destroy(image);
            Object.Destroy(texture);
        }
    }
}
