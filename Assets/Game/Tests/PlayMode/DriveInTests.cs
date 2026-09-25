using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>A drive-in cinema plays its programme on a screen facing the camera, with cars coming and going between shows.</summary>
    public class DriveInTests
    {
        const float Step = .05f;
        static readonly string[] NotCars = { "Screen", "Projector beam", "Visitor" };
        GameBootstrap app;
        Cell lot;
        int size;

        /// <summary>A new game with the town cleared down to one drive-in on a corner lot.</summary>
        IEnumerator BuildDriveIn()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var city = app.Game.World.cities[0];
            lot = new Cell(city.center.x - 10, city.center.z - 9);
            int def = System.Array.FindIndex(BuildingCatalog.Defaults, d => d.key == "drive-in");
            Assert.That(def, Is.GreaterThanOrEqualTo(0), "drive-in is in the catalog");
            size = BuildingCatalog.Size(def);
            city.buildings.Clear();
            city.roads.Clear();
            city.roads.Add(new RoadState { cell = city.center });
            for (int x = lot.x - 1; x <= lot.x + size; x++)
                city.roads.Add(new RoadState { cell = new Cell(x, lot.z - 1) });
            for (int z = lot.z; z < lot.z + size; z++)
                city.roads.Add(new RoadState { cell = new Cell(lot.x - 1, z) });
            city.buildings.Add(new BuildingState { cell = lot, def = def });
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
        }
        DriveInCinema Cinemas => app.World.GetComponentInChildren<DriveInCinema>(true);
        Vector3 View => app.Camera.view.transform.forward;
        static List<Transform> Cars(Transform root) => root.Cast<Transform>().Where(t => t.gameObject.activeSelf && !NotCars.Contains(t.name)).ToList();

        [UnityTest]
        public IEnumerator ScreenPlaysTheProgrammeToAFullLot()
        {
            yield return BuildDriveIn();
            var cinemas = Cinemas;
            cinemas.Animate(Step, false, true, View);
            Assert.That(cinemas.Sites, Is.EqualTo(1));
            var layout = cinemas.LayoutAt(0);
            Assert.That(layout.bays.Count, Is.InRange(30, 40), "four curved rows of bays");
            Assert.That(cinemas.TrafficAt(0).Parked, Is.GreaterThan(layout.bays.Count * .7f), "a new drive-in opens mid-show, nearly full");
            Assert.That(Cars(cinemas.RootAt(0)).Count, Is.EqualTo(cinemas.TrafficAt(0).Parked), "every parked car has a body");
            var screen = cinemas.ScreenAt(0).GetComponent<MeshRenderer>();
            Assert.That(screen.sharedMaterial.mainTexture, Is.SameAs(cinemas.Picture));
            int colours = cinemas.Picture.GetPixels32().Select(c => c.r << 16 | c.g << 8 | c.b).Distinct().Count();
            Assert.That(colours, Is.GreaterThan(20), "the screen shows a picture, not a blank");
            var before = cinemas.Picture.GetPixels32();
            for (int i = 0; i < 20; i++)
                cinemas.Animate(Step, false, true, View);
            int changed = cinemas.Picture.GetPixels32().Zip(before, (a, b) => a.r != b.r || a.g != b.g || a.b != b.b).Count(x => x);
            Assert.That(changed, Is.GreaterThan(500), "the film moves");
            Assert.That(DriveInFilm.ReelAt(cinemas.Clock, out _), Is.EqualTo(DriveInFilm.Reel.Western), "the first show is already running");
            Assert.That(cinemas.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
            foreach (var bay in layout.bays)
                Assert.That(Vector3.Angle(bay.Facing * Vector3.forward, layout.focus - bay.at), Is.LessThan(1f), "every bay points at the screen");
        }

        [UnityTest]
        public IEnumerator ScreenTurnsToFaceTheCamera()
        {
            yield return BuildDriveIn();
            var cinemas = Cinemas;
            var view = View;
            var art = app.World.transform.Find("Cities").Cast<Transform>().First(t => t.name == "Drive-in");
            for (int quarter = 0; quarter < 4; quarter++)
            {
                var turned = Quaternion.Euler(0, 90 * quarter, 0) * view;
                cinemas.Animate(Step, true, true, turned);
                var normal = cinemas.ScreenAt(0).rotation * Vector3.forward;
                Assert.That(Vector3.Dot(normal, -new Vector3(turned.x, 0, turned.z).normalized), Is.GreaterThan(.69f), $"screen faces the camera after {quarter} quarter turns");
                Assert.That(Quaternion.Angle(art.rotation, cinemas.RootAt(0).rotation), Is.LessThan(.5f), "the lot turns with its screen");
            }
        }

        [UnityTest]
        public IEnumerator CarsLeaveAfterTheShowAndTheNextAudienceParks()
        {
            yield return BuildDriveIn();
            var cinemas = Cinemas;
            var view = View;
            cinemas.Animate(Step, false, true, view);
            var traffic = cinemas.TrafficAt(0);
            int bays = cinemas.LayoutAt(0).bays.Count, full = traffic.Parked;
            cinemas.Clock = DriveInFilm.Start(DriveInFilm.Reel.Goodnight) - Step / 2;
            int fewest = full, most = 0;
            float closest = float.MaxValue;
            var root = cinemas.RootAt(0);
            for (int i = 0; i < 1800; i++)
            {
                cinemas.Animate(Step, false, true, view);
                fewest = Mathf.Min(fewest, traffic.Parked);
                most = Mathf.Max(most, traffic.Moving);
                closest = Mathf.Min(closest, Closest(traffic));
                Assert.That(Cars(root).Count, Is.EqualTo(traffic.Cars.Count), "one body per car");
            }
            Debug.Log($"Drive-in: {full} parked of {bays}, fewest {fewest}, up to {most} moving at once, closest cars {closest:0.000}");
            Assert.That(fewest, Is.LessThan(full * .7f), "about half the audience drives home after the show");
            Assert.That(most, Is.GreaterThan(2), "cars queue in and out");
            Assert.That(traffic.Moving + traffic.Expected, Is.Zero, "everyone has parked or gone home a minute and a half later");
            Assert.That(traffic.Parked, Is.GreaterThan(bays * .8f), "the next audience fills the lot again");
            Assert.That(closest, Is.GreaterThan(.22f), "cars on the move keep their distance");
            var layout = cinemas.LayoutAt(0);
            foreach (var car in Cars(root))
            {
                var bay = layout.bays.OrderBy(b => (b.at - car.localPosition).sqrMagnitude).First();
                Assert.That(Vector3.Distance(bay.at, car.localPosition), Is.LessThan(.05f), car.name + " stands in a bay");
            }
        }
        /// <summary>The smallest distance on the ground between two cars that are both driving.</summary>
        static float Closest(DriveInTraffic traffic)
        {
            float best = float.MaxValue;
            var cars = traffic.Cars;
            for (int i = 0; i < cars.Count; i++)
                for (int j = i + 1; j < cars.Count; j++)
                {
                    if (!cars[i].Driving || !cars[j].Driving)
                        continue;
                    var gap = cars[i].at - cars[j].at;
                    gap.y = 0;
                    best = Mathf.Min(best, gap.magnitude);
                }
            return best;
        }

        [UnityTest]
        public IEnumerator IntermissionSendsPeopleToTheSnackBar()
        {
            yield return BuildDriveIn();
            var cinemas = Cinemas;
            var view = View;
            cinemas.Animate(Step, false, true, view);
            cinemas.Clock = DriveInFilm.Start(DriveInFilm.Reel.Intermission) - Step / 2;
            cinemas.Animate(Step, false, true, view);
            Assert.That(cinemas.TrafficAt(0).Walkers.Count, Is.EqualTo(6), "six drivers head for the snack bar");
            for (int i = 0; i < 700; i++)
                cinemas.Animate(Step, false, true, view);
            Assert.That(cinemas.TrafficAt(0).Walkers.Count, Is.Zero, "and are back in their cars before the second film");
            yield return null; // Destroy takes effect at the end of the frame
            Assert.That(cinemas.RootAt(0).Cast<Transform>().Count(t => t.name == "Visitor"), Is.Zero, "their bodies are cleared away");
        }

        [Test]
        public void ProgrammeRunsInOrder()
        {
            var order = new[] { DriveInFilm.Reel.Welcome, DriveInFilm.Reel.Leader, DriveInFilm.Reel.Western, DriveInFilm.Reel.Intermission, DriveInFilm.Reel.SecondLeader, DriveInFilm.Reel.SciFi, DriveInFilm.Reel.Goodnight };
            float at = 0;
            foreach (var reel in order)
            {
                Assert.That(DriveInFilm.Start(reel), Is.EqualTo(at).Within(1e-3f));
                Assert.That(DriveInFilm.ReelAt(at + .01f, out _), Is.EqualTo(reel));
                at += DriveInFilm.LengthOf(reel);
            }
            Assert.That(at, Is.EqualTo(DriveInFilm.Length).Within(1e-3f));
            Assert.That(DriveInFilm.ReelAt(DriveInFilm.Length + .01f, out _), Is.EqualTo(DriveInFilm.Reel.Welcome), "and round again");
        }

        /// <summary>Renders the drive-in for review: Logs/drivein-wide, -screen, -leaving, -arrivals, -snacks and -far-side.png.</summary>
        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowDriveIn()
        {
            yield return BuildDriveIn();
            var cinemas = Cinemas;
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            app.Camera.enabled = false;
            var centre = new Vector3(lot.x + (size - 1) / 2f, .5f, lot.z + (size - 1) / 2f);
            cinemas.Animate(Step, false, true, camera.transform.forward);
            yield return Shoot(camera, centre, 4.2f, "Logs/drivein-wide.png");
            yield return Shoot(camera, cinemas.ScreenAt(0).position, 1.8f, "Logs/drivein-screen.png");
            cinemas.Clock = DriveInFilm.Start(DriveInFilm.Reel.Goodnight) - Step / 2;
            for (int i = 0; i < 380; i++)
                cinemas.Animate(Step, false, true, camera.transform.forward);
            yield return Shoot(camera, centre, 3.4f, "Logs/drivein-leaving.png");
            for (int i = 0; i < 500; i++)
                cinemas.Animate(Step, false, true, camera.transform.forward);
            yield return Shoot(camera, centre, 3.4f, "Logs/drivein-arrivals.png");
            cinemas.Clock = DriveInFilm.Start(DriveInFilm.Reel.Intermission) - Step / 2;
            for (int i = 0; i < 120; i++)
                cinemas.Animate(Step, false, true, camera.transform.forward);
            yield return Shoot(camera, cinemas.RootAt(0).TransformPoint(cinemas.LayoutAt(0).snackBar), 1.6f, "Logs/drivein-snacks.png");
            cinemas.Clock = DriveInFilm.Start(DriveInFilm.Reel.SciFi) + 12;
            camera.transform.rotation = Quaternion.Euler(35.264f, 225, 0);
            cinemas.Animate(Step, false, true, camera.transform.forward);
            yield return Shoot(camera, centre, 4.2f, "Logs/drivein-far-side.png");
            app.Camera.enabled = true;
            Assert.That(File.Exists("Logs/drivein-far-side.png"));
        }
        static IEnumerator Shoot(Camera camera, Vector3 target, float zoom, string file)
        {
            camera.orthographicSize = zoom;
            camera.transform.position = target - camera.transform.forward * 180;
            camera.aspect = 16f / 9;
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            for (int frame = 0; frame < 3; frame++)
                yield return null;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            File.WriteAllBytes(file, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(texture);
            Object.Destroy(image);
        }
    }
}
