using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>The silhouette the presenter draws for a building definition.</summary>
    public enum BuildingStyle
    {
        House, LargeHouse, Apartments, Tower, Shop, Store, Office, CommercialTower,
        Cottage, Bungalow, Farmhouse, Villa, Duplex, Townhouses, WalkUp, GardenFlats, HighRise, Cafe, MarketHall, Hotel,
        TownHall, Chapel, School, FireStation, MedicalCentre, PostOffice, Park, Police, Library, Hospital, Museum, Stadium,
        PoliceHeadquarters, ShoppingMall, GeneralHospital, Arena, University, Lido, DriveIn, CityPark, Zoo
    }
    [Serializable]
    public class BuildingDefinition
    {
        public string key, name; public BuildingCategory category; public BuildingStyle style; public int level, population, jobs;
        // Visual recipe: footprint and height in hundredths of a cell, palette indices resolved by the presenter.
        public int width, depth, height, wall, roof, windows; public bool coneRoof;
        /// <summary>Civic buildings only: the town level that unlocks them.</summary>
        public CityLevel unlock;
        /// <summary>Footprint in cells per side (1–8); larger landmarks are anchored at their south-west cell.</summary>
        public int size = 1;
        /// <summary>Kept so older saves load; towns no longer build it.</summary>
        public bool retired;
    }
    /// <summary>
    /// Building definitions. Saves store the index, so existing entries never move: append new ones at the end.
    /// Residential and commercial buildings upgrade between variants of the next level; civic buildings are unique per
    /// town, unlock by town level and never upgrade.
    /// </summary>
    public static class BuildingCatalog
    {
        public const int Levels = 4;
        static BuildingDefinition R(string key, string name, BuildingStyle style, int level, int population, int width, int depth, int height, int wall, int roof, int windows) =>
            new BuildingDefinition { key = key, name = name, category = BuildingCategory.Residential, style = style, level = level, population = population, width = width, depth = depth, height = height, wall = wall, roof = roof, windows = windows };
        static BuildingDefinition C(string key, string name, BuildingStyle style, int level, int jobs, int width, int depth, int height, int wall, int roof, int windows) =>
            new BuildingDefinition { key = key, name = name, category = BuildingCategory.Commercial, style = style, level = level, jobs = jobs, width = width, depth = depth, height = height, wall = wall, roof = roof, windows = windows };
        static BuildingDefinition Civic(string key, string name, BuildingStyle style, CityLevel unlock, int jobs, int width, int depth, int height, int size = 1, bool retired = false) =>
            new BuildingDefinition { key = key, name = name, category = BuildingCategory.Civic, style = style, level = 2, jobs = jobs, unlock = unlock, width = width, depth = depth, height = height, size = size, retired = retired };
        public static readonly BuildingDefinition[] Defaults =
        {
            // 0–7: the original catalog, one per category and level.
            new BuildingDefinition { key = "house", name = "House", category = BuildingCategory.Residential, style = BuildingStyle.House, level = 1, population = 40, width = 56, depth = 50, height = 60, wall = 0, roof = 0, windows = 1, coneRoof = true },
            new BuildingDefinition { key = "large-house", name = "Large house", category = BuildingCategory.Residential, style = BuildingStyle.LargeHouse, level = 2, population = 90, width = 66, depth = 58, height = 82, wall = 1, roof = 0, windows = 2, coneRoof = true },
            new BuildingDefinition { key = "apartments", name = "Apartments", category = BuildingCategory.Residential, style = BuildingStyle.Apartments, level = 3, population = 220, width = 70, depth = 64, height = 140, wall = 2, roof = 2, windows = 3 },
            new BuildingDefinition { key = "large-apartments", name = "Apartment tower", category = BuildingCategory.Residential, style = BuildingStyle.Tower, level = 4, population = 500, width = 78, depth = 70, height = 480, wall = 3, roof = 2, windows = 12 },
            new BuildingDefinition { key = "shop", name = "Shop", category = BuildingCategory.Commercial, style = BuildingStyle.Shop, level = 1, jobs = 20, width = 64, depth = 52, height = 42, wall = 4, roof = 1, windows = 1 },
            new BuildingDefinition { key = "store", name = "Store", category = BuildingCategory.Commercial, style = BuildingStyle.Store, level = 2, jobs = 50, width = 76, depth = 62, height = 56, wall = 4, roof = 1, windows = 1 },
            new BuildingDefinition { key = "office", name = "Office", category = BuildingCategory.Commercial, style = BuildingStyle.Office, level = 3, jobs = 150, width = 70, depth = 66, height = 160, wall = 5, roof = 2, windows = 4 },
            new BuildingDefinition { key = "large-commercial", name = "Office tower", category = BuildingCategory.Commercial, style = BuildingStyle.CommercialTower, level = 4, jobs = 350, width = 82, depth = 74, height = 620, wall = 5, roof = 1, windows = 16 },
            // 8–16: more homes.
            R("cottage", "Cottage", BuildingStyle.Cottage, 1, 30, 46, 42, 44, 1, 0, 1),
            R("bungalow", "Bungalow", BuildingStyle.Bungalow, 1, 40, 70, 52, 36, 0, 2, 1),
            R("farmhouse", "Farmhouse", BuildingStyle.Farmhouse, 1, 50, 62, 48, 58, 3, 0, 1),
            R("villa", "Villa", BuildingStyle.Villa, 2, 100, 64, 56, 86, 4, 0, 2),
            R("duplex", "Duplex", BuildingStyle.Duplex, 2, 80, 72, 52, 76, 2, 1, 2),
            R("townhouses", "Townhouses", BuildingStyle.Townhouses, 2, 110, 84, 50, 90, 3, 1, 2),
            R("walk-up", "Walk-up flats", BuildingStyle.WalkUp, 3, 240, 74, 62, 150, 3, 2, 4),
            R("garden-flats", "Garden flats", BuildingStyle.GardenFlats, 3, 260, 76, 66, 170, 0, 2, 4),
            R("high-rise", "High-rise", BuildingStyle.HighRise, 4, 560, 80, 72, 540, 2, 2, 12),
            // 17–19: more businesses.
            C("cafe", "Café", BuildingStyle.Cafe, 1, 15, 56, 48, 40, 0, 0, 1),
            C("market-hall", "Market hall", BuildingStyle.MarketHall, 2, 60, 84, 66, 60, 1, 0, 1),
            C("hotel", "Hotel", BuildingStyle.Hotel, 3, 170, 74, 64, 190, 0, 1, 5),
            // 20–31: municipal services, in unlock order.
            Civic("town-hall", "Town hall", BuildingStyle.TownHall, CityLevel.SmallVillage, 20, 78, 66, 90),
            Civic("chapel", "Chapel", BuildingStyle.Chapel, CityLevel.Village, 10, 50, 74, 70),
            Civic("school", "School", BuildingStyle.School, CityLevel.Village, 30, 86, 60, 70),
            Civic("fire-station", "Fire station", BuildingStyle.FireStation, CityLevel.SmallTown, 25, 80, 66, 64),
            Civic("medical-centre", "Medical centre", BuildingStyle.MedicalCentre, CityLevel.SmallTown, 40, 76, 62, 80),
            Civic("post-office", "Post office", BuildingStyle.PostOffice, CityLevel.SmallTown, 20, 70, 58, 60),
            Civic("park", "Park", BuildingStyle.Park, CityLevel.SmallTown, 0, 96, 96, 0),
            Civic("police", "Police station", BuildingStyle.Police, CityLevel.Town, 30, 76, 62, 76, retired: true),
            Civic("library", "Library", BuildingStyle.Library, CityLevel.Town, 15, 80, 64, 74),
            Civic("hospital", "Hospital", BuildingStyle.Hospital, CityLevel.City, 150, 88, 80, 190, retired: true),
            Civic("museum", "Museum", BuildingStyle.Museum, CityLevel.City, 40, 86, 74, 84),
            Civic("stadium", "Stadium", BuildingStyle.Stadium, CityLevel.LargeCity, 60, 96, 96, 50, retired: true),
            // 32–35: the first 2×2 landmarks, retired for the sized set below.
            Civic("police-headquarters", "Police headquarters", BuildingStyle.PoliceHeadquarters, CityLevel.Town, 60, 188, 182, 100, 2, true),
            Civic("shopping-mall", "Shopping mall", BuildingStyle.ShoppingMall, CityLevel.Town, 250, 190, 186, 90, 2, true),
            Civic("general-hospital", "Hospital", BuildingStyle.GeneralHospital, CityLevel.Town, 200, 186, 180, 230, 2, true),
            Civic("arena", "Stadium", BuildingStyle.Arena, CityLevel.City, 80, 196, 192, 70, 2, true),
            // 36–40: landmarks from 3×3 to 8×8. They may cover unbuilt street lines; the grid routes around them.
            Civic("police-compound", "Police headquarters", BuildingStyle.PoliceHeadquarters, CityLevel.Town, 80, 292, 288, 110, 3),
            Civic("hospital-campus", "Hospital", BuildingStyle.GeneralHospital, CityLevel.Town, 250, 390, 386, 260, 4),
            Civic("shopping-centre", "Shopping mall", BuildingStyle.ShoppingMall, CityLevel.Town, 350, 490, 486, 100, 5),
            Civic("university", "University", BuildingStyle.University, CityLevel.City, 300, 588, 584, 130, 6),
            Civic("stadium-bowl", "Stadium", BuildingStyle.Arena, CityLevel.City, 150, 788, 784, 150, 8),
            // Open-air pools with their own car parks, one PoolLayout plan per size (the others follow below).
            Civic("lido", "Lido", BuildingStyle.Lido, CityLevel.Town, 25, 392, 388, 55, 4),
            // Drive-in cinema: a big screen, curved rows of parked cars and a snack bar (DriveInLayout fixes its 5×5 plan).
            Civic("drive-in", "Drive-in cinema", BuildingStyle.DriveIn, CityLevel.Town, 30, 490, 490, 230, 5),
            // The other three open-air pools, drawn like the lido from their PoolLayout plans.
            Civic("splash-pool", "Splash pool", BuildingStyle.Lido, CityLevel.Village, 8, 192, 188, 40, 2),
            Civic("community-pool", "Community pool", BuildingStyle.Lido, CityLevel.SmallTown, 15, 292, 288, 45, 3),
            Civic("water-park", "Water park", BuildingStyle.Lido, CityLevel.City, 60, 490, 486, 70, 5),
            // City parks: a fountain plaza, a pond or boating lake, a playground and, as they grow, a bandstand and a café (ParkLayout).
            Civic("town-park", "Town park", BuildingStyle.CityPark, CityLevel.SmallTown, 5, 292, 292, 40, 3),
            Civic("city-park", "City park", BuildingStyle.CityPark, CityLevel.Town, 10, 392, 392, 40, 4),
            Civic("central-park", "Central park", BuildingStyle.CityPark, CityLevel.LargeCity, 20, 492, 492, 40, 5),
            // The city zoo: pens round a monkey island, a savanna, and a miniature railway round them all (ZooLayout fixes its 6×6 plan).
            Civic("zoo", "Zoo", BuildingStyle.Zoo, CityLevel.LargeCity, 80, 590, 590, 80, 6),
        };
        public const int Cottage = 8, Bungalow = 9, Farmhouse = 10, Villa = 11, Duplex = 12, Townhouses = 13, TownHall = 20;
        static readonly int[][] variants = BuildVariants();
        /// <summary>Civic definitions towns still build, in unlock order; <see cref="CivicSlot"/> maps a definition to its position here.</summary>
        public static readonly int[] CivicOrder = BuildCivicOrder();
        static readonly int[] civicSlot = BuildCivicSlots();
        public static int Index(BuildingCategory category, int level) => (int)category * Levels + level - 1;
        public static bool Valid(int def) => def >= 0 && def < Defaults.Length;
        public static BuildingDefinition Get(int def) => Defaults[def];
        public static bool IsCivic(int def) => Defaults[def].category == BuildingCategory.Civic;
        /// <summary>Towers over four cells tall (apartment towers, high-rises, office towers): their demolition is priced per storey.</summary>
        public const int SkyscraperHeight = 400;
        public static bool IsSkyscraper(int def) => Valid(def) && Defaults[def].height >= SkyscraperHeight;
        /// <summary>Storeys of a building: the rows of windows the presenter draws.</summary>
        public static int Storeys(int def) => Valid(def) ? Math.Max(1, Defaults[def].windows) : 1;
        /// <summary>Cells per side of a definition's footprint; 1 for unknown indices so validation can reject them first.</summary>
        public static int Size(int def) => Valid(def) ? Defaults[def].size : 1;
        /// <summary>The largest footprint side in the catalog, for sizing placement buffers.</summary>
        public static int MaxSize
        {
            get
            {
                int max = 1;
                foreach (var d in Defaults)
                    max = Math.Max(max, d.size);
                return max;
            }
        }
        /// <summary>Position in <see cref="CivicOrder"/>, or -1 for homes and businesses.</summary>
        public static int CivicSlot(int def) => civicSlot[def];
        /// <summary>Every residential or commercial definition of one level. Shared array: do not modify.</summary>
        public static int[] Variants(BuildingCategory category, int level) => variants[(int)category * Levels + level - 1];
        static int[][] BuildVariants()
        {
            var result = new int[2 * Levels][];
            for (int slot = 0; slot < result.Length; slot++)
            {
                var list = new List<int>();
                for (int i = 0; i < Defaults.Length; i++)
                    if (Defaults[i].category != BuildingCategory.Civic && (int)Defaults[i].category * Levels + Defaults[i].level - 1 == slot)
                        list.Add(i);
                result[slot] = list.ToArray();
            }
            return result;
        }
        static int[] BuildCivicOrder()
        {
            var list = new List<int>();
            for (var level = CityLevel.SmallVillage; level <= CityLevel.LargeCity; level++)
                for (int i = 0; i < Defaults.Length; i++)
                    if (Defaults[i].category == BuildingCategory.Civic && !Defaults[i].retired && Defaults[i].unlock == level)
                        list.Add(i);
            return list.ToArray();
        }
        static int[] BuildCivicSlots()
        {
            var slots = new int[Defaults.Length];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Array.IndexOf(CivicOrder, i);
            return slots;
        }
    }
}
