using System;
namespace ValleyRail.Core
{
    /// <summary>The game calendar: one month is one game minute (1200 ticks), and twelve months make a year.</summary>
    public static class GameCalendar
    {
        public const int TicksPerMonth = 1200, MonthsPerYear = 12;
        public static long Month(long tick) => tick / TicksPerMonth;
    }
    /// <summary>What a train earned from deliveries and spent on running costs over a period. Buying and selling are capital, not profit.</summary>
    public struct TrainProfit
    {
        public long income, cost;
        public long Profit => income - cost;
    }
    /// <summary>
    /// One train's delivery income and running costs in monthly buckets. Bucket 0 is <see cref="month"/> and bucket i the month
    /// i months before it, so the buckets hold the current month plus the full year before it. Reads never change the buckets.
    /// </summary>
    [Serializable]
    public class TrainAccounts
    {
        public const int Buckets = GameCalendar.MonthsPerYear + 1;
        public long month;
        public int[] income = new int[Buckets], cost = new int[Buckets];
        public void Record(long tick, int earned, int spent)
        {
            Roll(GameCalendar.Month(tick));
            income[0] += earned;
            cost[0] += spent;
        }
        /// <summary>The rolling month that ends at <paramref name="tick"/>.</summary>
        public TrainProfit LastMonth(long tick) => Window(tick, 1);
        /// <summary>The rolling twelve months that end at <paramref name="tick"/>; for a younger train, everything since it was bought.</summary>
        public TrainProfit LastYear(long tick) => Window(tick, GameCalendar.MonthsPerYear);
        public static bool Valid(TrainAccounts a, long tick)
        {
            if (a == null || a.income == null || a.cost == null || a.income.Length != Buckets || a.cost.Length != Buckets || a.month < 0 || a.month > GameCalendar.Month(tick))
                return false;
            for (int i = 0; i < Buckets; i++)
                if (a.income[i] < 0 || a.cost[i] < 0)
                    return false;
            return true;
        }
        // The current month so far, the whole months before it, and the share of the oldest month that is still inside the window.
        TrainProfit Window(long tick, int months)
        {
            long shift = GameCalendar.Month(tick) - month;
            long unexpired = GameCalendar.TicksPerMonth - tick % GameCalendar.TicksPerMonth;
            var total = new TrainProfit();
            for (int ago = 0; ago <= months; ago++)
            {
                long bucket = ago - shift;
                if (bucket < 0 || bucket >= Buckets)
                    continue;
                long earned = income[bucket], spent = cost[bucket];
                if (ago == months)
                {
                    earned = earned * unexpired / GameCalendar.TicksPerMonth;
                    spent = spent * unexpired / GameCalendar.TicksPerMonth;
                }
                total.income += earned;
                total.cost += spent;
            }
            return total;
        }
        void Roll(long now)
        {
            long shift = now - month;
            if (shift <= 0)
                return;
            for (int i = Buckets - 1; i >= 0; i--)
            {
                income[i] = i >= shift ? income[i - (int)shift] : 0;
                cost[i] = i >= shift ? cost[i - (int)shift] : 0;
            }
            month = now;
        }
    }
}
