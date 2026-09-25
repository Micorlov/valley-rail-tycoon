namespace ValleyRail.Core
{
    /// <summary>
    /// Roads to the ski resorts (see <see cref="SkiResorts"/>): an ordinary highway from a town's streets to the resort's car
    /// park entrance, stored with b = the resort's producer id and built a cell at a time like the others. A town builds one
    /// once no pair of towns needs a highway and no beach road is waiting; the resort's cars come when it is finished.
    /// </summary>
    public sealed partial class CitySimulation
    {
        /// <summary>The road to a ski resort's car park, or null. A plain loop: DevelopRoads asks every 200 ticks.</summary>
        public IntercityRoadState SkiRoadOf(int resortId)
        {
            foreach (var r in w.intercityRoads)
                if (r.ToSki && r.b == resortId)
                    return r;
            return null;
        }
        /// <summary>
        /// Starts the road to one resort that has none: from the nearest Village within SkiResorts.Reach of its entrance,
        /// or the next nearest when that town finds no route. True when a road was planned. Allocates only for its path.
        /// </summary>
        bool PlanSkiRoad()
        {
            foreach (var resort in w.producers)
            {
                if (resort.kind != ProducerKind.SkiResort || SkiRoadOf(resort.id) != null)
                    continue;
                var entrance = SkiResorts.Entrance(resort);
                if (!RoadSite(entrance))
                    continue;
                int lastDistance = -1, last = -1;
                for (int attempt = 0; attempt < w.cities.Count; attempt++)
                {
                    int best = -1, distance = int.MaxValue;
                    for (int i = 0; i < w.cities.Count; i++)
                    {
                        var city = w.cities[i];
                        int d = city.center.Distance(entrance);
                        bool later = d > lastDistance || (d == lastDistance && i > last);
                        if (!later || d > SkiResorts.Reach || d >= distance || city.level < CityLevel.Village)
                            continue;
                        best = i; distance = d;
                    }
                    if (best < 0)
                        break;
                    if (BuildSkiRoad(w.cities[best], resort, entrance))
                        return true;
                    lastDistance = distance; last = best;
                }
            }
            return false;
        }
        bool BuildSkiRoad(CityState town, ProducerState resort, Cell entrance)
        {
            BeginSearch();
            int count = 0;
            foreach (var r in town.roads) count = SeedSearch(r.cell, count);
            goal[entrance.Key] = 1;
            var path = Search(count, entrance.x - town.center.x, entrance.z - town.center.z);
            goal[entrance.Key] = 0;
            if (path.Count < 2)
                return false;
            w.intercityRoads.Add(new IntercityRoadState { a = town.producerId, b = resort.id, path = path, built = 1 });
            foreach (var cell in path) grid[cell.Key] |= Highway;
            Notify(Notification.SkiRoad, town.id, resort.id);
            w.cityRevision++;
            return true;
        }
        /// <summary>Tells the town its ski road opened, once the road's last cell is built.</summary>
        void SkiRoadOpened(IntercityRoadState road)
        {
            var town = CityFor(road.a);
            if (road.ToSki && road.Complete && town != null)
                Notify(Notification.SkiOpened, town.id, road.b);
        }
    }
}
