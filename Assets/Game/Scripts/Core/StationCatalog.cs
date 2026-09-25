using System;
namespace ValleyRail.Core
{
    /// <summary>
    /// The station ladder. Towns climb Halt → Town station → Central station → Grand terminal; industries climb Freight halt →
    /// Freight depot → Freight yard. <see cref="StationState.level"/> holds the rung; saves from before upgrades existed store 0.
    /// Each rung needs a minimum platform size, costs a one-off price and shortens loading; town rungs also speed up growth.
    /// </summary>
    public static class StationCatalog
    {
        static readonly string[] TownNames = { "Halt", "Town station", "Central station", "Grand terminal" };
        static readonly string[] FreightNames = { "Freight halt", "Freight depot", "Freight yard" };
        static readonly int[] MinLengths = { 3, 4, 5, 6 }, MinPlatforms = { 1, 1, 2, 3 };
        static readonly int[] Prices = { 0, 3000, 9000, 20000 };
        const int DwellCutPercent = 15, GrowthPointsPerLevel = 5;
        // A town served at a bigger station develops faster: its whole growth rate rises by GrowthPercents, and it may put up
        // ExtraBuildingsPerMinute more buildings each minute (a Central station one, a Grand terminal two).
        static readonly int[] GrowthPercents = { 0, 25, 50, 100 }, ExtraBuildingsPerMinute = { 0, 0, 1, 2 };
        /// <summary>Passenger stations climb the town ladder: towns and ski resorts (SkiResorts).</summary>
        public static bool Town(ProducerKind kind) => kind == ProducerKind.Town || kind == ProducerKind.SkiResort;
        public static int MaxLevel(ProducerKind kind) => Town(kind) ? TownNames.Length - 1 : FreightNames.Length - 1;
        public static bool ValidLevel(ProducerKind kind, int level) => level >= 0 && level <= MaxLevel(kind);
        public static string Name(ProducerKind kind, int level)
        {
            var names = Town(kind) ? TownNames : FreightNames;
            return names[Math.Max(0, Math.Min(level, names.Length - 1))];
        }
        public static int MinLength(int level) => MinLengths[Clamp(level)];
        public static int MinPlatformCount(int level) => MinPlatforms[Clamp(level)];
        /// <summary>The rung's one-off price on top of the platform size (StationLayout.Cost). An upgrade pays the difference.</summary>
        public static int LevelPrice(int level) => Prices[Clamp(level)];
        public static bool Fits(int level, int length, int platforms) => length >= MinLength(level) && platforms >= MinPlatformCount(level);
        /// <summary>Loading time at a station of this rung: 15% shorter per level, never longer than the balance value.</summary>
        public static int DwellTicks(Balance b, int level) => b.dwellTicks * (100 - DwellCutPercent * Clamp(level)) / 100;
        public static int DwellTicks(Balance b, StationState s) => s == null ? b.dwellTicks : DwellTicks(b, s.level);
        public static int FasterLoadingPercent(int level) => DwellCutPercent * Clamp(level);
        /// <summary>Extra town growth points per minute from a served station of this rung.</summary>
        public static int GrowthBonus(int level) => GrowthPointsPerLevel * Clamp(level);
        /// <summary>How much faster a town grows while a train serves its station of this rung: the whole growth rate rises by this percent.</summary>
        public static int GrowthPercent(int level) => GrowthPercents[Clamp(level)];
        /// <summary>Buildings a town may put up each minute on top of CityBalance.maxActions while a train serves its station of this rung.</summary>
        public static int ExtraBuildings(int level) => ExtraBuildingsPerMinute[Clamp(level)];
        static int Clamp(int level) => Math.Max(0, Math.Min(level, Prices.Length - 1));
    }
}
