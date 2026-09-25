using System.Collections.Generic;
using UnityEngine;
namespace ValleyRail
{
    /// <summary>
    /// The cars and people of one drive-in as plain numbers: who is parked in which bay, who is driving in or out along
    /// which path, and who is walking to the snack bar. After the last film about half the audience drives home, back row
    /// first so nobody drives past a row that is still pulling out; once they are gone the next audience queues in past
    /// the ticket booth and fills the lot again. <see cref="DriveInCinema"/>
    /// gives each car and person a body. No engine calls, so the whole programme can be run headless.
    /// </summary>
    public sealed class DriveInTraffic
    {
        public sealed class Car
        {
            public int id, model, livery, bay = -1;
            /// <summary>Where the car stands (lot frame) and its heading and nose-up tilt in degrees.</summary>
            public Vector3 at;
            public float yaw, pitch;
            public bool Moving => path != null;
            /// <summary>On its way in or out, not just waiting in its bay for its turn to leave.</summary>
            public bool Driving => path != null && started;
            internal List<Vector3> path;
            internal float[] along;
            internal float s, delay, pause, waited, forced;
            internal int stop = -1, order, row;
            // A leaving car is on the exit lane once it has driven this far.
            internal float merged;
            internal bool leaving, started, paid;
        }
        public sealed class Walker
        {
            public int id, look;
            public Vector3 at;
            public float yaw;
            /// <summary>False until the person has got out of the car.</summary>
            public bool visible;
            internal Vector3 home, counter;
            internal float s, delay, pause;
            internal bool back;
        }

        public const float DriveSpeed = .6f, TurnRate = 260, Occupancy = .88f, LeaveShare = .5f, TicketPause = 1.1f,
            ArrivalGap = 1.5f, DepartureGap = .7f, RowGap = 1.5f, FirstArrival = 3, Headway = .42f, GateRoom = .6f, GiveUp = 3, WalkSpeed = .3f, SnackPause = 3.5f;
        // Cone: how far off straight ahead another car still counts as in the way. ForceFor: how long a car that gave up
        // waiting drives on regardless.
        const float Cone = .35f, ForceFor = 1.2f;
        const int SnackRunners = 6;
        public readonly DriveInLayout layout;
        readonly Car[] bays;
        readonly List<Car> cars = new List<Car>();
        readonly List<Car> queue = new List<Car>();
        readonly List<Walker> walkers = new List<Walker>();
        readonly System.Random random;
        readonly int[] models;
        readonly int liveries, looks;
        int nextId, order;

        /// <summary>Every car with a body: parked, arriving or leaving.</summary>
        public IReadOnlyList<Car> Cars => cars;
        public IReadOnlyList<Walker> Walkers => walkers;
        /// <summary>Cars standing in their bays.</summary>
        public int Parked
        {
            get
            {
                int n = 0;
                foreach (var car in cars)
                    if (!car.Moving)
                        n++;
                return n;
            }
        }
        /// <summary>Cars driving in or out right now.</summary>
        public int Moving
        {
            get
            {
                int n = 0;
                foreach (var car in cars)
                    if (car.Driving)
                        n++;
                return n;
            }
        }
        /// <summary>Cars still to come for the next show.</summary>
        public int Expected => queue.Count;

        public DriveInTraffic(DriveInLayout layout, int seed, int[] models, int liveries, int looks)
        {
            this.layout = layout;
            this.models = models;
            this.liveries = liveries;
            this.looks = looks;
            bays = new Car[layout.bays.Count];
            random = new System.Random(seed);
        }

        /// <summary>A lot seen for the first time is mid-show: most bays already taken.</summary>
        public void Fill()
        {
            for (int i = 0; i < bays.Length; i++)
                if (bays[i] == null && random.NextDouble() < Occupancy)
                {
                    var car = NewCar();
                    cars.Add(car);
                    Park(car, i);
                }
        }
        /// <summary>What a reel change sets going: cars leave after the last film, new ones come for the next show, and the intermission sends people to the snack bar.</summary>
        public void Cue(DriveInFilm.Reel reel)
        {
            if (reel == DriveInFilm.Reel.Goodnight)
                ScheduleDepartures();
            else if (reel == DriveInFilm.Reel.Welcome)
                ScheduleArrivals();
            else if (reel == DriveInFilm.Reel.Intermission)
                SnackRun();
        }
        public void Step(float dt)
        {
            // The next car in line drives in when its time has come, the leaving audience has gone and the gate is clear.
            if (queue.Count > 0)
            {
                foreach (var car in queue)
                    car.delay -= dt;
                if (queue[0].delay <= 0 && GateClear())
                {
                    var car = queue[0];
                    queue.RemoveAt(0);
                    StartArrival(car);
                }
            }
            // Oldest first, so a queue moves up in order.
            cars.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (var car in cars)
                if (car.Moving)
                    Drive(car, dt);
            cars.RemoveAll(c => c.leaving && !c.Moving);
            Walk(dt);
        }

