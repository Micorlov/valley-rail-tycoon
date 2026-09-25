using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>
    /// Where a station's cells are. Each of its <see cref="Platforms"/> parallel tracks runs <see cref="Length"/> straight cells
    /// along the axis. Track 0 passes through <c>cell</c> with the building strip beside it on <c>side</c>; every further track
    /// lies one cell further from the strip. Saves from before sizes existed store 0 for both: the original 3 × 1 station.
    /// Nothing here allocates except the list helpers, so the simulation tick can use the rest freely.
    /// </summary>
    public static class StationLayout
    {
        public const int MinLength = 3, MaxLength = 6, MaxPlatforms = 4;
        public static int Length(StationState s) => s.length <= 0 ? MinLength : s.length;
        public static int Platforms(StationState s) => s.platforms <= 0 ? 1 : s.platforms;
        public static bool ValidSize(int length, int platforms) => length >= MinLength && length <= MaxLength && platforms >= 1 && platforms <= MaxPlatforms;
        /// <summary>Stored sizes: 0 (legacy) or a buildable size.</summary>
        public static bool ValidStored(StationState s) => (s.length == 0 || (s.length >= MinLength && s.length <= MaxLength)) && (s.platforms == 0 || (s.platforms >= 1 && s.platforms <= MaxPlatforms));
        /// <summary>Offset of the first platform cell from the station cell along the axis. Odd lengths centre on the cell; even ones reach one further forward.</summary>
        public static int First(int length) => -(length - 1) / 2;
        public static int Last(int length) => length / 2;
        /// <summary>Middle of the platform along the axis, relative to the station cell (0 or .5).</summary>
        public static float Middle(int length) => (First(length) + Last(length)) * .5f;
        public static int Mask(int axis) => axis == 1 ? 10 : 5;
        public static Cell Along(Cell c, int axis, int offset) => new Cell(c.x + (axis == 1 ? offset : 0), c.z + (axis == 0 ? offset : 0));
        /// <summary>The station cell of platform track <paramref name="platform"/>: where its trains stop.</summary>
        public static Cell Center(Cell cell, int side, int platform)
        {
            int away = Directions.Opp(side);
            return new Cell(cell.x + Directions.Dx[away] * platform, cell.z + Directions.Dz[away] * platform);
        }
        public static Cell Center(StationState s, int platform) => Center(s.cell, s.side, Math.Max(0, Math.Min(platform, Platforms(s) - 1)));
        /// <summary>Cells of every platform track of a planned station, track by track, each first to last along the axis.</summary>
        public static List<Cell> Cells(Cell cell, int axis, int side, int length, int platforms)
        {
            var cells = new List<Cell>(length * platforms);
            for (int k = 0; k < platforms; k++)
            {
                var center = Center(cell, side, k);
                for (int i = First(length); i <= Last(length); i++)
                    cells.Add(Along(center, axis, i));
            }
            return cells;
        }
        public static List<Cell> Cells(StationState s) => Cells(s.cell, s.axis, s.side, Length(s), Platforms(s));
        /// <summary>The clear strip beside track 0 where the station building stands.</summary>
        public static List<Cell> Strip(Cell cell, int axis, int side, int length)
        {
            var strip = new List<Cell>(length);
            var beside = cell.Move(side);
            for (int i = First(length); i <= Last(length); i++)
                strip.Add(Along(beside, axis, i));
            return strip;
        }
        public static List<Cell> Strip(StationState s) => Strip(s.cell, s.axis, s.side, Length(s));
        /// <summary>How many cells <paramref name="c"/> lies from track 0, away from the strip (-1 on the strip), or int.MinValue when it is off the station's length.</summary>
        static int Away(StationState s, Cell c)
        {
            int dx = c.x - s.cell.x, dz = c.z - s.cell.z, length = Length(s);
            int along = s.axis == 1 ? dx : dz;
            if (along < First(length) || along > Last(length))
                return int.MinValue;
            int away = Directions.Opp(s.side);
            // The axis component is zero in this product, so it measures only the distance across the tracks.
            return dx * Directions.Dx[away] + dz * Directions.Dz[away];
        }
        /// <summary>The platform track holding <paramref name="c"/>, or -1 when it is not one of the station's platform cells.</summary>
        public static int PlatformAt(StationState s, Cell c)
        {
            int away = Away(s, c);
            return away >= 0 && away < Platforms(s) ? away : -1;
        }
        public static bool OnStrip(StationState s, Cell c) => Away(s, c) == -1;
        /// <summary>True when <paramref name="c"/> is the stopping cell of one of the station's platforms.</summary>
        public static bool IsStop(StationState s, Cell c)
        {
            int k = PlatformAt(s, c);
            return k >= 0 && Center(s, k).Equals(c);
        }
        /// <summary>Construction price without track: bigger stations cost more, extra platforms most of all.</summary>
        public static int Cost(Balance b, int length, int platforms) => b.stationCost + (length - MinLength) * b.stationCost / 4 + (platforms - 1) * b.stationCost * 3 / 4;
        public static string Describe(int length, int platforms) => $"{length} cells × {platforms} platform{(platforms == 1 ? "" : "s")}";
        public static string Describe(StationState s) => Describe(Length(s), Platforms(s));
    }
}
