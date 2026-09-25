using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>
    /// Renders a close-up of every building definition to Logs/gallery/NN-key.png for art review. Explicit: run it with
    /// -testFilter ValleyRail.Tests.BuildingGalleryTests; normal PlayMode runs skip it.
    /// </summary>
    [Explicit("Art capture tool; run on demand.")]
    public class BuildingGalleryTests
    {
        const int Pixels = 720;
        [UnityTest]
        public IEnumerator RenderEveryBuilding()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var city = app.Game.World.cities[0];
            // Open valley floor well clear of the plaza and industries, with room for the 8×8 stadium.
            var lot = new Cell(city.center.x - 14, city.center.z - 12);
            var camera = app.Camera.view;
            Directory.CreateDirectory("Logs/gallery");
            for (int def = 0; def < BuildingCatalog.Defaults.Length; def++)
            {
                var d = BuildingCatalog.Get(def);
                int size = d.size;
                // A corner lot: streets along the south and west, so the building faces the camera and no tree hides it.
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
                foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                    label.gameObject.SetActive(false);
                yield return null;
                app.Camera.enabled = false;
                float height = d.height / 100f * (d.level == 4 ? 1.2f : 1f) + (d.level == 4 ? 1.2f : .5f);
                var target = new Vector3(lot.x + (size - 1) / 2f, height * .42f, lot.z + (size - 1) / 2f);
                camera.orthographicSize = Mathf.Max(.95f, size * .78f + .2f, height * .55f + .25f);
                camera.transform.position = target - camera.transform.forward * 180;
                yield return Capture(camera, $"Logs/gallery/{def:D2}-{d.key}.png");
            }
            app.Camera.enabled = true;
            Assert.That(Directory.GetFiles("Logs/gallery", "*.png").Length, Is.GreaterThanOrEqualTo(BuildingCatalog.Defaults.Length));
        }
        [UnityTest]
        public IEnumerator RenderTownCloseUps()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            // A grown town with details and life, and one new building going up as a construction site.
            var city = app.Game.World.cities[0];
            city.population = 6000;
            app.Game.Cities.Relayout();
            app.Game.World.cityRevision++;
            app.World.Refresh();
            app.SetSpeed(1);
            city.buildings.Add(new BuildingState { cell = new Cell(city.center.x + 9, city.center.z + 1), def = BuildingCatalog.Index(BuildingCategory.Residential, 3) });
            app.Game.World.cityRevision++;
            app.World.Refresh();
            for (int i = 0; i < 90; i++)
                yield return null;
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            var shots = new[] { new Vector3(city.center.x, 0, city.center.z), new Vector3(city.center.x + 7, 0, city.center.z + 1), new Vector3(city.center.x - 5, 0, city.center.z - 4) };
            for (int shot = 0; shot < shots.Length; shot++)
            {
                app.Camera.focus = shots[shot];
                app.Camera.zoom = 7;
                yield return null;
                app.Camera.enabled = false;
                camera.orthographicSize = 3.2f;
                camera.transform.position = shots[shot] - camera.transform.forward * 180;
                yield return CaptureWide(camera, $"Logs/gallery/town-{shot + 1}.png");
                app.Camera.enabled = true;
            }
            Assert.That(app.World.GetComponentInChildren<CityLife>(true).Walkers, Is.GreaterThan(0));
        }
        static IEnumerator CaptureWide(Camera camera, string filename)
        {
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
            File.WriteAllBytes(filename, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(texture);
            Object.Destroy(image);
        }
        static IEnumerator Capture(Camera camera, string filename)
        {
            camera.aspect = 1;
            var texture = new RenderTexture(Pixels, Pixels, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            for (int frame = 0; frame < 3; frame++)
                yield return null;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(Pixels, Pixels, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Pixels, Pixels), 0, 0);
            image.Apply();
            File.WriteAllBytes(filename, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(texture);
            Object.Destroy(image);
        }
    }
}
