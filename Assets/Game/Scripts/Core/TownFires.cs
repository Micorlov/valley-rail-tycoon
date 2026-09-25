using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>A house or business on fire and the fire station that answers: who drives where, and along which road.</summary>
    public sealed class FireCall
    {
        public int cityId, stationCityId;
        public BuildingState building, station;
        /// <summary>The side of the burning building, and of the fire station, that faces its street (a Directions index).</summary>
        public int facing, stationFacing;
        /// <summary>The street cell in front of the burning building, and in front of the fire station.</summary>
        public Cell street, stationStreet;
        /// <summary>The engine's way from the fire station's street to the burning building's, both ends included.</summary>
        public List<Cell> route = new List<Cell>();
    }
    /// <summary>
    /// Plans the town fires the presentation plays out now and then (CityFires): which home or business catches fire,
    /// which fire station answers and the road its engine takes. Only reads the world, so a fire never changes the
    /// simulation, its saves or its random state; the caller brings its own Random.
    /// </summary>
    public static class TownFires
    {
        /// <summary>The tallest home or business that catches fire, in hundredths of a cell: towers and high-rises never do.</summary>
        public const int TallestFire = 200;
        /// <summary>The longest drive in cells a fire engine makes to a fire, and the longest detour it takes home instead of turning round.</summary>
        public const int FarthestCall = 160, LongestDetour = 16;
        // Random buildings tried before a fire waits for the next time.
        const int Tries = 12;
        // The side a building faces: the first of these that touches a street, as the towns draw it (WorldView.LotFor).
        static readonly int[] FacingOrder = { 2, 0, 1, 3 };

        /// <summary>Homes and businesses on one cell, up to TallestFire high.</summary>
        public static bool Burns(int def)
        {
            if (!BuildingCatalog.Valid(def))
                return false;
            var d = BuildingCatalog.Get(def);
            return d.category != BuildingCategory.Civic && d.size == 1 && d.height <= TallestFire;
        }
        public static bool IsFireStation(int def) => BuildingCatalog.Valid(def) && BuildingCatalog.Get(def).style == BuildingStyle.FireStation;
        /// <summary>The keys of every town street in the valley.</summary>
        public static HashSet<int> Streets(WorldState w)
        {
            var streets = new HashSet<int>();
            foreach (var city in w.cities)
                foreach (var road in city.roads)
                    streets.Add(road.cell.Key);
            return streets;
        }
        /// <summary>The side of a one-cell building that faces a street; -1 when no street touches it.</summary>
        public static int Facing(HashSet<int> streets, Cell cell)
        {
            foreach (int d in FacingOrder)
                if (streets.Contains(cell.Move(d).Key))
                    return d;
            return -1;
        }

        /// <summary>
        /// A fire in a random home or business that a fire station can reach by road, or null when no town has an
        /// engine that can get to one. <paramref name="standing"/> leaves out buildings still under construction.
        /// </summary>
        public static FireCall Plan(WorldState w, RailNetwork net, RoadLanes lanes, Random random, Func<BuildingState, bool> standing = null)
        {
            var streets = Streets(w);
            var stations = Stations(w, lanes, streets, standing);
            if (stations.Count == 0)
                return null;
            var candidates = new List<(CityState city, BuildingState building)>();
            foreach (var city in w.cities)
                foreach (var b in city.buildings)
                    if (Burns(b.def) && (standing == null || standing(b)))
                        candidates.Add((city, b));
            for (int i = 0; i < Tries && candidates.Count > 0; i++)
            {
                var (city, b) = candidates[random.Next(candidates.Count)];
                var call = Answer(net, lanes, streets, stations, city, b);
                if (call != null)
                    return call;
            }
            return null;
        }
        /// <summary>The call for a fire in <paramref name="building"/>: the nearest fire station by road, or null when none can reach it.</summary>
        public static FireCall Call(WorldState w, RailNetwork net, RoadLanes lanes, CityState city, BuildingState building, Func<BuildingState, bool> standing = null)
        {
            if (!Burns(building.def) || (standing != null && !standing(building)))
                return null;
            var streets = Streets(w);
            return Answer(net, lanes, streets, Stations(w, lanes, streets, standing), city, building);
        }
        /// <summary>Fire stations by the key of the street in front of them, if an engine can drive off along it.</summary>
        static Dictionary<int, (int cityId, BuildingState station, int facing)> Stations(WorldState w, RoadLanes lanes, HashSet<int> streets, Func<BuildingState, bool> standing)
        {
            var stations = new Dictionary<int, (int, BuildingState, int)>();
            foreach (var city in w.cities)
                foreach (var b in city.buildings)
                {
                    if (!IsFireStation(b.def) || (standing != null && !standing(b)))
                        continue;
                    int facing = Facing(streets, b.cell);
                    if (facing >= 0 && lanes.Exits(b.cell.Move(facing)) != 0 && !stations.ContainsKey(b.cell.Move(facing).Key))
                        stations.Add(b.cell.Move(facing).Key, (city.id, b, facing));
                }
            return stations;
        }
        static FireCall Answer(RailNetwork net, RoadLanes lanes, HashSet<int> streets, Dictionary<int, (int cityId, BuildingState station, int facing)> stations, CityState city, BuildingState building)
        {
            int facing = Facing(streets, building.cell);
            if (facing < 0)
                return null;
            var street = building.cell.Move(facing);
            // The engine parks on the kerb: never on a level crossing, never on a street it cannot drive off.
            if (lanes.Exits(street) == 0 || net.At(street) != null)
                return null;
            var route = Route(lanes, street, c => stations.ContainsKey(c.Key), -1, FarthestCall);
            if (route == null)
                return null;
            var (cityId, station, stationFacing) = stations[route[0].Key];
            return new FireCall
            {
                cityId = city.id, stationCityId = cityId, building = building, station = station, facing = facing,
                stationFacing = stationFacing, street = street, stationStreet = route[0], route = route,
            };
        }

        /// <summary>
        /// The shortest road from <paramref name="from"/> to the nearest cell <paramref name="goal"/> accepts, listed from
        /// that goal back to <paramref name="from"/>; null when none is within <paramref name="limit"/> cells. Never enters
        /// the cell whose key is <paramref name="banned"/> (-1: none).
        /// </summary>
        public static List<Cell> Route(RoadLanes lanes, Cell from, Func<Cell, bool> goal, int banned, int limit)
        {
            var parent = new Dictionary<int, int> { [from.Key] = -1 };
            var queue = new Queue<(Cell cell, int depth)>();
            queue.Enqueue((from, 0));
            while (queue.Count > 0)
            {
                var (c, depth) = queue.Dequeue();
                if (goal(c))
                {
                    var path = new List<Cell>();
                    for (int key = c.Key; key != -1; key = parent[key])
                        path.Add(Cell.FromKey(key));
                    return path;
                }
                if (depth >= limit)
                    continue;
                int exits = lanes.Exits(c);
                for (int d = 0; d < 4; d++)
                {
                    var n = c.Move(d);
                    if ((exits & 1 << d) == 0 || n.Key == banned || parent.ContainsKey(n.Key))
                        continue;
                    parent.Add(n.Key, c.Key);
                    queue.Enqueue((n, depth + 1));
                }
            }
            return null;
        }
        /// <summary>
        /// The way the engine points when it parks by the burning building: along the street it drove in on, or, when it
        /// came in straight at the building, along the street towards a side it can drive off by.
        /// </summary>
        public static int ParkHeading(RoadLanes lanes, FireCall call)
        {
            var street = call.street;
            int arriving = call.route.Count > 1 ? Directions.Between(call.route[call.route.Count - 2], street) : call.stationFacing;
            if (arriving % 2 != call.facing % 2)
                return arriving;
            int left = (call.facing + 1) % 4, right = (call.facing + 3) % 4;
            return (lanes.Exits(street) & 1 << left) != 0 || (lanes.Exits(street) & 1 << right) == 0 ? left : right;
        }
        /// <summary>
        /// The drive home from the fire, street to station street: on ahead the way the engine is parked when that is at
        /// most LongestDetour cells longer than the way it came, otherwise back the way it came (turning round first).
        /// </summary>
        public static List<Cell> HomeRoute(RoadLanes lanes, FireCall call, int heading)
        {
            if ((lanes.Exits(call.street) & 1 << heading) != 0)
            {
                var ahead = Route(lanes, call.street.Move(heading), c => c.Equals(call.stationStreet), call.street.Key, FarthestCall);
                if (ahead != null && ahead.Count < call.route.Count + LongestDetour)
                {
                    // Route lists the station street first: turn it round and start from the fire.
                    ahead.Reverse();
                    ahead.Insert(0, call.street);
                    return ahead;
                }
            }
            var back = new List<Cell>(call.route);
            back.Reverse();
            return back;
        }
    }
}
