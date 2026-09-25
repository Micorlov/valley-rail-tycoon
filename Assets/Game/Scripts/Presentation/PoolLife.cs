using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Cosmetic life at every open-air pool, on its <see cref="PoolLayout"/> plan. Cars turn in from the street, park in the
    /// painted bays and later drive off again; their families walk over the zebra to the pool gate, swim lengths, sunbathe
    /// on the loungers or towels, splash in the paddling pool, go down the water park's slide and walk back to the car.
    /// Regulars swim lengths all day, children play in the paddling pool, a diver keeps jumping off the board, sliders keep
    /// climbing the tower and the lifeguard watches from the tall chair. People show only when zoomed in; cars always.
    /// Seeded per pool, never touching simulation randomness.
    /// </summary>
    public sealed class PoolLife : MonoBehaviour
    {
        /// <summary>A pool to bring to life: its lot frame, footprint, the driveway's column and whether the street runs on past it.</summary>
        public readonly struct Venue
        {
            public readonly Vector3 at;
            public readonly Quaternion turn;
            public readonly int hash, size;
            public readonly float gate;
            public readonly bool fromWest, toEast;
            public Venue(Vector3 at, Quaternion turn, int hash, int size, float gate, bool fromWest, bool toEast)
            {
                this.at = at;
                this.turn = turn;
                this.hash = hash;
                this.size = size;
                this.gate = gate;
                this.fromWest = fromWest;
                this.toEast = toEast;
            }
            public bool Same(Venue other) => (at - other.at).sqrMagnitude < 1e-4f && Quaternion.Angle(turn, other.turn) < 1 && size == other.size &&
                Mathf.Abs(gate - other.gate) < .01f && fromWest == other.fromWest && toEast == other.toEast;
        }
        enum Drive { Arriving, Parked, Leaving }
        enum Mode { Walk, Swim, Wade, Lie, Jump, Climb, Slide, Watch }
        enum Act { None, Swim, Lounge, Towel, Paddle, Dive, Slide, Home }
        enum Role { Visitor, Regular, Sunbather, Kid, Diver, Slider, Lifeguard }
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
        sealed class Person
        {
            public Transform view, splash;
            public Transform[] arms;
            public Role role;
            public Mode mode;
            public Act act;
            public Car car;
            public readonly List<Vector3> route = new List<Vector3>();
            public readonly Queue<Act> plan = new Queue<Act>();
            public int leg, lengths, seat = -1, lane;
            public float along, pace, rest, phase, scale, jump;
            public bool onBoard, poised, leaving, towel;
            /// <summary>Where a swimmer or paddler climbs out onto the deck.</summary>
            public Vector3 exit;
        }
        sealed class Pool
        {
            public Venue venue;
            public PoolLayout layout;
            public Transform root, people, splash;
            public List<PoolLayout.Bay> bays;
            public Car[] inBay;
            public bool[] loungers, towels;
            public readonly List<Car> cars = new List<Car>();
            public readonly List<Person> crowd = new List<Person>();
            public System.Random random;
            public int target;
            public float nextArrival, splashAge = -1;
        }
        // People are a little larger than pavement walkers, like the beach and the station crowds, so they read on the deck.
        const float Adult = 1.8f, Child = 1.25f, CarSpeed = .5f, TurnRate = 280f, WalkBob = .012f, JumpTime = .8f, JumpApex = .28f, JumpReach = .42f,
            SplashTime = .6f, ClimbPace = .22f, SlidePace = 1.1f;
        const int MaxCars = 18;
        // Mostly family cars, with the odd SUV or pickup.
        static readonly int[] Models = { 0, 1, 2, 0, 1, 2, 0, 5, 1, 2 };
        static readonly Color Skin = new Color(.93f, .76f, .6f), Spray = new Color(.95f, .98f, 1f), LifeguardRed = new Color(.88f, .14f, .12f);
        static readonly Color[] Swimwear = { new Color(.9f, .2f, .3f), new Color(.15f, .5f, .85f), new Color(.98f, .7f, .15f), new Color(.2f, .7f, .55f), new Color(.95f, .45f, .7f), new Color(.95f, .95f, .92f) };
        readonly List<Pool> pools = new List<Pool>();
        WorldView world;
        CityTraffic traffic;
        GameSession session;
        int cityRevision = -1;
        float time;
        bool showingPeople = true;

        public int Pools => pools.Count;
        public int Arrivals { get; private set; }
        public int Departures { get; private set; }
        public int Dives { get; private set; }
        public int Slides { get; private set; }
        public int Size(int pool) => pools[pool].layout.Size;
        public int Cars(int pool) => pools[pool].cars.Count;
        public int ParkedCars(int pool) => pools[pool].cars.FindAll(c => c.drive == Drive.Parked).Count;
        public int Bays(int pool) => pools[pool].bays.Count;
        public int People(int pool) => pools[pool].crowd.Count;
        public int Swimmers(int pool) => pools[pool].crowd.FindAll(p => p.mode == Mode.Swim).Count;
        public int Paddlers(int pool) => pools[pool].crowd.FindAll(p => p.mode == Mode.Wade).Count;
        public int Sunbathers(int pool) => pools[pool].crowd.FindAll(p => p.mode == Mode.Lie).Count;
        public Transform Root(int pool) => pools[pool].root;
        /// <summary>Swimmers in the lot frame, to check they stay in the water.</summary>
        public List<Vector3> SwimmerSpots(int pool)
        {
            var spots = new List<Vector3>();
            foreach (var p in pools[pool].crowd)
                if (p.mode == Mode.Swim)
                    spots.Add(p.view.localPosition);
            return spots;
        }
        /// <summary>Parked cars in the lot frame, to check they stand in their bays.</summary>
        public List<Vector3> ParkedSpots(int pool)
        {
            var spots = new List<Vector3>();
            foreach (var c in pools[pool].cars)
                if (c.drive == Drive.Parked)
                    spots.Add(c.view.localPosition);
            return spots;
        }

        /// <summary>Brings new pools to life and drops demolished ones; pools that stay keep their cars and people.</summary>
        public void Refresh(WorldView view, CityTraffic vehicles, GameSession game, List<Venue> venues)
        {
            world = view;
            traffic = vehicles;
            if (session == game && cityRevision == game.World.cityRevision)
                return;
            if (session != game)
                for (int i = pools.Count - 1; i >= 0; i--)
                    Close(i);
            session = game;
            cityRevision = game.World.cityRevision;
            var kept = new List<Pool>();
            foreach (var venue in venues)
            {
                var pool = pools.Find(p => p.venue.Same(venue) && !kept.Contains(p)) ?? Open(venue);
                pool.target = Target(game, pool);
                kept.Add(pool);
            }
            for (int i = pools.Count - 1; i >= 0; i--)
                if (!kept.Contains(pools[i]))
                    Close(i);
            pools.Clear();
            pools.AddRange(kept);
        }
        void Close(int index)
        {
            pools[index].root.gameObject.SetActive(false);
            Destroy(pools[index].root.gameObject);
            pools.RemoveAt(index);
        }
        /// <summary>Bigger pools and bigger towns fill more of the car park, always leaving a couple of bays free.</summary>
        static int Target(GameSession game, Pool pool)
        {
            var at = pool.venue.at;
            var town = game.Cities.CityAt(new Cell(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.z)));
            int population = town != null ? town.population : 0;
            return Mathf.Max(1, Mathf.Min(2 + pool.layout.Size + population / 1500, Mathf.Min(MaxCars, pool.bays.Count - 2)));
        }

        Pool Open(Venue venue)
        {
            var L = PoolLayout.For(venue.size);
            var pool = new Pool { venue = venue, layout = L, bays = L.Bays(venue.gate), random = new System.Random(venue.hash * 17 + 3) };
            pool.inBay = new Car[pool.bays.Count];
            pool.loungers = new bool[L.Loungers.Length];
            pool.towels = new bool[L.Towels.Length];
            pool.root = new GameObject("Swimming pool").transform;
            pool.root.SetParent(transform, false);
            pool.root.localPosition = venue.at;
            pool.root.localRotation = venue.turn;
            pool.people = new GameObject("Pool people").transform;
            pool.people.SetParent(pool.root, false);
            pool.people.gameObject.SetActive(showingPeople);
            pool.splash = world.Box("Splash", Vector3.zero, Vector3.one, Spray, pool.people).transform;
            pool.splash.gameObject.SetActive(false);
            pool.target = Target(session, pool);
            // The people who are there all day.
            if (L.HasChair)
            {
                var guard = Add(pool, Role.Lifeguard, Adult, L.Chair + new Vector3(0, L.ChairTop + .012f, 0) - L.ChairFacing * .01f, LifeguardRed, Mode.Watch);
                guard.view.localRotation = Quaternion.LookRotation(L.ChairFacing);
            }
            int regulars = Mathf.Min(3, L.Lanes);
            for (int i = 0; i < regulars; i++)
            {
                int lane = regulars > 1 ? i * (L.Lanes - 1) / (regulars - 1) : 0;
                var swimmer = Add(pool, Role.Regular, Adult, new Vector3(Mathf.Lerp(L.SwimFrom, L.SwimTo, Next(pool)), 0, L.LaneZ(lane)), null, Mode.Swim);
                swimmer.view.localRotation = Quaternion.LookRotation(i % 2 == 0 ? Vector3.right : Vector3.left);
                swimmer.lane = lane;
                swimmer.lengths = int.MaxValue;
                swimmer.pace = .16f + .06f * Next(pool);
                swimmer.route.Add(new Vector3(i % 2 == 0 ? L.SwimTo : L.SwimFrom, 0, L.LaneZ(lane)));
            }
            if (L.HasPaddling)
                for (int i = 0; i < L.Size - 1; i++)
                {
                    var kid = Add(pool, Role.Kid, Child, PaddleSpot(pool), null, Mode.Wade);
                    kid.lengths = int.MaxValue;
                    kid.pace = .12f + .06f * Next(pool);
                    kid.route.Add(PaddleSpot(pool));
                }
            for (int seat = 1; seat < L.Loungers.Length; seat += 3)
                LieDown(pool, Add(pool, Role.Sunbather, Adult, LoungerSpot(L, seat)), seat, false);
            for (int towel = 0; towel < L.Towels.Length; towel += 2)
                LieDown(pool, Add(pool, Role.Sunbather, Adult, TowelSpot(L, towel)), towel, true);
            if (L.HasBoard)
                Next(pool, Add(pool, Role.Diver, Adult, new Vector3(L.Pool.xMin + .3f, 0, L.PoolSide)));
            if (L.HasSlide)
                for (int i = 0; i < 3; i++)
                {
                    var slider = Add(pool, Role.Slider, i == 0 ? Adult : Child, new Vector3(L.CrossWalks[L.CrossWalks.Length - 1], 0, L.PoolSide));
                    slider.rest = 3 * i;
                    Next(pool, slider);
                }
            // A pool that opens (or a game that loads) already has some cars, their families inside.
            for (int i = 0; i < pool.target / 2 + 1; i++)
                ParkNow(pool);
            pool.nextArrival = 1 + 3 * Next(pool);
            return pool;
        }
        Person Add(Pool pool, Role role, float scale, Vector3 at, Color? shirt = null, Mode mode = Mode.Walk)
        {
            var person = new Person
            {
                role = role,
                mode = mode,
                scale = scale,
                pace = (.28f + .12f * Next(pool)) * (scale < Adult ? .85f : 1),
                phase = Next(pool) * 10,
                view = CityLife.Figure(world, pool.people, "Pool " + role, shirt ?? Swimwear[pool.random.Next(Swimwear.Length)], scale),
            };
            person.route.Add(at);
            person.view.localPosition = Grounded(pool, person, at);
            pool.crowd.Add(person);
            return person;
        }
        float Next(Pool pool) => (float)pool.random.NextDouble();
        Vector3 PaddleSpot(Pool pool)
        {
            var r = pool.layout.Paddling;
            return new Vector3(Mathf.Lerp(r.xMin + .08f, r.xMax - .08f, Next(pool)), 0, Mathf.Lerp(r.yMin + .08f, r.yMax - .08f, Next(pool)));
        }
        /// <summary>Where someone lies on lounger <paramref name="seat"/> (feet by the pool, head at the backrest).</summary>
        static Vector3 LoungerSpot(PoolLayout L, int seat) => new Vector3(L.Loungers[seat], 0, L.LoungerRow - .11f);
        /// <summary>The gap beside a lounger to stand in: the side without a parasol pole.</summary>
        static Vector3 BesideLounger(PoolLayout L, int seat) => new Vector3(L.Loungers[seat] + (seat % 2 == 0 ? -1 : 1) * L.LoungerPitch / 2, 0, L.LoungerRow + .03f);
        static Vector3 TowelSpot(PoolLayout L, int towel) => L.Towels[towel] + new Vector3(0, 0, -.12f);
        static Vector3 BesideTowel(PoolLayout L, int towel) => L.Towels[towel] + new Vector3(.12f, 0, 0);
        static int NearestLadder(PoolLayout L, float x) => Mathf.Abs(x - L.Ladders[0]) <= Mathf.Abs(x - L.Ladders[1]) ? 0 : 1;
        static float Row(PoolLayout L, float z) => z > (L.Promenade + L.PoolSide) / 2 ? L.Promenade : L.PoolSide;
        static Vector3 Here(Person p) => p.route[p.route.Count - 1];

        // Cars.
        int FreeBay(Pool pool)
        {
            int free = 0, chosen = -1;
            for (int i = 0; i < pool.inBay.Length; i++)
                if (pool.inBay[i] == null && pool.random.Next(++free) == 0)
                    chosen = i;
            return chosen;
        }
        Car NewCar(Pool pool, int bay)
        {
            var car = new Car { bay = bay, view = traffic.Vehicle(Models[pool.random.Next(Models.Length)], pool.random.Next(8), pool.root) };
            pool.inBay[bay] = car;
            pool.cars.Add(car);
            return car;
        }
        void ParkNow(Pool pool)
        {
            int bay = FreeBay(pool);
            if (bay < 0)
                return;
            var car = NewCar(pool, bay);
            car.view.localPosition = pool.bays[bay].at;
            car.view.localRotation = Quaternion.LookRotation(pool.bays[bay].facing);
            Unload(pool, car, true);
        }
        /// <summary>A car along the street, into the driveway, down the aisle and forwards into a free bay.</summary>
        void Arrive(Pool pool)
        {
            int bay = FreeBay(pool);
            if (bay < 0)
                return;
            var L = pool.layout;
            var car = NewCar(pool, bay);
            var spot = pool.bays[bay];
            float gate = pool.venue.gate;
            Arrivals++;
            car.drive = Drive.Arriving;
            car.route.Add(new Vector3(gate - (pool.venue.fromWest ? 1 : .45f), L.StreetTop, L.StreetLane));
            car.route.Add(new Vector3(gate - .14f, L.StreetTop, L.StreetLane));
            car.route.Add(new Vector3(gate - .1f, L.ParkTop, L.Hedge));
            car.route.Add(new Vector3(gate - .1f, L.ParkTop, L.Aisle));
            car.route.Add(new Vector3(spot.at.x, L.ParkTop, L.Aisle));
            car.route.Add(spot.at);
            car.view.localPosition = car.route[0];
            car.view.localRotation = Quaternion.LookRotation(car.route[1] - car.route[0]);
        }
        /// <summary>Reverses out into the aisle, drives to the driveway and turns right onto the street, away from the gate.</summary>
        void Leave(Pool pool, Car car)
        {
            var L = pool.layout;
            var spot = pool.bays[car.bay];
            float gate = pool.venue.gate;
            car.drive = Drive.Leaving;
            car.route.Clear();
            car.route.Add(spot.at);
            car.route.Add(new Vector3(spot.at.x, L.ParkTop, L.Aisle));
            car.route.Add(new Vector3(gate + .1f, L.ParkTop, L.Aisle));
            car.route.Add(new Vector3(gate + .1f, L.ParkTop, L.Hedge));
            car.route.Add(new Vector3(gate + .14f, L.StreetTop, L.StreetLane));
            car.route.Add(new Vector3(gate + (pool.venue.toEast ? 1 : .45f), L.StreetTop, L.StreetLane));
            car.leg = 0;
            car.along = 0;
            car.reversing = true;
        }
        /// <summary>One to four people get out: a grown-up first, children after. They head for the gate, or are already in.</summary>
        void Unload(Pool pool, Car car, bool alreadyInside)
        {
            var L = pool.layout;
            car.drive = Drive.Parked;
            int roll = pool.random.Next(20);
            car.aboard = roll < 5 ? 1 : roll < 12 ? 2 : roll < 17 ? 3 : 4;
            var door = Door(pool, car);
            for (int i = 0; i < car.aboard; i++)
            {
                bool kid = i > 0 && Next(pool) < .5f;
                var start = alreadyInside ? new Vector3(Mathf.Lerp(-L.Half + .4f, L.Half - .4f, Next(pool)), 0, L.Promenade) : door + new Vector3(0, 0, .08f * i);
                var person = Add(pool, Role.Visitor, kid ? Child : Adult, start);
                person.car = car;
                int activities = 1 + pool.random.Next(2);
                for (int a = 0; a < activities; a++)
                    person.plan.Enqueue(Pick(pool, kid));
                if (alreadyInside)
                {
                    Next(pool, person);
                    continue;
                }
                float walk = L.Aisle - .12f;
                person.route.Add(new Vector3(door.x, 0, walk));
                person.route.Add(new Vector3(L.EntranceX, 0, walk));
                person.route.Add(new Vector3(L.EntranceX, 0, L.Fence));
                person.route.Add(new Vector3(L.EntranceX, 0, L.Promenade));
                // Stagger a family so they walk in a line rather than on top of each other.
                person.rest = .5f * i;
            }
        }
        /// <summary>What a visitor does next, from what this pool has: children like the paddling pool and the slide.</summary>
        Act Pick(Pool pool, bool kid)
        {
            var L = pool.layout;
            float roll = Next(pool);
            if (L.HasSlide && roll < (kid ? .45f : .25f))
                return Act.Slide;
            roll = Next(pool);
            if (kid)
                return L.HasPaddling && roll < .65f ? Act.Paddle : Act.Swim;
            if (roll < .45f)
                return Act.Swim;
            return L.Loungers.Length > 0 && roll < .8f ? Act.Lounge : L.Towels.Length > 0 ? Act.Towel : Act.Swim;
        }
        /// <summary>Where passengers get out: in the gap beside the car.</summary>
        static Vector3 Door(Pool pool, Car car)
        {
            var spot = pool.bays[car.bay];
            return new Vector3(spot.at.x + pool.layout.BayPitch / 2, 0, spot.at.z);
        }

        /// <summary>Moves everything; people show only when zoomed in.</summary>
        public void Animate(float dt, float speed, bool near)
        {
            if (showingPeople != near)
            {
                showingPeople = near;
                foreach (var pool in pools)
                    pool.people.gameObject.SetActive(near);
            }
            if (speed <= 0)
                return;
            float step = dt * Mathf.Min(speed, 2);
            time += step;
            foreach (var pool in pools)
            {
                int coming = 0;
                foreach (var car in pool.cars)
                    if (car.drive != Drive.Leaving)
                        coming++;
                pool.nextArrival -= step;
                if (pool.nextArrival <= 0)
                {
                    pool.nextArrival = 4 + 5 * Next(pool);
                    if (coming < pool.target)
                        Arrive(pool);
                }
                for (int i = pool.cars.Count - 1; i >= 0; i--)
                    DriveCar(pool, pool.cars[i], step);
                for (int i = pool.crowd.Count - 1; i >= 0; i--)
                    Move(pool, pool.crowd[i], step);
                Splash(pool, step);
            }
        }
        void DriveCar(Pool pool, Car car, float step)
        {
            if (car.drive == Drive.Parked)
            {
                if (car.aboard == 0 && (car.wait -= step) <= 0)
                    Leave(pool, car);
                return;
            }
            float distance = CarSpeed * step;
            while (distance > 0 && car.leg + 1 < car.route.Count)
            {
                float left = Vector3.Distance(car.route[car.leg], car.route[car.leg + 1]) - car.along;
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
                Finish(pool, car);
                return;
            }
            Vector3 from = car.route[car.leg], to = car.route[car.leg + 1];
            float length = Vector3.Distance(from, to);
            car.view.localPosition = Vector3.Lerp(from, to, length > 1e-4f ? car.along / length : 1);
            var ahead = to - from;
            ahead.y = 0;
            if (ahead.sqrMagnitude > 1e-6f)
                car.view.localRotation = Quaternion.RotateTowards(car.view.localRotation, Quaternion.LookRotation(car.reversing ? -ahead : ahead), TurnRate * step);
        }
        void Finish(Pool pool, Car car)
        {
            if (car.drive == Drive.Arriving)
            {
                var spot = pool.bays[car.bay];
                car.view.localPosition = spot.at;
                car.view.localRotation = Quaternion.LookRotation(spot.facing);
                Unload(pool, car, false);
                return;
            }
            Departures++;
            pool.inBay[car.bay] = null;
            pool.cars.Remove(car);
            car.view.gameObject.SetActive(false);
            Destroy(car.view.gameObject);
        }

        // People.
        void Move(Pool pool, Person p, float step)
        {
            switch (p.mode)
            {
                case Mode.Watch:
                    // The lifeguard scans the water, turning slowly from one end of the pool to the other.
                    p.view.localRotation = Quaternion.LookRotation(pool.layout.ChairFacing) * Quaternion.Euler(0, 40 * Mathf.Sin(time * .5f + p.phase), 0);
                    return;
                case Mode.Jump:
                    Jump(pool, p, step);
                    return;
                case Mode.Lie:
                    if (p.role == Role.Sunbather || (p.rest -= step) > 0)
                        return;
                    GetUp(pool, p);
                    return;
            }
            bool swimming = p.mode == Mode.Swim;
            Stroke(p, swimming && p.rest <= 0);
            if (p.rest > 0)
            {
                p.rest -= step;
                if (swimming)
                    p.view.localPosition = Grounded(pool, p, p.view.localPosition); // treading water
                return;
            }
            if (p.leg + 1 >= p.route.Count)
            {
                Arrived(pool, p);
                return;
            }
            Vector3 a = p.route[p.leg], b = p.route[p.leg + 1];
            float length = Vector3.Distance(a, b);
            p.along += p.pace * step;
            if (p.along >= length)
            {
                p.leg++;
                p.along = 0;
                p.view.localPosition = Grounded(pool, p, b);
                return;
            }
            var at = Vector3.Lerp(a, b, length > 1e-4f ? p.along / length : 1);
            p.view.localPosition = Grounded(pool, p, at) + Vector3.up * (p.mode == Mode.Walk ? Mathf.Abs(Mathf.Sin(p.phase + time * 11)) * WalkBob : 0);
            var ahead = b - a;
            ahead.y = 0;
            if (ahead.sqrMagnitude > 1e-6f)
                p.view.localRotation = Quaternion.RotateTowards(p.view.localRotation, Quaternion.LookRotation(ahead), 400 * step);
        }
        /// <summary>
        /// The height a person stands at: the car park, the deck or lawn, the diving board, waist-deep or swimming. Climbers
        /// and sliders follow the heights in their route.
        /// </summary>
        Vector3 Grounded(Pool pool, Person p, Vector3 at)
        {
            var L = pool.layout;
            switch (p.mode)
            {
                case Mode.Swim:
                    // Only the head and a stroking arm clear the water; the body hides under the opaque water and the ground.
                    at.y = L.WaterTop + .012f - .13f * p.scale + .006f * Mathf.Sin(p.phase + time * 4);
                    return at;
                case Mode.Wade:
                    at.y = L.WaterTop - .075f * p.scale;
                    return at;
                case Mode.Lie:
                case Mode.Watch:
                case Mode.Jump:
                case Mode.Climb:
                case Mode.Slide:
                    return at;
            }
            at.y = p.onBoard ? L.BoardTop : at.z > L.Fence ? L.ParkTop : L.HasLawn && L.Lawn.Contains(new Vector2(at.x, at.z)) ? L.DeckTop + .006f : L.DeckTop;
            return at;
        }
        /// <summary>Freestyle: arms windmill over the water and the kick sprays behind; hidden out of the water.</summary>
        void Stroke(Person p, bool on)
        {
            if (p.arms == null)
            {
                if (!on)
                    return;
                p.arms = new Transform[2];
                for (int side = 0; side < 2; side++)
                {
                    var pivot = new GameObject("Arm").transform;
                    pivot.SetParent(p.view, false);
                    // Shoulders just under the surface, so each arm clears the water only on its recovery.
                    pivot.localPosition = new Vector3((side == 0 ? -1 : 1) * .03f, .1f, 0) * p.scale;
                    world.Box("Arm", new Vector3(0, .026f, 0) * p.scale, new Vector3(.012f, .052f, .012f) * p.scale, Skin, pivot);
                    p.arms[side] = pivot;
                }
                p.splash = world.Box("Kick", new Vector3(0, .125f, -.1f) * p.scale, Vector3.one, Spray, p.view).transform;
            }
            if (p.splash.gameObject.activeSelf != on)
            {
                p.splash.gameObject.SetActive(on);
                p.arms[0].gameObject.SetActive(on);
                p.arms[1].gameObject.SetActive(on);
            }
            if (!on)
                return;
            float angle = time * 260 + p.phase * 57;
            p.arms[0].localRotation = Quaternion.Euler(angle, 0, 0);
            p.arms[1].localRotation = Quaternion.Euler(angle + 180, 0, 0);
            float kick = .7f + .5f * Mathf.Abs(Mathf.Sin(time * 9 + p.phase));
            p.splash.localScale = new Vector3(.05f * kick, .008f, .09f * kick) * p.scale / Adult;
        }

        /// <summary>A person reached the end of their route: what they do next.</summary>
        void Arrived(Pool pool, Person p)
        {
            var L = pool.layout;
            var here = Here(p);
            p.route.Clear();
            p.route.Add(here);
            p.leg = 0;
            p.along = 0;
            switch (p.mode)
            {
                case Mode.Swim:
                    if (p.lengths > 0)
                    {
                        p.lengths--;
                        p.route.Add(new Vector3(here.x > (L.SwimFrom + L.SwimTo) / 2 ? L.SwimFrom : L.SwimTo, 0, L.LaneZ(p.lane)));
                        p.rest = .3f + .8f * Next(pool); // a breather at the wall
                    }
                    else if (!p.leaving)
                    {
                        float ladder = L.Ladders[NearestLadder(L, here.x)];
                        p.route.Add(new Vector3(ladder, 0, here.z));
                        p.route.Add(new Vector3(ladder, 0, L.Pool.yMax - .07f));
                        p.exit = new Vector3(ladder, 0, L.Pool.yMax + .08f);
                        p.leaving = true;
                    }
                    else
                        StepOut(pool, p);
                    return;
                case Mode.Wade:
                    if (p.lengths-- > 0)
                    {
                        p.route.Add(PaddleSpot(pool));
                        p.rest = .5f + 1.5f * Next(pool);
                    }
                    else if (!p.leaving)
                    {
                        p.route.Add(new Vector3(L.Paddling.center.x, 0, L.Paddling.yMax - .07f));
                        p.exit = new Vector3(L.Paddling.center.x, 0, L.Paddling.yMax + .08f);
                        p.leaving = true;
                    }
                    else
                        StepOut(pool, p);
                    return;
                case Mode.Climb:
                    // At the top of the tower: sit in and go.
                    p.mode = Mode.Slide;
                    p.pace = SlidePace;
                    p.route.Clear();
                    foreach (var point in L.Chute)
                        p.route.Add(point + Vector3.up * .01f);
                    p.view.localPosition = p.route[0];
                    return;
                case Mode.Slide:
                    Splashdown(pool, p, here);
                    return;
            }
            // On foot.
            if (p.onBoard)
            {
                if (!p.poised)
                {
                    // A moment at the end of the board, looking at the water, then off.
                    p.poised = true;
                    p.rest = .5f + Next(pool);
                    p.view.localRotation = Quaternion.LookRotation(Vector3.right);
                    return;
                }
                p.poised = false;
                p.mode = Mode.Jump;
                p.jump = 0;
                return;
            }
            switch (p.act)
            {
                case Act.Swim:
                    p.mode = Mode.Swim;
                    p.lane = QuietLane(pool);
                    p.lengths = 1 + pool.random.Next(3);
                    p.pace = .14f + .06f * Next(pool);
                    p.route.Clear();
                    p.route.Add(new Vector3(here.x, 0, L.Pool.yMax - .07f));
                    p.route.Add(new Vector3(here.x, 0, L.LaneZ(p.lane)));
                    p.route.Add(new Vector3(here.x > (L.SwimFrom + L.SwimTo) / 2 ? L.SwimFrom : L.SwimTo, 0, L.LaneZ(p.lane)));
                    p.view.localPosition = Grounded(pool, p, p.route[0]);
                    return;
                case Act.Lounge:
                case Act.Towel:
                    LieDown(pool, p, p.seat, p.act == Act.Towel);
                    return;
                case Act.Paddle:
                    p.mode = Mode.Wade;
                    p.lengths = 2 + pool.random.Next(4);
                    p.route.Clear();
                    p.route.Add(new Vector3(here.x, 0, L.Paddling.yMax - .07f));
                    p.route.Add(PaddleSpot(pool));
                    p.view.localPosition = Grounded(pool, p, p.route[0]);
                    return;
                case Act.Dive:
                    // Up onto the board and out along it.
                    p.onBoard = true;
                    p.route.Clear();
                    p.route.Add(new Vector3(L.BoardFoot, 0, here.z));
                    p.route.Add(new Vector3(L.BoardTip - .03f, 0, here.z));
                    p.view.localPosition = Grounded(pool, p, p.route[0]);
                    return;
                case Act.Slide:
                    // Up the rungs on the tower's side, then onto its deck.
                    p.mode = Mode.Climb;
                    p.pace = ClimbPace;
                    p.route.Clear();
                    p.route.Add(new Vector3(L.SlideFoot.x, L.DeckTop, L.SlideFoot.z));
                    p.route.Add(new Vector3(L.SlideFoot.x, L.TowerTop + .03f, L.SlideFoot.z));
                    p.route.Add(L.Chute[0] + Vector3.up * .03f);
                    p.view.localPosition = p.route[0];
                    p.view.localRotation = Quaternion.LookRotation(Vector3.left);
                    return;
                case Act.Home:
                    pool.crowd.Remove(p);
                    p.view.gameObject.SetActive(false);
                    Destroy(p.view.gameObject);
                    if (--p.car.aboard == 0)
                        p.car.wait = .8f + Next(pool);
                    return;
                default:
                    Next(pool, p);
                    return;
            }
        }
        /// <summary>Out of the chute into the splash pool with a splash, then a swim to its ladder.</summary>
        void Splashdown(Pool pool, Person p, Vector3 end)
        {
            var L = pool.layout;
            Slides++;
            pool.splashAge = 0;
            pool.splash.localPosition = new Vector3(end.x, L.WaterTop + .004f, end.z);
            p.mode = Mode.Swim;
            p.lengths = 0;
            p.leaving = true;
            p.pace = .16f + .05f * Next(pool);
            var surfaced = new Vector3(end.x, 0, end.z);
            p.route.Clear();
            p.route.Add(surfaced);
            p.route.Add(new Vector3(L.LeisureLadder, 0, end.z));
            p.route.Add(new Vector3(L.LeisureLadder, 0, L.Leisure.yMax - .07f));
            p.exit = new Vector3(L.LeisureLadder, 0, L.Leisure.yMax + .08f);
            p.leg = 0;
            p.along = 0;
            p.rest = .3f;
            p.view.localPosition = Grounded(pool, p, surfaced);
        }
        /// <summary>Climbs out of a pool onto the deck and carries on with the day.</summary>
        void StepOut(Pool pool, Person p)
        {
            p.mode = Mode.Walk;
            p.leaving = false;
            p.pace = (.28f + .12f * Next(pool)) * (p.scale < Adult ? .85f : 1);
            Stroke(p, false);
            p.route.Clear();
            p.route.Add(p.exit);
            p.view.localPosition = Grounded(pool, p, p.exit);
            Next(pool, p);
        }
        /// <summary>The lane with the fewest swimmers, so newcomers spread out.</summary>
        int QuietLane(Pool pool)
        {
            var use = new int[pool.layout.Lanes];
            foreach (var other in pool.crowd)
                if (other.mode == Mode.Swim && other.lane < use.Length)
                    use[other.lane]++;
            int best = pool.random.Next(use.Length);
            for (int i = 0; i < use.Length; i++)
                if (use[i] < use[best])
                    best = i;
            return best;
        }
        /// <summary>Picks the next thing to do and walks there: a ladder, a free lounger or towel, the paddling pool, the board, the slide or home.</summary>
        void Next(Pool pool, Person p)
        {
            var L = pool.layout;
            var act = p.plan.Count > 0 ? p.plan.Dequeue() : p.role == Role.Diver ? Act.Dive : p.role == Role.Slider ? Act.Slide : Act.Home;
            if (act == Act.Lounge || act == Act.Towel)
            {
                var taken = act == Act.Towel ? pool.towels : pool.loungers;
                int seat = FreeSeat(pool, taken);
                if (seat < 0)
                    act = Act.Swim; // everywhere's taken: have a swim instead
                else
                {
                    taken[seat] = true;
                    p.seat = seat;
                }
            }
            p.act = act;
            var here = Here(p);
            switch (act)
            {
                case Act.Swim:
                    WalkTo(L, p, here, new Vector3(L.Ladders[NearestLadder(L, here.x)], 0, L.Pool.yMax + .07f));
                    break;
                case Act.Lounge:
                    WalkTo(L, p, here, BesideLounger(L, p.seat));
                    break;
                case Act.Towel:
                    WalkTo(L, p, here, BesideTowel(L, p.seat));
                    break;
                case Act.Paddle:
                    WalkTo(L, p, here, new Vector3(L.Paddling.center.x, 0, L.Paddling.yMax + .08f));
                    break;
                case Act.Dive:
                    WalkTo(L, p, here, new Vector3(L.BoardFoot, 0, L.Pool.center.y));
                    break;
                case Act.Slide:
                    WalkTo(L, p, here, L.SlideFoot);
                    break;
                default:
                    // Out through the gate, over the zebra and back to the car.
                    WalkTo(L, p, here, new Vector3(L.EntranceX, 0, L.Fence));
                    var door = Door(pool, p.car);
                    float walk = L.Aisle - .12f;
                    p.route.Add(new Vector3(L.EntranceX, 0, walk));
                    p.route.Add(new Vector3(door.x, 0, walk));
                    p.route.Add(door);
                    break;
            }
        }
        int FreeSeat(Pool pool, bool[] taken)
        {
            int free = 0, chosen = -1;
            for (int i = 0; i < taken.Length; i++)
                if (!taken[i] && pool.random.Next(++free) == 0)
                    chosen = i;
            return chosen;
        }
        /// <summary>
        /// A walk on the deck: out to the nearer walk (the promenade by the lawn, or the poolside), across between the
        /// loungers at the nearest gap if the goal is on the other walk, along it and in to the goal.
        /// </summary>
        static void WalkTo(PoolLayout L, Person p, Vector3 from, Vector3 goal)
        {
            float rowFrom = Row(L, from.z), rowTo = Row(L, goal.z);
            p.route.Clear();
            p.leg = 0;
            p.along = 0;
            p.route.Add(from);
            Via(p, new Vector3(from.x, 0, rowFrom));
            if (rowFrom != rowTo && L.CrossWalks.Length > 0)
            {
                float middle = (from.x + goal.x) / 2, cross = L.CrossWalks[0];
                foreach (float x in L.CrossWalks)
                    if (Mathf.Abs(x - middle) < Mathf.Abs(cross - middle))
                        cross = x;
                Via(p, new Vector3(cross, 0, rowFrom));
                Via(p, new Vector3(cross, 0, rowTo));
            }
            Via(p, new Vector3(goal.x, 0, rowTo));
            Via(p, goal);
        }
        static void Via(Person p, Vector3 point)
        {
            if ((point - Here(p)).sqrMagnitude > 1e-6f)
                p.route.Add(point);
        }
        void LieDown(Pool pool, Person p, int seat, bool towel)
        {
            var L = pool.layout;
            (towel ? pool.towels : pool.loungers)[seat] = true;
            p.seat = seat;
            p.towel = towel;
            p.mode = Mode.Lie;
            p.rest = 6 + 8 * Next(pool);
            var at = towel ? TowelSpot(L, seat) : LoungerSpot(L, seat);
            // The root sits a body's half-thickness above the cushion or towel.
            at.y = (towel ? L.DeckTop + .008f : L.LoungerTop) + .015f * p.scale;
            p.view.localPosition = at;
            // Flat on the back, head towards the backrest (+z), feet towards the pool.
            p.view.localRotation = Quaternion.Euler(-90, 180, 0);
        }
        void GetUp(Pool pool, Person p)
        {
            var L = pool.layout;
            (p.towel ? pool.towels : pool.loungers)[p.seat] = false;
            var stand = p.towel ? BesideTowel(L, p.seat) : BesideLounger(L, p.seat);
            p.seat = -1;
            p.mode = Mode.Walk;
            p.route.Clear();
            p.route.Add(stand);
            p.leg = 0;
            p.along = 0;
            p.view.localPosition = Grounded(pool, p, stand);
            p.view.localRotation = Quaternion.LookRotation(Vector3.forward);
            Next(pool, p);
        }
        /// <summary>Off the end of the board in an arc, head first into the water with a splash, then a swim to the ladder.</summary>
        void Jump(Pool pool, Person p, float step)
        {
            var L = pool.layout;
            p.jump += step / JumpTime;
            float t = Mathf.Clamp01(p.jump), tip = L.BoardTip - .03f, z = Here(p).z;
            float water = L.WaterTop + .012f - .13f * p.scale;
            float y = Mathf.Lerp(L.BoardTop, water, t * t) + 4 * JumpApex * t * (1 - t);
            p.view.localPosition = new Vector3(tip + JumpReach * t, y, z);
            p.view.localRotation = Quaternion.LookRotation(Vector3.right) * Quaternion.Euler(150 * t * t, 0, 0);
            if (t < 1)
                return;
            Dives++;
            pool.splashAge = 0;
            pool.splash.localPosition = new Vector3(tip + JumpReach, L.WaterTop + .004f, z);
            p.onBoard = false;
            p.mode = Mode.Swim;
            p.lane = (L.Lanes - 1) / 2;
            p.lengths = 0;
            p.leaving = true;
            var surfaced = new Vector3(tip + JumpReach, 0, z);
            float ladder = L.Ladders[0];
            p.route.Clear();
            p.route.Add(surfaced);
            p.route.Add(new Vector3(ladder, 0, z));
            p.route.Add(new Vector3(ladder, 0, L.Pool.yMax - .07f));
            p.exit = new Vector3(ladder, 0, L.Pool.yMax + .08f);
            p.leg = 0;
            p.along = 0;
            p.rest = .4f;
            p.view.localRotation = Quaternion.LookRotation(Vector3.left);
            p.view.localPosition = Grounded(pool, p, surfaced);
        }
        /// <summary>A splash (off the board or out of the chute): a ring of spray that spreads and sinks away.</summary>
        void Splash(Pool pool, float step)
        {
            if (pool.splashAge < 0)
                return;
            pool.splashAge += step;
            float s = pool.splashAge / SplashTime;
            bool on = s < 1;
            if (pool.splash.gameObject.activeSelf != on)
                pool.splash.gameObject.SetActive(on);
            if (!on)
            {
                pool.splashAge = -1;
                return;
            }
            pool.splash.localScale = new Vector3(.08f + .22f * s, .06f * (1 - s) + .004f, .08f + .22f * s);
        }
    }
}
