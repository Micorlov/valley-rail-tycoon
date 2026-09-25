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
    /// <summary>People waiting on town platforms, and the STATIONS list that ranks stations by load.</summary>
    public class StationCrowdTests
    {
        static GameBootstrap App()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }
        static Transform Safe(GameBootstrap app) => app.UI.transform.Find("Safe area");
        static void Press(GameBootstrap app, string path)
        {
            var target = Safe(app).Find(path);
            Assert.That(target, Is.Not.Null, path + " exists");
            Assert.That(target.gameObject.activeInHierarchy, Is.True, path + " is visible before it is pressed");
            target.GetComponent<Button>().onClick.Invoke();
        }
        static StationState TownStation(GameBootstrap app) =>
            app.Game.World.stations.Find(s => app.Game.Cargo.Producer(s.producerId).kind == ProducerKind.Town);
        static List<Transform> Cards(GameBootstrap app)
        {
            var cards = new List<Transform>();
            foreach (Transform child in Safe(app).Find("Context/Scrollable details"))
                if (child.gameObject.activeSelf && child.name.StartsWith("Station card "))
                    cards.Add(child);
            return cards;
        }
        static string Hex(LoadLevel level) => ColorUtility.ToHtmlStringRGB(StationCrowds.LoadColor(level));

        [UnityTest]
        public IEnumerator PlatformCrowdFollowsTheWaitingQueue()
        {
            var app = App();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            var station = TownStation(app);
            var town = app.Game.Cargo.Producer(station.producerId);
            var crowds = app.World.GetComponentInChildren<StationCrowds>();
            Assert.That(crowds, Is.Not.Null);
            Assert.That(town.storage, Is.GreaterThan(StationLoad.MaxFigures), "town queue is large enough to fill a platform gradually");

            town.inventory = 0;
            app.UI.UpdateHud();
            Assert.That(crowds.Showing(station.id), Is.Zero, "an empty queue leaves the platform empty");

            town.inventory = town.storage / 2;
            app.UI.UpdateHud();
            Assert.That(crowds.Showing(station.id), Is.EqualTo(StationLoad.MaxFigures / 2), "half a queue fills half the platform");

            town.inventory = town.storage;
            app.UI.UpdateHud();
            Assert.That(crowds.Showing(station.id), Is.EqualTo(StationLoad.MaxFigures));
            var crowd = app.World.transform.Find("Station crowds/Crowd " + station.id);
            int standing = 0;
            foreach (Transform person in crowd)
                if (person.gameObject.activeSelf && person.name == "Passenger")
                    standing++;
            Assert.That(standing, Is.EqualTo(StationLoad.MaxFigures), "the figures are really shown, not just counted");
            app.Game.World.revision++;
            app.World.Refresh();
            Assert.That(app.World.transform.Find("Station crowds/Crowd " + station.id), Is.SameAs(crowd), "a track edit elsewhere keeps the platform's crowd");
            Assert.That(crowds.Showing(station.id), Is.EqualTo(StationLoad.MaxFigures));
            foreach (var other in app.Game.World.stations)
                if (other.producerId == town.id)
                    Assert.That(crowds.Showing(other.id), Is.EqualTo(StationLoad.MaxFigures), "a town queue is shared by all its stations");
            var label = app.World.transform.Find("Stations/Station status " + station.id).GetComponent<TMPro.TMP_Text>();
            Assert.That(label.text, Does.Contain(" waiting"));
            Assert.That(ColorUtility.ToHtmlStringRGB(label.color), Is.EqualTo(Hex(LoadLevel.Crowded)), "a full platform's sign turns red");

            var freight = app.Game.World.stations.Find(s => !StationLoad.Of(app.Game.World, app.Game.Balance, s).Passengers);
            Assert.That(freight, Is.Not.Null, "the demo has a freight station");
            Assert.That(crowds.Showing(freight.id), Is.Zero);
            Assert.That(app.World.transform.Find("Station crowds/Crowd " + freight.id), Is.Null, "freight platforms get no passengers");

            app.Camera.focus = new Vector3(station.cell.x, 0, station.cell.z);
            app.Camera.zoom = 8;
            yield return Capture(app, "Logs/station-crowd.png");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator StationsButtonListsBusiestFirstAndFliesThere()
        {
            var app = App();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            foreach (var p in app.Game.World.producers)
                p.inventory = 0;
            var station = TownStation(app);
            var town = app.Game.Cargo.Producer(station.producerId);
            town.inventory = town.storage;

            Press(app, "Tools/STATIONS");
            yield return null;
            var cards = Cards(app);
            Assert.That(cards.Count, Is.EqualTo(app.Game.World.stations.Count), "every station has a card");
            int firstId = int.Parse(cards[0].name.Substring("Station card ".Length));
            Assert.That(app.Game.Trains.Station(firstId).producerId, Is.EqualTo(town.id), "the full town is listed first");
            var firstText = cards[0].GetComponentInChildren<TMPro.TMP_Text>();
            Assert.That(firstText.text, Does.Contain("100%").And.Contain(Hex(LoadLevel.Crowded)));
            Assert.That(cards[0].Find("Load bar/Load").GetComponent<RectTransform>().anchorMax.x, Is.EqualTo(1).Within(.001f), "a full queue fills the bar");
            yield return Capture(app, "Logs/station-list.png");

            town.inventory = 0;
            app.UI.UpdateHud();
            Assert.That(firstText.text, Does.Contain("0 passengers waiting"), "the open list follows the queue live");

            Press(app, "Context/Scrollable details/Station card " + station.id);
            yield return null;
            Assert.That(app.Camera.focus.x, Is.EqualTo(station.cell.x).Within(.01f));
            Assert.That(app.Camera.focus.z, Is.EqualTo(station.cell.z).Within(.01f));
            Assert.That(app.Camera.zoom, Is.LessThanOrEqualTo(12f), "close enough to see the people waiting");
            var details = Safe(app).Find("Context/Scrollable details").GetComponentsInChildren<TMPro.TMP_Text>();
            Assert.That(System.Array.Exists(details, t => t.text == station.name), Is.True, "the station's details open");

            Press(app, "Context/Scrollable details/ALL STATIONS");
            yield return null;
            Assert.That(Cards(app).Count, Is.EqualTo(app.Game.World.stations.Count), "the station panel leads back to the list");
            LogAssert.NoUnexpectedReceived();
        }
        static IEnumerator Capture(GameBootstrap app, string filename)
        {
            var camera = app.Camera.view;
            camera.aspect = 16f / 9;
            var canvas = app.UI.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 3;
            Canvas.ForceUpdateCanvases();
            var texture = new RenderTexture(1600, 900, 24);
            camera.targetTexture = texture;
            for (int frame = 0; frame < 30; frame++)
                yield return null;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(filename, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(texture);
            Object.Destroy(image);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
