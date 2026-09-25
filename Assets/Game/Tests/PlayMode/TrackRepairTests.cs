using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>AI FIX in the track tool: shown only while laying track, previews the repair and builds it on confirm.</summary>
    public class TrackRepairTests
    {
        static Transform Safe(GameBootstrap app) => app.UI.transform.Find("Safe area");
        static bool Visible(GameBootstrap app, string path)
        {
            var target = Safe(app).Find(path);
            return target && target.gameObject.activeInHierarchy;
        }
        [UnityTest]
        public IEnumerator AiFixButtonFillsAGapBetweenLineEnds()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var game = app.Game;
            game.World.money = 200000;
            Assert.That(game.Build.CommitBuild(game.Build.Preview(new Cell(14, 36), new Cell(18, 36))).ok, Is.True);
            Assert.That(game.Build.CommitBuild(game.Build.Preview(new Cell(20, 36), new Cell(24, 36))).ok, Is.True);
            app.World.Refresh();
            Assert.That(Visible(app, "Tools/AI FIX"), Is.False, "AI FIX waits for the track tool");
            app.ChooseTool(ToolMode.Track);
            yield return null;
            Assert.That(Visible(app, "Tools/AI FIX") && Visible(app, "Tools/ACTIVE TOOL") && Visible(app, "Tools/DONE"), Is.True, "AI FIX sits beside the track tool");
            Safe(app).Find("Tools/AI FIX").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return Capture(app, "Logs/ai-track-fix.png");
            var body = Safe(app).Find("Context/Scrollable details");
            Assert.That(System.Array.Exists(body.GetComponentsInChildren<TMPro.TMP_Text>(), t => t.text.Contains("1 gap")), Is.True, "The panel names the gap");
            Transform confirm = null;
            foreach (Transform child in body)
                if (child.name.StartsWith("FIX ALL"))
                    confirm = child;
            Assert.That(confirm, Is.Not.Null, "The panel offers to fix everything");
            int revision = game.World.revision, money = game.World.money;
            confirm.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.That(game.World.revision, Is.GreaterThan(revision));
            Assert.That(game.World.money, Is.EqualTo(money - 100));
            Assert.That(game.Network.At(new Cell(19, 36))?.mask, Is.EqualTo(10), "The gap is filled with a straight");
            Assert.That(app.Mode, Is.EqualTo(ToolMode.Track), "The player keeps laying track");
            Assert.That(Visible(app, "Tools/AI FIX"), Is.True, "The panel closes back to the track tool");
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
