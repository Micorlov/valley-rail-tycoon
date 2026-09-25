using System.Collections.Generic;

namespace ValleyRail.Core
{
    /// <summary>Roadside service areas (see <see cref="Roadside"/>): planned beside open highways and built a stage at a time.</summary>
    public sealed partial class CitySimulation
    {
        /// <summary>
        /// Every service area under construction goes up a stage, and the first open highway without one gets a site. Runs
        /// halfway between road steps, so highway and beach timing stay as they were. Allocates nothing.
        /// </summary>
        void DevelopServices()
        {
            bool changed = false;
            foreach (var road in w.intercityRoads)
                if (Roadside.Planned(road) && !Roadside.Open(road))
                {
                    road.service++;
                    changed = true;
                    var town = CityFor(road.a);
                    if (Roadside.Open(road) && town != null)
                        Notify(Notification.ServiceOpened, town.id, road.b);
                }
            foreach (var road in w.intercityRoads)
                if (!road.ToBeach && !road.ToSki && road.Complete && !Roadside.Planned(road) && road.path.Count >= Roadside.MinRoad && PlanService(road))
                {
                    changed = true;
                    break;
                }
            if (changed)
                w.cityRevision++;
        }
        /// <summary>Holds the free site nearest the road's middle, on either hand. False when no stretch of the road has one.</summary>
        bool PlanService(IntercityRoadState road)
        {
            int n = road.path.Count, middle = n / 2;
            for (int k = 0; k <= n; k++)
            {
                // Cells middle, +1, -1, +2, -2 ... along the road.
                int at = middle + (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);
                if (!Roadside.Straight(road.path, at))
                    continue;
                if (Campsites.Planned(road) && System.Math.Abs(at - road.campAt) < Campsites.ServiceGap)
                    continue;
                int along = Roadside.Along(road.path, at);
                for (int hand = 0; hand < 2; hand++)
                {
                    // Roads differ in the hand they try first, so the service areas are not all on one side.
                    int side = (along + ((road.a + road.b + hand) % 2 == 0 ? 1 : 3)) % 4;
                    if (!ServiceSite(road.path, at, side))
                        continue;
                    road.serviceAt = at;
                    road.serviceSide = side;
                    road.serviceKind = Planned() % Roadside.Kinds;
                    road.service = 1;
                    ReserveService(road);
                    return true;
                }
            }
            return false;
        }
        /// <summary>Service areas planned so far; the next one takes the next station style in turn.</summary>
        int Planned()
        {
            int planned = 0;
            foreach (var road in w.intercityRoads)
                if (Roadside.Planned(road))
                    planned++;
            return planned;
        }
        /// <summary>
        /// A site beside plain open road (no level crossing, bridge or town street along it) on flat, free ground that no
        /// town, track, station, industry or other reserved land uses, and away from the towns.
        /// </summary>
        bool ServiceSite(List<Cell> path, int at, int side)
        {
            for (int i = at - Roadside.StraightReach; i <= at + Roadside.StraightReach; i++)
            {
                var c = path[i];
                if (!MapDefinition.InBounds(c) || MapDefinition.Water(c) || net.At(c) != null || (grid[c.Key] & (Building | Road | Plaza)) != 0)
                    return false;
            }
            for (int i = 0; i < Roadside.Cells; i++)
            {
                var c = Roadside.SiteCell(path, at, side, i);
                if (!MapDefinition.InBounds(c) || grid[c.Key] != 0 || MapDefinition.Raised(c) || MapDefinition.Water(c) || Coast.Beach(c) ||
                    MapDefinition.Blocked(c, w) || net.At(c) != null || BuildService.StationFootprint(w, c) || NearTown(c))
                    return false;
            }
            return true;
        }
        /// <summary>True when a town street, square or building lies within Roadside.TownGap cells.</summary>
        bool NearTown(Cell c)
        {
            for (int dz = -Roadside.TownGap; dz <= Roadside.TownGap; dz++)
                for (int dx = -Roadside.TownGap; dx <= Roadside.TownGap; dx++)
                {
                    var n = new Cell(c.x + dx, c.z + dz);
                    if (MapDefinition.InBounds(n) && (grid[n.Key] & (Building | Road | Plaza)) != 0)
                        return true;
                }
            return false;
        }
        /// <summary>Holds a service area's cells from the day it is planned, so towns, tracks, stations and highways stay off them.</summary>
        void ReserveService(IntercityRoadState road)
        {
            for (int i = 0; i < Roadside.Cells; i++)
            {
                var c = Roadside.SiteCell(road, i);
                if (MapDefinition.InBounds(c))
                    grid[c.Key] |= Service;
            }
        }
        static bool SameRoute(List<Cell> a, List<Cell> b)
        {
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
                if (!a[i].Equals(b[i]))
                    return false;
            return true;
        }
    }
}
