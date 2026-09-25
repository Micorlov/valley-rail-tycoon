using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Cosmetic life at every open roadside service area. Cars leave the highway, pull up at a free pump, fill up while
    /// the driver stands by the pump, and drive back onto the road; a car goes up and down on the garage lift with a
    /// mechanic under it; at the tyre shop a car stands on a jack while a fitter rolls tyres between it and the stacks.
    /// People show only when zoomed in; cars always. Never reads or changes simulation randomness.
    /// </summary>
    public sealed class RoadsideLife : MonoBehaviour
    {
        enum Drive { Arriving, Fuelling, Leaving }
        sealed class Car
        {
            public Transform view, driver;
            public Drive drive;
            public int lane;
            public bool front;
            public readonly List<Vector3> route = new List<Vector3>();
            public int leg;
            public float along, wait;
        }
        sealed class Site
        {
            public int a, b, at, side, kind;
            public RoadsideLayout layout;
            public Transform root, people;
            // Pump slots: [lane * 2 + (front ? 1 : 0)].
            public readonly Car[] slots = new Car[4];
            public readonly List<Car> cars = new List<Car>();
            public float nextArrival, clock;
            public Transform liftCar, liftArms, liftMechanic, tyreCar, tyre, fitter, rotor;
            public float spin;
        }
        const float CarSpeed = .55f, TurnRate = 280f, FigureScale = 1.5f, LiftHeight = .2f, LiftCycle = 12f, TyreCycle = 9f;
        const int Target = 3;
        // Mostly cars at the pumps, with the odd van or pickup; no buses or emergency vehicles.
        static readonly int[] Models = { 0, 1, 2, 3, 0, 1, 2, 5, 4, 1, 2, 0 };
        // Per style (Roadside.Kinds), the vehicles at the front pump of a lane and behind it: lorries fill up at the truck
        // stop's front pumps (a lorry is too long to stand behind another vehicle), only cars charge at the eco station.
        static readonly int[][] FrontModels = { Models, new[] { 0, 1, 2, 3, 5, 1, 2 }, new[] { 6, 7, 6, 4, 5, 7, 1 }, new[] { 0, 1, 2, 3, 0, 2 } };
        static readonly int[][] RearModels = { Models, new[] { 0, 1, 2, 3, 5, 1, 2 }, new[] { 4, 5, 4, 0, 2 }, new[] { 0, 1, 2, 3, 0, 2 } };
        const float RotorSpeed = 150f;
        static readonly Color Overalls = new Color(.16f, .3f, .62f);
        static readonly Color[] Shirts = { new Color(.9f, .3f, .3f), new Color(.95f, .95f, .92f), new Color(.3f, .6f, .9f), new Color(.95f, .75f, .2f), new Color(.35f, .7f, .45f), new Color(.6f, .4f, .75f) };
        readonly List<Site> sites = new List<Site>();
        readonly System.Random random = new System.Random(4242);
        WorldView world;
        CityTraffic traffic;
        GameSession session;
        int cityRevision = -1;
        bool showingPeople = true;

        public int Sites => sites.Count;
        /// <summary>Cars that have pulled in from the highway, and cars that have driven back onto it, since the game loaded.</summary>
        public int Arrivals { get; private set; }
        public int Departures { get; private set; }
        public int Fuelling(int site) => sites[site].cars.FindAll(c => c.drive == Drive.Fuelling).Count;
        public int Cars(int site) => sites[site].cars.Count;
        public float LiftRaise(int site) => sites[site].liftArms.localPosition.y - sites[site].layout.At(0, 0).y;
        public int Kind(int site) => sites[site].kind;
        /// <summary>The eco station's turbine rotor, or null at other styles.</summary>
        public Transform Rotor(int site) => sites[site].rotor;

        /// <summary>Opens life at service areas that have just opened; ones that stay open keep their cars.</summary>
        public void Refresh(WorldView view, GameSession game, CityTraffic vehicles)
        {
            world = view;
            traffic = vehicles;
            if (session == game && cityRevision == game.World.cityRevision)
                return;
            if (session != game)
                for (int i = sites.Count - 1; i >= 0; i--)
                    Close(i);
            session = game;
            cityRevision = game.World.cityRevision;
            for (int i = sites.Count - 1; i >= 0; i--)
                if (Road(game, sites[i]) == null)
                    Close(i);
            foreach (var road in game.World.intercityRoads)
                if (Roadside.Open(road) && Roadside.Shaped(road) && sites.Find(s => Same(s, road)) == null)
                    sites.Add(Open(road));
        }
        static bool Same(Site site, IntercityRoadState road) =>
            site.a == road.a && site.b == road.b && site.at == road.serviceAt && site.side == road.serviceSide && site.kind == road.serviceKind;
        static IntercityRoadState Road(GameSession game, Site site)
        {
            foreach (var road in game.World.intercityRoads)
                if (Roadside.Open(road) && Roadside.Shaped(road) && Same(site, road))
                    return road;
            return null;
        }
        void Close(int index)
        {
            sites[index].root.gameObject.SetActive(false);
            Destroy(sites[index].root.gameObject);
            sites.RemoveAt(index);
        }
        Site Open(IntercityRoadState road)
        {
            var f = new RoadsideLayout(road);
            var site = new Site { a = road.a, b = road.b, at = road.serviceAt, side = road.serviceSide, kind = road.serviceKind, layout = f };
            site.root = new GameObject("Service area " + road.path[road.serviceAt]).transform;
            site.root.SetParent(transform, false);
            site.people = new GameObject("Service area people").transform;
            site.people.SetParent(site.root, false);
            site.people.gameObject.SetActive(showingPeople);
            // The garage: arms on the lift and a car on them, nose into the open bay, and a mechanic under it.
            site.liftArms = new GameObject("Lift arms").transform;
            site.liftArms.SetParent(site.root, false);
            world.Box("Lift arm", Vector3.zero, f.Size(.34f, .02f, .05f), WorldView.Gold, site.liftArms);
            world.Box("Lift arm", f.Direction(0, 1) * .12f, f.Size(.34f, .02f, .05f), WorldView.Gold, site.liftArms);
            world.Box("Lift arm", -f.Direction(0, 1) * .12f, f.Size(.34f, .02f, .05f), WorldView.Gold, site.liftArms);
            site.liftCar = traffic.Vehicle(Models[random.Next(Models.Length)], random.Next(8), site.root);
            site.liftCar.localRotation = f.Facing(0, 1);
            site.liftMechanic = CityLife.Figure(world, site.people, "Mechanic", Overalls, FigureScale);
            // The tyre shop: a car on a jack beside the stacks, one tyre off, and a fitter with a tyre.
            site.tyreCar = traffic.Vehicle(Models[random.Next(Models.Length)], random.Next(8), site.root);
            site.tyreCar.localPosition = f.At(RoadsideLayout.TyreCarU, RoadsideLayout.TyreCarV, RoadsideLayout.Deck + .025f);
            // Nose towards the shop, lifted on the stack side where the wheel is off.
            site.tyreCar.localRotation = Quaternion.AngleAxis(-4, f.Direction(0, 1)) * f.Facing(0, 1);
            site.tyre = new GameObject("Loose tyre").transform;
            site.tyre.SetParent(site.root, false);
            var shape = world.Box("Tyre tread", Vector3.zero, Vector3.one, new Color(.09f, .09f, .1f), site.tyre);
            if (shape)
            {
                shape.GetComponent<MeshFilter>().sharedMesh = world.Drum();
                shape.transform.localScale = new Vector3(.15f, .15f, .045f);
            }
            var hub = world.Box("Tyre hub", Vector3.zero, Vector3.one, new Color(.72f, .74f, .77f), site.tyre);
            if (hub)
            {
                hub.GetComponent<MeshFilter>().sharedMesh = world.Drum();
                hub.transform.localScale = new Vector3(.075f, .075f, .05f);
            }
            site.fitter = CityLife.Figure(world, site.people, "Tyre fitter", Overalls, FigureScale);
            if (f.kind == 3)
                site.rotor = Rotor(f, site.root);
            site.clock = 20 * Next();
            // A station that opens (or a game that loads) already has a car or two at the pumps.
            for (int i = 0; i < 2; i++)
                FuelNow(site);
            site.nextArrival = 1 + 2 * Next();
            Pose(site);
            return site;
        }
        /// <summary>Three white blades on a hub facing the road, at the top of the eco station's mast.</summary>
        Transform Rotor(in RoadsideLayout f, Transform parent)
        {
            var rotor = new GameObject("Wind rotor").transform;
            rotor.SetParent(parent, false);
            rotor.localPosition = f.At(RoadsideLayout.TurbineU, RoadsideLayout.TurbineV - .08f, RoadsideLayout.HubHeight);
            rotor.localRotation = Quaternion.LookRotation(-f.away);
            world.Box("Rotor hub", Vector3.zero, new Vector3(.05f, .05f, .05f), Color.white, rotor);
            for (int k = 0; k < 3; k++)
            {
                var blade = new GameObject("Blade").transform;
                blade.SetParent(rotor, false);
                blade.localRotation = Quaternion.Euler(0, 0, k * 120);
                world.Box("Blade", new Vector3(0, .19f, 0), new Vector3(.035f, .34f, .012f), Color.white, blade);
            }
            return rotor;
        }
        float Next() => (float)random.NextDouble();
        Transform NewCar(Site site, Car car)
        {
            var models = car.front ? FrontModels[site.layout.kind] : RearModels[site.layout.kind];
            car.view = traffic.Vehicle(models[random.Next(models.Length)], random.Next(8), site.root);
            site.cars.Add(car);
            return car.view;
        }
        /// <summary>
        /// A free slot: the front one of an empty lane first, else the rear one behind a car at the front. A front pump left
        /// free while a car still fills up behind it waits for that car: reaching it would mean driving through it.
        /// </summary>
        static int FreeSlot(Site site, int pick)
        {
            for (int k = 0; k < 2; k++)
            {
                int lane = (pick + k) % 2;
                if (site.slots[lane * 2] == null && site.slots[lane * 2 + 1] == null)
                    return lane * 2 + 1;
            }
            for (int k = 0; k < 2; k++)
            {
                int lane = (pick + k) % 2;
                if (site.slots[lane * 2] == null && site.slots[lane * 2 + 1] != null && site.slots[lane * 2 + 1].drive != Drive.Leaving)
                    return lane * 2;
            }
            return -1;
        }
        Car Take(Site site, int slot)
        {
            var car = new Car { lane = slot / 2, front = slot % 2 == 1 };
            site.slots[slot] = car;
            NewCar(site, car);
            return car;
        }
        /// <summary>A car already standing at a pump.</summary>
        void FuelNow(Site site)
        {
            int slot = FreeSlot(site, random.Next(2));
            if (slot < 0)
                return;
            var car = Take(site, slot);
            car.view.localPosition = site.layout.Slot(car.lane, car.front);
            car.view.localRotation = site.layout.Facing(1, 0);
            StartFuelling(site, car);
        }
        /// <summary>A car coming up the road and turning in to a free pump.</summary>
        void Arrive(Site site, IntercityRoadState road)
        {
            int slot = FreeSlot(site, random.Next(2));
            if (slot < 0)
                return;
            var car = Take(site, slot);
            Arrivals++;
            car.drive = Drive.Arriving;
            car.route.AddRange(site.layout.WayIn(road, car.lane));
            car.route.Add(site.layout.Slot(car.lane, car.front));
            car.view.localPosition = car.route[0];
            var ahead = Flat(car.route[1] - car.route[0]);
            car.view.localRotation = ahead.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(ahead) : site.layout.Facing(1, 0);
        }
        void StartFuelling(Site site, Car car)
        {
            car.drive = Drive.Fuelling;
            car.wait = 3 + 4 * Next();
            // The driver stands between the car and the pump island, by the filler.
            var f = site.layout;
            float v = RoadsideLayout.PumpLanes[car.lane];
            car.driver = CityLife.Figure(world, site.people, "Driver", Shirts[random.Next(Shirts.Length)], FigureScale);
            car.driver.localPosition = f.At((car.front ? RoadsideLayout.PumpU : -RoadsideLayout.PumpU) - .07f, v + (RoadsideLayout.IslandV - v) * .6f, RoadsideLayout.Deck);
            car.driver.localRotation = f.Facing(0, RoadsideLayout.IslandV - v);
        }
        void Leave(Site site, Car car, IntercityRoadState road)
        {
            // The pump is free as soon as the car pulls away; a car behind follows it out at the same speed.
            int slot = car.lane * 2 + (car.front ? 1 : 0);
            if (site.slots[slot] == car)
                site.slots[slot] = null;
            car.drive = Drive.Leaving;
            if (car.driver)
                Destroy(car.driver.gameObject);
            car.driver = null;
            car.route.Clear();
            car.route.Add(site.layout.Slot(car.lane, car.front));
            car.route.AddRange(site.layout.WayOut(road, car.lane));
            car.leg = 0;
            car.along = 0;
        }

        /// <summary>Moves everything; people show only when zoomed in. Nothing moves while the game is paused.</summary>
        public void Animate(float dt, float speed, bool near)
        {
            if (showingPeople != near)
            {
                showingPeople = near;
                foreach (var site in sites)
                    site.people.gameObject.SetActive(near);
            }
            if (speed <= 0 || session == null)
                return;
            float step = dt * Mathf.Min(speed, 2);
            foreach (var site in sites)
            {
                var road = Road(session, site);
                if (road == null)
                    continue;
                site.clock += step;
                site.nextArrival -= step;
                if (site.nextArrival <= 0)
                {
                    site.nextArrival = 2 + 4 * Next();
                    int coming = 0;
                    foreach (var car in site.cars)
                        if (car.drive != Drive.Leaving)
                            coming++;
                    if (coming < Target)
                        Arrive(site, road);
                }
                for (int i = site.cars.Count - 1; i >= 0; i--)
                    DriveCar(site, site.cars[i], step, road);
                if (site.rotor)
                {
                    site.spin = (site.spin + RotorSpeed * step) % 360;
                    site.rotor.localRotation = Quaternion.LookRotation(-site.layout.away) * Quaternion.Euler(0, 0, site.spin);
                }
                Pose(site);
            }
        }
        void DriveCar(Site site, Car car, float step, IntercityRoadState road)
        {
            if (car.drive == Drive.Fuelling)
            {
                car.wait -= step;
                // A car at a rear pump waits for the one in front to pull away before it can drive on.
                bool blocked = !car.front && site.slots[car.lane * 2 + 1] != null;
                if (car.wait <= 0 && !blocked)
                    Leave(site, car, road);
                return;
            }
            float distance = CarSpeed * step;
            while (distance > 0 && car.leg + 1 < car.route.Count)
            {
                Vector3 a = car.route[car.leg], b = car.route[car.leg + 1];
                float left = Vector3.Distance(a, b) - car.along;
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
                }
            }
            if (car.leg + 1 >= car.route.Count)
            {
                Finish(site, car);
                return;
            }
            Vector3 from = car.route[car.leg], to = car.route[car.leg + 1];
            float length = Vector3.Distance(from, to);
            car.view.localPosition = Vector3.Lerp(from, to, length > 1e-4f ? car.along / length : 1);
            var ahead = Flat(to - from);
            if (ahead.sqrMagnitude > 1e-6f)
                car.view.localRotation = Quaternion.RotateTowards(car.view.localRotation, Quaternion.LookRotation(ahead), TurnRate * step);
        }
        void Finish(Site site, Car car)
        {
            if (car.drive == Drive.Arriving)
            {
                car.view.localPosition = site.layout.Slot(car.lane, car.front);
                car.view.localRotation = site.layout.Facing(1, 0);
                StartFuelling(site, car);
                return;
            }
            Departures++;
            site.cars.Remove(car);
            car.view.gameObject.SetActive(false);
            Destroy(car.view.gameObject);
        }
        /// <summary>
        /// The garage lift rises, holds while the mechanic works under the car, and comes down again; the fitter rolls the
        /// old tyre from the car to the stacks and a new one back.
        /// </summary>
        void Pose(Site site)
        {
            var f = site.layout;
            float t = site.clock % LiftCycle, raise = t < 2 ? t / 2 : t < 8 ? 1 : t < 10 ? 1 - (t - 8) / 2 : 0;
            raise = Mathf.SmoothStep(0, 1, raise) * LiftHeight;
            site.liftArms.localPosition = f.At(RoadsideLayout.LiftU, RoadsideLayout.LiftV, RoadsideLayout.Deck + .03f + raise);
            site.liftCar.localPosition = f.At(RoadsideLayout.LiftU, RoadsideLayout.LiftV, RoadsideLayout.Deck + .04f + raise);
            // The mechanic steps under the raised car and back out when it comes down.
            float under = raise / LiftHeight;
            site.liftMechanic.localPosition = f.At(RoadsideLayout.LiftU + .2f - .2f * under, RoadsideLayout.LiftV - .2f + .2f * under + .03f * Mathf.Sin(site.clock * 2), RoadsideLayout.Deck);
            site.liftMechanic.localRotation = f.Facing(-under, 1 - under + .01f);
            // Tyre: at the car (0-2 s), rolled to the stacks (2-4), set down (4-5), a new one rolled back (5-7), fitted (7-9).
            float s = site.clock % TyreCycle, along = s < 2 ? 0 : s < 4 ? (s - 2) / 2 : s < 5 ? 1 : s < 7 ? 1 - (s - 5) / 2 : 0;
            var wheel = f.At(RoadsideLayout.TyreCarU + .16f, RoadsideLayout.TyreCarV - .1f, RoadsideLayout.Deck + .075f);
            var stack = f.At(RoadsideLayout.StackU, RoadsideLayout.StackV[0] - .22f, RoadsideLayout.Deck + .075f);
            var roll = Vector3.Lerp(wheel, stack, Mathf.SmoothStep(0, 1, along));
            site.tyre.localPosition = roll;
            // Rolling: the axle stays across the way it goes and the tyre turns with the distance covered.
            var way = Flat(stack - wheel);
            float turned = along * way.magnitude / (Mathf.PI * .15f) * 360;
            site.tyre.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, way.normalized)) * Quaternion.Euler(0, 0, turned);
            bool moving = (s >= 2 && s < 4) || (s >= 5 && s < 7);
            site.fitter.localPosition = roll + Vector3.Cross(way.normalized, Vector3.up) * .09f + Vector3.down * (RoadsideLayout.Deck + .075f - RoadsideLayout.Deck);
            site.fitter.localRotation = Quaternion.LookRotation(moving ? (s < 4 ? way : -way) : f.Direction(-1, 0));
        }
        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v;
        }
    }
}
