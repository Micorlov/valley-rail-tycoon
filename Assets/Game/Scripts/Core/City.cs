using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    public enum CityLevel
    {
        SmallVillage, Village, SmallTown, Town, City, LargeCity
    }
    public enum BuildingCategory
    {
        Residential, Commercial, Civic
    }
    [Serializable]
    public struct BuildingState
    {
        public Cell cell; public int def;
    }
    [Serializable]
    public class IntercityRoadState
    {
        // A beach road (b == Coast.Resort) also counts the car park stages built after the road itself (see Coast).
        public int a, b, built, diversionRemainder, park;
        /// <summary>
        /// Roadside service area (see Roadside): 0 none yet, else its construction stage up to Roadside.Steps (open). It
        /// stands beside path[serviceAt], towards the serviceSide direction, in the serviceKind style (Roadside.Kinds).
        /// Saves from before service areas load as 0.
        /// </summary>
        public int service, serviceAt, serviceSide, serviceKind;
        /// <summary>True once the player has bought the open service area (ServiceSales); serviceEarned is what it has paid them since.</summary>
        public bool serviceOwned;
        public int serviceEarned;
        /// <summary>
        /// Campsite (see Campsites): 0 none yet, else its construction stage up to Campsites.Steps (open). Its frontage
        /// runs along path[campAt - 1 .. campAt + 2], towards the campSide direction, in the campKind style. Saves from
        /// before campsites load as 0.
        /// </summary>
        public int camp, campAt, campSide, campKind;
        public List<Cell> path = new List<Cell>();
        public bool Complete => path.Count > 0 && built == path.Count;
        public bool ToBeach => b == Coast.Resort;
        /// <summary>A road to a ski resort's car park: b is the resort's producer id (SkiResorts, CitySimulation.Ski).</summary>
        public bool ToSki => SkiResorts.IsResortId(b);
    }
    [Serializable]
    public struct RoadState
    {
        public Cell cell;
    }
    /// <summary>A demolished cell the city leaves alone until the tick passes, so the player can lay track there.</summary>
    [Serializable]
    public struct ClearedCell
    {
        public Cell cell; public long untilTick;
    }
    /// <summary>
    /// A town's growth state. The Town producer remains the cargo and catchment entity; the city decorates it with
    /// population, buildings and roads. Population is the sum of the buildings, never simulated per citizen.
    /// </summary>
    [Serializable]
    public class CityState
    {
        public int id, producerId, population, jobs, growthPoints, bucket, blockedEvaluations;
        public CityLevel level;
        public string name;
        public Cell center;
        public uint rng;
        public long lastMilestoneTick;
        /// <summary>Tick settlers founded this town (CitySimulation.Founding); 0 for the map's own towns and older saves.</summary>
        public long founded;
        /// <summary>Dollars the player donated that the town has not built with yet (CitySimulation.Donations).</summary>
        public int fund;
        /// <summary>
        /// Tick the town opened its surface light-rail line (TownTramLine, CitySimulation.TownTrams); 0 while it has none.
        /// Saves from before town trams load as 0, and the town opens its line on a later check.
        /// </summary>
        public long tram;
        // Rolling one-minute buckets of railway service; "recent" is the sum of all four.
        public int[] paxIn = new int[4], paxOut = new int[4], goodsIn = new int[4];
        public List<BuildingState> buildings = new List<BuildingState>();
        public List<RoadState> roads = new List<RoadState>();
        public List<ClearedCell> cleared = new List<ClearedCell>();
    }
    /// <summary>Growth tuning. Points per game minute buy buildings; every value is an integer so growth is deterministic.</summary>
    [Serializable]
    public class CityBalance
    {
        public bool growthEnabled = true;
        public int basePoints = 7, paxDivisor = 5, paxCap = 60, goodsDivisor = 4, goodsCap = 30, connectionPoints = 15, connectionCap = 4, stationPoints = 10, stationCap = 3, maxActions = 2;
        public int passengerBase = 8, residentsPerPassenger = 35, storagePerResident = 8, upgradeEveryN = 3, demolitionCost = 500, demolitionCooldown = 6000, maxBuildings = 320, maxRoads = 320;
        // Demolition extras (CitySimulation.DemolitionCost): a skyscraper also costs skyscraperStoreyCost per storey, and a
        // station upgrade pays streetClearCost for every town street cell it takes up.
        public int skyscraperStoreyCost = 5000, streetClearCost = 400;
        // Street grid: blocks of blockWidth × blockDepth lots between streets. Every municipal service adds civicPoints
        // per minute; with the day-0 town hall a town earns the same 10 base points as before services existed.
        public int blockWidth = 3, blockDepth = 2, civicPoints = 3, civicCap = 16;
        // New towns (CitySimulation.Founding): the next is founded once the valley's population reaches foundingPopulation
        // plus foundingStep per town already founded, at least foundingInterval ticks after the last one, up to maxFounded
        // towns and never within foundingSpacing cells of another town's plaza.
        public bool foundingEnabled = true;
        public int foundingPopulation = 3000, foundingStep = 3000, foundingInterval = 12000, maxFounded = 8, foundingSpacing = 18;
        // Donations: a funded building costs the fund its growth points × fundPointPrice dollars. Gifts add up without a
        // practical limit; maxFund only keeps the int fund from overflowing.
        public int fundPointPrice = 12, maxFund = 2000000000;
        // Town trams (CitySimulation.TownTrams): a town of tramLevel or bigger opens a light-rail line through its streets.
        // The line adds tramPoints growth points a minute and tramPassengerPercent to the passengers the town makes.
        public bool tramsEnabled = true;
        public CityLevel tramLevel = CityLevel.City;
        public int tramPoints = 10, tramPassengerPercent = 15;
        public int[] actionCost = { 60, 90, 130, 180, 240, 320 }, levelThresholds = { 500, 1200, 2500, 5000, 10000 }, influenceRadius = { 4, 5, 7, 9, 11, 13 }, maxBuildingLevel = { 1, 2, 2, 3, 4, 4 }, commercialShare = { 10, 15, 20, 25, 30, 35 };
        public static readonly string[] LevelNames = { "Small Village", "Village", "Small Town", "Town", "City", "Large City" };
    }
}
