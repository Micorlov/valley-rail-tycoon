using System.Linq;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>The town window's DONATE TO TOWN button: a gift leaves the player's money and fills the town's development fund.</summary>
    public class DonationTests
    {
        static GameBootstrap App()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }
        static Transform Body(GameBootstrap app) => app.UI.transform.Find("Safe area/Context/Scrollable details");
        static string PanelText(GameBootstrap app)
        {
            var text = "";
            foreach (var t in Body(app).GetComponentsInChildren<TMP_Text>())
                text += t.text + "\n";
            return text;
        }
        static Button Find(GameBootstrap app, string name)
        {
            var child = Body(app).Find(name);
            Assert.That(child, Is.Not.Null, name + " is in the window");
            return child.GetComponent<Button>();
        }
        [UnityTest]
        public IEnumerator TownWindowTakesADonation()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var town = app.Game.World.producers.Find(p => p.kind == ProducerKind.Town);
            var city = app.Game.Cities.CityFor(town.id);
            app.UI.Producer(town);
            yield return null;
            Find(app, "DONATE TO TOWN").onClick.Invoke();
            yield return null;
            Assert.That(PanelText(app), Does.Contain("DONATE TO " + town.name.ToUpperInvariant()).And.Contain("development fund"));
            int gift = CitySimulation.Gifts[0], money = app.Game.World.money, price = app.Game.Cities.FundBuildingPrice(city);
            Assert.That(Find(app, "GIFT " + CitySimulation.Gifts[2]).interactable, Is.False, "a gift above the player's money is greyed out");
            Find(app, "GIFT " + gift).onClick.Invoke();
            yield return null;
            Assert.That(app.Game.World.money, Is.EqualTo(money - gift), "the gift is paid");
            Assert.That(city.fund, Is.LessThanOrEqualTo(gift - CitySimulation.GiftStarts * price), "a gift starts three buildings at once");
            // The game is paused, yet the gift's sites open at once with cranes, and the cranes swing.
            Assert.That(app.World.GiftCranes, Is.GreaterThanOrEqualTo(1), "a gift puts cranes to work right away");
            var jib = app.World.transform.Find("Construction sites").GetComponentsInChildren<Transform>().First(t => t.name == "Crane jib");
            var turn = jib.localRotation;
            yield return new WaitForSeconds(.5f);
            Assert.That(Quaternion.Angle(turn, jib.localRotation), Is.GreaterThan(1f), "the crane swings while the game is paused");
            // The gifts stay open, so the player can give again and again.
            Assert.That(PanelText(app), Does.Contain("DONATE TO " + town.name.ToUpperInvariant()).And.Contain($"Fund now: ${city.fund:N0}"));
            int fund = city.fund;
            Find(app, "GIFT " + gift).onClick.Invoke();
            yield return null;
            Assert.That(app.Game.World.money, Is.EqualTo(money - 2 * gift), "a second gift is paid too");
            Assert.That(city.fund, Is.GreaterThan(fund), "the second gift adds to the fund");
            Assert.That(app.World.GiftCranes, Is.GreaterThanOrEqualTo(1), "every gift puts cranes to work");
            Find(app, "DONE").onClick.Invoke();
            yield return null;
            Assert.That(PanelText(app), Does.Contain(town.name).And.Contain("Fund: $"), "the town window shows the fund");
            app.UI.UpdateHud();
            Assert.That(PanelText(app), Does.Contain("Fund: $"), "the live refresh keeps the fund line");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
