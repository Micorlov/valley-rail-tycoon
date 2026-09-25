using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Cosmetic life at the ski resorts (SkiResorts, drawn by SkiArt): gondola cabins run up one cable and down the other
    /// without end; skiers carve down the three pistes, walk from the plaza to the valley station, ride up and ski again.
    /// Once a town's road reaches the resort, cars drive in, park in the bays and let out skiers who take a few runs and
    /// then drive home. Skiers show only when zoomed in; cabins and cars always. Everything stops while the game is paused.
    /// Never reads or changes simulation randomness.
    /// </summary>
    public sealed class SkiLife : MonoBehaviour
    {
        enum Drive { Arriving, Parked, Leaving }
        enum Mode { Skiing, Walking, Riding }
        sealed class Car
        {
            public Transform view;
            public Drive drive;
            public int bay, aboard;
            public readonly List<Vector3> route = new List<Vector3>();
            public int leg;
            public float along, wait;
            public bool reversing;
        }
        sealed class Skier
        {
            public Transform view;
            public Mode mode;
            public Car car;
            public int piste, runsLeft;
            // Skiing: t down the piste, carving phase and pace. Walking: waypoints. Riding: seconds left in the cabin.
            public float t, phase, pace, rideLeft, along;
            public readonly List<Vector3> route = new List<Vector3>();
            public int leg;
            /// <summary>Where a walk ends: the lift (then ride up) or the car (then get in).</summary>
            public bool toCar;
        }
        sealed class Resort
        {
            public SkiLayout layout;
            public Transform root, people;
            public readonly List<Transform> cabins = new List<Transform>();
            public float cableLength, loop, travel;
            public readonly List<Skier> skiers = new List<Skier>();
            public readonly List<Car> cars = new List<Car>();
            public Car[] inBay;
            public List<Vector3> laneIn, laneOut;
            public int target;
            public float nextArrival;
            public bool open;
        }
        const float FigureScale = 1.9f, CabinSpeed = .45f, CabinSpacing = 1.5f, WalkSpeed = .28f, CarSpeed = .55f, TurnRate = 280f, Lane = .2f, RoadDeck = .028f;
        const int Residents = 9;
        // Mostly cars and SUVs in the mountains, with the odd van.
        static readonly int[] Models = { 2, 1, 0, 2, 5, 1, 2, 4, 0, 2 };
        static readonly Color[] Jackets =
        {
            new Color(.9f, .15f, .2f), new Color(.15f, .45f, .9f), new Color(.98f, .75f, .1f), new Color(.1f, .7f, .55f),
            new Color(.95f, .45f, .75f), new Color(.2f, .2f, .25f), new Color(.98f, .5f, .1f), new Color(.6f, .3f, .85f),
        };
        static readonly Color CabinRed = new Color(.8f, .14f, .12f), CabinYellow = new Color(.96f, .76f, .16f), CabinGlass = new Color(.16f, .24f, .3f),
            CabinRoof = new Color(.72f, .74f, .76f), Hanger = new Color(.2f, .2f, .22f), Ski = new Color(.95f, .95f, .95f);
        readonly List<Resort> resorts = new List<Resort>();
        readonly Dictionary<int, (Mesh mesh, Material[] finish)> cabinBodies = new Dictionary<int, (Mesh, Material[])>();
        readonly System.Random random = new System.Random(4861);
        WorldView world;
        CityTraffic traffic;
        GameSession session;
        int cityRevision = -1;
        bool showingPeople = true;

        public int Resorts => resorts.Count;
        public int Cabins(int resort) => resorts[resort].cabins.Count;
        public int Skiers(int resort) => resorts[resort].skiers.Count;
        public int Skiing(int resort) => resorts[resort].skiers.FindAll(s => s.mode == Mode.Skiing).Count;
        public int Riding(int resort) => resorts[resort].skiers.FindAll(s => s.mode == Mode.Riding).Count;
        public int Cars(int resort) => resorts[resort].cars.Count;
        public int ParkedCars(int resort) => resorts[resort].cars.FindAll(c => c.drive == Drive.Parked).Count;
        public int Bays(int resort) => resorts[resort].layout.bays.Count;
        public bool Open(int resort) => resorts[resort].open;
        public SkiLayout Layout(int resort) => resorts[resort].layout;
        public Transform Cabin(int resort, int index) => resorts[resort].cabins[index];
        /// <summary>Runs skied to the bottom, cars that drove in and cars that drove home since the game loaded.</summary>
        public int Runs { get; private set; }
        public int Arrivals { get; private set; }
        public int Departures { get; private set; }

        /// <summary>Starts every resort's life once per session; afterwards only notices a road opening.</summary>
        public void Refresh(WorldView view, GameSession game, CityTraffic vehicles)
        {
            world = view;
            traffic = vehicles;
            if (session != game)
            {
                foreach (var r in resorts)
                    Destroy(r.root.gameObject);
                resorts.Clear();
                session = game;
                cityRevision = -1;
                foreach (var layout in view.SkiLayouts)
                    resorts.Add(OpenResort(layout));
            }
            if (cityRevision == game.World.cityRevision)
                return;
            cityRevision = game.World.cityRevision;
            foreach (var resort in resorts)
            {
                var road = game.Cities.SkiRoadOf(resort.layout.resort.id);
                bool open = road != null && road.Complete;
                resort.target = Target(game, road, resort.layout.bays.Count);
                if (!open || resort.open)
                    continue;
                resort.open = true;
                resort.laneIn = LaneIn(resort.layout, road);
                resort.laneOut = LaneOut(resort.layout, road);
                // A road that opens (or a game that loads) finds some cars already parked, their skiers up the mountain.
                for (int i = 0; i < resort.target / 2 + 1; i++)
                    ParkNow(resort);
                resort.nextArrival = 1 + 3 * Next();
            }
        }
        /// <summary>Bigger towns send more cars, always leaving a few bays free for the next arrivals.</summary>
        static int Target(GameSession game, IntercityRoadState road, int bays)
        {
            var town = road == null ? null : game.Cities.CityFor(road.a);
            int population = town != null ? town.population : 0;
            return Mathf.Clamp(4 + population / 1000, 4, bays - 3);
        }
        Resort OpenResort(SkiLayout layout)
        {
            var resort = new Resort { layout = layout, inBay = new Car[layout.bays.Count] };
            resort.root = new GameObject("Ski resort " + layout.resort.name).transform;
            resort.root.SetParent(transform, false);
            resort.people = new GameObject("Skiers").transform;
            resort.people.SetParent(resort.root, false);
            resort.people.gameObject.SetActive(showingPeople);
            resort.cableLength = SkiLayout.CableLength(layout.upCable);
            resort.loop = 2 * resort.cableLength;
            int count = Mathf.Max(6, Mathf.RoundToInt(resort.loop / CabinSpacing));
            for (int i = 0; i < count; i++)
            {
                var cabin = CabinView(i % 3 == 2 ? 1 : 0, resort.root);
                resort.cabins.Add(cabin);
            }
            PlaceCabins(resort);
            // Guests staying at the lodge ski all day: most on the pistes, a few in the gondola.
            for (int i = 0; i < Residents; i++)
            {
                var skier = AddSkier(resort, null);
                if (i % 4 == 3)
                {
                    skier.mode = Mode.Riding;
                    skier.rideLeft = resort.cableLength / CabinSpeed * Next();
                    skier.view.gameObject.SetActive(false);
                }
                else
                    StartRun(resort, skier, Next() * .9f);
            }
            return resort;
        }
        Transform CabinView(int paint, Transform parent)
        {
            if (!cabinBodies.TryGetValue(paint, out var body))
                cabinBodies[paint] = body = BuildCabin(paint == 0 ? CabinRed : CabinYellow);
            var cabin = new GameObject("Gondola cabin", typeof(MeshFilter), typeof(MeshRenderer));
            cabin.transform.SetParent(parent, false);
            cabin.GetComponent<MeshFilter>().sharedMesh = body.mesh;
            cabin.GetComponent<MeshRenderer>().sharedMaterials = body.finish;
            return cabin.transform;
        }
        /// <summary>A cabin hanging from its grip: origin on the cable, the body CabinDrop below, windows all round.</summary>
        (Mesh mesh, Material[] finish) BuildCabin(Color paint)
        {
            var template = new GameObject("Cabin template").transform;
            float drop = SkiLayout.CabinDrop;
            world.Box("Grip", new Vector3(0, -.01f, 0), new Vector3(.03f, .04f, .06f), Hanger, template);
            world.Box("Hanger", new Vector3(0, -drop / 2, 0), new Vector3(.018f, drop, .018f), Hanger, template);
            world.Box("Cabin roof", new Vector3(0, -drop + .005f, 0), new Vector3(.2f, .03f, .24f), CabinRoof, template);
            world.Box("Cabin windows", new Vector3(0, -drop - .06f, 0), new Vector3(.19f, .08f, .23f), CabinGlass, template);
            world.Box("Cabin body", new Vector3(0, -drop - .12f, 0), new Vector3(.2f, .1f, .24f), paint, template);
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
            var mesh = new Mesh { name = "Gondola cabin" };
            mesh.CombineMeshes(pieces.ToArray(), false, true);
            foreach (var piece in pieces)
                Destroy(piece.mesh);
            template.gameObject.SetActive(false);
            Destroy(template.gameObject);
            return (mesh, materials.ToArray());
        }
        /// <summary>Spaces the cabins evenly round the loop: up the one cable, round the top bullwheel, down the other.</summary>
        void PlaceCabins(Resort resort)
        {
            var l = resort.layout;
            for (int i = 0; i < resort.cabins.Count; i++)
            {
                float s = Mathf.Repeat(resort.travel + i * resort.loop / resort.cabins.Count, resort.loop);
                bool up = s < resort.cableLength;
                var at = up ? SkiLayout.OnCable(l.upCable, s, out var heading) : SkiLayout.OnCable(l.downCable, resort.loop - s, out heading);
                var cabin = resort.cabins[i];
                cabin.localPosition = at;
                cabin.localRotation = Quaternion.LookRotation(up ? heading : -heading);
            }
        }
        Skier AddSkier(Resort resort, Car car)
        {
            var jacket = Jackets[random.Next(Jackets.Length)];
            var view = CityLife.Figure(world, resort.people, "Skier", jacket, FigureScale);
            float s = FigureScale;
            world.Box("Hat", new Vector3(0, .15f, 0) * s, new Vector3(.034f, .014f, .034f) * s, Jackets[random.Next(Jackets.Length)], view);
            for (int side = -1; side <= 1; side += 2)
                world.Box("Ski", new Vector3(side * .012f, .002f, .01f) * s, new Vector3(.01f, .004f, .15f) * s, Ski, view);
            var skier = new Skier { view = view, car = car, pace = .32f + .22f * Next(), phase = Next() * 6.28f, runsLeft = 1 + random.Next(3) };
            resort.skiers.Add(skier);
            return skier;
        }
        void StartRun(Resort resort, Skier skier, float t = 0)
        {
            skier.mode = Mode.Skiing;
            skier.piste = random.Next(resort.layout.pistes.Count);
            skier.t = t;
            skier.view.gameObject.SetActive(true);
            PoseSkier(resort, skier);
        }
        void Walk(Resort resort, Skier skier, bool toCar, params Vector3[] points)
        {
            skier.mode = Mode.Walking;
            skier.toCar = toCar;
            skier.route.Clear();
            skier.route.Add(skier.view.localPosition);
            skier.route.AddRange(points);
            skier.leg = 0;
            skier.along = 0;
            skier.view.gameObject.SetActive(true);
        }
        float Next() => (float)random.NextDouble();

        /// <summary>The road's inbound lane over its last few cells, then through the gate to the aisle, on the right-hand side.</summary>
        static List<Vector3> LaneIn(SkiLayout l, IntercityRoadState road)
        {
            var lane = new List<Vector3>();
            int n = road.path.Count;
            for (int i = Mathf.Max(0, n - 4); i < n; i++)
            {
                var cell = road.path[i];
                var ahead = i + 1 < n ? Step(cell, road.path[i + 1]) : l.up;
                lane.Add(new Vector3(cell.x, RoadDeck, cell.z) + Right(ahead) * Lane);
            }
            lane.Add(l.At(SkiLayout.LaneA, SkiLayout.FrontF, SkiLayout.Deck));
            lane.Add(l.At(SkiLayout.LaneA, SkiLayout.AisleF, SkiLayout.Deck));
            return lane;
        }
        /// <summary>The outbound lane: from the aisle out of the gate and back along the road.</summary>
        static List<Vector3> LaneOut(SkiLayout l, IntercityRoadState road)
        {
            int n = road.path.Count;
            var lane = new List<Vector3> { l.At(-SkiLayout.LaneA, SkiLayout.AisleF, SkiLayout.Deck), l.At(-SkiLayout.LaneA, SkiLayout.FrontF, SkiLayout.Deck) };
            for (int i = n - 1; i >= Mathf.Max(0, n - 4); i--)
            {
                var cell = road.path[i];
                var ahead = i > 0 ? Step(cell, road.path[i - 1]) : -l.up;
                lane.Add(new Vector3(cell.x, RoadDeck, cell.z) + Right(ahead) * Lane);
            }
            return lane;
        }
        static Vector3 Step(Cell from, Cell to)
        {
            var d = new Vector3(to.x - from.x, 0, to.z - from.z);
            return d.sqrMagnitude > 0 ? d.normalized : Vector3.forward;
        }
        static Vector3 Right(Vector3 ahead) => Vector3.Cross(Vector3.up, ahead);

        int FreeBay(Resort resort)
        {
            int free = 0, chosen = -1;
            for (int i = 0; i < resort.inBay.Length; i++)
                if (resort.inBay[i] == null && random.Next(++free) == 0)
                    chosen = i;
            return chosen;
        }
        Car NewCar(Resort resort, int bay)
        {
            var car = new Car { bay = bay, view = traffic.Vehicle(Models[random.Next(Models.Length)], random.Next(8), resort.root) };
            resort.inBay[bay] = car;
            resort.cars.Add(car);
            return car;
        }
        /// <summary>A car already standing in a free bay, its skiers somewhere on the mountain.</summary>
        void ParkNow(Resort resort)
        {
            int bay = FreeBay(resort);
            if (bay < 0)
                return;
            var car = NewCar(resort, bay);
            var spot = resort.layout.bays[bay];
            car.view.localPosition = spot.at;
            car.view.localRotation = Quaternion.LookRotation(spot.facing);
            car.drive = Drive.Parked;
            car.aboard = 1 + random.Next(3);
            for (int i = 0; i < car.aboard; i++)
                StartRun(resort, AddSkier(resort, car), Next() * .9f);
        }
        void Arrive(Resort resort)
        {
            int bay = FreeBay(resort);
            if (bay < 0)
                return;
            var car = NewCar(resort, bay);
            var spot = resort.layout.bays[bay];
            Arrivals++;
            car.drive = Drive.Arriving;
            car.route.AddRange(resort.laneIn);
            car.route.Add(spot.aisle);
            car.route.Add(spot.at);
            car.view.localPosition = car.route[0];
            car.view.localRotation = Quaternion.LookRotation(car.route[1] - car.route[0]);
        }
        void Leave(Resort resort, Car car)
        {
            var spot = resort.layout.bays[car.bay];
            car.drive = Drive.Leaving;
            car.route.Clear();
            car.route.Add(spot.at);
            car.route.Add(spot.aisle);
            car.route.AddRange(resort.laneOut);
            car.leg = 0;
            car.along = 0;
            car.reversing = true;
        }
        /// <summary>Beside the car, on the aisle side of its bay.</summary>
        static Vector3 Door(Resort resort, Car car)
        {
            var spot = resort.layout.bays[car.bay];
            return new Vector3(spot.at.x, 0, spot.at.z) - spot.facing * .25f + Vector3.Cross(Vector3.up, spot.facing) * .14f;
        }
        /// <summary>One to three skiers get out and walk over to the valley station.</summary>
        void Unload(Resort resort, Car car)
        {
            car.drive = Drive.Parked;
            car.aboard = 1 + random.Next(3);
            var door = Door(resort, car);
            var l = resort.layout;
            for (int i = 0; i < car.aboard; i++)
            {
                var skier = AddSkier(resort, car);
                skier.view.localPosition = door + l.right * (.07f * i);
                Walk(resort, skier, false, l.At(-SkiLayout.LaneA - .25f, SkiLayout.AisleF, 0), l.At(.35f, -.2f, 0), l.At(.4f, .3f, 0), l.liftDoor + l.right * (.1f * i));
            }
        }

        /// <summary>Moves everything; skiers show only when zoomed in. Nothing moves while the game is paused.</summary>
        public void Animate(float dt, float speed, bool near)
        {
            if (showingPeople != near)
            {
                showingPeople = near;
                foreach (var resort in resorts)
                    resort.people.gameObject.SetActive(near);
            }
            if (speed <= 0)
                return;
            float step = dt * Mathf.Min(speed, 2);
            foreach (var resort in resorts)
            {
                resort.travel = Mathf.Repeat(resort.travel + CabinSpeed * step, resort.loop);
                PlaceCabins(resort);
                if (resort.open)
                {
                    int coming = 0;
                    foreach (var car in resort.cars)
                        if (car.drive != Drive.Leaving)
                            coming++;
                    resort.nextArrival -= step;
                    if (resort.nextArrival <= 0)
                    {
                        resort.nextArrival = 3 + 5 * Next();
                        if (coming < resort.target)
                            Arrive(resort);
                    }
                }
                for (int i = resort.cars.Count - 1; i >= 0; i--)
                    DriveCar(resort, resort.cars[i], step);
                for (int i = resort.skiers.Count - 1; i >= 0; i--)
                    Move(resort, resort.skiers[i], step);
            }
        }
        void DriveCar(Resort resort, Car car, float step)
        {
            if (car.drive == Drive.Parked)
            {
                if (car.aboard == 0 && (car.wait -= step) <= 0)
                    Leave(resort, car);
                return;
            }
            float distance = CarSpeed * step;
            while (distance > 0 && car.leg + 1 < car.route.Count)
            {
                float length = Vector3.Distance(car.route[car.leg], car.route[car.leg + 1]), left = length - car.along;
                if (distance < left)
                {
                    car.along += distance;
                    distance = 0;
                }
                else
                {
                    distance -= left;
                    car.leg++;
                    car.along = 0;
                    car.reversing = false;
                }
            }
            if (car.leg + 1 >= car.route.Count)
            {
                Finish(resort, car);
                return;
            }
            Vector3 from = car.route[car.leg], to = car.route[car.leg + 1];
            float span = Vector3.Distance(from, to);
            car.view.localPosition = Vector3.Lerp(from, to, span > 1e-4f ? car.along / span : 1);
            var ahead = to - from;
            ahead.y = 0;
            if (ahead.sqrMagnitude > 1e-6f)
                car.view.localRotation = Quaternion.RotateTowards(car.view.localRotation, Quaternion.LookRotation(car.reversing ? -ahead : ahead), TurnRate * step);
        }
        void Finish(Resort resort, Car car)
        {
            if (car.drive == Drive.Arriving)
            {
                var spot = resort.layout.bays[car.bay];
                car.view.localPosition = spot.at;
                car.view.localRotation = Quaternion.LookRotation(spot.facing);
                Unload(resort, car);
                return;
            }
            Departures++;
            resort.inBay[car.bay] = null;
            resort.cars.Remove(car);
            car.view.gameObject.SetActive(false);
            Destroy(car.view.gameObject);
        }
        void Move(Resort resort, Skier skier, float step)
        {
            var l = resort.layout;
            switch (skier.mode)
            {
                case Mode.Riding:
                    if ((skier.rideLeft -= step) > 0)
                        return;
                    // Out of the top station and off down a piste.
                    StartRun(resort, skier);
                    return;
                case Mode.Skiing:
                    skier.t += skier.pace * step / Length(l, skier.piste);
                    skier.phase += step * (2.2f + skier.pace * 2);
                    if (skier.t < 1)
                    {
                        PoseSkier(resort, skier);
                        return;
                    }
                    Runs++;
                    skier.runsLeft--;
                    // At the bottom: back to the car when a visiting skier is done, else over to the lift again.
                    if (skier.car != null && skier.runsLeft <= 0)
                        Walk(resort, skier, true, l.At(.3f, .95f, 0), l.At(.35f, -.2f, 0), l.At(-SkiLayout.LaneA - .25f, SkiLayout.AisleF, 0), Door(resort, skier.car));
                    else
                        // Round the front of the valley station, never through it, to its doors.
                        Walk(resort, skier, false, l.At(.3f, .95f, 0), l.At(.4f, .3f, 0), l.liftDoor + l.right * ((Next() - .5f) * .3f));
                    return;
                case Mode.Walking:
                    float distance = WalkSpeed * step;
                    while (distance > 0 && skier.leg + 1 < skier.route.Count)
                    {
                        float length = Vector3.Distance(skier.route[skier.leg], skier.route[skier.leg + 1]), left = length - skier.along;
                        if (distance < left)
                        {
                            skier.along += distance;
                            distance = 0;
                        }
                        else
                        {
                            distance -= left;
                            skier.leg++;
                            skier.along = 0;
                        }
                    }
                    if (skier.leg + 1 >= skier.route.Count)
                    {
                        Arrived(resort, skier);
                        return;
                    }
                    Vector3 from = skier.route[skier.leg], to = skier.route[skier.leg + 1];
                    float span = Vector3.Distance(from, to);
                    var at = Vector3.Lerp(from, to, span > 1e-4f ? skier.along / span : 1);
                    skier.view.localPosition = SkiLayout.OnGround(at, .025f);
                    var ahead = to - from;
                    ahead.y = 0;
                    if (ahead.sqrMagnitude > 1e-6f)
                        skier.view.localRotation = Quaternion.LookRotation(ahead);
                    return;
            }
        }
        /// <summary>A walk is over: into the gondola, or into the car (which leaves once everyone is back).</summary>
        void Arrived(Resort resort, Skier skier)
        {
            if (!skier.toCar)
            {
                skier.mode = Mode.Riding;
                skier.rideLeft = resort.cableLength / CabinSpeed;
                skier.view.gameObject.SetActive(false);
                return;
            }
            var car = skier.car;
            resort.skiers.Remove(skier);
            skier.view.gameObject.SetActive(false);
            Destroy(skier.view.gameObject);
            if (--car.aboard <= 0)
                car.wait = 1 + 2 * Next();
        }
        static float Length(SkiLayout l, int piste)
        {
            var line = l.pistes[piste].line;
            return Mathf.Max(1, (line.Count - 1) * .15f);
        }
        /// <summary>Across the piste and back in linked turns, facing the way the skier is travelling.</summary>
        void PoseSkier(Resort resort, Skier skier)
        {
            var l = resort.layout;
            float side = Mathf.Sin(skier.phase) * .7f;
            var at = l.OnPiste(skier.piste, skier.t, side, out var downhill);
            var ahead = l.OnPiste(skier.piste, skier.t + .02f, Mathf.Sin(skier.phase + .25f) * .7f, out _) - at;
            skier.view.localPosition = at + Vector3.up * .04f;
            ahead.y = 0;
            if (ahead.sqrMagnitude > 1e-6f)
                skier.view.localRotation = Quaternion.LookRotation(ahead) * Quaternion.Euler(0, 0, -Mathf.Cos(skier.phase) * 14);
        }
        void OnDestroy()
        {
            foreach (var body in cabinBodies.Values)
                if (body.mesh)
                    Destroy(body.mesh);
        }
    }
}
