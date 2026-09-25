namespace ValleyRail.Core
{
    /// <summary>Buying roadside service areas and the income they pay their owner (see <see cref="ServiceSales"/>).</summary>
    public sealed partial class CitySimulation
    {
        /// <summary>People living in the two towns the service area's highway links.</summary>
        public int ServicePeople(IntercityRoadState road) => (CityFor(road.a)?.population ?? 0) + (CityFor(road.b)?.population ?? 0);
        public int ServicePrice(IntercityRoadState road) => ServiceSales.Price(road.serviceKind, ServicePeople(road));
        public int ServiceIncome(IntercityRoadState road) => ServiceSales.Income(road.serviceKind, ServicePeople(road));
        /// <summary>Buys an open service area at today's price.</summary>
        public Result BuyService(IntercityRoadState road)
        {
            if (road == null || !w.intercityRoads.Contains(road) || !Roadside.Planned(road))
                return Result.Fail("That filling station is no longer there.");
            if (road.serviceOwned)
                return Result.Fail("You already own this station.");
            if (!Roadside.Open(road))
                return Result.Fail("It is still being built. You can buy it once it opens.");
            int price = ServicePrice(road);
            if (w.money < price)
                return Result.Fail($"You need ${price:N0} to buy it. You have ${w.money:N0}.");
            EconomyService.Spend(w, price);
            road.serviceOwned = true;
            road.serviceEarned = 0;
            // The towns redraw, so the owner's flag goes up over the forecourt.
            w.cityRevision++;
            return Result.Good($"You bought the {ServiceSales.Title(road.serviceKind).ToLowerInvariant()} for ${price:N0}. It pays you about ${ServiceIncome(road):N0} every minute.");
        }
        /// <summary>Stations the player owns.</summary>
        public int OwnedServices()
        {
            int owned = 0;
            foreach (var road in w.intercityRoads)
                if (road.serviceOwned)
                    owned++;
            return owned;
        }
        /// <summary>Once a game minute every owned, open station pays its takings. Allocates nothing.</summary>
        void PayServices()
        {
            foreach (var road in w.intercityRoads)
                if (road.serviceOwned && Roadside.Open(road))
                {
                    int income = ServiceIncome(road);
                    EconomyService.Credit(w, income, true);
                    road.serviceEarned = (int)System.Math.Min(int.MaxValue, (long)road.serviceEarned + income);
                }
        }
    }
}
