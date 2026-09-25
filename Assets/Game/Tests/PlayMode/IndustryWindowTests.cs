using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>Industries carry no floating caption; a tap on one opens a window saying what it takes in and ships out.</summary>
    public class IndustryWindowTests
    {
        static GameBootstrap App()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }
        static string PanelText(GameBootstrap app)
        {
            var body = app.UI.transform.Find("Safe area/Context/Scrollable details");
            if (!body || !body.gameObject.activeInHierarchy)
                return "";
            var text = "";
            foreach (var t in body.GetComponentsInChildren<TMP_Text>())
                text += t.text + "\n";
            return text;
        }
        [UnityTest]
        public IEnumerator IndustriesHaveNoFloatingCaption()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            foreach (var label in app.World.GetComponentsInChildren<TextMeshPro>(true))
            {
                Assert.That(label.text, Does.Not.Contain(" > "), "no input > output caption over industries");
                foreach (var p in app.Game.World.producers)
                    if (p.kind != ProducerKind.Town)
                        Assert.That(label.text, Is.Not.EqualTo(p.name.ToUpperInvariant()), p.name + " has no floating name");
            }
        }
        [UnityTest]
        public IEnumerator TappingTheSteelMillOpensWhatItTakesAndShips()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            var mill = app.Game.World.producers.Find(p => p.kind == ProducerKind.SteelMill);
            app.Camera.focus = new Vector3(mill.cell.x, 0, mill.cell.z);
            app.Camera.zoom = 12;
            yield return null;
            yield return null;
            // Aim at the furnaces: the ray meets the ground well behind the yard, where a ground lookup alone misses the mill.
            var view = app.Camera.view;
            var ray = view.ScreenPointToRay(view.WorldToScreenPoint(new Vector3(mill.cell.x + .5f, 2f, mill.cell.z + .5f)));
            float t = -ray.origin.y / ray.direction.y;
            var ground = ray.GetPoint(t);
            var cell = new Cell(Mathf.RoundToInt(ground.x), Mathf.RoundToInt(ground.z));
            Assert.That(Mathf.Max(Mathf.Abs(cell.x - mill.cell.x), Mathf.Abs(cell.z - mill.cell.z)), Is.GreaterThan(1), "the ground under the finger is outside the mill's yard");
            app.Tap(cell, ray);
            yield return null;
            var text = PanelText(app);
            Assert.That(text, Does.Contain(mill.name), "the window is titled with the mill's name");
            Assert.That(text, Does.Contain("TAKES IN").And.Contain("Iron ore").And.Contain("Highland Iron Mine"));
            Assert.That(text, Does.Contain("SHIPS OUT").And.Contain("Steel").And.Contain("Valley Works"));
            app.UI.UpdateHud();
            Assert.That(PanelText(app), Does.Contain("TAKES IN"), "the live refresh keeps the same window");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
