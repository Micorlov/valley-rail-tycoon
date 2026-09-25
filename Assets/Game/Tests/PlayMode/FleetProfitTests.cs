using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>The fleet list and the train panel show each train's profit or loss over the last month and year.</summary>
    public class FleetProfitTests
    {
        [UnityTest]
        public IEnumerator FleetCardsShowLiveMonthAndYearProfit()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(true);
            yield return null;
            app.SetSpeed(0);
            var train = app.Game.World.trains[0];
            var body = app.UI.transform.Find("Safe area/Context/Scrollable details");
            string card = "#" + train.number + " ";
            // A known month: $600 of deliveries against $200 of running costs.
            train.accounts = new TrainAccounts();
            train.accounts.Record(app.Game.World.tick, 600, 200);
            app.UI.TrainList();
            Assert.That(Shows(body, card, "PROFIT", "Month", "+$400", "Year"), Is.True, Dump(body));
            Assert.That(Shows(body, "Fleet", "last month", "last year"), Is.True, Dump(body));
            // A later loss appears without reopening the list.
            train.accounts.Record(app.Game.World.tick, 0, 700);
            app.UI.UpdateHud();
            Assert.That(Shows(body, card, "LOSS", "-$300"), Is.True, Dump(body));
            app.UI.Train(train);
            Assert.That(Shows(body, "Month: ", "-$300", "$600 - $900"), Is.True, Dump(body));
            Assert.That(Shows(body, "Year: ", "-$300"), Is.True, Dump(body));
        }
        static bool Shows(Transform body, params string[] parts) =>
            System.Array.Exists(body.GetComponentsInChildren<TMPro.TMP_Text>(), t => System.Array.TrueForAll(parts, p => t.text.Contains(p)));
        static string Dump(Transform body) =>
            string.Join("\n---\n", System.Array.ConvertAll(body.GetComponentsInChildren<TMPro.TMP_Text>(), t => t.text));
    }
}
