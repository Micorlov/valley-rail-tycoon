using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Highway breakdowns. Now and then a car stops dead on an open highway with its hazard lights blinking, steam
    /// rising from the bonnet and a warning triangle behind it. The traffic behind it queues up, and more cars keep
    /// arriving from behind until the jam is several cars long. Then a tow truck comes up the hard shoulder past the
    /// queue, pulls in ahead of the car, backs up to it, winches it up onto its flatbed and drives off with it, and the
    /// jam clears. The jam's extra cars and the tow truck leave the road out of sight, and the car the breakdown took
    /// is replaced out of sight, so the traffic keeps its size.
    /// </summary>
    public sealed partial class CityTraffic
    {
        enum Trouble { None, Broken, Towing, Loading }
        sealed partial class Car
        {
            /// <summary>The part this vehicle plays in a breakdown: the broken-down car, or the tow truck on its way or winching.</summary>
            public Trouble trouble;
            /// <summary>Added for a jam or a tow: leaves the road once out of sight after the breakdown.</summary>
            public bool extra;
        }
        // Traffic seconds before the first breakdown, and between the end of one and the next; retry sooner when no car can break down.
        const float FirstBreakdown = 25, ShortestGap = 45, LongestGap = 90, Retry = 12;
        // Seconds the car stands before the tow truck sets off, and the truck's pace on the shoulder, pulling in and backing up.
        const float TowDelay = 8, TowPace = 1.8f, PullPace = .6f, BackPace = .35f;
        // Cells (as progress) the truck overshoots before pulling in, then drives on while pulling in; the gap it leaves to the car.
        const float Overshoot = .3f, PullRun = .3f, TowGap = .05f;
        // Seconds of winching, and when in it the ramps go down, the car rolls and the ramps come up again.
        const float LoadTime = 5, RampsDown = .12f, RollFrom = .2f, RollTo = .85f, RampsUp = .92f;
        // Cells the tow truck and the jam's cars start behind the car at most; jam cars; traffic seconds between them.
        const int TowReach = 10, JamReach = 12, JamCars = 6;
        const float JamGap = 1.8f;
        // A car counts as out of sight this far (in screen fractions) past the edge; far out, cars may come and go in view.
        const float OutOfSight = .08f, FarZoom = 14, BlinkBeat = .38f, LongestLoad = .42f;
        // Room a new car needs where it starts: the longest bus and a car nose to tail, with the following gap.
        const float StartRoom = .8f;
        // Traffic seconds a car may stand in a gridlock before it leaves the road, out of sight and on screen.
        const float Gridlock = 30, GridlockInView = 120;
        static readonly Color Hazard = new Color(1f, .62f, .08f), Warning = new Color(.9f, .12f, .1f), Steam = new Color(.9f, .9f, .92f),
            RampSteel = new Color(.42f, .44f, .47f);

        readonly System.Random fate = new System.Random(4077);
        // The broken-down car and the tow truck on its way to it (null once it drives off with the car).
        Car stranded, truck;
        float incidentClock, loadClock, nextBreakdown = FirstBreakdown, towStop, jamWait, tidyWait;
        int jamLeft, towStep, regulars = -1;
        // Where the car stood in the truck's frame when winching began.
        Vector3 loadFrom;
        Quaternion loadTurn;
        Transform warning, ramps;
        readonly List<Transform> steam = new List<Transform>();
        readonly List<GameObject> hazards = new List<GameObject>();
        // Hazard lights and tow-truck beacons, each lit on its beat (0 or 1).
        readonly List<(Renderer lamp, int beat)> blinkers = new List<(Renderer, int)>();
        // The spawn slots of the settled traffic: replacements for cars a breakdown took start from one out of sight.
        List<(Cell cell, int entry)> homes;
        CameraController rig;
        Mesh triangle;

        /// <summary>The broken-down car right now, or null.</summary>
        public Transform BrokenDown => stranded?.view;
        /// <summary>The tow truck on its way to the broken-down car or winching it up, or null.</summary>
        public Transform TowTruck => truck?.view;

        /// <summary>Runs the breakdown clock: a new breakdown, the jam behind one, the tow truck setting off, tidying up after.</summary>
        void RunIncident(float seconds)
        {
            Tidy(seconds);
            if (stranded == null)
            {
                nextBreakdown -= seconds;
                if (nextBreakdown <= 0)
                    nextBreakdown = BreakDown() ? LongestGap : Retry;
                return;
            }
            incidentClock += seconds;
            if (truck != null && truck.trouble == Trouble.Loading)
            {
                loadClock += seconds;
                return;
            }
            Puff();
            AddJamCar(seconds);
            if (truck == null && incidentClock >= TowDelay)
                SendTowTruck();
        }
        /// <summary>Drives a vehicle with a part in the breakdown; false for one the ordinary rules drive.</summary>
        bool DriveIncident(Car car, float seconds)
        {
            switch (car.trouble)
            {
                case Trouble.Broken:
                    // It stands where it stopped (on the truck's bed while winched): never posed, never waiting for anyone.
                    car.waitFor = -1;
                    return true;
                case Trouble.Towing:
                    DriveTowTruck(car, seconds);
                    Pose(car);
                    return true;
                case Trouble.Loading:
                    Winch(car);
                    return true;
            }
            return false;
        }
        /// <summary>A tow truck on the hard shoulder is passing the jam, not queuing in it.</summary>
        static bool OnShoulder(Car car) => car.lane > Lane + .01f;

        /// <summary>Stops a car on an open, straight stretch of highway: one on screen if the camera is close, else any.</summary>
        bool BreakDown()
        {
            var candidates = new List<Car>();
            var seen = new List<Car>();
            foreach (var car in cars)
                if (CanBreakDown(car))
                {
                    candidates.Add(car);
                    if (!FarView && Seen(car.at))
                        seen.Add(car);
                }
            if (candidates.Count == 0)
                return false;
            var pick = seen.Count > 0 ? seen : candidates;
            stranded = pick[fate.Next(pick.Count)];
            stranded.trouble = Trouble.Broken;
            stranded.waitFor = stranded.claim = stranded.wants = -1;
            stranded.stalled = 0;
            incidentClock = loadClock = 0;
            jamLeft = JamCars;
            jamWait = 0;
            towStep = 0;
            towStop = (VehicleCatalog.TowTruck.length + stranded.length) / 2 + TowGap;
            Strand(stranded);
            return true;
        }
        bool CanBreakDown(Car car)
        {
            if (car.isolated || car.extra || car.trouble != Trouble.None || car.stalled > 0 || car.progress < .15f || car.progress > .45f)
                return false;
            var model = VehicleCatalog.Models[car.model];
            if ((model.kind != VehicleKind.Car && model.kind != VehicleKind.Truck) || model.length > LongestLoad)
                return false;
            // Straight on here and in the next cell, where the truck pulls in, with room behind for the truck to come.
            var next = car.cell.Move(car.exit);
            return Straight(car.cell) && Straight(next) && OpenRoad(next.Move(car.exit)) && Trail(car, 3, true).Count >= 3;
        }
        /// <summary>A highway cell between junctions: two ways, both highway, off the rails, off the water, out of town.</summary>
        bool OpenRoad(Cell c)
        {
            int exits = lanes.Exits(c);
            if (Directions.Count(exits) != 2 || lanes.IsTownStreet(c) || GroupOf(c) >= 0 || OnRails(c) || MapDefinition.Water(c))
                return false;
            for (int d = 0; d < 4; d++)
                if ((exits & 1 << d) != 0 && !lanes.IsHighway(c, d))
                    return false;
            return true;
        }
        bool Straight(Cell c) => OpenRoad(c) && Directions.Straight(lanes.Exits(c));
        /// <summary>
        /// The open highway cells behind a car in its lane, nearest first, each with the side traffic enters it by; only
        /// up to the nearest bend if <paramref name="straight"/> (on a bend the hard shoulder runs too close to the lane).
        /// </summary>
        List<(Cell cell, int entry)> Trail(Car car, int reach, bool straight)
        {
            var trail = new List<(Cell, int)>();
            Cell c = car.cell;
            int entry = car.entry;
            for (int k = 0; k < reach; k++)
            {
                var back = c.Move(entry);
                if (straight ? !Straight(back) : !OpenRoad(back))
                    break;
                int ahead = Directions.Opp(entry), from = -1, exits = lanes.Exits(back);
                for (int d = 0; d < 4; d++)
                    if (d != ahead && (exits & 1 << d) != 0)
                        from = d;
                trail.Add((back, from));
                c = back;
                entry = from;
            }
            return trail;
        }

        /// <summary>Hazard lights on all four corners, steam at the bonnet and a warning triangle on the verge behind.</summary>
        void Strand(Car car)
        {
            float half = car.length / 2;
            for (int end = -1; end <= 1; end += 2)
                for (int side = -1; side <= 1; side += 2)
                {
                    var lamp = world.Box("Hazard light", new Vector3(side * .08f, .1f, end * (half + .002f)), new Vector3(.04f, .026f, .012f), Hazard, car.view);
                    hazards.Add(lamp);
                    blinkers.Add((lamp.GetComponent<Renderer>(), 0));
                }
            if (!triangle)
                triangle = Round(3, true);
            var sign = new GameObject("Warning triangle", typeof(MeshFilter), typeof(MeshRenderer));
            sign.transform.SetParent(car.view, false);
            sign.transform.localPosition = new Vector3(Shoulder - Lane, .045f, -half - .5f);
            // Stood up on its base edge, point up, facing the traffic coming from behind.
            sign.transform.localRotation = Quaternion.Euler(-90, 0, 0) * Quaternion.Euler(0, -90, 0);
            sign.transform.localScale = new Vector3(.1f, .012f, .1f);
            sign.GetComponent<MeshFilter>().sharedMesh = triangle;
            sign.GetComponent<MeshRenderer>().sharedMaterial = world.Mat(Warning);
            warning = sign.transform;
            for (int k = 0; k < 3; k++)
                steam.Add(world.Box("Steam", new Vector3(0, .2f, half - .07f), Vector3.one * .04f, Steam, car.view).transform);
            Puff();
        }
        /// <summary>Steam puffs rise from the bonnet, swell and start again.</summary>
        void Puff()
        {
            for (int k = 0; k < steam.Count; k++)
            {
                if (!steam[k])
                    continue;
                float phase = Mathf.Repeat(incidentClock * .6f + k / (float)steam.Count, 1);
                var puff = steam[k];
                puff.localPosition = new Vector3(Mathf.Sin(phase * 5 + k) * .03f, .17f + phase * .3f, puff.localPosition.z);
                puff.localScale = Vector3.one * (.03f + phase * .07f);
                puff.localRotation = Quaternion.Euler(0, phase * 90 + k * 30, phase * 40);
            }
        }
        /// <summary>
        /// Another car comes along the highway to join the jam: it starts out of sight (anywhere, zoomed far out) in the
        /// farthest open cell behind the broken-down car whose lane is clear, and drives up to the back of the queue.
        /// </summary>
        void AddJamCar(float seconds)
        {
            jamWait -= seconds;
            if (jamLeft <= 0 || jamWait > 0 || stranded.trouble != Trouble.Broken)
                return;
            jamWait = JamGap;
            var trail = Trail(stranded, JamReach, false);
            for (int k = trail.Count - 1; k >= 2; k--)
            {
                var (cell, entry) = trail[k];
                var start = LanePoint(cell, entry);
                if ((!FarView && Seen(start)) || !Clear(start, cell))
                    continue;
                int model;
                do
                    model = VehicleCatalog.Mix[fate.Next(VehicleCatalog.Mix.Length)];
                while (VehicleCatalog.Models[model].kind == VehicleKind.Emergency);
                Spawn(cell, entry, 0, model).extra = true;
                jamLeft--;
                return;
            }
        }
        /// <summary>Where a car entering <paramref name="cell"/> by side <paramref name="entry"/> starts, in its lane at the edge.</summary>
        static Vector3 LanePoint(Cell cell, int entry)
        {
            var travel = -Direction(entry);
            return new Vector3(cell.x, 0, cell.z) - travel * .5f + Vector3.Cross(Vector3.up, travel) * Lane;
        }
        /// <summary>No vehicle within StartRoom of where a new one starts, nor of the middle of its cell.</summary>
        bool Clear(Vector3 at, Cell cell)
        {
            foreach (var car in cars)
                if (new Vector2(car.at.x - at.x, car.at.z - at.z).sqrMagnitude < StartRoom * StartRoom ||
                    new Vector2(car.at.x - cell.x, car.at.z - cell.z).sqrMagnitude < StartRoom * StartRoom)
                    return false;
            return true;
        }

        /// <summary>The tow truck sets off from the farthest open cell behind the car that is out of sight, on the hard shoulder.</summary>
        void SendTowTruck()
        {
            var trail = Trail(stranded, TowReach, true);
            if (trail.Count == 0)
            {
                // No way to reach it any more: the driver gets it going again.
                Recover(stranded);
                stranded = null;
                nextBreakdown = ShortestGap;
                return;
            }
            var start = trail[trail.Count - 1];
            for (int k = trail.Count - 1; k >= 0; k--)
                if (!Seen(LanePoint(trail[k].cell, trail[k].entry)))
                {
                    start = trail[k];
                    break;
                }
            var model = VehicleCatalog.TowTruck;
            truck = new Car { model = -1, length = model.length, cell = start.cell, entry = start.entry, trouble = Trouble.Towing, extra = true, lane = Shoulder };
            ChooseExit(truck);
            truck.view = CreateVehicle(model, fate.Next(model.liveries.Length));
            foreach (var lens in model.beacons)
            {
                var lamp = world.Box("Beacon", lens.center, lens.size + Vector3.one * .008f, lens.flash, truck.view);
                lamp.GetComponent<Renderer>().enabled = false;
                blinkers.Add((lamp.GetComponent<Renderer>(), lens.beat));
            }
            towStep = 0;
            cars.Add(truck);
            Pose(truck);
        }
        /// <summary>
        /// Up the shoulder at speed until level with the broken-down car, past it, pulling in ahead of it, then backing up
        /// to leave TowGap between them; there it starts winching. It holds on the shoulder while the lane ahead is busy.
        /// </summary>
        void DriveTowTruck(Car t, float seconds)
        {
            float step = seconds * CellsPerSecond;
            if (towStep == 0)
            {
                t.progress += step * TowPace;
                while (t.progress >= 1 && !t.cell.Equals(stranded.cell))
                {
                    t.progress -= 1;
                    t.cell = t.cell.Move(t.exit);
                    t.entry = Directions.Opp(t.exit);
                    TakePlannedExit(t);
                }
                if (!t.cell.Equals(stranded.cell))
                    return;
                towStep = 1;
            }
            float lead = Lead(t), clear = towStop + Overshoot;
            if (towStep == 1)
            {
                lead = Mathf.Min(clear, lead + step * TowPace * (lead > towStop ? .5f : 1));
                if (lead >= clear && LaneFree(clear, clear + PullRun + t.length))
                    towStep = 2;
            }
            else if (towStep == 2)
            {
                lead = Mathf.Min(clear + PullRun, lead + step * PullPace);
                t.lane = Mathf.Lerp(Shoulder, Lane, Mathf.SmoothStep(0, 1, (lead - clear) / PullRun));
                if (lead >= clear + PullRun)
                    towStep = 3;
            }
            else
            {
                lead = Mathf.Max(towStop, lead - step * BackPace);
                if (lead <= towStop)
                    StartLoading(t);
            }
            PlaceAhead(t, lead);
        }
        /// <summary>How far (in cells of progress) the truck is ahead of the broken-down car along the road.</summary>
        float Lead(Car t) => (t.cell.Equals(stranded.cell) ? 0 : 1) + t.progress - stranded.progress;
        /// <summary>Puts the truck <paramref name="lead"/> ahead of the car: in its cell or the straight cell after it.</summary>
        void PlaceAhead(Car t, float lead)
        {
            float at = stranded.progress + lead;
            t.entry = stranded.entry;
            t.exit = stranded.exit;
            t.cell = at < 1 ? stranded.cell : stranded.cell.Move(stranded.exit);
            t.progress = at < 1 ? at : at - 1;
        }
        /// <summary>No other vehicle in the broken-down car's lane between <paramref name="from"/> and <paramref name="to"/> ahead of it.</summary>
        bool LaneFree(float from, float to)
        {
            var heading = -Direction(stranded.entry);
            foreach (var other in cars)
            {
                if (other == truck || other == stranded || other.isolated)
                    continue;
                float dx = other.at.x - stranded.at.x, dz = other.at.z - stranded.at.z;
                float along = dx * heading.x + dz * heading.z, across = dx * heading.z - dz * heading.x;
                if (Mathf.Abs(across) < LaneHalf * 1.5f && along > from - other.length && along < to + other.length)
                    return false;
            }
            return true;
        }
        void StartLoading(Car t)
        {
            t.trouble = Trouble.Loading;
            t.lane = Lane;
            loadClock = 0;
            // The driver picks up the triangle; the steam has stopped.
            if (warning)
                Destroy(warning.gameObject);
            foreach (var puff in steam)
                if (puff)
                    Destroy(puff.gameObject);
            steam.Clear();
            stranded.view.SetParent(t.view, true);
            loadFrom = stranded.view.localPosition;
            loadTurn = stranded.view.localRotation;
            ramps = new GameObject("Ramps").transform;
            ramps.SetParent(t.view, false);
            float tail = -t.length / 2;
            for (int side = -1; side <= 1; side += 2)
                world.Box("Ramp", new Vector3(side * .06f, VehicleCatalog.TowBedTop / 2, tail - .09f), new Vector3(.05f, .012f, .2f), RampSteel, ramps,
                    Quaternion.Euler(-Mathf.Atan2(VehicleCatalog.TowBedTop, .19f) * Mathf.Rad2Deg, 0, 0));
            ramps.gameObject.SetActive(false);
        }
        /// <summary>The ramps come down, the car rolls up them nose first onto the bed, the ramps go up; then the truck drives off.</summary>
        void Winch(Car t)
        {
            float u = Mathf.Clamp01(loadClock / LoadTime);
            if (ramps)
                ramps.gameObject.SetActive(u > RampsDown && u < RampsUp);
            float roll = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(RollFrom, RollTo, u));
            var bed = new Vector3(0, VehicleCatalog.TowBedTop, VehicleCatalog.TowBedMiddle);
            stranded.view.localPosition = Vector3.Lerp(loadFrom, bed, roll);
            stranded.view.localRotation = Quaternion.Slerp(loadTurn, Quaternion.identity, roll) * Quaternion.Euler(-Mathf.Sin(roll * Mathf.PI) * 14, 0, 0);
            if (u < 1)
                return;
            // Loaded: the hazards go off and the truck joins the traffic with the car on its back, beacons still turning.
            foreach (var lamp in hazards)
                if (lamp)
                    Destroy(lamp);
            hazards.Clear();
            if (ramps)
                Destroy(ramps.gameObject);
            Drop(cars.IndexOf(stranded), false);
            stranded = null;
            t.trouble = Trouble.None;
            t.waitFor = t.claim = t.wants = -1;
            t.stalled = 0;
            Plan(t);
            truck = null;
            nextBreakdown = Mathf.Lerp(ShortestGap, LongestGap, (float)fate.NextDouble());
        }
        /// <summary>The broken-down car drives on again: its hazards, steam and triangle go.</summary>
        void Recover(Car car)
        {
            foreach (var lamp in hazards)
                if (lamp)
                    Destroy(lamp);
            hazards.Clear();
            foreach (var puff in steam)
                if (puff)
                    Destroy(puff.gameObject);
            steam.Clear();
            if (warning)
                Destroy(warning.gameObject);
            car.trouble = Trouble.None;
        }
        /// <summary>
        /// Ends any breakdown at once when the roads change: a car on the truck goes with it, a car still waiting drives on,
        /// and every extra vehicle leaves; the traffic is counted again once it has been refilled.
        /// </summary>
        void EndIncident()
        {
            if (stranded != null)
            {
                int index = cars.IndexOf(stranded);
                if (index >= 0 && stranded.view.parent != transform)
                    Drop(index, false);
                else
                    Recover(stranded);
            }
            for (int i = cars.Count - 1; i >= 0; i--)
                if (cars[i].extra)
                    Drop(i, true);
            if (ramps)
                Destroy(ramps.gameObject);
            stranded = truck = null;
            hazards.Clear();
            steam.Clear();
            regulars = -1;
        }
        /// <summary>
        /// Twice a second: once no breakdown is on, extra vehicles out of sight leave the road; a car stuck in a gridlock
        /// that the waiting-circle rule could not clear leaves too (queuing behind a breakdown is no gridlock); and while
        /// the traffic is short of the cars it settled with, one more starts from a spawn slot out of sight.
        /// </summary>
        void Tidy(float seconds)
        {
            tidyWait -= seconds;
            if (tidyWait > 0)
                return;
            tidyWait = .5f;
            if (regulars < 0)
                regulars = Regulars();
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                var car = cars[i];
                if (car.isolated || car.trouble != Trouble.None)
                    continue;
                bool leave = car.extra ? stranded == null && !Seen(car.at) : car.stalled >= Gridlock && !Queuing(car) && (car.stalled >= GridlockInView || !Seen(car.at));
                if (leave)
                    Drop(i, true);
            }
            if (homes == null || homes.Count == 0 || Regulars() >= regulars)
                return;
            for (int tries = 0; tries < 8; tries++)
            {
                var (cell, entry) = homes[fate.Next(homes.Count)];
                var start = LanePoint(cell, entry);
                if (!Drivable(cell) || Seen(start) || Seen(new Vector3(cell.x, 0, cell.z)) || !Clear(start, cell))
                    continue;
                Spawn(cell, entry, 0, VehicleCatalog.Mix[spawned % VehicleCatalog.Mix.Length]);
                return;
            }
        }
        /// <summary>The car waits, through the cars ahead of it, for the broken-down car.</summary>
        bool Queuing(Car car)
        {
            if (stranded == null)
                return false;
            for (int k = car.waitFor, steps = 0; k >= 0 && steps < cars.Count; k = cars[k].waitFor, steps++)
                if (cars[k] == stranded)
                    return true;
            return false;
        }
        int Regulars()
        {
            int count = 0;
            foreach (var car in cars)
                if (!car.extra && car.trouble == Trouble.None)
                    count++;
            return count;
        }
        /// <summary>Takes a vehicle off the road list, keeping the other cars' waitFor indices right; destroys its body if asked.</summary>
        void Drop(int index, bool destroy)
        {
            if (index < 0)
                return;
            var car = cars[index];
            if (destroy && car.view)
            {
                car.view.gameObject.SetActive(false);
                Destroy(car.view.gameObject);
            }
            cars.RemoveAt(index);
            foreach (var other in cars)
                if (other.waitFor == index)
                    other.waitFor = -1;
                else if (other.waitFor > index)
                    other.waitFor--;
            if (car == truck)
                truck = null;
        }
        /// <summary>Whether a point on the road is on screen, give or take OutOfSight. Without a camera nothing is.</summary>
        bool Seen(Vector3 at)
        {
            if (!rig)
                rig = FindAnyObjectByType<CameraController>();
            var view = rig ? rig.view : null;
            if (!view)
                return false;
            var p = view.WorldToViewportPoint(transform.TransformPoint(at));
            return p.x > -OutOfSight && p.x < 1 + OutOfSight && p.y > -OutOfSight && p.y < 1 + OutOfSight;
        }
        /// <summary>Zoomed out so far that a car coming or going in view is a speck.</summary>
        bool FarView => rig && rig.zoom > FarZoom;
        /// <summary>Hazard lights blink together; a tow truck's beacons take turns.</summary>
        void FlashIncident(float time)
        {
            int beat = (int)(time / BlinkBeat) % 2;
            for (int i = blinkers.Count - 1; i >= 0; i--)
            {
                var (lamp, on) = blinkers[i];
                if (!lamp)
                {
                    blinkers.RemoveAt(i);
                    continue;
                }
                if (lamp.enabled != (on == beat))
                    lamp.enabled = on == beat;
            }
        }
    }
}
