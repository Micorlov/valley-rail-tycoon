namespace ValleyRail.Core
{
    /// <summary>Street-grid geometry and the day-0 town, shared by growth, the relayout of old saves and the presenter.</summary>
    public static class CityLayout
    {
        /// <summary>Saves whose WorldState.cityLayout is lower are re-laid out once when a session starts.</summary>
        public const int Current = 3; // 2: 2×2 landmarks; 3: landmarks from 3×3 to 8×8
        public struct Lot
        {
            public int dx, dz, def;
        }
        /// <summary>The main-street stubs beside the plaza, as x offsets.</summary>
        public static readonly int[] DayZeroStreets = { -1, 1 };
        /// <summary>
        /// Day-0 lots in the 3×3 around the plaza: the town hall faces the plaza from the north, five different homes
        /// house exactly 420 people. The bungalow comes first so it is buildings[0].
        /// </summary>
        public static readonly Lot[] DayZero =
        {
            new Lot { dx = -1, dz = -1, def = BuildingCatalog.Bungalow },
            new Lot { dx = 0, dz = -1, def = BuildingCatalog.Townhouses },
            new Lot { dx = 1, dz = -1, def = BuildingCatalog.Duplex },
            new Lot { dx = -1, dz = 1, def = BuildingCatalog.Index(BuildingCategory.Residential, 2) },
            new Lot { dx = 0, dz = 1, def = BuildingCatalog.TownHall },
            new Lot { dx = 1, dz = 1, def = BuildingCatalog.Villa },
        };
        /// <summary>
        /// A settlers' village (CitySimulation.Founding): the town hall and three small homes around the plaza, 120 people.
        /// The lots south of the main street stay open, so the first new home can face the plaza.
        /// </summary>
        public static readonly Lot[] Hamlet =
        {
            new Lot { dx = -1, dz = -1, def = BuildingCatalog.Cottage },
            new Lot { dx = 1, dz = -1, def = BuildingCatalog.Farmhouse },
            new Lot { dx = -1, dz = 1, def = BuildingCatalog.Bungalow },
            new Lot { dx = 0, dz = 1, def = BuildingCatalog.TownHall },
        };
        public static int FootprintCells(BuildingState bs) => BuildingCatalog.Size(bs.def) * BuildingCatalog.Size(bs.def);
        /// <summary>The i-th cell a building covers, counted from its south-west anchor.</summary>
        public static Cell FootprintCell(BuildingState bs, int i)
        {
            int size = BuildingCatalog.Size(bs.def);
            return new Cell(bs.cell.x + i % size, bs.cell.z + i / size);
        }
        public static bool Covers(BuildingState bs, Cell c)
        {
            int size = BuildingCatalog.Size(bs.def);
            return c.x >= bs.cell.x && c.x < bs.cell.x + size && c.z >= bs.cell.z && c.z < bs.cell.z + size;
        }
        static int Mod(int a, int n) => (a % n + n) % n;
        /// <summary>Streets run along every (blockWidth + 1)th column and (blockDepth + 1)th row, counted from the plaza.</summary>
        public static bool StreetLine(Cell center, Cell c, CityBalance cb) =>
            Mod(c.x - center.x, System.Math.Max(1, cb.blockWidth) + 1) == 0 || Mod(c.z - center.z, System.Math.Max(1, cb.blockDepth) + 1) == 0;
    }
}
