using UnityEngine;
using UnityEngine.UI;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// DONATE TO TOWN. The town window's button opens a choice of gifts (CitySimulation.Gifts); a gift goes into the
    /// town's development fund, which pays for extra buildings every minute until it runs out.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        void DonateButton(ProducerState p)
        {
            if (app.Game.Cities.CityFor(p.id) == null)
                return;
            Named(Button(contextBody, "DONATE TO TOWN", () => Donations(p), new Vector2(22, -210), new Vector2(345, 58), true), "DONATE TO TOWN");
        }
        /// <summary>"\nFund: $45,000 · about 9 buildings" in the town window while the fund lasts.</summary>
        string FundLine(CityState city)
        {
            if (city.fund <= 0)
                return "";
            int buildings = city.fund / Mathf.Max(1, app.Game.Cities.FundBuildingPrice(city));
            string pace = city.blockedEvaluations > 0 ? "waiting for room" : $"about {buildings} building{(buildings == 1 ? "" : "s")}";
            return $"\n<color=#{ColorUtility.ToHtmlStringRGB(violet)}>Fund: ${city.fund:N0} · {pace}</color>";
        }
        void Donations(ProducerState p)
        {
            var cities = app.Game.Cities;
            var city = cities.CityFor(p.id);
            int price = cities.FundBuildingPrice(city), money = app.Game.World.money;
            Context("DONATE TO " + p.name.ToUpperInvariant(),
                $"Your gift goes into {p.name}'s development fund. While it lasts, the town puts up {CitySimulation.FundBuildsPerMinute} extra buildings a minute.\n" +
                $"Give as often as you like: every gift adds to the fund.\nOne building here costs the fund ${price:N0}.\n" +
                $"<color=#{ColorUtility.ToHtmlStringRGB(violet)}>Fund now: ${city.fund:N0}</color> · you have ${money:N0}");
            foreach (int gift in CitySimulation.Gifts)
            {
                int amount = gift;
                bool affordable = amount <= money;
                string label = affordable ? $"GIVE ${amount:N0} · ~{amount / price} buildings" : $"${amount:N0} · NOT ENOUGH MONEY";
                var button = Named(Button(contextBody, label, () => Donate(p, amount), new Vector2(22, -210), new Vector2(345, 58), affordable), "GIFT " + amount);
                button.GetComponent<Button>().interactable = affordable;
            }
            Named(Button(contextBody, "DONE", () => Producer(p), new Vector2(22, -280), new Vector2(345, 54)), "DONE");
        }
        /// <summary>Gives, then stays on the gifts with the new fund and money, so the player can give again at once.</summary>
        void Donate(ProducerState p, int amount)
        {
            // Before the gift: the buildings it starts open as construction sites with working cranes, even when paused.
            app.World.MarkGift(app.Game.Cities.CityFor(p.id).id);
            if (app.Perform(() => app.Game.Cities.Donate(p.id, amount)).ok)
                Donations(p);
        }
    }
}
