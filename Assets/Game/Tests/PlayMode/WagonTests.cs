using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>The player chooses how many wagons a train gets when buying it, and changes them later at a station.</summary>
    public class WagonTests
    {
        static readonly FieldInfo TrainViews = typeof(WorldView).GetField("trainViews", BindingFlags.NonPublic | BindingFlags.Instance);

        [UnityTest]
        public IEnumerator PlayerChoosesWagonsWhenBuyingAndChangesThemAtAStation()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(true);
            yield return null;
            app.SetSpeed(0);
            var game = app.Game;
            var old = game.World.trains.Find(t => t.cargo == Cargo.Coal);
            var station = game.Trains.Station(old.a);
            Assert.That(game.Trains.Sell(old.id).ok, Is.True);
            game.World.money = 100000;
            var body = app.UI.transform.Find("Safe area/Context/Scrollable details");
            // Buying: a shop card opens the picker at the standard length; + adds wagons and the price follows.
            app.UI.Station(station);
            Click(body, "BUY A TRAIN");
            Click(body, "Small freight · Coal");
            Assert.That(Shows(body, "2 WAGONS"), Is.True, Dump(body));
            Assert.That(Shows(body, "BUY · $8,000"), Is.True, Dump(body));
            Click(body, "More wagons");
            Click(body, "More wagons");
            Assert.That(Shows(body, "4 WAGONS"), Is.True, Dump(body));
            Assert.That(Shows(body, "4 wagons · 60 tons · $30/min"), Is.True, Dump(body));
            yield return Capture(app, "Logs/wagon-picker-buy.png");
            int money = game.World.money;
            Click(body, "BUY · $12,000");
            var train = game.World.trains.Find(t => t.stationId == station.id);
            Assert.That(train, Is.Not.Null, app.Notice);
            Assert.That(train.wagons, Is.EqualTo(4));
            Assert.That(game.World.money, Is.EqualTo(money - 12000));
            Assert.That(Shows(body, "TRAIN #" + train.number), Is.True, Dump(body));
            Assert.That(Cars(app, train), Is.EqualTo(5), "A locomotive and four wagons on the map");
            // Editing: the train panel opens the same picker; removing a wagon refunds half its price.
            Click(body, "WAGONS: 4 · CHANGE");
            Click(body, "Fewer wagons");
            Assert.That(Shows(body, "Refund: $1,000"), Is.True, Dump(body));
            yield return Capture(app, "Logs/wagon-picker-edit.png");
            money = game.World.money;
            Click(body, "REMOVE 1 WAGON · +$1,000");
            Assert.That(train.wagons, Is.EqualTo(3), app.Notice);
            Assert.That(game.World.money, Is.EqualTo(money + 1000));
            app.World.Animate(.1f, true);
            Assert.That(Cars(app, train), Is.EqualTo(4), "The map rebuilds the shorter consist");
            // The length stops at the maximum.
            Click(body, "WAGONS: 3 · CHANGE");
            for (int i = 0; i < 10 && Find(body, "More wagons").interactable; i++)
                Click(body, "More wagons");
            Assert.That(Shows(body, TrainCatalog.MaxWagons + " WAGONS"), Is.True, Dump(body));
            Assert.That(Find(body, "More wagons").interactable, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        static int Cars(GameBootstrap app, TrainState train) => ((Dictionary<int, Transform[]>)TrainViews.GetValue(app.World))[train.id].Length;

        // Cleared panel buttons stay in the hierarchy until the frame ends, so only active ones count.
        static UnityEngine.UI.Button Find(Transform body, string name)
        {
            var button = System.Array.Find(body.GetComponentsInChildren<UnityEngine.UI.Button>(), b => b.name.StartsWith(name));
            Assert.That(button, Is.Not.Null, "No button " + name + "\n" + Dump(body));
            return button;
        }

        static void Click(Transform body, string name) => Find(body, name).onClick.Invoke();

        static bool Shows(Transform body, params string[] parts) =>
            System.Array.Exists(body.GetComponentsInChildren<TMPro.TMP_Text>(), t => System.Array.TrueForAll(parts, p => t.text.Contains(p)));

        static string Dump(Transform body) =>
            string.Join("\n---\n", System.Array.ConvertAll(body.GetComponentsInChildren<TMPro.TMP_Text>(), t => t.text));

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
