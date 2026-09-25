using System.Collections.Generic;

namespace ValleyRail.Core
{
    /// <summary>Campsites (see <see cref="Campsites"/>): planned beside open highways just outside a town and built a stage at a time.</summary>
    public sealed partial class CitySimulation
    {
        /// <summary>
        /// Every campsite under construction goes up a stage, and the first open highway whose town still has none gets a
        /// site. Runs between the road and service steps, so their timing stays as it was. Allocates nothing.
        /// </summary>
        void DevelopCamps()
        {
            bool changed = false;
            foreach (var road in w.intercityRoads)
                if (Campsites.Planned(road) && !Campsites.Open(road))
                {
                    road.camp++;
                    changed = true;
                    var town = CityFor(Campsites.Town(road));
                    if (Campsites.Open(road) && town != null)
                        Notify(Notification.CampOpened, town.id, road.campKind);
                }
            foreach (var road in w.intercityRoads)
                if (!road.ToSki && road.Complete && !Campsites.Planned(road) && road.path.Count >= Campsites.MinRoad && PlanCamp(road))
                {
                    changed = true;
                    break;
                }
            if (changed)
                w.cityRevision++;
        }
        /// <summary>
        /// Holds a site for the first town at either end of the road that has no campsite yet: within Campsites.Reach
        /// road cells past the town's edge (the first road cell clear of its streets), in the town's half of the road,
        /// preferring woods and water and then nearness. A beach road only has a town at its first end.
        /// </summary>
        bool PlanCamp(IntercityRoadState road)
        {
            int n = road.path.Count;
            for (int end = 0; end < (road.ToBeach ? 1 : 2); end++)
            {
                if (HasCamp(end == 0 ? road.a : road.b))
                    continue;
                int edge = 0;
                while (edge < n / 2 && NearTown(road.path[end == 0 ? edge : n - 1 - edge]))
                    edge++;
                int best = -1, bestSide = 0, bestScore = int.MinValue;
                for (int k = 0; k < Campsites.Reach; k++)
                {
                    // The frontage path[at - 1 .. at + 2] starts k cells past town a's edge, or ends k cells before town b's.
                    int at = end == 0 ? edge + k + 1 : n - 3 - edge - k;
                    if (at < 2 || at + 3 >= n || (at * 2 < n) != (end == 0) || !Campsites.Straight(road.path, at))
                        continue;
                    if (Roadside.Planned(road) && System.Math.Abs(at - road.serviceAt) < Campsites.ServiceGap)
                        continue;
                    int along = Roadside.Along(road.path, at);
                    for (int hand = 0; hand < 2; hand++)
                    {
                        // The opposite hand to the one the road's service area tries first.
                        int side = (along + ((road.a + road.b + hand) % 2 == 0 ? 3 : 1)) % 4;
                        if (!CampSite(road.path, at, side))
                            continue;
                        int score = 3 * Scenic(road.path, at, side) - 4 * k;
                        if (score > bestScore)
                        {
                            best = at;
                            bestSide = side;
                            bestScore = score;
                        }
                    }
                }
                if (best < 0)
                    continue;
                road.campAt = best;
                road.campSide = bestSide;
                road.campKind = PlannedCamps() % Campsites.Kinds;
                road.camp = 1;
                ReserveCamp(road);
                return true;
            }
            return false;
        }
        /// <summary>True when a planned campsite already belongs to the town.</summary>
        bool HasCamp(int town)
        {
            foreach (var road in w.intercityRoads)
                if (Campsites.Planned(road) && Campsites.Town(road) == town)
                    return true;
            return false;
        }
        /// <summary>Campsites planned so far; the next one takes the next style in turn.</summary>
        int PlannedCamps()
        {
            int planned = 0;
            foreach (var road in w.intercityRoads)
                if (Campsites.Planned(road))
                    planned++;
            return planned;
        }
        /// <summary>
        /// A site beside plain open road (no level crossing, bridge or town street along it) on flat, free ground that no
        /// town, track, station, industry, service area or other reserved land uses, and away from the town's streets.
        /// </summary>
        bool CampSite(List<Cell> path, int at, int side)
        {
            for (int i = at - 2; i <= at + 3; i++)
            {
                var c = path[i];
                if (!MapDefinition.InBounds(c) || MapDefinition.Water(c) || net.At(c) != null || (grid[c.Key] & (Building | Road | Plaza)) != 0)
                    return false;
            }
            for (int i = 0; i < Campsites.Cells; i++)
            {
                var c = Campsites.SiteCell(path, at, side, i);
                if (!MapDefinition.InBounds(c) || grid[c.Key] != 0 || MapDefinition.Raised(c) || MapDefinition.Water(c) || Coast.Beach(c) ||
                    MapDefinition.Blocked(c, w) || net.At(c) != null || BuildService.StationFootprint(w, c) || NearTown(c))
                    return false;
            }
            return true;
        }
        /// <summary>MapDefinition.SurfaceNames index of woodland.</summary>
        const int Woodland = 1;
        /// <summary>Woodland cells, and water cells counted twice, within two cells of the site.</summary>
        static int Scenic(List<Cell> path, int at, int side)
        {
            Cell a = Campsites.SiteCell(path, at, side, 0), b = Campsites.SiteCell(path, at, side, Campsites.Cells - 1);
            int x0 = System.Math.Min(a.x, b.x) - 2, x1 = System.Math.Max(a.x, b.x) + 2, z0 = System.Math.Min(a.z, b.z) - 2, z1 = System.Math.Max(a.z, b.z) + 2;
            int score = 0;
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    var c = new Cell(x, z);
                    if (!MapDefinition.InBounds(c))
                        continue;
                    if (MapDefinition.Water(c))
                        score += 2;
                    else if (MapDefinition.Surface(c) == Woodland)
                        score++;
                }
            return score;
        }
        /// <summary>Holds a campsite's cells from the day it is planned, so towns, tracks, stations and highways stay off them.</summary>
        void ReserveCamp(IntercityRoadState road)
        {
            for (int i = 0; i < Campsites.Cells; i++)
            {
                var c = Campsites.SiteCell(road, i);
                if (MapDefinition.InBounds(c))
                    grid[c.Key] |= Camp;
            }
        }
    }
}
