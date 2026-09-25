using System.Collections.Generic;

namespace ValleyRail.Core
{
    /// <summary>
    /// Campsites in the country just outside the towns. Once a highway is open, a town that has no campsite yet gets one
    /// beside a straight stretch of it a few cells past its last street, where woods or water are near. It goes up a
    /// stage at a time (IntercityRoadState.camp, up to <see cref="Steps"/>). The site is <see cref="Frontage"/> cells
    /// along the road and <see cref="Depth"/> cells deep, and is held like a service area: towns, tracks, stations and
    /// later highways stay off it. Ski roads get none; a beach road may carry its town's campsite.
    /// </summary>
    public static class Campsites
    {
        /// <summary>Construction stages; a campsite at the last one is open.</summary>
        public const int Steps = 3;
        public const int Frontage = 4, Depth = 3, Cells = Frontage * Depth;
        /// <summary>The shortest highway that gets a campsite.</summary>
        public const int MinRoad = 10;
        /// <summary>Road cells past the town's edge (the first highway cell clear of its streets) that are tried for a site.</summary>
        public const int Reach = 16;
        /// <summary>Road cells kept between a campsite's frontage and a service area's middle on the same highway.</summary>
        public const int ServiceGap = 7;
        /// <summary>
        /// Campsite styles (IntercityRoadState.campKind), taken in turn as campsites are planned: 0 tent meadow,
        /// 1 caravan park, 2 log cabins. Append new kinds; never reorder.
        /// </summary>
        public const int Kinds = 3;
        /// <summary>Pitches on every site: tents, caravans or cabins.</summary>
        public const int Pitches = 6;

        public static string KindName(int kind)
        {
            switch (kind)
            {
                case 1: return "caravan park";
                case 2: return "log cabin camp";
                default: return "campsite";
            }
        }
        /// <summary>What stands on the pitches, for the tap window.</summary>
        public static string KindDetails(int kind)
        {
            switch (kind)
            {
                case 1: return "Caravans on hook-up pitches, a shower block and a campfire.";
                case 2: return "Log cabins with porches, a shower block and a campfire.";
                default: return "Tents on grass pitches, a shower block and a campfire.";
            }
        }

        public static bool Planned(IntercityRoadState road) => road.camp > 0;
        public static bool Open(IntercityRoadState road) => road.camp >= Steps;
        /// <summary>The town the campsite belongs to: the one at the nearer end of its highway.</summary>
        public static int Town(IntercityRoadState road) => road.campAt * 2 < road.path.Count ? road.a : road.b;
        /// <summary>The direction the road's path runs past the site (from path[campAt - 1] to path[campAt]).</summary>
        public static int Along(IntercityRoadState road) => Roadside.Along(road.path, road.campAt);
        /// <summary>
        /// Site cell <paramref name="i"/> (0 to Cells - 1): i % Frontage steps along the road from the cell before
        /// campAt (so the frontage is path[campAt - 1] to path[campAt + 2]), i / Frontage is the row away from it.
        /// </summary>
        public static Cell SiteCell(IntercityRoadState road, int i) => SiteCell(road.path, road.campAt, road.campSide, i);
        public static Cell SiteCell(List<Cell> path, int at, int side, int i)
        {
            var c = path[at + i % Frontage - 1];
            for (int row = 0; row <= i / Frontage; row++)
                c = c.Move(side);
            return c;
        }
        /// <summary>True when the path runs straight from <paramref name="at"/> - 2 to <paramref name="at"/> + 3: the frontage and a cell either side.</summary>
        public static bool Straight(List<Cell> path, int at) => Roadside.Straight(path, at) && Roadside.Straight(path, at + 1);
        /// <summary>Whether the stored site fits its road. A save that fails this is rejected (SaveService).</summary>
        public static bool Shaped(IntercityRoadState road) =>
            road.path != null && Straight(road.path, road.campAt) && road.campSide >= 0 && road.campSide < 4 &&
            road.campSide % 2 != Along(road) % 2;
        /// <summary>The planned campsite with a cell within <paramref name="reach"/> of <paramref name="c"/> (0: on its ground), or null.</summary>
        public static IntercityRoadState At(WorldState w, Cell c, int reach = 0)
        {
            foreach (var road in w.intercityRoads)
                if (Planned(road) && Shaped(road))
                    for (int i = 0; i < Cells; i++)
                        if (SiteCell(road, i).Distance(c) <= reach)
                            return road;
            return null;
        }
    }
}
