using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>
    /// Four-way crossings are junctions: a train turns at one onto a siding, as it does to get round a crossing another train
    /// holds. Writes Logs/crossing-turns.png and crossing-turns-back.png (the view turned round), the train half-way round
    /// the turn back onto the main line.
    /// </summary>
    public class CrossingTurnTests
    {
        [UnityTest]
        public IEnumerator TrainTurnsThroughCrossingsOntoSiding()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            var game = app.Game;
            Build(game, new Cell(8, 15), new Cell(50, 15));
            for (int x = 25; x <= 27; x++)
                Build(game, new Cell(x, 14), new Cell(x, 16));
            Build(game, new Cell(25, 16), new Cell(27, 16));
            int a = OK(game.Stations.Place(new Cell(10, 15), 1)), b = OK(game.Stations.Place(new Cell(48, 15), 2));
            int id = OK(game.Trains.Buy(a, 0, Cargo.Coal));
            OK(game.Trains.AssignRoute(id, a, b));
            var train = game.Trains.Train(id);
            // Round the siding, as if another train stood across the middle crossing.
            int from = train.path.FindIndex(s => s.trackId == game.Network.At(new Cell(24, 15)).id);
            var start = train.path[from];
            var detour = game.Pathfinder.FindDetour(start.trackId, start.entry, train.path[train.path.Count - 1].trackId, new[] { game.Network.At(new Cell(26, 15)).id });
            Assert.That(detour, Is.Not.Null, "the siding leads round the middle crossing");
            train.path.RemoveRange(from, train.path.Count - from);
            train.path.AddRange(detour);
            int bend = train.path.FindIndex(s => s.trackId == game.Network.At(new Cell(27, 15)).id);
            Assert.That(train.path[bend].entry, Is.EqualTo(0), "it comes down the cross line from the siding");
            Assert.That(train.path[bend].exit, Is.EqualTo(1), "and turns east onto the main line");
            train.state = ServiceState.Travelling;
            train.step = bend;
            train.distance = train.path[bend].length / 2;
            app.World.Refresh();
            var front = RailGeometry.TrainPosition(game, train, 0, out var forward);
            Assert.That(Mathf.Abs(front.x - 27), Is.LessThan(.5f));
            Assert.That(Mathf.Abs(front.z - 15), Is.LessThan(.5f));
            Assert.That(Mathf.Abs(forward.x), Is.GreaterThan(.5f), "half-way round the turn the train runs diagonally");
            Assert.That(Mathf.Abs(forward.z), Is.GreaterThan(.5f), "half-way round the turn the train runs diagonally");
            Assert.That(CrossingSignals.IsGreen(game.World, train.path[bend].trackId, 0), Is.True, "the turning train's own signal is green");
            Assert.That(CrossingSignals.IsGreen(game.World, train.path[bend].trackId, 3), Is.False, "every other signal at the crossing is red");
            // Errors fail the test by themselves; warnings (such as unrendered music) are not this test's business.
            app.Camera.focus = front;
            app.Camera.zoom = CameraController.MinZoom;
            yield return Capture(app, "Logs/crossing-turns.png");
            app.Camera.SetTurn(2);
            yield return Capture(app, "Logs/crossing-turns-back.png");
            app.Camera.SetTurn(0);
        }
        static void Build(GameSession game, Cell a, Cell b)
        {
            var plan = game.Build.Preview(a, b);
            Assert.That(plan.valid, Is.True, plan.reason);
            OK(game.Build.CommitBuild(plan));
        }
        static int OK(Result r)
        {
            Assert.That(r.ok, Is.True, r.message);
            return r.id;
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
