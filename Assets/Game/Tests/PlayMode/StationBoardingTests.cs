using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>Passengers getting off and on a train while it stands at a town platform.</summary>
    public class StationBoardingTests
    {
        const int TickLimit = 40000;
        const float TickSeconds = .05f;
        static GameBootstrap App()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }

        /// <summary>Starts the demo zoomed in on the passenger line's far town, so walkers are drawn.</summary>
        static IEnumerator Setup(GameBootstrap app)
        {
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            var train = app.Game.World.trains.Find(t => t.cargo == Cargo.Passengers);
            Assert.That(train, Is.Not.Null, "the demo runs a passenger train");
            var far = app.Game.Trains.Station(train.destination);
            app.Camera.focus = new Vector3(far.cell.x, 0, far.cell.z);
            app.Camera.zoom = 4;
            for (int frame = 0; frame < 3; frame++)
                yield return null;
        }

        /// <summary>Runs the demo's passenger train to its next town with people aboard and a full platform waiting there.</summary>
        static TrainState ArriveAtFullPlatform(GameBootstrap app, out StationState stop)
        {
            var game = app.Game;
            var train = game.World.trains.Find(t => t.cargo == Cargo.Passengers);
            for (int i = 0; i < TickLimit && train.state != ServiceState.Travelling; i++)
                game.Step();
            Assert.That(train.state, Is.EqualTo(ServiceState.Travelling), "the train leaves its first stop");
            stop = game.Trains.Station(train.destination);
            // stationId still names the stop just left.
            train.units = game.Balance.Capacity(train) / 2;
            train.origin = game.Trains.Station(train.stationId).producerId;
            train.cargoDestination = stop.producerId;
            var town = game.Cargo.Producer(stop.producerId);
            town.inventory = town.storage;
            app.UI.UpdateHud();
            app.World.Animate(.02f, false);
            for (int i = 0; i < TickLimit && !(train.state == ServiceState.Loading && train.stationId == stop.id); i++)
            {
                town.inventory = town.storage;
                game.Step();
            }
            Assert.That(train.state == ServiceState.Loading && train.stationId == stop.id, Is.True, "the train reaches the far town");
            return train;
        }

        static int Active(Component root, string name)
        {
            int count = 0;
            foreach (Transform child in root.transform)
                if (child.gameObject.activeSelf && child.name == name)
                    count++;
            return count;
        }

        [UnityTest]
        public IEnumerator ArrivalsGetOffThenTheQueueBoards()
        {
            var app = App();
            yield return Setup(app);
            var boarding = app.World.GetComponentInChildren<StationBoarding>(true);
            var crowds = app.World.GetComponentInChildren<StationCrowds>(true);
            Assert.That(boarding, Is.Not.Null);
            Assert.That(app.World.ZoomedIn, Is.True, "close enough to draw the walkers");

            var train = ArriveAtFullPlatform(app, out var stop);
            int waiting = crowds.Showing(stop.id), left = StationLoad.Of(app.Game.World, app.Game.Balance, stop).Figures;
            Assert.That(waiting, Is.EqualTo(StationLoad.MaxFigures), "the platform was full before the train came");
            Assert.That(left, Is.LessThan(waiting), "the simulation boarded part of the queue");
            app.World.Animate(.02f, false);
            var (alighting, boarders) = boarding.Planned(train.id);
            Assert.That(alighting, Is.InRange(3, 8), "a half-full train lets several people off");
            Assert.That(boarders, Is.InRange(System.Math.Min(waiting - left, 8), 8), "everyone who left the queue gets on");
            app.UI.UpdateHud();
            Assert.That(crowds.Showing(stop.id), Is.EqualTo(waiting), "boarders keep standing until their walk starts");

            var across = new Vector3(Directions.Dx[stop.side], 0, Directions.Dz[stop.side]);
            var cell = new Vector3(stop.cell.x, 0, stop.cell.z);
            int tick = 0, firstOff = -1, firstOn = -1, lastOff = -1, lastOn = -1;
            int dwellTicks = StationCatalog.DwellTicks(app.Game.Balance, stop);
            while (train.state == ServiceState.Loading && tick < dwellTicks + 5)
            {
                app.Game.Step();
                app.World.Animate(TickSeconds, false);
                app.UI.UpdateHud();
                tick++;
                int off = Active(boarding, "Alighting passenger"), on = Active(boarding, "Boarding passenger");
                if (off > 0) { firstOff = firstOff < 0 ? tick : firstOff; lastOff = tick; }
                if (on > 0) { firstOn = firstOn < 0 ? tick : firstOn; lastOn = tick; }
                foreach (Transform walker in boarding.transform)
                {
                    if (!walker.gameObject.activeSelf)
                        continue;
                    var offset = walker.position - cell;
                    Assert.That(Vector3.Dot(offset, across), Is.InRange(-.1f, 1.45f), walker.name + " stays between the train and the back of the platform");
                    Assert.That(walker.position.y, Is.InRange(.29f, .33f), walker.name + " walks on the platform, not under it");
                }
                Assert.That(crowds.Showing(stop.id), Is.InRange(left, waiting), "the queue only shrinks as people walk to the train");
            }
            Assert.That(firstOff, Is.GreaterThan(0), "people were seen getting off");
            Assert.That(firstOn, Is.GreaterThan(firstOff), "the queue boards after the first arrivals step out");
            Assert.That(lastOn, Is.LessThan(dwellTicks), "everyone is aboard before the train leaves");
            Assert.That(lastOff, Is.LessThan(lastOn), "arrivals are gone before the last boarder gets on");
            Assert.That(crowds.Showing(stop.id), Is.EqualTo(left), "the platform ends with the people still waiting");

            for (int i = 0; i < 20 && train.state == ServiceState.Loading; i++)
                app.Game.Step();
            Assert.That(train.state, Is.EqualTo(ServiceState.Travelling));
            app.World.Animate(TickSeconds, false);
            Assert.That(boarding.Walking, Is.Zero);
            Assert.That(boarding.Planned(train.id), Is.EqualTo((0, 0)), "the stop ends when the train departs");
            Assert.That(Active(boarding, "Alighting passenger") + Active(boarding, "Boarding passenger"), Is.Zero, "no one is left walking");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Explicit("Writes Logs/boarding-*.png for a visual review")]
        public IEnumerator ShowPassengersBoarding()
        {
            var app = App();
            yield return Setup(app);
            var train = ArriveAtFullPlatform(app, out var stop);
            app.World.Animate(.02f, false);
            app.Camera.focus = new Vector3(stop.cell.x, 0, stop.cell.z) + new Vector3(Directions.Dx[stop.side], 0, Directions.Dz[stop.side]) * .4f;
            app.Camera.zoom = 3.5f;
            int dwellTicks = StationCatalog.DwellTicks(app.Game.Balance, stop);
            float[] moments = { .12f, .3f, .45f, .6f, .75f, .9f };
            for (int i = 0; i < moments.Length; i++)
            {
                while (train.state == ServiceState.Loading && dwellTicks - train.dwell < moments[i] * dwellTicks)
                {
                    app.Game.Step();
                    app.World.Animate(TickSeconds, false);
                }
                app.UI.UpdateHud();
                yield return Capture(app, $"Logs/boarding-{i + 1}.png");
            }
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
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            for (int frame = 0; frame < 10; frame++)
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
