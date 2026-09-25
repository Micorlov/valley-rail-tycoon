using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ValleyRail.Tests
{
    public class VehicleArtTests
    {
        static GameBootstrap Boot()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }

        static Bounds BoundsOf(List<VehiclePart> parts)
        {
            var bounds = new Bounds(parts[0].center, parts[0].size);
            foreach (var part in parts)
                bounds.Encapsulate(new Bounds(part.center, part.size));
            return bounds;
        }

        [Test]
        public void CatalogHasCarsTrucksBusesAndEmergencyVehicles()
        {
            var models = VehicleCatalog.Models;
            Assert.That(models.Length, Is.EqualTo(13));
            Assert.That(models.Count(m => m.kind == VehicleKind.Car), Is.EqualTo(4));
            Assert.That(models.Count(m => m.kind == VehicleKind.Truck), Is.EqualTo(4));
            Assert.That(models.Count(m => m.kind == VehicleKind.Bus), Is.EqualTo(2));
            Assert.That(models.Where(m => m.kind == VehicleKind.Emergency).Select(m => m.name),
                Is.EquivalentTo(new[] { "Police car", "Ambulance", "Fire engine" }));
            Assert.That(models.Select(m => m.name).Distinct().Count(), Is.EqualTo(13), "Every model has its own name");
            Assert.That(VehicleCatalog.Mix.Distinct().OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, 13)), "Traffic spawns every model");
            Assert.That(VehicleCatalog.Mix.Take(13).Distinct().Count(), Is.EqualTo(13), "Every model shows up within the first thirteen");
            Assert.That(models[VehicleCatalog.Mix[1]].name, Is.EqualTo("Police car"), "Even a town with two vehicles has a police car");
            Assert.That(VehicleCatalog.Mix.Count(i => models[i].kind == VehicleKind.Car), Is.GreaterThan(VehicleCatalog.Mix.Length / 2),
                "Cars stay the majority");
            Assert.That(VehicleCatalog.Mix.Count(i => models[i].kind == VehicleKind.Emergency), Is.InRange(3, VehicleCatalog.Mix.Length / 5),
                "Emergency vehicles show up often, but stay a small share");
        }

        [Test]
        public void EmergencyVehiclesCarryBeaconsThatFlashInTurn()
        {
            foreach (var model in VehicleCatalog.Models)
            {
                bool emergency = model.kind == VehicleKind.Emergency;
                Assert.That(model.beacons.Length > 0, Is.EqualTo(emergency), model.name + (emergency ? " has beacons" : " has no beacons"));
                if (!emergency)
                    continue;
                Assert.That(model.beacons.Select(b => b.beat).Distinct().OrderBy(b => b), Is.EqualTo(new[] { 0, 1 }), model.name + " flashes on two beats");
                Assert.That(model.beacons.Count(b => b.beat == 0), Is.EqualTo(model.beacons.Count(b => b.beat == 1)), model.name + " lights as many beacons on each beat");
                var parts = model.Parts(0);
                foreach (var beacon in model.beacons)
                {
                    Assert.That(beacon.flash, Is.Not.EqualTo(beacon.color), model.name + " beacon lights brighter than its lens");
                    Assert.That(parts.Count(p => p.center == beacon.center && p.IsBeacon), Is.EqualTo(1), model.name + " paints each lens into its body");
                    // Nothing of the vehicle covers a beacon from above.
                    bool buried = parts.Any(p => !p.IsBeacon &&
                        Mathf.Abs(p.center.x - beacon.center.x) < (p.size.x + beacon.size.x) / 2 &&
                        Mathf.Abs(p.center.z - beacon.center.z) < (p.size.z + beacon.size.z) / 2 &&
                        p.center.y + p.size.y / 2 > beacon.center.y + .001f);
                    Assert.That(buried, Is.False, $"{model.name} beacon at {beacon.center} shows on the roof");
                }
            }
        }

        [Test]
        public void EveryVehicleStandsOnTheRoadAndFitsItsLane()
        {
            foreach (var model in VehicleCatalog.Models)
                for (int livery = 0; livery < model.liveries.Length; livery++)
                {
                    string what = $"{model.name} livery {livery}";
                    var parts = model.Parts(livery);
                    var bounds = BoundsOf(parts);
                    Assert.That(bounds.min.y, Is.EqualTo(0).Within(.001f), what + " has its wheels on the road");
                    Assert.That(bounds.size.z, Is.EqualTo(model.length).Within(.02f), what + " is as long as the catalogue says");
                    Assert.That(bounds.center.z, Is.EqualTo(0).Within(.01f), what + " is centred on its lane position");
                    Assert.That(bounds.extents.x, Is.LessThan(.13f), what + " fits its lane");
                    Assert.That(bounds.max.y, Is.LessThan(model.kind == VehicleKind.Car ? .3f : .55f), what);
                    Assert.That(parts.Select(p => p.color).Distinct().Count(), Is.InRange(4, 8), what + " stays a few materials");
                    Assert.That(parts.All(p => p.size.x > 0 && p.size.y > 0 && p.size.z > 0), what);
                }
            float Height(string name) => BoundsOf(VehicleCatalog.Models.First(m => m.name == name).Parts(0)).max.y;
            Assert.That(Height("Double-decker bus"), Is.GreaterThan(Height("City bus") * 1.4f), "The double-decker has a second deck");
            Assert.That(Height("City bus"), Is.GreaterThan(Height("Saloon")));
            Assert.That(Height("Box truck"), Is.GreaterThan(Height("Delivery van")));
        }

        // The demo's small towns hold only a handful of vehicles; the catalogue test covers every model showing up by twelve.
        [UnityTest]
        public IEnumerator TownTrafficDrivesAMixOfVehicles()
        {
            var app = Boot();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            yield return null;
            var traffic = Object.FindAnyObjectByType<CityTraffic>();
            Assert.That(traffic, Is.Not.Null);
            // Vehicles are the traffic's direct children; their beacons are children of the vehicles.
            var vehicles = traffic.transform.Cast<Transform>().Where(t => t.GetComponent<MeshRenderer>()).ToList();
            Assert.That(vehicles, Is.Not.Empty, "The demo towns have traffic");
            Assert.That(vehicles.Select(v => v.name).Distinct().Count(), Is.GreaterThanOrEqualTo(Mathf.Min(vehicles.Count, 5)),
                "Neighbouring vehicles are different models");
            foreach (var vehicle in vehicles)
            {
                var model = VehicleCatalog.Models.FirstOrDefault(m => m.name == vehicle.name);
                Assert.That(model, Is.Not.Null, vehicle.name + " is a catalogue vehicle");
                var mesh = vehicle.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.bounds.size.z, Is.EqualTo(model.length).Within(.02f), model.name);
                Assert.That(vehicle.GetComponent<MeshRenderer>().sharedMaterials.Length, Is.EqualTo(mesh.subMeshCount), model.name);
            }
            // Vehicles of one model and livery share a mesh, so the whole fleet needs only a few dozen.
            int meshes = vehicles.Select(v => v.GetComponent<MeshFilter>().sharedMesh).Distinct().Count();
            Assert.That(meshes, Is.LessThanOrEqualTo(VehicleCatalog.Models.Sum(m => m.liveries.Length)));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PoliceCarBeaconsFlashRedAndBlueInTurn()
        {
            var app = Boot();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            yield return null;
            var traffic = Object.FindAnyObjectByType<CityTraffic>();
            var police = traffic.transform.Cast<Transform>().FirstOrDefault(t => t.name == "Police car");
            Assert.That(police, Is.Not.Null, "The demo's handful of vehicles includes a police car");
            var beacons = police.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Beacon").ToList();
            Assert.That(beacons.Count, Is.EqualTo(2), "A red and a blue beacon on the roof");
            Assert.That(beacons.All(b => b.GetComponent<Collider>() == null), "Beacons carry no colliders");
            // The town is paused, yet the beacons flash, in real time, one at a time. A beat lasts .22 s; frames can
            // stall, so watch until both have shown instead of for a fixed time.
            var seen = new HashSet<int>();
            float until = Time.unscaledTime + 3f;
            while (seen.Count < 2 && Time.unscaledTime < until)
            {
                yield return null;
                var lit = Enumerable.Range(0, beacons.Count).Where(i => beacons[i].enabled).ToList();
                Assert.That(lit.Count, Is.LessThanOrEqualTo(1), "The two beacons take turns");
                seen.UnionWith(lit);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { 0, 1 }), "Both beacons light up in turn");
            Assert.That(beacons.Select(b => b.sharedMaterial.color).Distinct().Count(), Is.EqualTo(2), "One red and one blue");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SirenSoundsOnlyWhenZoomedInOnAnEmergencyVehicle()
        {
            int savedPreference = PlayerPrefs.GetInt("sound", 1);
            var app = Boot();
            yield return null;
            try
            {
                app.NewGame(true);
                app.SetSpeed(0);
                if (!app.SoundEnabled)
                    app.ToggleSound();
                yield return null;
                yield return null;
                var sfx = app.Sfx;
                Assert.That(sfx.Siren.clip, Is.Not.Null, "Render the siren with python3 Tools/compose_sfx.py --only siren_loop");
                var traffic = app.World.Traffic;
                var police = traffic.transform.Cast<Transform>().FirstOrDefault(v => v.name == "Police car");
                Assert.That(police, Is.Not.Null, "The demo's traffic includes a police car");
                // On an ordinary trip a police car drives with its siren off, even zoomed all the way in.
                traffic.Emergency.Dispatch(police, false);
                app.Camera.zoom = CameraController.MinZoom;
                for (float waited = 0; waited < .4f; waited += Time.unscaledDeltaTime)
                {
                    app.Camera.focus = police.position;
                    yield return null;
                }
                Assert.That(sfx.Siren.isPlaying, Is.False, "Off a call: no siren");
                // Out on a call, short of the closest zoom, it stays quiet even in the middle of the screen.
                traffic.Emergency.Dispatch(police, true);
                Assert.That(traffic.Emergency.Sirens, Does.Contain(police));
                foreach (float zoom in new[] { 29f, 8f, CameraController.MinZoom + 2 })
                {
                    app.Camera.zoom = zoom;
                    for (float waited = 0; waited < .4f; waited += Time.unscaledDeltaTime)
                    {
                        app.Camera.focus = police.position;
                        yield return null;
                    }
                    Assert.That(sfx.Siren.isPlaying, Is.False, $"At zoom {zoom}: no siren");
                }
                // Zoomed all the way in on it, the siren wails, even while the game is paused.
                app.Camera.zoom = CameraController.MinZoom;
                for (float waited = 0; waited < 3 && sfx.Siren.volume < .5f; waited += Time.unscaledDeltaTime)
                {
                    app.Camera.focus = police.position;
                    yield return null;
                }
                Assert.That(sfx.Siren.isPlaying, Is.True, "Zoomed in on a police car: its siren sounds");
                Assert.That(sfx.Siren.volume, Is.GreaterThan(.5f), "Loud with the police car in the middle of the screen");
                // Zooming back out even a little fades it away.
                app.Camera.zoom = 8;
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(sfx.Siren.isPlaying, Is.False, "Zoomed out again: the siren fades out");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                PlayerPrefs.SetInt("sound", savedPreference);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator OnlyOneTripInTwentyIsAnEmergencyCall()
        {
            var app = Boot();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            var lights = new EmergencyLights();
            var vehicle = new GameObject("Police car").transform;
            lights.Fit(app.World, vehicle, VehicleCatalog.Models.First(m => m.name == "Police car"));
            // Eleven hours of driving is several hundred trips; the random stream is seeded, so the share is exact.
            int seconds = 40000, onCall = 0, calls = 0;
            bool before = lights.Sirens.Count > 0;
            for (int s = 0; s < seconds; s++)
            {
                lights.Drive(1);
                bool now = lights.Sirens.Count > 0;
                if (now)
                    onCall++;
                if (now && !before)
                    calls++;
                before = now;
            }
            Assert.That(calls, Is.GreaterThan(10), "Calls come up now and then");
            Assert.That(onCall / (float)seconds, Is.InRange(.03f, .07f), "About one trip in twenty is a call, siren on");
            Object.Destroy(vehicle.gameObject);
            yield return null;
            lights.Drive(1);
            Assert.That(lights.Sirens, Is.Empty, "A vehicle that left the road takes its siren with it");
            lights.Release();
            LogAssert.NoUnexpectedReceived();
        }

        // Renders close-ups for review: all thirteen vehicles side by side, the emergency vehicles lit, and live town traffic.
        [UnityTest, Explicit("Writes review renders to Logs; run it by name.")]
        public IEnumerator GalleryShowsEveryVehicleUpClose()
        {
            var app = Boot();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            var root = new GameObject("Vehicle gallery").transform;
            root.position = new Vector3(2000, 0, 2000);
            app.World.Box("Gallery ground", new Vector3(0, -.02f, 0), new Vector3(6, .04f, 5), new Color(.39f, .55f, .28f), root);
            app.World.Box("Gallery road", new Vector3(0, -.003f, 0), new Vector3(3.2f, .01f, 2.9f), new Color(.3f, .3f, .32f), root);
            for (int i = 0; i < VehicleCatalog.Count; i++)
            {
                var vehicle = new GameObject(VehicleCatalog.Models[i].name).transform;
                vehicle.SetParent(root, false);
                vehicle.localPosition = new Vector3((i % 5 - 2) * .55f, .002f, (1 - i / 5) * .8f);
                // Offset liveries so the cars do not all show their first (white) paint.
                foreach (var part in VehicleCatalog.Models[i].Parts(i + 3))
                    app.World.Box("Part", part.center, part.size, part.color, vehicle);
                // Light the first beat's beacons, as a frame of the flashing would.
                foreach (var beacon in VehicleCatalog.Models[i].beacons.Where(b => b.beat == 0))
                    app.World.Box("Beacon", beacon.center, beacon.size + Vector3.one * .016f, beacon.flash, vehicle);
            }
            yield return null;
            Shoot(root.position + new Vector3(2.4f, 2.1f, 2.6f), root.position + new Vector3(0, .1f, 0), "Logs/vehicle-gallery.png", 38);
            Shoot(root.position + new Vector3(-2.4f, 1.6f, -2.6f), root.position + new Vector3(0, .1f, 0), "Logs/vehicle-gallery-backs.png", 38);
            // The emergency vehicles fill the back row's first three places, x -1.1 .. 0.
            Shoot(root.position + new Vector3(.9f, .95f, -2.2f), root.position + new Vector3(-.55f, .1f, -.8f), "Logs/vehicle-emergency.png", 40);
            Object.Destroy(root.gameObject);
            yield return null;

            app.SetSpeed(1);
            for (int frame = 0; frame < 60; frame++)
                yield return null;
            app.SetSpeed(0);
            var vehicles = Object.FindAnyObjectByType<CityTraffic>().transform.Cast<Transform>().Where(t => t.GetComponent<MeshRenderer>()).ToList();
            var subject = vehicles.FirstOrDefault(t => t.name == "Police car") ?? vehicles.FirstOrDefault(t => t.name.EndsWith("bus")) ?? vehicles.First();
            app.Camera.focus = subject.position;
            app.Camera.zoom = 5;
            for (int frame = 0; frame < 10; frame++)
                yield return null;
            var camera = app.Camera.view;
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            camera.Render();
            Save(texture, "Logs/vehicle-traffic.png");
            camera.targetTexture = null;
            Object.Destroy(texture);
            LogAssert.NoUnexpectedReceived();
        }

        static void Shoot(Vector3 from, Vector3 at, string file, float fieldOfView)
        {
            var go = new GameObject("Gallery camera");
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.62f, .74f, .8f);
            go.transform.position = from;
            go.transform.LookAt(at);
            var texture = new RenderTexture(2400, 1350, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            camera.Render();
            Save(texture, file);
            camera.targetTexture = null;
            Object.Destroy(texture);
            Object.Destroy(go);
        }

        static void Save(RenderTexture texture, string file)
        {
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(file, image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
