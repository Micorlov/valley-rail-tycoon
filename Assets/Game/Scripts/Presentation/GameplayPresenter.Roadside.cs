using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// The windows a tap on a roadside service area or a campsite opens. A service area can be bought once it is open
    /// (ServiceSales): always for more than a million dollars, after which it pays its takings every game minute.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        public void ServiceArea(IntercityRoadState road)
        {
            var game = app.Game;
            Context(ServiceSales.Title(road.serviceKind), ServiceText(road));
            if (Roadside.Open(road) && !road.serviceOwned)
            {
                int price = game.Cities.ServicePrice(road);
                Button(contextBody, $"BUY FOR ${price:N0}", () => { app.Perform(() => app.Game.Cities.BuyService(road)); ServiceArea(road); },
                    new Vector2(22, -210), new Vector2(345, 58), true);
            }
        }
        string ServiceText(IntercityRoadState road)
        {
            var game = app.Game;
            var cities = game.Cities;
            string from = TownName(road.a), to = TownName(road.b);
            var lines = new List<string> { $"On the {from} – {to} highway.\nPumps, a shop, a tyre shop and a garage." };
            if (!Roadside.Open(road))
            {
                lines.Add(Heading("UNDER CONSTRUCTION", ShipsColor));
                lines.Add($"Stage {road.service} of {Roadside.Steps}. It goes on sale once it opens.");
                return string.Join("\n", lines);
            }
            int income = cities.ServiceIncome(road);
            if (road.serviceOwned)
            {
                lines.Add(Heading("YOUR STATION", TakesColor));
                lines.Add(Value($"+${income:N0} a minute"));
                lines.Add($"Earned so far: ${road.serviceEarned:N0}");
                lines.Add("<size=20>Takings rise as the two towns grow.</size>");
            }
            else
            {
                lines.Add(Heading("FOR SALE", ShipsColor));
                lines.Add(Value($"${cities.ServicePrice(road):N0}"));
                lines.Add($"Earns about ${income:N0} a minute and pays for itself in about {ServiceSales.PaybackMinutes} minutes.");
                lines.Add($"<size=20>The price follows the traffic: {cities.ServicePeople(road):N0} people live in {from} and {to}. You have ${game.World.money:N0}.</size>");
            }
            return string.Join("\n", lines);
        }
        public void Campsite(IntercityRoadState road)
        {
            string town = TownName(Campsites.Town(road));
            Context($"{town} {Capitalised(Campsites.KindName(road.campKind))}", CampText(road, town));
        }
        string CampText(IntercityRoadState road, string town)
        {
            var lines = new List<string> { Campsites.KindName(road.campKind).ToUpperInvariant(), $"In the country outside {town}, beside the {TownName(road.a)} – {TownName(road.b)} highway." };
            if (!Campsites.Open(road))
            {
                lines.Add(Heading("UNDER CONSTRUCTION", ShipsColor));
                lines.Add($"Stage {road.camp} of {Campsites.Steps}.");
                return string.Join("\n", lines);
            }
            lines.Add(Heading("PITCHES", TakesColor));
            lines.Add(Value(Campsites.Pitches.ToString()) + "\n" + Campsites.KindDetails(road.campKind));
            lines.Add("<size=20>Campers drive in from the highway, settle on their pitch and gather round the campfire.</size>");
            return string.Join("\n", lines);
        }
        string TownName(int producerId) => app.Game.Cargo.Producer(producerId)?.name ?? "?";
        static string Capitalised(string words)
        {
            var parts = words.Split(' ');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }
    }
}