        Car NewCar() => new Car { id = ++nextId, model = models[random.Next(models.Length)], livery = random.Next(liveries) };
        void Park(Car car, int bay)
        {
            var spot = layout.bays[bay];
            car.bay = bay;
            bays[bay] = car;
            car.path = null;
            // Nose up on the ramp, towards the screen.
            car.at = spot.at + Vector3.up * .012f;
            car.yaw = spot.yaw;
            car.pitch = -DriveInLayout.RampTilt;
        }
        void ScheduleDepartures()
        {
            var parked = new List<Car>();
            foreach (var car in cars)
                if (!car.Moving)
                    parked.Add(car);
            Shuffle(parked);
            int leaving = Mathf.RoundToInt(parked.Count * LeaveShare);
            parked.RemoveRange(leaving, parked.Count - leaving);
            // Back row first, and along a row the end nearest the way out first: nobody pulls out in front of a car behind,
            // and cars driving up the exit lane only pass rows that have already left.
            parked.Sort((a, b) =>
            {
                var x = layout.bays[a.bay];
                var y = layout.bays[b.bay];
                return x.row != y.row ? y.row.CompareTo(x.row) : x.angle.CompareTo(y.angle);
            });
            float delay = 0;
            for (int i = 0; i < parked.Count; i++)
            {
                var car = parked[i];
                if (i > 0)
                    delay += layout.bays[car.bay].row != layout.bays[parked[i - 1].bay].row ? RowGap : DepartureGap;
                car.leaving = true;
                car.started = false;
                car.delay = delay + (float)random.NextDouble() * .2f;
                car.row = layout.bays[car.bay].row;
                SetPath(car, layout.DeparturePath(layout.bays[car.bay]), -1);
                car.merged = car.along[car.along.Length - DriveInLayout.ExitLanePoints];
            }
        }
        void ScheduleArrivals()
        {
            int staying = 0;
            foreach (var car in cars)
                if (!car.leaving)
                    staying++;
            int wanted = Mathf.RoundToInt(bays.Length * Occupancy) - staying - queue.Count;
            for (int i = 0; i < wanted; i++)
            {
                var car = NewCar();
                car.delay = FirstArrival + i * ArrivalGap + (float)random.NextDouble() * .4f;
                queue.Add(car);
            }
        }
        /// <summary>No leaving car still in the lot and nobody just inside the gate.</summary>
        bool GateClear()
        {
            foreach (var car in cars)
                if (car.Moving && (car.leaving || (car.at - layout.gateIn).sqrMagnitude < GateRoom * GateRoom))
                    return false;
            return true;
        }
        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        static void SetPath(Car car, List<Vector3> path, int stop)
        {
            car.path = path;
            car.stop = stop;
            car.s = 0;
            car.along = new float[path.Count];
            for (int i = 1; i < path.Count; i++)
                car.along[i] = car.along[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        }
        /// <summary>An arriving car takes a free bay when its turn comes; with none left it stays home.</summary>
        void StartArrival(Car car)
        {
            int free = 0, chosen = -1;
            for (int i = 0; i < bays.Length; i++)
                if (bays[i] == null && random.Next(++free) == 0)
                    chosen = i;
            if (chosen < 0)
                return;
            car.bay = chosen;
            bays[chosen] = car;
            SetPath(car, layout.ArrivalPath(layout.bays[chosen], out int stop), stop);
            car.started = true;
            car.order = ++order;
            cars.Add(car);
            Place(car, 0, true);
        }
        void Drive(Car car, float dt)
        {
            if (!car.started)
            {
                car.delay -= dt;
                if (car.delay > 0 || BackRowsLeaving(car))
                    return;
                car.started = true;
                car.order = ++order;
            }
            if (car.pause > 0)
            {
                car.pause -= dt;
                return;
            }
            if (car.forced > 0)
                car.forced -= dt;
            else if (Blocked(car))
            {
                // A last resort: after waiting a while the car squeezes past for a moment rather than stay stuck.
                if ((car.waited += dt) < GiveUp)
                    return;
                car.forced = ForceFor;
            }
            car.waited = 0;
            float end = car.along[car.along.Length - 1], next = car.s + DriveSpeed * dt;
            if (!car.paid && car.stop >= 0 && next >= car.along[car.stop])
            {
                // Pay at the ticket booth.
                next = car.along[car.stop];
                car.paid = true;
                car.pause = TicketPause;
            }
            car.s = Mathf.Min(next, end);
            // A leaving car frees its bay once it has pulled out.
            if (car.leaving && car.bay >= 0 && car.s > .45f)
            {
                bays[car.bay] = null;
                car.bay = -1;
            }
            if (car.s < end)
                Place(car, dt, false);
            else if (car.leaving)
                car.path = null; // gone home
            else
                Park(car, car.bay);
        }
        /// <summary>A row pulls out only once every car from the rows behind it has joined the exit lane, so none is still merging.</summary>
        bool BackRowsLeaving(Car car)
        {
            foreach (var other in cars)
                if (other.leaving && other.Driving && other.row != car.row && other.s < other.merged)
                    return true;
            return false;
        }
        /// <summary>
        /// Another moving car close ahead, roughly the way this one is heading. When each is in the other's way, the one
        /// that set off first has right of way.
        /// </summary>
        bool Blocked(Car car)
        {
            var ahead = Forward(car.yaw);
            foreach (var other in cars)
            {
                if (other == car || !other.Moving || !other.started)
                    continue;
                var gap = other.at - car.at;
                gap.y = 0;
                float distance = gap.magnitude;
                if (distance >= Headway || Vector3.Dot(gap, ahead) <= Cone * distance)
                    continue;
                bool mutual = Vector3.Dot(-gap, Forward(other.yaw)) > Cone * distance;
                if (mutual && car.order < other.order)
                    continue;
                return true;
            }
            return false;
        }
        static Vector3 Forward(float yaw) => new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0, Mathf.Cos(yaw * Mathf.Deg2Rad));
        /// <summary>Puts a moving car on its path, turned towards a point a little ahead so it rounds corners.</summary>
        static void Place(Car car, float dt, bool snap)
        {
            car.at = Sample(car, car.s);
            var ahead = Sample(car, car.s + .12f) - Sample(car, car.s - .03f);
            car.pitch = snap ? 0 : Mathf.MoveTowards(car.pitch, 0, 20 * dt);
            if (ahead.x * ahead.x + ahead.z * ahead.z < 1e-6f)
                return;
            float target = Mathf.Atan2(ahead.x, ahead.z) * Mathf.Rad2Deg;
            car.yaw = snap ? target : Mathf.MoveTowardsAngle(car.yaw, target, TurnRate * dt);
        }
        static Vector3 Sample(Car car, float s)
        {
            var path = car.path;
            var along = car.along;
            int last = path.Count - 1;
            if (s <= 0 || last == 0)
                return path[0];
            if (s >= along[last])
                return path[last] + (path[last] - path[last - 1]).normalized * (s - along[last]);
            int i = 1;
            while (along[i] < s)
                i++;
            float span = along[i] - along[i - 1];
            return Vector3.Lerp(path[i - 1], path[i], span > 0 ? (s - along[i - 1]) / span : 1);
        }

