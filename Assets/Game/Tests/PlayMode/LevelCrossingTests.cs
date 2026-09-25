using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    public class LevelCrossingTests
    {
        [UnityTest]
        public IEnumerator BarriersCloseForATrainAndOpenAfterIt()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            // Lengthen the first town's main street east and lay a railway straight across it.
            var city = app.Game.Cities.CityFor(4);
            for (int x = city.center.x + 2; x <= city.center.x + 6; x++)
                city.roads.Add(new RoadState { cell = new Cell(x, city.center.z) });
            app.Game.World.cityRevision++;
            var cell = new Cell(city.center.x + 4, city.center.z);
            var plan = app.Game.Build.Preview(cell.Move(2).Move(2).Move(2), cell.Move(0).Move(0).Move(0));
            Assert.That(plan.valid, Is.True, plan.reason);
            Assert.That(app.Game.Build.CommitBuild(plan).ok, Is.True);
            app.World.Refresh();
            yield return null;

            var crossings = app.World.GetComponentInChildren<LevelCrossings>();
            var track = app.Game.Network.At(cell);
            Assert.That(crossings.Count, Is.EqualTo(1), "one level crossing where the street meets the railway");
            var root = crossings.transform.Find("Level crossing " + track.id);
            var booms = Parts(root, "Barrier boom");
            var lamps = Parts(root, "Crossing lamp");
            Assert.That(booms.Count, Is.EqualTo(2), "a boom on each road approach");
            Assert.That(lamps.Count, Is.EqualTo(8), "two lamps on each face of each post");
            Assert.That(crossings.Open(track.id), Is.True);
            foreach (var boom in booms)
                Assert.That(boom.bounds.max.y, Is.GreaterThan(.7f), "booms stand up with no train about");
            Assert.That(Lit(lamps), Is.EqualTo(0));

            // A train heading over the crossing: the lamps flash at once, the booms follow after the warning.
            var train = new TrainState { state = ServiceState.Travelling };
            train.path.Add(new RailStep { trackId = track.id, length = 1000 });
            app.Game.World.trains.Add(train);
            try
            {
                crossings.Animate(CrossingBarrier.Warning / 2, .1f);
                Assert.That(crossings.Open(track.id), Is.False, "cars stop as soon as the lamps flash");
                Assert.That(crossings.Lowered(track.id), Is.EqualTo(0));
                Assert.That(Lit(lamps), Is.EqualTo(4), "one lamp of each pair is lit");
                crossings.Animate(CrossingBarrier.Warning + CrossingBarrier.LowerTime, .1f);
                Assert.That(crossings.Lowered(track.id), Is.EqualTo(1));
            }
            finally
            {
                app.Game.World.trains.Remove(train);
            }
            foreach (var boom in booms)
                Assert.That(boom.bounds.max.y, Is.LessThan(.35f), "booms lie across the road");
            app.Camera.focus = new Vector3(cell.x, 0, cell.z);
            app.Camera.zoom = 4;
            yield return Capture(app, "Logs/level-crossing-closed.png");

            // Once the train is clear the booms rise, and cars go only when they are fully up.
            crossings.Animate(CrossingBarrier.RaiseTime / 2, .1f);
            Assert.That(crossings.Open(track.id), Is.False, "half-raised booms still hold the cars");
            yield return Capture(app, "Logs/level-crossing-rising.png");
            crossings.Animate(CrossingBarrier.RaiseTime, .1f);
            Assert.That(crossings.Open(track.id), Is.True);
            Assert.That(Lit(lamps), Is.EqualTo(0), "the lamps go dark");
            yield return Capture(app, "Logs/level-crossing-open.png");

            // Bulldozing the railway removes the barrier.
            Assert.That(app.Game.Build.Bulldoze(cell).ok, Is.True);
            app.World.Refresh();
            yield return null;
            Assert.That(crossings.Count, Is.EqualTo(0));
            LogAssert.NoUnexpectedReceived();
        }
        static List<Renderer> Parts(Transform root, string name)
        {
            var parts = new List<Renderer>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                if (renderer.name == name)
                    parts.Add(renderer);
            return parts;
        }
        static int Lit(List<Renderer> lamps)
        {
            int lit = 0;
            foreach (var lamp in lamps)
                if (lamp.sharedMaterial.color.r > .9f)
                    lit++;
            return lit;
        }
        static IEnumerator Capture(GameBootstrap app, string filename)
        {
            var camera = app.Camera.view;
            camera.aspect = 16f / 9;
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
        }
    }
}
