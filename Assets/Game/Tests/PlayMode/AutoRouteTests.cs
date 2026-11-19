using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace ValleyRail.Tests
{
    /// <summary>AUTO ROUTE: a new game asks where the line starts and ends, and one tap builds stations, track and a running train.</summary>
    public class AutoRouteTests
    {
        GameBootstrap app;
        Transform Safe => app.UI.transform.Find("Safe area");
        Transform Details => Safe.Find("Context/Scrollable details");
        Button Find(string prefix)
        {
            foreach (var b in Details.GetComponentsInChildren<Button>(false))
                if (b.name.StartsWith(prefix))
                    return b;
            return null;
        }
        IEnumerator NewGame()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewGameAsksWhereAndBuildsTheWholeLine()
        {
            yield return NewGame();
            // NEW GAME on the title screen sets this; the window opens once the HUD is up.
            app.OfferAutoRoute = true;
            yield return null;
            Assert.That(Safe.Find("Context").gameObject.activeInHierarchy, Is.True, "the AUTO ROUTE window pops up");
            Assert.That(Find("SUGGESTED"), Is.Not.Null, "the tutorial's line is offered in one tap");
            var from = Find("PINECREST MINE");
            Assert.That(from, Is.Not.Null, "the mine is offered as a start");
            from.onClick.Invoke();
            yield return null;
            Assert.That(Find("EASTBANK POWER"), Is.Not.Null, "the power station takes coal");
            Assert.That(Find("WILLOWBROOK"), Is.Null, "towns do not take coal");
            Find("EASTBANK POWER").onClick.Invoke();
            yield return null;
            var build = Find("BUILD IT");
            Assert.That(build, Is.Not.Null);
            Assert.That(build.interactable, Is.True, "the first line is affordable");
            build.onClick.Invoke();
            yield return null;
            var w = app.Game.World;
            Assert.That(w.stations.Count, Is.EqualTo(2));
            Assert.That(w.trains.Count, Is.EqualTo(1));
            Assert.That(w.trains[0].a, Is.Not.EqualTo(0), "the train starts its route");
            Assert.That(Safe.Find("Context").gameObject.activeInHierarchy, Is.False, "the window closes once built");
            Assert.That(Safe.Find("Tools/AUTO ROUTE").gameObject.activeInHierarchy, Is.True, "AUTO ROUTE stays on the bottom bar");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator LightRailWaitsForTheFirstDelivery()
        {
            yield return NewGame();
            app.ChooseTool(ToolMode.LightRail);
            Assert.That(app.Mode, Is.Not.EqualTo(ToolMode.LightRail));
            Assert.That(app.Notice, Does.Contain("first delivery"));
            app.Game.World.delivered = 1;
            app.ChooseTool(ToolMode.LightRail);
            Assert.That(app.Mode, Is.EqualTo(ToolMode.LightRail));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
