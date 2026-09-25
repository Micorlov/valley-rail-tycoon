using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Cosmetic road traffic: cars, trucks, buses and emergency vehicles with flashing beacons (see
    /// <see cref="VehicleCatalog"/>) roam the town streets and, once a highway opens, drive out along it to the next town.
    /// They keep their distance in the lane, take turns at junctions and wait before a level crossing while a train is
    /// near. Some junctions have traffic lights or a roundabout (see CityTraffic.Junctions), and now and then a car
    /// breaks down on a highway and a tow truck comes for it (see CityTraffic.Incidents). Never modifies the simulation
    /// or its saved random state.
    /// </summary>
    public sealed partial class CityTraffic : MonoBehaviour
    {
        sealed partial class Car
        {
            public Transform view;
            public int model;
            public Cell cell;
            public int entry, exit;
            public float progress;
            public bool isolated;
            // Nose to tail, in cells.
            public float length = CarLength;
            // The way on already chosen for the next cell; the junction group the car may drive into and the one it
            // queues for, with the clock when it joined that queue; the car it waits for this frame (-1: none or a train);
            // the last circle of cars all waiting for one another it was found in, and the circle whose cars it may drive
            // through until its next cell to break that circle (0: none).
            public int nextExit = -1, claim = -1, wants = -1, waitFor = -1, circle, passing;
            public float since, stalled;
            public Vector3 at, heading;
        }
        const int CarsPerTown = 12, TownSpacing = 4, HighwaySpacing = 10, MaxCars = 160;
        // A car turns onto a highway a quarter as often as into a town street. Once traffic has spread over the whole
        // network that keeps about half the cars in the towns and the rest on the highways between them.
        const int StreetWeight = 4, HighwayWeight = 1;
        const float CellsPerSecond = .65f, HoldLine = .85f, StreetDeck = .026f, BridgeDeck = .136f;
        // A car keeps half the two lengths plus Spacing behind the car ahead in its lane (up to LaneHalf either side of
        // its line). After StallCheck seconds standing still it looks for a circle of cars all waiting for one another.
        // Oncoming: the cosine below which another car's heading counts as coming the other way.
        const float CarLength = .34f, Spacing = .08f, LaneHalf = .16f, StallCheck = 2f, Oncoming = -.3f;
        readonly List<Car> cars = new List<Car>();
        readonly HashSet<int> tracksNearTrains = new HashSet<int>();
        // Junction or dead-end cell key → its group (the key of one member); neighbouring ones share a group.
        readonly Dictionary<int, int> junctions = new Dictionary<int, int>();
        readonly Stack<Cell> flood = new Stack<Cell>();
        float clock; // traffic seconds driven so far: orders the junction queues
        int circles; // waiting circles found so far: numbers them
        readonly System.Random random = new System.Random(731);
        // One merged body per vehicle model and livery, shared by every vehicle: a vehicle is a single renderer, not dozens of boxes.
        readonly Dictionary<(VehicleModel model, int livery), (Mesh mesh, Material[] finish)> bodies = new Dictionary<(VehicleModel, int), (Mesh, Material[])>();
        // Each model takes its liveries in turn, so two vans in a row are painted differently.
        readonly int[] liveryTurns = new int[VehicleCatalog.Count];
        // Police cars, ambulances and fire engines flash their beacons.
        readonly EmergencyLights lights = new EmergencyLights();
        /// <summary>The beacons of the police cars, ambulances and fire engines, and which of them are out on a call.</summary>
        public EmergencyLights Emergency => lights;
        WorldView world;
        GameSession game;
        RoadLanes lanes;
        int revision = -1, cityRevision = -1, spawned;
        /// <summary>The level-crossing barriers: cars wait until the booms are fully up. Without them cars go once the train is clear.</summary>
        public LevelCrossings barriers;
        /// <summary>True for a cell a tram covers or is about to enter (WorldView.LightRailBlocks); cars wait before it.</summary>
        public System.Func<Cell, bool> LightRailBlocks;

        public void Refresh(WorldView view, GameSession session)
        {
            world = view;
            if (game == session && revision == session.World.revision && cityRevision == session.World.cityRevision)
                return;
            game = session;
            revision = session.World.revision;
            cityRevision = session.World.cityRevision;
            var next = RoadLanes.Build(session.World, session.Network);
            if (next.SameAs(lanes))
                return;
            lanes = next;
            EndIncident();
            GroupJunctions();
            ControlJunctions();
            Populate(SpawnSlots());
        }
        /// <summary>Cars still on a road keep driving; cars on a vanished road leave and new ones fill the spawn slots evenly.</summary>
        void Populate(List<(Cell cell, int entry)> slots)
        {
            for (int i = cars.Count - 1; i >= 0; i--)
                if (!Drivable(cars[i].cell))
                    Remove(i);
            while (cars.Count > slots.Count)
                Remove(cars.Count - 1);
            homes = slots;
            foreach (var car in cars)
                if (car.isolated || (lanes.Exits(car.cell) & 1 << car.exit) == 0)
                    ChooseExit(car);
            var taken = new HashSet<int>();
            foreach (var car in cars)
                taken.Add(car.cell.Key);
            int missing = slots.Count - cars.Count;
            for (int k = 0; k < missing; k++)
            {
                // Spread new cars evenly over the slots, never on a cell where a car already stands.
                int i = k * slots.Count / missing;
                for (int tries = 0; tries < slots.Count && taken.Contains(slots[i].cell.Key); tries++)
                    i = (i + 1) % slots.Count;
                var slot = slots[i];
                if (!taken.Add(slot.cell.Key))
                    break;
                Spawn(slot.cell, slot.entry, .15f * (spawned % 4), VehicleCatalog.Mix[spawned % VehicleCatalog.Mix.Length]);
            }
        }
        /// <summary>A new vehicle of catalogue <paramref name="model"/> driving into <paramref name="cell"/> from side <paramref name="entry"/>.</summary>
        Car Spawn(Cell cell, int entry, float progress, int model)
        {
            var car = new Car { model = model, length = VehicleCatalog.Models[model].length, cell = cell, entry = entry, progress = progress };
            spawned++;
            ChooseExit(car);
            car.view = CreateCar(model, liveryTurns[model]++);
            lights.Fit(world, car.view, VehicleCatalog.Models[model]);
            cars.Add(car);
            Pose(car);
            return car;
        }
        /// <summary>A few cars in every town and one every ten cells of each open highway, alternating directions.</summary>
        List<(Cell cell, int entry)> SpawnSlots()
        {
            var slots = new List<(Cell cell, int entry)>();
            foreach (var city in game.World.cities)
            {
                int count = 0, index = 0;
                foreach (var road in city.roads)
                {
                    // Keep new cars off rail crossings and station platforms.
                    if (!Drivable(road.cell) || game.Network.At(road.cell) != null)
                        continue;
                    if (index++ % TownSpacing != 0 || count >= CarsPerTown)
                        continue;
                    slots.Add((road.cell, 2));
                    count++;
                }
            }
            foreach (var road in game.World.intercityRoads)
            {
                if (!road.Complete)
                    continue;
                for (int i = 1, seen = 0; i + 1 < road.path.Count; i++)
                {
                    var cell = road.path[i];
                    if (lanes.IsTownStreet(cell) || lanes.Exits(cell) == 0 || game.Network.At(cell) != null)
                        continue;
                    if (seen++ % HighwaySpacing != HighwaySpacing / 2)
                        continue;
                    var from = road.path[slots.Count % 2 == 0 ? i - 1 : i + 1];
                    if (cell.Distance(from) == 1)
                        slots.Add((cell, Directions.Between(cell, from)));
                }
            }
            if (slots.Count <= MaxCars)
                return slots;
            var even = new List<(Cell cell, int entry)>(MaxCars);
            for (int i = 0; i < MaxCars; i++)
                even.Add(slots[i * slots.Count / MaxCars]);
            return even;
        }
        /// <summary>A road cell a car may stand on: part of the road graph, or a lone town street away from rails and platforms.</summary>
        bool Drivable(Cell c) => lanes.Exits(c) != 0 ||
            (lanes.IsTownStreet(c) && game.Network.At(c) == null && !BuildService.StationFootprint(game.World, c));
        void Remove(int index)
        {
            var view = cars[index].view.gameObject;
            view.SetActive(false);
            Destroy(view);
            cars.RemoveAt(index);
        }
        Transform CreateCar(int model, int livery) => CreateVehicle(VehicleCatalog.Models[model], livery);
        Transform CreateVehicle(VehicleModel model, int livery)
        {
            var key = (model, livery % model.liveries.Length);
            if (!bodies.TryGetValue(key, out var body))
                bodies[key] = body = BuildBody(key.Item1, key.Item2);
            var car = new GameObject(model.name, typeof(MeshFilter), typeof(MeshRenderer));
            car.transform.SetParent(transform, false);
            car.GetComponent<MeshFilter>().sharedMesh = body.mesh;
            car.GetComponent<MeshRenderer>().sharedMaterials = body.finish;
            return car.transform;
        }
        /// <summary>A vehicle body from the traffic's shared meshes, for vehicles another view drives (the beach car parks).</summary>
        public Transform Vehicle(int model, int livery, Transform parent)
        {
            var vehicle = CreateCar(model, livery);
            vehicle.SetParent(parent, false);
            return vehicle;
        }
        /// <summary>Draws a vehicle from boxes once, then merges them into one mesh with a submesh per material.</summary>
        (Mesh mesh, Material[] finish) BuildBody(VehicleModel model, int livery)
        {
            var template = new GameObject("Vehicle template").transform;
            foreach (var part in model.Parts(livery))
                world.Box("Part", part.center, part.size, part.color, template);
            return Merge(template, model.name);
        }
        /// <summary>Merges the parts under <paramref name="template"/> into one mesh with a submesh per material, and destroys the template.</summary>
        static (Mesh mesh, Material[] finish) Merge(Transform template, string name)
        {
            var parts = new Dictionary<Material, List<CombineInstance>>();
            foreach (var filter in template.GetComponentsInChildren<MeshFilter>())
            {
                var material = filter.GetComponent<Renderer>().sharedMaterial;
                if (!parts.TryGetValue(material, out var list))
                    parts[material] = list = new List<CombineInstance>();
                var part = filter.transform;
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = Matrix4x4.TRS(part.localPosition, part.localRotation, part.localScale) });
            }
            var pieces = new List<CombineInstance>();
            var materials = new List<Material>();
            foreach (var pair in parts)
            {
                var piece = new Mesh();
                piece.CombineMeshes(pair.Value.ToArray(), true, true);
                pieces.Add(new CombineInstance { mesh = piece, transform = Matrix4x4.identity });
                materials.Add(pair.Key);
            }
            var body = new Mesh { name = name };
            body.CombineMeshes(pieces.ToArray(), false, true);
            foreach (var piece in pieces)
                Destroy(piece.mesh);
            template.gameObject.SetActive(false);
            Destroy(template.gameObject);
            return (body, materials.ToArray());
        }
        void ChooseExit(Car car)
        {
            int chosen = Pick(car.cell, car.entry);
            car.isolated = chosen < 0;
            car.exit = chosen < 0 ? 0 : chosen;
            Plan(car);
        }
        /// <summary>A random way on from a cell entered from side <paramref name="entry"/>, turning back only at a dead end; -1 when there is none.</summary>
        int Pick(Cell cell, int entry)
        {
            int mask = lanes.Exits(cell), chosen = -1, total = 0;
            for (int d = 0; d < 4; d++)
            {
                if (d == entry || (mask & 1 << d) == 0)
                    continue;
                int weight = lanes.IsHighway(cell, d) ? HighwayWeight : StreetWeight;
                total += weight;
                if (random.Next(total) < weight)
                    chosen = d;
            }
            if (chosen < 0 && (mask & 1 << entry) != 0)
                chosen = entry;
            return chosen;
        }
        /// <summary>Chooses the way through the next cell a cell early, so the car can see whether there is room beyond a junction.</summary>
        void Plan(Car car) => car.nextExit = car.isolated ? -1 : Pick(car.cell.Move(car.exit), Directions.Opp(car.exit));
        public void Animate(float seconds)
        {
            if (seconds <= 0 || game == null)
                return;
            RoadLanes.TracksNearTrains(game.World, tracksNearTrains);
            clock += seconds;
            lights.Drive(seconds);
            RunIncident(seconds);
            for (int i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                // A broken-down car and the tow truck coming for it follow the breakdown, not the traffic rules.
                if (car.trouble != Trouble.None && DriveIncident(car, seconds))
                    continue;
                // Keep the distance to the car ahead in the lane instead of driving into it.
                if (!car.isolated && (car.waitFor = Blocker(i)) >= 0)
                {
                    Stall(i, seconds);
                    continue;
                }
                // Longer vehicles hold further back, so every nose stops at the same line.
                float before = car.progress, start = before, hold = HoldLine - (car.length - CarLength) / 2;
                car.progress += seconds * CellsPerSecond * Pace(car);
                bool moved = false;
                while (true)
                {
                    if (!car.isolated && car.progress > hold && MustWait(i))
                    {
                        // Wait at the crossing or junction, without backing up if the car was already past the hold line.
                        car.progress = Mathf.Max(hold, before);
                        break;
                    }
                    if (car.progress < 1)
                        break;
                    car.progress -= 1;
                    before = 0;
                    moved = true;
                    if (!car.isolated)
                    {
                        car.cell = car.cell.Move(car.exit);
                        car.entry = Directions.Opp(car.exit);
                        car.claim = -1; // standing in the junction now keeps it busy
                        car.passing = 0;
                    }
                    TakePlannedExit(car);
                }
                if (moved || car.progress != start)
                {
                    car.stalled = 0;
                    car.waitFor = -1;
                }
                else
                    Stall(i, seconds);
                Pose(car);
            }
            RunSignals(seconds);
        }
        void TakePlannedExit(Car car)
        {
            if (car.isolated || car.nextExit < 0 || (lanes.Exits(car.cell) & 1 << car.nextExit) == 0)
            {
                ChooseExit(car);
                return;
            }
            car.exit = car.nextExit;
            Plan(car);
        }
        /// <summary>
        /// True while the car must stop before its next cell: a train near the level crossing there, no room to drive
        /// out beyond the junction there, or another car in, entering or queued earlier for that junction.
        /// </summary>
        bool MustWait(int index)
        {
            var car = cars[index];
            var next = car.cell.Move(car.exit);
            car.waitFor = -1;
            if (CrossingClosed(next))
                return true;
            if (LightRailBlocks != null && LightRailBlocks(next))
                return true;
            if (!junctions.TryGetValue(next.Key, out int group) || car.claim == group || GroupOf(car.cell) == group)
                return false;
            // Stop at a red or amber traffic light; only a green light lets a car queue for the junction.
            if (RedLight(car, next))
            {
                car.wants = -1;
                return true;
            }
            // Never stop inside a junction: a car standing there would block the cars that must leave it first.
            if ((car.waitFor = Occupant(index, next, group)) >= 0)
            {
                car.wants = -1;
                return true;
            }
            if (car.wants != group)
            {
                car.wants = group;
                car.since = clock;
            }
            if ((car.waitFor = Holder(index, group)) >= 0)
                return true;
            car.claim = group;
            car.wants = -1;
            return false;
        }
        int GroupOf(Cell c) => junctions.TryGetValue(c.Key, out int group) ? group : -1;
        /// <summary>A car in, entering or queued earlier for the junction group (or as early, earlier in the list); -1 when it is free.</summary>
        int Holder(int index, int group)
        {
            var car = cars[index];
            for (int j = 0; j < cars.Count; j++)
            {
                var other = cars[j];
                if (j == index || Passes(car, other) || other.isolated)
                    continue;
                if (other.claim == group || GroupOf(other.cell) == group ||
                    (other.wants == group && (other.since < car.since || (other.since == car.since && j < index))))
                    return j;
            }
            return -1;
        }
        /// <summary>A car too close to the start of the lane the car will leave the junction group by; -1 when there is room.</summary>
        int Occupant(int index, Cell junction, int group)
        {
            var car = cars[index];
            if (car.nextExit < 0)
                return -1;
            var beyond = junction.Move(car.nextExit);
            if (GroupOf(beyond) == group)
                return -1;
            int side = Directions.Opp(car.nextExit);
            for (int j = 0; j < cars.Count; j++)
            {
                var other = cars[j];
                if (j != index && !Passes(car, other) && !other.isolated && other.cell.Equals(beyond) && other.entry == side &&
                    other.progress < car.length / 2 + (car.length + other.length) / 2 + Spacing)
                    return j;
            }
            return -1;
        }
        /// <summary>The first car standing just ahead in this car's lane, or -1. Of two cars facing each other, the first in the list drives on.</summary>
        int Blocker(int index)
        {
            var car = cars[index];
            for (int j = 0; j < cars.Count; j++)
            {
                var other = cars[j];
                if (j == index || Passes(car, other) || other.isolated || OnShoulder(other) || !Ahead(car, other) || (j > index && Ahead(other, car)))
                    continue;
                return j;
            }
            return -1;
        }
        /// <summary>
        /// Whether the other car is in this car's lane, ahead by less than half their two lengths plus Spacing. A car coming
        /// the other way never is: mid-turn a car can point at the oncoming lane it is about to pass.
        /// </summary>
        static bool Ahead(Car car, Car other)
        {
            float dx = other.at.x - car.at.x, dz = other.at.z - car.at.z, reach = (car.length + other.length) / 2 + Spacing;
            if (Mathf.Abs(dx) > reach || Mathf.Abs(dz) > reach || car.heading.x * other.heading.x + car.heading.z * other.heading.z < Oncoming)
                return false;
            float along = dx * car.heading.x + dz * car.heading.z, across = dx * car.heading.z - dz * car.heading.x;
            return along > -.01f && along < reach && Mathf.Abs(across) < LaneHalf;
        }
        static bool Passes(Car car, Car other) => car.passing != 0 && other.circle == car.passing;
        /// <summary>
        /// Counts how long a car has stood still. When it waits in a circle of cars that all wait for one another (a
        /// train is never part of one), the first car of the circle drives through the others until its next cell.
        /// </summary>
        void Stall(int index, float seconds)
        {
            var car = cars[index];
            car.stalled += seconds;
            if (car.stalled < StallCheck || car.waitFor < 0)
                return;
            int first = index;
            for (int k = car.waitFor, steps = 0; k >= 0 && steps < cars.Count; k = cars[k].waitFor, steps++)
            {
                if (k == index)
                {
                    if (first != index)
                        return;
                    car.passing = ++circles;
                    for (int m = car.waitFor; m != index; m = cars[m].waitFor)
                        cars[m].circle = circles;
                    return;
                }
                if (k < first)
                    first = k;
            }
        }
        /// <summary>
        /// Groups every junction (three or more ways) and dead end (where cars turn back) with the ones beside it. One car
        /// at a time drives through a group, so cars never cross paths in a junction or meet on a turning circle.
        /// </summary>
        void GroupJunctions()
        {
            junctions.Clear();
            foreach (var city in game.World.cities)
                foreach (var road in city.roads)
                    Group(road.cell);
            foreach (var road in game.World.intercityRoads)
                foreach (var cell in road.path)
                    Group(cell);
            foreach (var car in cars)
            {
                car.claim = car.wants = car.waitFor = -1;
                car.passing = 0;
                car.stalled = 0;
                Plan(car);
            }
        }
        void Group(Cell start)
        {
            if (!Shared(start) || junctions.ContainsKey(start.Key))
                return;
            junctions[start.Key] = start.Key;
            flood.Push(start);
            while (flood.Count > 0)
            {
                var c = flood.Pop();
                int exits = lanes.Exits(c);
                for (int d = 0; d < 4; d++)
                {
                    var n = c.Move(d);
                    if ((exits & 1 << d) == 0 || !Shared(n) || junctions.ContainsKey(n.Key))
                        continue;
                    junctions[n.Key] = start.Key;
                    flood.Push(n);
                }
            }
        }
        bool Shared(Cell c)
        {
            int ways = Directions.Count(lanes.Exits(c));
            return ways == 1 || ways >= 3;
        }
        bool CrossingClosed(Cell c)
        {
            var track = game.Network.At(c);
            return track != null && (tracksNearTrains.Contains(track.id) || (barriers != null && !barriers.Open(track.id)));
        }
        static Vector3 Direction(int d) => new Vector3(Directions.Dx[d], 0, Directions.Dz[d]);
        /// <summary>Highways cross water on a raised deck; town streets never do. Every road crosses rails on a level crossing's deck.</summary>
        internal float Deck(Cell c) => OnRails(c) ? LevelCrossings.DeckTop : MapDefinition.Water(c) ? BridgeDeck : StreetDeck;
        bool OnRails(Cell c) => game.Network.At(c) != null;
        void Pose(Car car)
        {
            float deck = Deck(car.cell);
            var center = new Vector3(car.cell.x, deck, car.cell.z);
            Vector3 p, forward;
            if (car.isolated)
            {
                float angle = car.progress * Mathf.PI * 2;
                p = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * .23f;
                forward = new Vector3(Mathf.Cos(angle), 0, -Mathf.Sin(angle));
            }
            else
            {
                float t = car.progress;
                // Round a roundabout's island; everywhere else a curve from the lane at the entry edge to the exit edge,
                // car.lane right of the centre line (on the hard shoulder for a tow truck passing a jam).
                if (!Ring(car, out p, out forward))
                {
                    var incoming = -Direction(car.entry);
                    var outgoing = Direction(car.exit);
                    var a = -incoming * .5f + Vector3.Cross(Vector3.up, incoming) * car.lane;
                    var b = outgoing * .5f + Vector3.Cross(Vector3.up, outgoing) * car.lane;
                    var control = (Vector3.Cross(Vector3.up, incoming) + Vector3.Cross(Vector3.up, outgoing)) * (car.lane / 2);
                    p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * control + t * t * b;
                    forward = 2 * ((1 - t) * (control - a) + t * (b - control));
                }
                // Meet the neighbouring cell's deck halfway across the edge, so cars ramp onto a bridge instead of jumping.
                // A level crossing's deck is flat all the way across; the street beside it ramps the whole way up to it.
                var beside = car.cell.Move(t < .5f ? car.entry : car.exit);
                float edge = Deck(beside), share = OnRails(car.cell) ? 0 : OnRails(beside) ? 1 : .5f;
                p.y = (edge - deck) * share * Mathf.Abs(t - .5f) * 2;
            }
            car.at = center + p;
            car.view.localPosition = car.at;
            if (forward.sqrMagnitude > .00001f)
            {
                car.heading = new Vector3(forward.x, 0, forward.z).normalized;
                car.view.localRotation = Quaternion.LookRotation(forward);
            }
        }
        void Update()
        {
            lights.Flash(Time.unscaledTime);
            FlashIncident(Time.unscaledTime);
            ShowSignalsWhenNear();
        }
        void OnDestroy()
        {
            lights.Release();
            ReleaseJunctions();
            foreach (var body in bodies.Values)
                if (body.mesh)
                    Destroy(body.mesh);
        }
    }
}
