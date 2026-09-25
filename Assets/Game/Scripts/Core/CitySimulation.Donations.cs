namespace ValleyRail.Core
{
    /// <summary>
    /// Donations: the player gives money to a town's development fund. While the fund lasts, the town puts up
    /// FundBuildsPerMinute extra buildings each game minute on top of its own growth, each paid from the fund at the
    /// town's action cost × fundPointPrice (bigger towns build bigger, so each building costs more). A gift breaks
    /// ground at once. A town with no room to build keeps its fund untouched until it has room again. The player may
    /// give as often as they like: every gift adds to the fund (maxFund only guards the int).
    /// </summary>
    public sealed partial class CitySimulation
    {
        /// <summary>The gifts the town panel offers; each can be given again and again.</summary>
        public static readonly int[] Gifts = { 10000, 50000, 200000, 1000000 };
        /// <summary>A funded building every FundEvery evaluation slots: every 10 seconds of the town's minute.</summary>
        const int FundEvery = 10;
        public const int FundBuildsPerMinute = Slots / FundEvery;
        /// <summary>Buildings a gift starts at once (while its fund covers them), so every gift shows cranes at work.</summary>
        public const int GiftStarts = 3;

        public Result Donate(int producerId, int amount)
        {
            var city = CityFor(producerId);
            if (city == null)
                return Result.Fail("Only towns take donations.");
            if (amount <= 0)
                return Result.Fail("Choose an amount to give.");
            if (amount > w.money)
                return Result.Fail($"Not enough money: the gift is ${amount:N0}.");
            if (city.fund > cb.maxFund - amount)
                return Result.Fail($"{city.name}'s fund can hold at most ${cb.maxFund:N0}.");
            EconomyService.Spend(w, amount);
            city.fund += amount;
            int started = 0;
            while (started < GiftStarts && cb.growthEnabled && BuildFromFund(city))
                started++;
            return Result.Good(started > 0
                ? $"You gave ${amount:N0} to {city.name}: {started} building{(started == 1 ? "" : "s")} started, and the fund holds ${city.fund:N0} for {FundBuildsPerMinute} more a minute."
                : $"You gave ${amount:N0} to {city.name}: its fund now holds ${city.fund:N0} and waits until the town has room to build.", city.id);
        }
        /// <summary>What one funded building costs the fund in this town now.</summary>
        public int FundBuildingPrice(CityState city) => ActionCost(city) * cb.fundPointPrice;
        /// <summary>Called from Step on the town's funded slots.</summary>
        void StepFund(CityState city, int slot, int index)
        {
            if (city.fund > 0 && (slot - index % Slots + Slots) % FundEvery == 0)
                BuildFromFund(city);
        }
        /// <summary>One building paid from the fund; false when the fund is empty or the town has no room (the fund is kept).</summary>
        bool BuildFromFund(CityState city)
        {
            if (city.fund <= 0 || city.blockedEvaluations > 0)
                return false;
            int price = FundBuildingPrice(city);
            if (city.fund < price)
            {
                // The last few dollars become growth points, so no fund is left stranded below one building's price.
                city.growthPoints += city.fund / cb.fundPointPrice;
                city.fund = 0;
                return false;
            }
            if (!Act(city, Producer(city.producerId)))
            {
                city.blockedEvaluations++;
                return false;
            }
            city.fund -= price;
            return true;
        }
    }
}
