using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    public class IndustryMotionTests
    {
        [UnityTest]
        public IEnumerator IndustriesWorkWhileTheGameRunsAndFreezeWhenPaused()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;

            var motion = app.World.GetComponentInChildren<IndustryMotion>();
            var producers = app.Game.World.producers;
            int Count(ProducerKind kind) => producers.FindAll(p => p.kind == kind).Count;
            Assert.That(motion.Pumps, Is.EqualTo(2 * Count(ProducerKind.OilWells)), "two pumpjacks at every oil field");
            Assert.That(motion.Hoists, Is.EqualTo(Count(ProducerKind.IronMine)), "a winding hoist at every iron mine");
            int expectedStacks = 2 * Count(ProducerKind.Plant) + Count(ProducerKind.Mine) + Count(ProducerKind.Factory) +
                Count(ProducerKind.Sawmill) + Count(ProducerKind.Refinery) + 2 * Count(ProducerKind.SteelMill);
            Assert.That(motion.Stacks, Is.EqualTo(expectedStacks), "both power-station stacks and every works chimney smoke");
            var puffs = Named(motion.transform, "Smoke puff");
            Assert.That(puffs.Count, Is.EqualTo(expectedStacks * IndustryMotion.PuffsPerStack));

            // Smoke rises from the top of each power-station stack.
            var power = producers.Find(p => p.kind == ProducerKind.Plant);
            for (int i = 0; i < 2; i++)
            {
                var mouth = new Vector3(power.cell.x - .65f + i * 1.2f, 2.96f, power.cell.z + .75f);
                Assert.That(puffs.Exists(p => Mathf.Abs(p.localPosition.x - mouth.x) < 1.5f && Mathf.Abs(p.localPosition.z - mouth.z) < 1.5f && p.localPosition.y > mouth.y),
                    "a plume above power stack " + i);
            }

            var scenery = app.World.transform.Find("Scenery");
            var beams = Named(scenery, "Walking beam");
            var cages = Named(scenery, "Hoist cage");
            Assert.That(beams.Count, Is.EqualTo(motion.Pumps));
            Assert.That(cages.Count, Is.EqualTo(motion.Hoists));
            var tilts = beams.ConvertAll(Tilt);
            var places = puffs.ConvertAll(p => p.localPosition);

            // Paused: nothing moves, even over many frames.
            for (int frame = 0; frame < 10; frame++)
                app.World.Animate(.1f, true);
            for (int i = 0; i < beams.Count; i++)
                Assert.That(Tilt(beams[i]), Is.EqualTo(tilts[i]), "pumps stop while paused");
            for (int i = 0; i < puffs.Count; i++)
                Assert.That(puffs[i].localPosition, Is.EqualTo(places[i]), "smoke hangs while paused");

            // Running: the smoke moves on at once.
            motion.Animate(.5f);
            for (int i = 0; i < puffs.Count; i++)
                Assert.That((puffs[i].localPosition - places[i]).magnitude, Is.GreaterThan(.05f), "puff " + i + " drifts");
            // Over a whole stroke every beam rocks through its full swing and every cage rides the shaft.
            var lowest = beams.ConvertAll(Tilt);
            var highest = beams.ConvertAll(Tilt);
            var bottom = cages.ConvertAll(c => c.localPosition.y);
            var top = cages.ConvertAll(c => c.localPosition.y);
            for (int step = 0; step < 24; step++)
            {
                motion.Animate(IndustryMotion.HoistCycle / 24);
                for (int i = 0; i < beams.Count; i++)
                {
                    lowest[i] = Mathf.Min(lowest[i], Tilt(beams[i]));
                    highest[i] = Mathf.Max(highest[i], Tilt(beams[i]));
                }
                for (int i = 0; i < cages.Count; i++)
                {
                    bottom[i] = Mathf.Min(bottom[i], cages[i].localPosition.y);
                    top[i] = Mathf.Max(top[i], cages[i].localPosition.y);
                }
            }
            for (int i = 0; i < beams.Count; i++)
                Assert.That(highest[i] - lowest[i], Is.GreaterThan(30), "pumpjack " + i + " rocks");
            for (int i = 0; i < cages.Count; i++)
                Assert.That(top[i] - bottom[i], Is.GreaterThan(IndustryMotion.HoistTravel), "mine cage " + i + " rides the shaft");

            // The linkage holds together: each pitman arm runs from its crank pin to the beam's tail.
            foreach (var pump in Named(scenery, "Pumpjack"))
            {
                var beam = pump.Find("Walking beam");
                var crank = pump.Find("Crank");
                foreach (var arm in Named(pump, "Pitman arm"))
                {
                    var along = arm.localRotation * Vector3.up * (arm.localScale.y / 2);
                    Vector3 low = arm.localPosition - along, high = arm.localPosition + along;
                    var side = new Vector3(Mathf.Sign(arm.localPosition.x) * IndustryMotion.PitmanSide, 0, 0);
                    var pin = crank.localPosition + crank.localRotation * new Vector3(0, 0, -IndustryMotion.CrankRadius) + side;
                    var tail = beam.localPosition + beam.localRotation * new Vector3(0, 0, IndustryMotion.TailArm) + side;
                    Assert.That((low - pin).magnitude, Is.LessThan(.01f), "arm meets the crank pin");
                    Assert.That((high - tail).magnitude, Is.LessThan(.01f), "arm meets the beam's tail");
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>Close-ups of every working industry for a visual check: Logs/industry-*.png.</summary>
        [UnityTest, Explicit]
        public IEnumerator GalleryShowsEveryIndustryAtWork()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var motion = app.World.GetComponentInChildren<IndustryMotion>();
            var shots = new[] { ProducerKind.OilWells, ProducerKind.Plant, ProducerKind.Refinery, ProducerKind.SteelMill, ProducerKind.IronMine, ProducerKind.Sawmill, ProducerKind.Mine, ProducerKind.Factory };
            foreach (var kind in shots)
            {
                var site = app.Game.World.producers.Find(p => p.kind == kind);
                app.Camera.focus = new Vector3(site.cell.x, 0, site.cell.z);
                app.Camera.zoom = 5;
                motion.Animate(1.1f);
                yield return Capture(app, "Logs/industry-" + kind.ToString().ToLowerInvariant() + ".png");
            }
            // The oil field again, half a stroke on, to compare the beams' swing.
            var oil = app.Game.World.producers.Find(p => p.kind == ProducerKind.OilWells);
            app.Camera.focus = new Vector3(oil.cell.x, 0, oil.cell.z);
            motion.Animate(IndustryMotion.PumpStroke / 2);
            yield return Capture(app, "Logs/industry-oilwells-2.png");
        }

        // A beam's pitch, signed: positive while the horse head is up.
        static float Tilt(Transform beam)
        {
            float x = beam.localEulerAngles.x;
            return x > 180 ? x - 360 : x;
        }
        static List<Transform> Named(Transform root, string name)
        {
            var found = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    found.Add(t);
            return found;
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