        /// <summary>In the intermission a few drivers walk from their cars to the snack bar counter and back.</summary>
        void SnackRun()
        {
            var parked = new List<Car>();
            foreach (var car in cars)
                if (!car.Moving)
                    parked.Add(car);
            Shuffle(parked);
            float counterZ = layout.snackBar.z + layout.snackBarSize.z / 2 + .07f;
            for (int i = 0; i < Mathf.Min(SnackRunners, parked.Count); i++)
            {
                var spot = layout.bays[parked[i].bay];
                var right = new Vector3(Mathf.Cos(spot.yaw * Mathf.Deg2Rad), 0, -Mathf.Sin(spot.yaw * Mathf.Deg2Rad));
                var home = spot.at + right * .17f;
                walkers.Add(new Walker
                {
                    id = ++nextId,
                    look = random.Next(looks),
                    home = home,
                    at = home,
                    counter = new Vector3((i % 3 - 1) * .12f, DriveInLayout.Deck, counterZ + i / 3 * .07f),
                    delay = i * .9f + (float)random.NextDouble(),
                });
            }
        }
        void Walk(float dt)
        {
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                var w = walkers[i];
                if (w.delay > 0)
                {
                    w.delay -= dt;
                    continue;
                }
                w.visible = true;
                if (w.pause > 0)
                {
                    w.pause -= dt;
                    continue;
                }
                Vector3 from = w.back ? w.counter : w.home, to = w.back ? w.home : w.counter;
                float length = Vector3.Distance(from, to);
                w.s = Mathf.Min(1, w.s + WalkSpeed * dt / Mathf.Max(length, .01f));
                w.at = Vector3.Lerp(from, to, w.s) + Vector3.up * Mathf.Abs(Mathf.Sin(w.s * length * 40)) * .008f;
                w.yaw = Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
                if (w.s < 1)
                    continue;
                if (!w.back)
                {
                    w.back = true;
                    w.s = 0;
                    w.pause = SnackPause;
                    continue;
                }
                walkers.RemoveAt(i);
            }
        }
    }
}
