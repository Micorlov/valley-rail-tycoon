using System.Collections.Generic;

namespace ValleyRail.Core
{
    /// <summary>
    /// Roadside service areas. Once a highway between two towns is open, a filling station with a tyre shop and a garage
    /// goes up beside a straight stretch near the road's middle, a stage at a time (IntercityRoadState.service, up to
    /// <see cref="Steps"/>). The site is <see cref="Frontage"/> cells along the road and <see cref="Depth"/> cells deep and
    /// is held like a beach car park: towns, tracks, stations and later highways stay off it. Beach roads get none.
    /// </summary>
    public static class Roadside
    {
        /// <summary>Construction stages; a service area at the last one is open.</summary>
        public const int Steps = 4;
        public const int Frontage = 3, Depth = 2, Cells = Frontage * Depth;
        /// <summary>The shortest highway that gets a service area.</summary>
        public const int MinRoad = 12;
        /// <summary>Cells kept between the site and any town street or building, so it stands on the open road.</summary>
        public const int TownGap = 3;
        /// <summary>Highway cells either side of the site's middle that must run straight, so cars pull in and out on a straight road.</summary>
        public const int StraightReach = 2;
        /// <summary>
        /// Station styles (IntercityRoadState.serviceKind), taken in turn as service areas are planned: 0 highway station,
        /// 1 retro station, 2 truck stop, 3 eco station. Every one has its tyre shop and garage. Append new kinds; never reorder.
        /// </summary>
        public const int Kinds = 4;
        /// <summary>The style's name with its article, to start a sentence: "A truck stop".</summary>
        public static string KindName(int kind)
        {
            switch (kind)
            {
                case 1: return "A retro filling station";
                case 2: return "A truck stop";
                case 3: return "An eco station";
                default: return "A filling station";
            }
        }

        public static bool Planned(IntercityRoadState road) => road.service > 0;
        public static bool Open(IntercityRoadState road) => road.service >= Steps;
        /// <summary>The direction the road's path runs past the site (from path[serviceAt - 1] to path[serviceAt]).</summary>
        public static int Along(IntercityRoadState road) => Along(road.path, road.serviceAt);
        public static int Along(List<Cell> path, int at) => Directions.Between(path[at - 1], path[at]);
        /// <summary>
        /// Site cell <paramref name="i"/> (0 to Cells - 1): i % Frontage steps along the road from the cell before the
        /// middle, i / Frontage is the row away from it (0 touches the road).
        /// </summary>
        public static Cell SiteCell(IntercityRoadState road, int i) => SiteCell(road.path, road.serviceAt, road.serviceSide, i);
        public static Cell SiteCell(List<Cell> path, int at, int side, int i)
        {
            var c = path[at + i % Frontage - 1];
            for (int row = 0; row <= i / Frontage; row++)
                c = c.Move(side);
            return c;
        }
        /// <summary>True when the path runs in one straight line from <paramref name="at"/> - StraightReach to <paramref name="at"/> + StraightReach.</summary>
        public static bool Straight(List<Cell> path, int at)
        {
            if (at - StraightReach < 0 || at + StraightReach >= path.Count)
                return false;
            int dx = path[at].x - path[at - 1].x, dz = path[at].z - path[at - 1].z;
            if (System.Math.Abs(dx) + System.Math.Abs(dz) != 1)
                return false;
            for (int i = at - StraightReach + 1; i <= at + StraightReach; i++)
                if (path[i].x - path[i - 1].x != dx || path[i].z - path[i - 1].z != dz)
                    return false;
            return true;
        }
        /// <summary>
        /// Whether the stored site fits its road: a straight stretch around serviceAt and a side square to it. A save that
        /// fails this is rejected (SaveService); the city grid only reserves sites that pass.
        /// </summary>
        public static bool Shaped(IntercityRoadState road) =>
            road.path != null && Straight(road.path, road.serviceAt) && road.serviceSide >= 0 && road.serviceSide < 4 &&
            road.serviceSide % 2 != Along(road) % 2;
    }
}
