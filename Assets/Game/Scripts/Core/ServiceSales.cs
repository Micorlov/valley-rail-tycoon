using System;

namespace ValleyRail.Core
{
    /// <summary>
    /// Buying a roadside service area (see <see cref="Roadside"/>). An open filling station, truck stop or eco station
    /// is for sale; its price is always above <see cref="MinPrice"/> and rises with the people living in the two towns
    /// its highway links. Once bought (IntercityRoadState.serviceOwned) it pays its owner a share of its price every
    /// game minute (<see cref="PayPeriod"/>), so it earns its price back in about <see cref="PaybackMinutes"/> minutes.
    /// </summary>
    public static class ServiceSales
    {
        /// <summary>No service area ever sells for this much or less.</summary>
        public const int MinPrice = 1_000_000;
        const int BasePrice = 1_100_000, PricePerPerson = 25, TrafficCap = 1_000_000, PriceStep = 5_000;
        /// <summary>Per style (Roadside.Kinds): highway station, retro station, truck stop, eco station.</summary>
        static readonly int[] Premium = { 100_000, 200_000, 400_000, 300_000 };
        /// <summary>Game minutes an owned station takes to earn back its price while its towns stay the size they are.</summary>
        public const int PaybackMinutes = 100;
        /// <summary>Ticks between payments (one game minute) and the tick in each period they are paid on.</summary>
        public const int PayPeriod = 1200, PayPhase = 600;

        /// <summary>The asking price for a station of <paramref name="kind"/> on a highway whose two towns hold <paramref name="people"/> people.</summary>
        public static int Price(int kind, int people)
        {
            long traffic = Math.Min((long)Math.Max(0, people) * PricePerPerson, TrafficCap);
            long price = BasePrice + Premium[Math.Max(0, Math.Min(Premium.Length - 1, kind))] + traffic;
            price = (price + PriceStep / 2) / PriceStep * PriceStep;
            return (int)Math.Max(MinPrice + PriceStep, price);
        }
        /// <summary>What an owned station pays each game minute.</summary>
        public static int Income(int kind, int people) => Price(kind, people) / PaybackMinutes;
        /// <summary>The style's name for a window title: "TRUCK STOP".</summary>
        public static string Title(int kind)
        {
            switch (kind)
            {
                case 1: return "RETRO FILLING STATION";
                case 2: return "TRUCK STOP";
                case 3: return "ECO STATION";
                default: return "FILLING STATION";
            }
        }
        /// <summary>The planned service area with a cell within <paramref name="reach"/> of <paramref name="c"/> (0: on its ground), or null.</summary>
        public static IntercityRoadState At(WorldState w, Cell c, int reach = 0)
        {
            foreach (var road in w.intercityRoads)
                if (Roadside.Planned(road) && Roadside.Shaped(road))
                    for (int i = 0; i < Roadside.Cells; i++)
                        if (Roadside.SiteCell(road, i).Distance(c) <= reach)
                            return road;
            return null;
        }
    }
}
