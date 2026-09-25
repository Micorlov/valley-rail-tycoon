using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>Settlers found a new village in the running game: it is announced, drawn, signed and grows like any town.</summary>
    public class FoundingTests
    {
        [UnityTest]
        public IEnumerator SettlersFoundAVillageThatIsDrawnSignedAndGrows()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var w = app.Game.World;
            int towns = w.cities.Count;
            // Due at once: no population needed, and the founding check runs on the step that reaches tick 12660.
            app.Game.Balance.city.foundingPopulation = 0;
            w.tick = 12659;
            app.Game.Step();
            yield return null;
            yield return null;
            Assert.That(w.cities.Count, Is.EqualTo(towns + 1), "settlers founded a town");
            var town = w.cities[w.cities.Count - 1];
            Assert.That(town.founded, Is.EqualTo(w.tick));
            Assert.That(app.Notice, Does.StartWith("Settlers founded " + town.name + ", a new village"));
            var sign = app.World.TownSignFor(town);
            Assert.That(sign, Is.Not.Null, town.name + " has a sign");
            bool spelled = false;
            foreach (var text in sign.GetComponentsInChildren<TextMeshPro>(true))
                spelled |= text.text == town.name.ToUpperInvariant();
            Assert.That(spelled, Is.True, town.name + " sign spells the new name");
            Assert.That(app.Game.Stations.Nearby(new Cell(town.center.x, town.center.z + 3), 1).Exists(p => p.id == town.producerId), Is.True,
                "a station beside the village serves it");
            yield return Render(app, town, "Logs/founding-village.png");
            // A few minutes of growth later the village has spread along its streets.
            app.Game.Balance.city.basePoints = 400;
            int buildings = town.buildings.Count;
            for (int i = 0; i < 6000; i++)
                app.Game.Step();
            yield return null;
            yield return null;
            Assert.That(town.buildings.Count, Is.GreaterThan(buildings), town.name + " grows");
            SaveService.Validate(w, app.Game.Balance);
            yield return Render(app, town, "Logs/founding-village-grown.png");
        }
        static IEnumerator Render(GameBootstrap app, CityState town, string filename)
        {
            var camera = app.Camera.view;
            var focus = new Vector3(town.center.x, 0, town.center.z);
            app.Camera.focus = focus;
            app.Camera.zoom = 9;
            yield return null;
            app.Camera.enabled = false;
            camera.orthographicSize = 7f;
            camera.transform.position = focus - camera.transform.forward * 180;
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
            app.Camera.enabled = true;
        }
    }
}
