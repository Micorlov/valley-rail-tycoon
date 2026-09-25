using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace ValleyRail.Tests
{
    /// <summary>The play HUD keeps the map clear: five corner controls, a build tray on demand and short-lived notices.</summary>
    public class HudTests
    {
        static GameBootstrap App()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }
        static Transform Safe(GameBootstrap app) => app.UI.transform.Find("Safe area");
        static bool Visible(GameBootstrap app, string path)
        {
            var target = Safe(app).Find(path);
            return target && target.gameObject.activeInHierarchy;
        }
        static void Press(GameBootstrap app, string path)
        {
            var target = Safe(app).Find(path);
            Assert.That(target, Is.Not.Null, path + " exists");
            Assert.That(target.gameObject.activeInHierarchy, Is.True, path + " is visible before it is pressed");
            target.GetComponent<Button>().onClick.Invoke();
        }
        [UnityTest]
        public IEnumerator CompactHudShowsOnlySevenControls()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            yield return null;
            var names = new List<string>();
            foreach (var button in Safe(app).GetComponentsInChildren<Button>(false))
                if (!button.transform.IsChildOf(Safe(app).Find("Context")) && !button.transform.IsChildOf(Safe(app).Find("Menu")))
                    names.Add(button.name);
            names.Sort();
            Assert.That(names, Is.EqualTo(new[] { "BUILD", "MENU", "PAUSE", "ROTATE", "SPEED", "STATIONS", "TRAINS" }));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator BuildTrayOpensChoosesToolAndCollapses()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            yield return null;
            Press(app, "Tools/BUILD");
            yield return null;
            Assert.That(Visible(app, "Tools/TRACK") && Visible(app, "Tools/STATION") && Visible(app, "Tools/BULLDOZE") && Visible(app, "Tools/LIGHT RAIL"), Is.True, "tray shows the build tools");
            Assert.That(Visible(app, "Tools/TRAINS") || Visible(app, "Tools/STATIONS"), Is.False, "tray replaces the idle buttons");
            Press(app, "Tools/TRACK");
            yield return null;
            Assert.That(app.Mode, Is.EqualTo(ToolMode.Track));
            Assert.That(Visible(app, "Tools/TRACK"), Is.False, "tray collapses after a tool is chosen");
            Assert.That(Visible(app, "Tools/ACTIVE TOOL") && Visible(app, "Tools/DONE"), Is.True, "active tool chip and exit are shown");
            Press(app, "Tools/DONE");
            yield return null;
            Assert.That(app.Mode, Is.EqualTo(ToolMode.Browse));
            Assert.That(Visible(app, "Tools/STATIONS") && Visible(app, "Tools/TRAINS") && Visible(app, "Tools/BUILD"), Is.True);
            Assert.That(Visible(app, "Tools/DONE"), Is.False);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator PauseAndSpeedButtonsControlSimulation()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            yield return null;
            Assert.That(app.Game.World.speed, Is.EqualTo(1));
            Press(app, "Controls/PAUSE");
            Assert.That(app.Game.World.speed, Is.Zero);
            Press(app, "Controls/PAUSE");
            Assert.That(app.Game.World.speed, Is.EqualTo(1), "resume restores the previous speed");
            Press(app, "Controls/SPEED");
            Assert.That(app.Game.World.speed, Is.EqualTo(2));
            Press(app, "Controls/SPEED");
            Assert.That(app.Game.World.speed, Is.EqualTo(4));
            Press(app, "Controls/SPEED");
            Assert.That(app.Game.World.speed, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ToolsHideWhileDetailsPanelIsOpen()
        {
            var app = App();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            app.UI.Station(app.Game.World.stations[0]);
            yield return null;
            Assert.That(Visible(app, "Tools"), Is.False, "the details panel covers the bottom-right corner");
            Press(app, "Context/Scrollable details/CLOSE");
            yield return null;
            Assert.That(Visible(app, "Tools"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator NoticeToastFadesAfterDelay()
        {
            var app = App();
            yield return null;
            app.NewGame(true);
            // Paused: no ticks, so no town news can replace the notice under test.
            app.SetSpeed(0);
            app.Notice = "Toast test";
            yield return null;
            Assert.That(Visible(app, "Guidance"), Is.True);
            Assert.That(Safe(app).Find("Guidance").GetComponentInChildren<TMPro.TMP_Text>().text, Does.Contain("Toast test"));
            yield return new WaitForSecondsRealtime(GameplayPresenter.NoticeSeconds + .8f);
            Assert.That(Visible(app, "Guidance"), Is.False, "the notice disappears on its own");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator RotateButtonTurnsViewAndLabelsAndIsSaved()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            Assert.That(app.Camera.Turn, Is.Zero);
            Press(app, "View/ROTATE");
            Assert.That(app.Camera.Turn, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(app.Camera.view.transform.eulerAngles.y, Is.EqualTo(135).Within(.5f), "camera finished a quarter turn");
            var label = app.World.GetComponentInChildren<TMPro.TextMeshPro>();
            Assert.That(label.transform.eulerAngles.y, Is.EqualTo(135).Within(.5f), "world labels keep facing the camera");
            Press(app, "View/ROTATE");
            app.Save(false);
            app.Load(false);
            yield return null;
            Assert.That(app.Camera.Turn, Is.EqualTo(2), "the view direction is saved");
            Assert.That(app.Camera.view.transform.eulerAngles.y, Is.EqualTo(225).Within(.5f), "a loaded game starts facing the saved direction");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator TitleScreenHidesPlayHud()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            yield return null;
            app.Title();
            yield return null;
            Assert.That(Visible(app, "HUD") || Visible(app, "Controls") || Visible(app, "Tools") || Visible(app, "View") || Visible(app, "Guidance"), Is.False);
            Assert.That(Visible(app, "Menu"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
