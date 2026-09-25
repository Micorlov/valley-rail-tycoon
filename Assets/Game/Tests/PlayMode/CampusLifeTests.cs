using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>A university quad is never empty: students walk its paths, chat on the lawn and read on the grass and benches.</summary>
    public class CampusLifeTests
    {
        const float Step = .05f;
        GameBootstrap app;
        Cell lot;
        int size;

        /// <summary>A new game with the town cleared down to one university on a corner lot.</summary>
        IEnumerator BuildCampus()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var city = app.Game.World.cities[0];
            lot = new Cell(city.center.x - 12, city.center.z - 10);
            int def = System.Array.FindIndex(BuildingCatalog.Defaults, d => d.key == "university");
            Assert.That(def, Is.GreaterThanOrEqualTo(0), "university is in the catalog");
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

        [UnityTest]
        public IEnumerator CampusQuadIsAlwaysFullOfStudents()
        {
            yield return BuildCampus();
            var life = app.World.GetComponentInChildren<CityLife>(true);
            Assert.That(life.Campuses, Is.EqualTo(1));
            Assert.That(life.Students, Is.GreaterThanOrEqualTo(20), "the quad is busy");
            var campus = life.transform.Find("Campus");
            Assert.That(campus, Is.Not.Null);
            Assert.That(campus.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
            var walkers = campus.Cast<Transform>().Where(t => t.name == "Student" && t.Find("Backpack")).ToArray();
            Assert.That(walkers.Length, Is.EqualTo(7));
            var start = walkers.Select(t => t.localPosition).ToArray();
            float span = size - .12f;
            // Five game minutes: everyone keeps to the quad and clear of the fountain and the statue on the middle path.
            for (int i = 0; i < 6000; i++)
            {
                life.Animate(Step, 1, true);
                if (i % 20 != 0)
                    continue;
                foreach (Transform person in campus)
                {
                    if (!person.name.Contains("tudent"))
                        continue;
                    var p = person.localPosition;
                    Assert.That(Mathf.Abs(p.x), Is.LessThan(span * .28f), $"{person.name} stays between the wings");
                    Assert.That(p.z, Is.InRange(-span * .27f, span * .47f), $"{person.name} stays between the main hall and the gate");
                    // The basin is .5 across and the plinth .18; the margin covers half a student's width.
                    const float Clear = .03f;
                    bool inFountain = Mathf.Abs(p.x) < .25f + Clear && Mathf.Abs(p.z + span * .08f) < .25f + Clear;
                    bool onStatue = Mathf.Abs(p.x) < .09f + Clear && Mathf.Abs(p.z - span * .12f) < .09f + Clear;
                    Assert.That(inFountain || onStatue, Is.False, $"{person.name} at {p} walks through the fountain or the statue");
                }
            }
            Assert.That(walkers.Select((t, i) => Vector3.Distance(t.localPosition, start[i])).Count(d => d > .05f), Is.GreaterThanOrEqualTo(5), "students walk about");
            Assert.That(life.Students, Is.GreaterThanOrEqualTo(20), "nobody leaves for good");
            // Zoomed out, the campus hides with the rest of town life; zooming back in shows the same students.
            life.Animate(Step, 1, false);
            Assert.That(life.gameObject.activeSelf, Is.False);
            life.Animate(Step, 1, true);
            Assert.That(life.gameObject.activeSelf, Is.True);
            // A town redraw rebuilds the campus with its students.
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(life.Campuses, Is.EqualTo(1));
            Assert.That(life.Students, Is.GreaterThanOrEqualTo(20));
        }

        /// <summary>Renders the quad for review: Logs/campus-wide.png and Logs/campus-quad.png.</summary>
        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowCampus()
        {
            yield return BuildCampus();
            var life = app.World.GetComponentInChildren<CityLife>(true);
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            app.Camera.enabled = false;
            for (int i = 0; i < 400; i++)
                life.Animate(Step, 1, true);
            var centre = new Vector3(lot.x + (size - 1) / 2f, .3f, lot.z + (size - 1) / 2f);
            yield return Shoot(camera, centre, 4.4f, "Logs/campus-wide.png");
            yield return Shoot(camera, centre, 2.2f, "Logs/campus-quad.png");
            app.Camera.enabled = true;
            Assert.That(File.Exists("Logs/campus-quad.png"));
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
