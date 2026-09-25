using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Cosmetic life on the east coast. Waves roll onto the beach all along it. At every open beach car park, cars come
    /// down the beach road, park in the bays and later leave; their passengers walk over the boardwalk onto the sand,
    /// stroll, paddle, lie on a sun lounger for a while and walk back. Strollers, sunbathers on loungers and towels,
    /// swimmers and the ice-cream seller stay all day. People show only when zoomed in; cars and waves always. Never
    /// reads or changes simulation randomness.
    /// </summary>
    public sealed class BeachLife : MonoBehaviour
    {
        enum Drive { Arriving, Parked, Leaving }
        enum Walk { ToBeach, Strolling, Back, Pacing, Swimming, Lying, Selling }
        sealed class Car
        {
            public Transform view;
            public Drive drive;
            public int bay, aboard;
            // Waypoints and progress on the current leg; the first leg out of a bay is driven in reverse.
            public readonly List<Vector3> route = new List<Vector3>();
            public int leg;
            public float along, wait;
            public bool reversing;
        }
        sealed class Person
        {
            public Transform view;
            public Walk walk;
            public Car car;
            public readonly List<Vector3> route = new List<Vector3>();
            public int leg, legsLeft;
            public float along, rest, pace, phase;
            // The sun lounger this visitor is heading for or lying on (-1: none).
            public int lounger = -1;
            public bool lying;
        }
        sealed class Resort
        {
            public int producerId;
            public Cell entrance;
            public Transform root, people;
            public List<BeachLayout.Bay> bays;
            public Car[] inBay;
            public List<Vector3> loungers;
            public bool[] taken;
            public readonly List<Car> cars = new List<Car>();
            public readonly List<Person> crowd = new List<Person>();
            // The beach road's lane in (from a few cells up the road to the gate) and out (gate back up the road).
            public List<Vector3> lanesIn, lanesOut;
            public int target;
            public float nextArrival;
        }
        sealed class Wave
        {
            public Transform view;
            public float z, phase;
            public bool crest;
        }
        // People are a little larger than pavement walkers, like station crowds, so they read on the sand.
        const float FigureScale = 1.8f, Lane = .2f, RoadDeck = .028f, CarSpeed = .55f, TurnRate = 280f, WalkBob = .012f;
        const int Strollers = 10, Swimmers = 5, FoamSegments = 72, CrestRows = 3;
        // Crests roll in across the strip of shallows from its outer edge (CrestStart past the waterline) to the foam.
        const float FoamPeriod = 4.2f, CrestPeriod = 5f, CrestStart = BeachLayout.WaterWidth - .45f, CrestEnd = .3f;
        // Mostly cars at the seaside, with the odd pickup or van.
        static readonly int[] Models = { 0, 1, 2, 3, 0, 1, 2, 5, 0, 4, 1, 2 };
        static readonly Color Foam = new Color(.96f, .98f, .98f), Crest = new Color(.6f, .83f, .92f);
        static readonly Color[] Swimwear = { new Color(.9f, .2f, .3f), new Color(.15f, .5f, .85f), new Color(.98f, .7f, .15f), new Color(.2f, .7f, .55f), new Color(.95f, .45f, .7f), new Color(.95f, .95f, .92f) };
        readonly List<Resort> resorts = new List<Resort>();
        readonly List<Wave> waves = new List<Wave>();
        readonly System.Random random = new System.Random(2718);
        WorldView world;
        CityTraffic traffic;
        GameSession session;
        int cityRevision = -1;
        float time;
        bool showingPeople = true;

        public int Beaches => resorts.Count;
        /// <summary>Cars that have driven in from the beach road, and cars that have driven away again, since the game loaded.</summary>
        public int Arrivals { get; private set; }
        public int Departures { get; private set; }
        /// <summary>Times a visitor has lain down on a free sun lounger.</summary>
        public int LoungerVisits { get; private set; }
        public int Loungers(int beach) => resorts[beach].loungers.Count;
        public int LoungersTaken(int beach) => System.Array.FindAll(resorts[beach].taken, t => t).Length;
        public int ParkedCars(int beach) => resorts[beach].cars.FindAll(c => c.drive == Drive.Parked).Count;
        public int Cars(int beach) => resorts[beach].cars.Count;
        public int People(int beach) => resorts[beach].crowd.Count;
        public int Bays(int beach) => resorts[beach].bays.Count;
        public int OnSand(int beach) => resorts[beach].crowd.FindAll(p => p.view.localPosition.x > BeachLayout.East).Count;

        /// <summary>Opens a beach's life when its car park is finished; beaches that stay open keep their cars and people.</summary>
        public void Refresh(WorldView view, GameSession game, CityTraffic vehicles)
        {
            world = view;
            traffic = vehicles;
            if (session == game && cityRevision == game.World.cityRevision)
                return;
            if (session != game)
                for (int i = resorts.Count - 1; i >= 0; i--)
                    Close(i);
            session = game;
            cityRevision = game.World.cityRevision;
            if (waves.Count == 0)
                BuildWaves();
            for (int i = resorts.Count - 1; i >= 0; i--)
                if (Road(game, resorts[i]) == null)
                    Close(i);
            foreach (var road in game.World.intercityRoads)
            {
                if (!Coast.Open(road))
                    continue;
                var entrance = Coast.Entrance(road);
                var resort = resorts.Find(r => r.producerId == road.a && r.entrance.Equals(entrance));
                if (resort == null)
                    resorts.Add(resort = Open(road));
                resort.lanesIn = LaneIn(road);
                resort.lanesOut = LaneOut(road);
                resort.target = Target(game, road.a, resort.bays.Count);
            }
        }
        static IntercityRoadState Road(GameSession game, Resort resort)
        {
            foreach (var road in game.World.intercityRoads)
                if (Coast.Open(road) && road.a == resort.producerId && Coast.Entrance(road).Equals(resort.entrance))
                    return road;
            return null;
        }
        void Close(int index)
        {
            resorts[index].root.gameObject.SetActive(false);
            Destroy(resorts[index].root.gameObject);
            resorts.RemoveAt(index);
        }
        /// <summary>Bigger towns fill more of the car park, always leaving a couple of bays free for the next arrivals.</summary>
        static int Target(GameSession game, int producerId, int bays)
        {
            var town = game.Cities.CityFor(producerId);
            int population = town != null ? town.population : 0;
            return Mathf.Clamp(3 + population / 900, 3, bays - 2);
        }

        Resort Open(IntercityRoadState road)
        {
            var entrance = Coast.Entrance(road);
            var resort = new Resort { producerId = road.a, entrance = entrance, bays = BeachLayout.Bays(entrance), loungers = BeachLayout.Loungers(entrance) };
            resort.inBay = new Car[resort.bays.Count];
            resort.taken = new bool[resort.loungers.Count];
            resort.root = new GameObject("Beach " + entrance).transform;
            resort.root.SetParent(transform, false);
            resort.people = new GameObject("Beach people").transform;
            resort.people.SetParent(resort.root, false);
            resort.people.gameObject.SetActive(showingPeople);
            resort.lanesIn = LaneIn(road);
            resort.lanesOut = LaneOut(road);
            resort.target = Target(session, road.a, resort.bays.Count);
            float from = BeachLayout.StrollFrom(entrance), to = BeachLayout.StrollTo(entrance);
            // People who stay all day: strollers pacing the shore, sunbathers on loungers and towels, swimmers and the ice-cream seller.
            for (int i = 0; i < Strollers; i++)
            {
                float x = Mathf.Lerp(BeachLayout.ShoreWest + .15f, BeachLayout.SandEast, (i + .5f) / Strollers);
                var person = Add(resort, Walk.Pacing, new Vector3(x, 0, Mathf.Lerp(from, to, Next())));
                person.route.Add(new Vector3(x, 0, i % 2 == 0 ? to : from));
                person.pace = .22f + .1f * Next();
            }
            for (int i = 0; i < resort.loungers.Count; i++)
            {
                if (Next() < .5f)
                    continue;
                resort.taken[i] = true;
                Add(resort, Walk.Lying, BeachLayout.OnLounger(resort.loungers[i])).view.localRotation = BeachLayout.LyingOnLounger;
            }
            foreach (var spot in BeachLayout.UmbrellaSpots(entrance))
            {
                var person = Add(resort, Walk.Lying, BeachLayout.Towel(spot) + new Vector3(0, .035f, -.13f));
                person.view.localRotation = Quaternion.Euler(90, 0, 0);
            }
            for (int i = 0; i < Swimmers; i++)
            {
                var person = Add(resort, Walk.Swimming, Swim(entrance));
                person.route.Add(Swim(entrance));
                person.pace = .08f + .05f * Next();
            }
            var kiosk = BeachLayout.Kiosk(entrance);
            Add(resort, Walk.Selling, kiosk + new Vector3(.05f, 0, 0)).view.localRotation = Quaternion.LookRotation(Vector3.left);
            // A car park that opens (or a game that loads) already has some cars, their passengers out on the sand.
            for (int i = 0; i < resort.target / 2 + 1; i++)
                ParkNow(resort);
            resort.nextArrival = 1 + 3 * Next();
            return resort;
        }
        Person Add(Resort resort, Walk walk, Vector3 at)
        {
            var person = new Person
            {
                walk = walk,
                pace = .3f + .15f * Next(),
                phase = Next() * 10,
                view = CityLife.Figure(world, resort.people, "Beach " + walk, Swimwear[random.Next(Swimwear.Length)], FigureScale),
            };
            person.view.localPosition = Grounded(resort, person, at);
            person.route.Add(at);
            resort.crowd.Add(person);
            return person;
        }
        /// <summary>A spot in the shallows for a swimmer, off the stretch of beach in use.</summary>
        Vector3 Swim(Cell entrance) => new Vector3(BeachLayout.Waterline + .35f + (BeachLayout.WaterWidth - .6f) * Next(), 0,
            Mathf.Lerp(BeachLayout.StrollFrom(entrance) + 1, BeachLayout.StrollTo(entrance) - 1, Next()));
        /// <summary>
        /// Where a visitor goes next: often a free sun lounger (reserved for them), otherwise along the shore or paddling
        /// at the water's edge. The rows of loungers stay clear of people just walking.
        /// </summary>
        Vector3 NextSpot(Resort resort, Person person)
        {
            float roll = Next();
            if (roll < .35f)
            {
                int free = 0, chosen = -1;
                for (int i = 0; i < resort.taken.Length; i++)
                    if (!resort.taken[i] && random.Next(++free) == 0)
                        chosen = i;
                if (chosen >= 0)
                {
                    resort.taken[chosen] = true;
                    person.lounger = chosen;
                    return BeachLayout.BesideLounger(resort.loungers[chosen]);
                }
            }
            float x = roll < .6f ? BeachLayout.Waterline + .1f + .25f * Next() : Mathf.Lerp(BeachLayout.ShoreWest, BeachLayout.SandEast, Next());
            return new Vector3(x, 0, Mathf.Lerp(BeachLayout.StrollFrom(resort.entrance), BeachLayout.StrollTo(resort.entrance), Next()));
        }
        void LieDown(Resort resort, Person person)
        {
            person.lying = true;
            person.view.localPosition = BeachLayout.OnLounger(resort.loungers[person.lounger]);
            person.view.localRotation = BeachLayout.LyingOnLounger;
            person.rest = 8 + 10 * Next();
            LoungerVisits++;
        }
        void GetUp(Resort resort, Person person, Vector3 beside)
        {
            resort.taken[person.lounger] = false;
            person.lounger = -1;
            person.lying = false;
            person.view.localPosition = Grounded(resort, person, beside);
            person.view.localRotation = Quaternion.LookRotation(Vector3.right);
        }
        float Next() => (float)random.NextDouble();

        /// <summary>The beach road's inbound lane over its last few cells, ending at the gate, on the right-hand side.</summary>
        static List<Vector3> LaneIn(IntercityRoadState road)
        {
            var lane = new List<Vector3>();
            int n = road.path.Count;
            for (int i = Mathf.Max(0, n - 4); i < n; i++)
            {
                var cell = road.path[i];
                var ahead = i + 1 < n ? Step(cell, road.path[i + 1]) : Vector3.right;
                lane.Add(new Vector3(cell.x, RoadDeck, cell.z) + Right(ahead) * Lane);
            }
            lane.Add(new Vector3(BeachLayout.West + .15f, BeachLayout.Deck, road.path[n - 1].z - Lane));
            return lane;
        }
        /// <summary>The outbound lane: from the gate back up the beach road.</summary>
        static List<Vector3> LaneOut(IntercityRoadState road)
        {
            int n = road.path.Count;
            var lane = new List<Vector3> { new Vector3(BeachLayout.West + .15f, BeachLayout.Deck, road.path[n - 1].z + Lane) };
            for (int i = n - 1; i >= Mathf.Max(0, n - 4); i--)
            {
                var cell = road.path[i];
                var ahead = i > 0 ? Step(cell, road.path[i - 1]) : Vector3.left;
                lane.Add(new Vector3(cell.x, RoadDeck, cell.z) + Right(ahead) * Lane);
            }
            return lane;
        }
        static Vector3 Step(Cell from, Cell to)
        {
            var d = new Vector3(to.x - from.x, 0, to.z - from.z);
            return d.sqrMagnitude > 0 ? d.normalized : Vector3.right;
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
            int model = Models[random.Next(Models.Length)];
            var car = new Car { bay = bay, view = traffic.Vehicle(model, random.Next(8), resort.root) };
            resort.inBay[bay] = car;
            resort.cars.Add(car);
            return car;
        }
        /// <summary>A car already standing in a free bay, its passengers somewhere on the beach.</summary>
        void ParkNow(Resort resort)
        {
            int bay = FreeBay(resort);
            if (bay < 0)
                return;
            var car = NewCar(resort, bay);
            var spot = resort.bays[bay];
            car.view.localPosition = spot.at;
            car.view.localRotation = Quaternion.LookRotation(spot.facing);
            Unload(resort, car, true);
        }
        /// <summary>A car coming down the beach road to a free bay.</summary>
        void Arrive(Resort resort)
        {
            int bay = FreeBay(resort);
            if (bay < 0)
                return;
            var car = NewCar(resort, bay);
            var spot = resort.bays[bay];
            Arrivals++;
            car.drive = Drive.Arriving;
            car.route.AddRange(resort.lanesIn);
            car.route.Add(new Vector3(BeachLayout.Aisle, BeachLayout.Deck, resort.entrance.z - Lane * .5f));
            car.route.Add(new Vector3(BeachLayout.Aisle, BeachLayout.Deck, spot.at.z));
            car.route.Add(spot.at);
            car.view.localPosition = car.route[0];
            car.view.localRotation = Quaternion.LookRotation(car.route[1] - car.route[0]);
        }
        void Leave(Resort resort, Car car)
        {
            var spot = resort.bays[car.bay];
            car.drive = Drive.Leaving;
            car.route.Clear();
            car.route.Add(spot.at);
            car.route.Add(new Vector3(BeachLayout.Aisle, BeachLayout.Deck, spot.at.z));
            car.route.Add(new Vector3(BeachLayout.Aisle, BeachLayout.Deck, resort.entrance.z + Lane * .5f));
            car.route.AddRange(resort.lanesOut);
            car.leg = 0;
            car.along = 0;
            car.reversing = true;
        }
        /// <summary>One to three people get out beside the car and head for the sand (or are already there).</summary>
        void Unload(Resort resort, Car car, bool alreadyOnSand)
        {
            car.drive = Drive.Parked;
            car.aboard = 1 + random.Next(3);
            var door = Door(resort, car);
            for (int i = 0; i < car.aboard; i++)
            {
                var person = Add(resort, alreadyOnSand ? Walk.Strolling : Walk.ToBeach, door + new Vector3(0, 0, .09f * i));
                person.car = car;
                if (alreadyOnSand)
                {
                    var spot = NextSpot(resort, person);
                    person.route[0] = spot;
                    person.view.localPosition = Grounded(resort, person, spot);
                }
                person.legsLeft = 1 + random.Next(4);
                if (alreadyOnSand)
                    continue;
                float row = BeachLayout.WalkRow(resort.entrance);
                person.route.Add(new Vector3(BeachLayout.Aisle, 0, door.z + .09f * i));
                person.route.Add(new Vector3(BeachLayout.Aisle, 0, row));
                person.route.Add(new Vector3(BeachLayout.East - .02f, 0, row));
                person.route.Add(new Vector3(BeachLayout.WalkwayEnd, 0, row + (Next() - .5f) * .2f));
                // Stagger a family so they walk in a line rather than on top of each other.
                person.rest = .5f * i;
            }
        }
        /// <summary>Where passengers get out: beside the car, on the aisle side of its bay.</summary>
        static Vector3 Door(Resort resort, Car car)
        {
            var spot = resort.bays[car.bay];
            return new Vector3(spot.at.x - spot.facing.x * .3f, 0, spot.at.z + .15f);
        }

        /// <summary>Moves everything; people show only when zoomed in. Waves keep rolling while paused.</summary>
        public void Animate(float dt, float speed, bool near)
        {
            if (showingPeople != near)
            {
                showingPeople = near;
                foreach (var resort in resorts)
                    resort.people.gameObject.SetActive(near);
            }
            AnimateWaves(dt);
            if (speed <= 0)
                return;
            float step = dt * Mathf.Min(speed, 2);
            time += step;
            foreach (var resort in resorts)
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
                for (int i = resort.cars.Count - 1; i >= 0; i--)
                    DriveCar(resort, resort.cars[i], step);
                for (int i = resort.crowd.Count - 1; i >= 0; i--)
                    Move(resort, resort.crowd[i], step);
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
                Vector3 a = car.route[car.leg], b = car.route[car.leg + 1];
                float length = Vector3.Distance(a, b), left = length - car.along;
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
            float t = Vector3.Distance(from, to) > 1e-4f ? car.along / Vector3.Distance(from, to) : 1;
            car.view.localPosition = Vector3.Lerp(from, to, t);
            var ahead = to - from;
            ahead.y = 0;
            if (ahead.sqrMagnitude > 1e-6f)
            {
                var facing = Quaternion.LookRotation(car.reversing ? -ahead : ahead);
                car.view.localRotation = Quaternion.RotateTowards(car.view.localRotation, facing, TurnRate * step);
            }
        }
        void Finish(Resort resort, Car car)
        {
            if (car.drive == Drive.Arriving)
            {
                var spot = resort.bays[car.bay];
                car.view.localPosition = spot.at;
                car.view.localRotation = Quaternion.LookRotation(spot.facing);
                Unload(resort, car, false);
                return;
            }
            Departures++;
            resort.inBay[car.bay] = null;
            resort.cars.Remove(car);
            car.view.gameObject.SetActive(false);
            Destroy(car.view.gameObject);
        }
        void Move(Resort resort, Person person, float step)
        {
            var view = person.view;
            switch (person.walk)
            {
                case Walk.Lying:
                case Walk.Selling:
                    return;
            }
            if (person.rest > 0)
            {
                person.rest -= step;
                return;
            }
            if (person.leg + 1 >= person.route.Count)
            {
                Arrived(resort, person);
                return;
            }
            Vector3 a = person.route[person.leg], b = person.route[person.leg + 1];
            float length = Vector3.Distance(a, b);
            person.along += person.pace * step;
            if (person.along >= length)
            {
                person.leg++;
                person.along = 0;
                view.localPosition = Grounded(resort, person, b);
                return;
            }
            var at = Vector3.Lerp(a, b, length > 1e-4f ? person.along / length : 1);
            view.localPosition = Grounded(resort, person, at) + Vector3.up * Mathf.Abs(Mathf.Sin(person.phase + time * 11)) * (person.walk == Walk.Swimming ? 0 : WalkBob);
            var ahead = b - a;
            ahead.y = 0;
            if (ahead.sqrMagnitude > 1e-6f)
                view.localRotation = Quaternion.RotateTowards(view.localRotation, Quaternion.LookRotation(ahead), 400 * step);
        }
        /// <summary>The height a person stands at: the car park deck, the boardwalk, the sand, ankle-deep at the water's edge, or swimming.</summary>
        static Vector3 Grounded(Resort resort, Person person, Vector3 at)
        {
            if (person.walk == Walk.Lying)
                return at;
            if (person.walk == Walk.Swimming)
                at.y = -.22f + .012f * Mathf.Sin(person.phase + at.z * 3);
            else if (person.walk == Walk.Selling)
                at.y = .03f;
            else if (at.x < BeachLayout.East)
                at.y = BeachLayout.Deck;
            else if (at.x < BeachLayout.WalkwayEnd && Mathf.Abs(at.z - BeachLayout.WalkRow(resort.entrance)) < .17f)
                at.y = .034f;
            else if (at.x > BeachLayout.Waterline)
                at.y = -.045f;
            else
                at.y = 0;
            return at;
        }
        /// <summary>A person reached the end of their route: what they do next.</summary>
        void Arrived(Resort resort, Person person)
        {
            var here = person.route[person.route.Count - 1];
            person.route.Clear();
            person.route.Add(here);
            person.leg = 0;
            person.along = 0;
            switch (person.walk)
            {
                case Walk.Pacing:
                    // Back along the beach, after a look at the sea now and then.
                    float from = BeachLayout.StrollFrom(resort.entrance), to = BeachLayout.StrollTo(resort.entrance);
                    person.route.Add(new Vector3(here.x, 0, here.z > (from + to) / 2 ? from : to));
                    if (Next() < .4f)
                        Pause(person, 2 + 3 * Next());
                    break;
                case Walk.Swimming:
                    person.route.Add(Swim(resort.entrance));
                    break;
                case Walk.ToBeach:
                    person.walk = Walk.Strolling;
                    Head(resort, person, here, -1);
                    break;
                case Walk.Strolling:
                    if (person.lounger >= 0 && !person.lying)
                    {
                        LieDown(resort, person);
                        break;
                    }
                    int left = -1;
                    if (person.lying)
                    {
                        left = person.lounger;
                        GetUp(resort, person, here);
                    }
                    if (person.legsLeft-- > 0)
                    {
                        Pause(person, 1.5f + 4 * Next());
                        Head(resort, person, here, left);
                        break;
                    }
                    // Time to go home: along the shore to the boardwalk, then back over it to the car.
                    person.walk = Walk.Back;
                    float row = BeachLayout.WalkRow(resort.entrance);
                    var door = Door(resort, person.car);
                    ToShore(resort, person, here, left);
                    person.route.Add(new Vector3(Shore, 0, row));
                    person.route.Add(new Vector3(BeachLayout.WalkwayEnd, 0, row));
                    person.route.Add(new Vector3(BeachLayout.East - .02f, 0, row));
                    person.route.Add(new Vector3(BeachLayout.Aisle, 0, row));
                    person.route.Add(new Vector3(BeachLayout.Aisle, 0, door.z));
                    person.route.Add(door);
                    break;
                case Walk.Back:
                    resort.crowd.Remove(person);
                    person.view.gameObject.SetActive(false);
                    Destroy(person.view.gameObject);
                    if (--person.car.aboard == 0)
                        person.car.wait = .8f + Next();
                    break;
            }
        }
        // Visitors walk along the shore and reach the loungers through the gaps between their rows, never across them.
        const float Shore = BeachLayout.ShoreWest + .05f;
        /// <summary>Sets off for the visitor's next spot (maybe a lounger it reserves), by way of the shore.</summary>
        void Head(Resort resort, Person person, Vector3 here, int leftLounger)
        {
            var target = NextSpot(resort, person);
            ToShore(resort, person, here, leftLounger);
            if (person.lounger >= 0)
                person.route.AddRange(LoungerWay(resort, person.lounger, false));
            person.route.Add(target);
        }
        /// <summary>From the boardwalk or a lounger out to the shore path.</summary>
        void ToShore(Resort resort, Person person, Vector3 here, int leftLounger)
        {
            if (here.x >= BeachLayout.ShoreWest)
                return;
            if (leftLounger >= 0)
                person.route.AddRange(LoungerWay(resort, leftLounger, true));
            else
                person.route.Add(new Vector3(Shore, 0, here.z));
        }
        /// <summary>
        /// The way between the shore path and the spot beside a lounger: straight in for the seaward column, along the gap
        /// between two rows and the aisle between the columns for the inland one. <paramref name="outward"/> reverses it.
        /// </summary>
        List<Vector3> LoungerWay(Resort resort, int index, bool outward)
        {
            var lounger = resort.loungers[index];
            float gap = lounger.z + (index % 2 == 0 ? -1 : 1) * (BeachLayout.ClubPitch / 2 - BeachLayout.LoungerGap);
            var way = lounger.x < BeachLayout.ClubAisle
                ? new List<Vector3> { new Vector3(Shore, 0, gap), new Vector3(BeachLayout.ClubAisle, 0, gap) }
                : new List<Vector3> { new Vector3(Shore, 0, lounger.z) };
            if (outward)
                way.Reverse();
            return way;
        }
        /// <summary>Stands still facing the sea for a while.</summary>
        static void Pause(Person person, float seconds)
        {
            person.rest = seconds;
            person.view.localRotation = Quaternion.LookRotation(Vector3.right);
        }

        void BuildWaves()
        {
            var root = new GameObject("Waves").transform;
            root.SetParent(transform, false);
            var seed = new System.Random(99);
            for (int i = 0; i < FoamSegments; i++)
            {
                float z = (i + .5f) * MapDefinition.Size / FoamSegments - .5f;
                waves.Add(new Wave { view = world.Box("Foam", Vector3.zero, Vector3.one, Foam, root).transform, z = z, phase = (float)seed.NextDouble() });
                for (int r = 0; r < CrestRows; r++)
                    waves.Add(new Wave { view = world.Box("Wave crest", Vector3.zero, Vector3.one, Crest, root).transform, z = z, phase = (r + .3f * (float)seed.NextDouble()) / CrestRows, crest = true });
            }
            AnimateWaves(0);
        }
        /// <summary>Foam washes up the sand and back; crests roll in across the shallows and fade as they reach the foam.</summary>
        void AnimateWaves(float dt)
        {
            waveTime += dt;
            float length = MapDefinition.Size / (float)FoamSegments;
            foreach (var wave in waves)
            {
                if (!wave.crest)
                {
                    float s = .5f + .5f * Mathf.Sin((waveTime / FoamPeriod + wave.phase) * Mathf.PI * 2);
                    float width = .08f + .22f * s;
                    wave.view.localPosition = new Vector3(BeachLayout.Waterline + .08f - .32f * s + width / 2, .008f, wave.z);
                    wave.view.localScale = new Vector3(width, .012f, length * (.55f + .3f * wave.phase));
                    continue;
                }
                float life = Mathf.Repeat(waveTime / CrestPeriod + wave.phase, 1f);
                float swell = Mathf.Sin(life * Mathf.PI);
                wave.view.localPosition = new Vector3(BeachLayout.Waterline + Mathf.Lerp(CrestStart, CrestEnd, life), -.01f, wave.z + (wave.phase - .5f) * .6f);
                wave.view.localScale = new Vector3(.06f + .1f * swell, .01f, length * (.6f + 1.4f * swell));
            }
        }
        float waveTime;
    }
}
