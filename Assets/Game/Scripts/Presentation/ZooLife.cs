using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// Life in a city zoo, so it is never empty: visitors strolling the aisles (families with a child out in front, some
    /// with balloons or a pram), people leaning on the railings and feeding the giraffes from the deck, a queue at the
    /// ice-cream kiosk, keepers, people waiting on the platforms, and the miniature train shuttling round the pens with
    /// its passengers. The animals are in ZooAnimals.cs. Built by <see cref="CityLife"/> from the zoo emitter; everything
    /// sits in the lot frame and follows <see cref="ZooLayout.Plan"/>. A zoo off the screen sleeps.
    /// </summary>
    public sealed partial class ZooLife
    {
        sealed class Walker
        {
            public Transform view, companion;
            public Vector3[] route;
            public float[] speeds;
            public bool loop;
            public float at, length, pace, wait, bob = WalkBob, stride = 40, lane = Lane, ahead;
            public int heading = 1;
            public Vector3 facing = Vector3.forward;
        }
        sealed class Idler
        {
            public Transform view;
            public Vector3 at;
            public float yaw, phase, sway, bounce, rate = 1;
        }
        const float Adult = 1.6f, Child = 1.15f, Rider = 1.25f, Lane = .045f, WalkBob = .008f, Margin = .35f;
        const float TrainTop = .45f, TrainAccel = .2f, Dwell = 6f, RailTop = .056f;
        static readonly Color Khaki = new Color(.62f, .56f, .34f), HatGreen = new Color(.3f, .38f, .2f), Bucket = new Color(.55f, .58f, .62f),
            Leaf = new Color(.32f, .6f, .24f), PramBlue = new Color(.2f, .28f, .45f), Steel = new Color(.7f, .72f, .75f), Soot = new Color(.12f, .12f, .13f),
            Brass = new Color(.95f, .76f, .3f), Buffer = new Color(.86f, .24f, .2f), Steam = new Color(.93f, .93f, .95f), Canopy = new Color(.96f, .95f, .91f),
            String = new Color(.95f, .95f, .95f), CameraBody = new Color(.1f, .1f, .12f);
        static readonly Color[] Balloons = { new Color(.95f, .2f, .25f), new Color(.2f, .55f, .95f), new Color(.98f, .8f, .15f), new Color(.6f, .3f, .85f) };
        static readonly Color[] Liveries = { new Color(.12f, .45f, .25f), new Color(.75f, .16f, .14f) };
        static readonly Color[] CoachColours = { new Color(.2f, .38f, .7f), new Color(.95f, .62f, .15f) };
        readonly List<Walker> walkers = new List<Walker>();
        readonly List<Idler> idlers = new List<Idler>();
        readonly System.Random random;
        readonly WorldView world;
        readonly ZooLayout plan = ZooLayout.Plan;
        readonly Transform root;
        Transform[] cars;
        Transform[] puffs;
        float clock, trainSpeed, dwell = 2;
        int trainHeading = -1, riders, staff;

        public ZooLife(WorldView world, Transform parent, WorldView.Emitter spot)
        {
            this.world = world;
            // Seeded by the lot, so each zoo looks the same after every redraw and never touches the town's own randomness.
            random = new System.Random(Mathf.RoundToInt(spot.at.x) * 7919 + Mathf.RoundToInt(spot.at.z) * 104729 + 57);
            root = new GameObject("Zoo").transform;
            root.SetParent(parent, false);
            root.localPosition = spot.at;
            root.localRotation = spot.turn;
            AddAnimals();
            AddWalks();
            AddWatchers();
            AddStaff();
            AddTrain();
            // Swimmers, climbers and the rest take their places before the first frame.
            AnimateAnimals(0);
        }

        public Transform Root => root;
        /// <summary>Everyone at the zoo: walking, watching, queueing, working or riding the train.</summary>
        public int People => walkers.Count + idlers.Count + riders;
        public int Walkers => walkers.Count;
        public int Keepers => staff;
        public int Riders => riders;
        /// <summary>The visitors walking the aisles.</summary>
        public IEnumerable<Transform> Strollers
        {
            get
            {
                foreach (var w in walkers)
                    yield return w.view;
            }
        }
        /// <summary>Loco, coach, coach, loco, west end first.</summary>
        public Transform[] TrainCars => cars;
        /// <summary>Distance along the track of the train's west end.</summary>
        public float TrainAt { get; private set; }
        /// <summary>How many times the train has pulled up at a platform.</summary>
        public int TrainStops { get; private set; }

        float Next() => (float)random.NextDouble();
        float Range(float min, float max) => min + (max - min) * Next();
        static float Toward(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
        static float Yaw(Vector3 facing) => Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        Color Clothes() => CityLife.Shirts[random.Next(CityLife.Shirts.Length)];
        Transform Standing(string name, float scale, Color shirt, Transform parent = null) => CityLife.Figure(world, parent ? parent : root, name, shirt, scale);
        Transform Node(string name, Transform parent = null)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent ? parent : root, false);
            return node;
        }
        GameObject Box(string name, Vector3 at, Vector3 size, Color color, Transform parent, Quaternion? turn = null) => world.Box(name, at, size, color, parent, turn);
        /// <summary>A seated person on a seat <paramref name="seat"/> above <paramref name="at"/>, looking along <paramref name="yaw"/>.</summary>
        Transform Seated(Transform parent, string name, Vector3 at, float seat, float yaw, float s, Color shirt)
        {
            var view = Node(name, parent);
            view.localPosition = at;
            view.localRotation = Quaternion.Euler(0, yaw, 0);
            Box("Legs", new Vector3(0, seat + .0125f * s, .035f * s), new Vector3(.035f, .025f, .07f) * s, CityLife.Trousers, view);
            Box("Shins", new Vector3(0, seat / 2, .07f * s), new Vector3(.03f * s, seat, .02f * s), CityLife.Trousers, view);
            Box("Body", new Vector3(0, seat + .055f * s, 0), new Vector3(.05f, .06f, .03f) * s, shirt, view);
            Box("Head", new Vector3(0, seat + .1f * s, 0), Vector3.one * .03f * s, CityLife.Skin, view);
            return view;
        }
        Idler Idle(Transform view, Vector3 at, float yaw, float sway, float bounce = 0, float rate = 1)
        {
            var idler = new Idler { view = view, at = at, yaw = yaw, phase = Next() * 6, sway = sway, bounce = bounce, rate = rate };
            view.localPosition = at;
            view.localRotation = Quaternion.Euler(0, yaw, 0);
            idlers.Add(idler);
            return idler;
        }
        /// <summary>A keeper in khaki with a bush hat.</summary>
        Transform Keeper(string name)
        {
            var keeper = Standing(name, Adult, Khaki);
            Box("Hat", new Vector3(0, .158f, 0) * Adult, new Vector3(.05f, .012f, .05f) * Adult, HatGreen, keeper);
            staff++;
            return keeper;
        }

        // ---- Visitors ---------------------------------------------------------------------------------------------------
        void AddWalks()
        {
            int n = 0;
            foreach (var route in plan.Walks)
                for (int i = 0; i < 2; i++, n++)
                    Dress(AddWalker(route, false, Next(), .16f + .05f * Next()), n);
            for (int i = 0; i < 3; i++, n++)
            {
                var w = AddWalker(plan.Round, true, (i + .3f * Next()) / 3, .15f + .05f * Next());
                w.heading = i % 2 == 0 ? 1 : -1;
                Dress(w, n);
            }
        }
        Walker AddWalker(Vector3[] path, bool loop, float start, float pace)
        {
            var route = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++)
                route[i] = path[i] + Vector3.up * plan.PathTop;
            float length = 0;
            for (int i = 0; i < route.Length - (loop ? 0 : 1); i++)
                length += Vector3.Distance(route[i], route[(i + 1) % route.Length]);
            var w = new Walker { view = Standing("Zoo visitor", Adult, Clothes()), route = route, loop = loop, length = length, at = start * length, pace = pace };
            walkers.Add(w);
            Pose(w);
            return w;
        }
        /// <summary>Every third visitor brings a child walking out in front, some with a balloon; every fifth pushes a pram.</summary>
        void Dress(Walker w, int n)
        {
            if (n % 3 == 0)
            {
                var child = Standing("Visitor child", Child, Clothes());
                if (n % 2 == 0)
                    Balloon(child, Balloons[n / 2 % Balloons.Length]);
                w.companion = child;
                w.ahead = .08f;
            }
            else if (n % 5 == 1)
            {
                var pram = Node("Pram");
                Box("Pram body", new Vector3(0, .05f, 0), new Vector3(.045f, .04f, .07f), PramBlue, pram);
                Box("Pram hood", new Vector3(0, .08f, -.02f), new Vector3(.045f, .03f, .03f), PramBlue, pram);
                for (int i = 0; i < 4; i++)
                    Box("Pram wheel", new Vector3(i % 2 == 0 ? -.025f : .025f, .012f, i < 2 ? -.025f : .025f), new Vector3(.008f, .024f, .024f), CityLife.Trousers, pram);
                Box("Pram handle", new Vector3(0, .085f, -.05f), new Vector3(.04f, .008f, .008f), Steel, pram);
                w.companion = pram;
                w.ahead = .1f;
            }
        }
        void Balloon(Transform child, Color color)
        {
            Box("Balloon string", new Vector3(.035f, .23f, 0), new Vector3(.003f, .15f, .003f), String, child);
            Box("Balloon", new Vector3(.035f, .33f, 0), new Vector3(.055f, .065f, .055f), color, child, Quaternion.Euler(0, 45, 0));
        }
        /// <summary>The point <paramref name="distance"/> along a walker's route, and the way that stretch runs.</summary>
        static void Locate(Walker w, float distance, out Vector3 point, out Vector3 along, out int segment)
        {
            var route = w.route;
            int count = w.loop ? route.Length : route.Length - 1;
            float left = w.loop ? Mathf.Repeat(distance, w.length) : Mathf.Clamp(distance, 0, w.length);
            for (int i = 0; i < count; i++)
            {
                Vector3 a = route[i], b = route[(i + 1) % route.Length];
                float leg = Vector3.Distance(a, b);
                if (left <= leg || i == count - 1)
                {
                    along = b - a;
                    point = a + (leg > 0 ? along * Mathf.Clamp01(left / leg) : Vector3.zero);
                    segment = i;
                    return;
                }
                left -= leg;
            }
            point = route[0];
            along = Vector3.forward;
            segment = 0;
        }
        /// <summary>Places a walker (and whoever walks with it) on its route, keeping to its right so people passing do not overlap.</summary>
        static void Pose(Walker w)
        {
            Locate(w, w.at, out var point, out var along, out _);
            var flat = new Vector3(along.x, 0, along.z) * w.heading;
            if (flat.sqrMagnitude > 1e-6f)
                w.facing = flat.normalized;
            var right = new Vector3(w.facing.z, 0, -w.facing.x);
            float bob = w.wait > 0 ? 0 : Mathf.Abs(Mathf.Sin(w.at * w.stride)) * w.bob;
            var turn = Quaternion.LookRotation(w.facing);
            w.view.localPosition = point + right * w.lane + Vector3.up * bob;
            w.view.localRotation = turn;
            if (!w.companion)
                return;
            Locate(w, w.at + w.heading * w.ahead, out var lead, out var leadAlong, out _);
            var leadFacing = new Vector3(leadAlong.x, 0, leadAlong.z) * w.heading;
            var leadRight = leadFacing.sqrMagnitude > 1e-6f ? new Vector3(leadFacing.z, 0, -leadFacing.x).normalized : right;
            w.companion.localPosition = lead + leadRight * w.lane + Vector3.up * bob * .5f;
            w.companion.localRotation = leadFacing.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(leadFacing) : turn;
        }
        void Walk(Walker w, float step)
        {
            if (w.wait > 0)
            {
                // Paused at the end of a walk (the gate, a platform, an aisle's end), then heads back.
                w.wait -= step;
                if (w.wait <= 0)
                    w.heading = -w.heading;
            }
            else
            {
                w.at += step * w.pace * w.heading;
                if (w.loop)
                    w.at = Mathf.Repeat(w.at, w.length);
                else if (w.at >= w.length || w.at <= 0)
                {
                    w.at = Mathf.Clamp(w.at, 0, w.length);
                    w.wait = 1.5f + Next() * 3;
                }
            }
            Pose(w);
        }

        /// <summary>People at the railings (some children, one with a camera), feeders on the deck, the kiosk queue, benches and platforms.</summary>
        void AddWatchers()
        {
            foreach (var spot in plan.Viewpoints)
            {
                if (Next() < .3f)
                    continue;
                bool child = Next() < .3f;
                var view = Standing(child ? "Watching child" : "Watcher", child ? Child : Adult, Clothes());
                if (!child && Next() < .2f)
                    Box("Camera", new Vector3(0, .125f, .03f) * Adult, new Vector3(.03f, .02f, .02f) * Adult, CameraBody, view);
                Idle(view, spot.at + Vector3.up * plan.PathTop, Yaw(spot.facing) + Range(-12, 12), 8);
            }
            foreach (var spot in plan.Feeders)
            {
                var feeder = Standing("Giraffe feeder", Next() < .35f ? Child : Adult, Clothes());
                Box("Leaves", new Vector3(0, .13f, .07f) * Adult, new Vector3(.03f, .03f, .04f), Leaf, feeder);
                Idle(feeder, spot.at, Yaw(spot.facing) + Range(-10, 10), 6, .004f, 1.4f);
            }
            for (int i = 0; i < 2; i++)
            {
                var at = plan.Hatch + plan.HatchFacing * (.1f + i * .11f) + Vector3.up * plan.PlazaTop;
                Idle(Standing("Queueing", i == 1 ? Child : Adult, Clothes()), at, Yaw(-plan.HatchFacing), 10);
            }
            foreach (var bench in plan.Benches)
            {
                if (Next() < .35f)
                    continue;
                bool platform = bench.at.z > plan.PlatformEdge;
                var at = bench.at + Vector3.up * ((platform ? plan.PlatformTop : plan.PlazaTop) + .002f);
                Idle(Seated(root, "Bench sitter", at, .055f, 0, Adult, Clothes()), at, Yaw(bench.facing), 5, 0, .8f);
            }
            foreach (var spot in plan.Waiting)
                if (Next() < .75f)
                    Idle(Standing("Waiting for the train", Next() < .3f ? Child : Adult, Clothes()), spot.at + Vector3.up * plan.PlatformTop, Yaw(spot.facing) + Range(-20, 20), 10);
        }
        /// <summary>A keeper feeding the giraffes from the deck and one with a bucket of fish among the penguins.</summary>
        void AddStaff()
        {
            var deck = Keeper("Keeper");
            Box("Leaves", new Vector3(0, .15f, .08f) * Adult, new Vector3(.04f, .04f, .05f), Leaf, deck);
            var d = plan.Deck;
            Idle(deck, new Vector3(d.xMax - .06f, plan.DeckTop, d.center.y), 180, 12, .005f, 1.2f);
            var fish = Keeper("Keeper");
            Box("Fish bucket", new Vector3(.05f, .05f, .02f) * Adult, new Vector3(.035f, .035f, .035f) * Adult, Bucket, fish);
            Idle(fish, plan.PenguinKeeper.at + Vector3.up * plan.PenTop, Yaw(plan.PenguinKeeper.facing), 25, 0, .7f);
        }

        // ---- The zoo train ----------------------------------------------------------------------------------------------
        void AddTrain()
        {
            cars = new Transform[plan.CarLengths.Length];
            puffs = new Transform[2];
            for (int i = 0; i < cars.Length; i++)
            {
                bool loco = i == 0 || i == cars.Length - 1;
                cars[i] = loco ? Loco(Liveries[i == 0 ? 0 : 1], out puffs[i == 0 ? 0 : 1]) : Coach(CoachColours[(i - 1) % CoachColours.Length]);
            }
            TrainAt = plan.WestStop;
            PoseTrain();
        }
        /// <summary>A little steam-outline loco facing +z, with a driver on the footplate; <paramref name="puff"/> is its chimney's smoke.</summary>
        Transform Loco(Color livery, out Transform puff)
        {
            var loco = Node("Zoo train loco");
            Box("Frame", new Vector3(0, .035f, 0), new Vector3(.13f, .03f, .28f), Soot, loco);
            for (int side = -1; side <= 1; side += 2)
                for (int k = -1; k <= 1; k++)
                    Box("Wheel", new Vector3(side * .068f, .03f, k * .08f), new Vector3(.02f, .05f, .05f), Soot, loco);
            Box("Boiler", new Vector3(0, .1f, .06f), new Vector3(.1f, .09f, .15f), livery, loco);
            Box("Smokebox", new Vector3(0, .1f, .14f), new Vector3(.1f, .09f, .02f), Soot, loco);
            Box("Chimney", new Vector3(0, .17f, .11f), new Vector3(.035f, .06f, .035f), Soot, loco);
            Box("Dome", new Vector3(0, .155f, .04f), new Vector3(.04f, .03f, .04f), Brass, loco);
            Box("Cab", new Vector3(0, .12f, -.05f), new Vector3(.13f, .13f, .08f), livery, loco);
            Box("Cab roof", new Vector3(0, .19f, -.06f), new Vector3(.15f, .015f, .11f), Soot, loco);
            Box("Buffer beam", new Vector3(0, .05f, .145f), new Vector3(.14f, .025f, .015f), Buffer, loco);
            Seated(loco, "Driver", new Vector3(0, .05f, -.115f), .02f, 0, Rider, CityLife.Shirts[1]);
            puff = Box("Steam puff", new Vector3(0, .2f, .11f), Vector3.one * .05f, Steam, loco).transform;
            return loco;
        }
        /// <summary>An open coach with three benches of passengers under a striped canopy.</summary>
        Transform Coach(Color livery)
        {
            var coach = Node("Zoo train coach");
            Box("Frame", new Vector3(0, .035f, 0), new Vector3(.15f, .03f, .3f), Soot, coach);
            for (int side = -1; side <= 1; side += 2)
                for (int k = -1; k <= 1; k += 2)
                    Box("Wheel", new Vector3(side * .078f, .03f, k * .1f), new Vector3(.02f, .05f, .05f), Soot, coach);
            Box("Floor", new Vector3(0, .06f, 0), new Vector3(.16f, .015f, .3f), livery, coach);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Side", new Vector3(side * .076f, .085f, 0), new Vector3(.01f, .04f, .3f), livery, coach);
                Box("End", new Vector3(0, .085f, side * .145f), new Vector3(.16f, .04f, .01f), livery, coach);
            }
            for (int i = 0; i < 4; i++)
                Box("Canopy post", new Vector3(i % 2 == 0 ? -.07f : .07f, .155f, i < 2 ? -.13f : .13f), new Vector3(.01f, .17f, .01f), Steel, coach);
            for (int k = 0; k < 3; k++)
                Box("Canopy", new Vector3(0, .245f, (k - 1) * .1f), new Vector3(.18f, .012f, .1f), k % 2 == 0 ? livery : Canopy, coach);
            for (int row = -1; row <= 1; row++)
            {
                float z = row * .09f;
                Box("Bench", new Vector3(0, .085f, z), new Vector3(.14f, .012f, .04f), Soot, coach);
                Box("Bench back", new Vector3(0, .11f, z - .022f), new Vector3(.14f, .04f, .008f), Soot, coach);
                for (int side = -1; side <= 1; side += 2)
                {
                    if (Next() < .3f)
                        continue;
                    Seated(coach, "Passenger", new Vector3(side * .035f, .067f, z - .005f), .024f, 0, Next() < .35f ? Child : Rider, Clothes());
                    riders++;
                }
            }
            return coach;
        }
        /// <summary>Runs the train out and back: it speeds up, brakes into the far platform, waits there and sets off again.</summary>
        void RunTrain(float step)
        {
            if (dwell > 0)
            {
                dwell -= step;
                if (dwell <= 0)
                    trainHeading = -trainHeading;
                PoseTrain();
                return;
            }
            float goal = trainHeading > 0 ? plan.EastStop : plan.WestStop, left = Mathf.Abs(goal - TrainAt);
            float braking = trainSpeed * trainSpeed / (2 * TrainAccel);
            trainSpeed = left <= braking ? Mathf.Max(.04f, trainSpeed - TrainAccel * step) : Mathf.Min(TrainTop, trainSpeed + TrainAccel * step);
            float move = Mathf.Min(left, trainSpeed * step);
            TrainAt += move * trainHeading;
            if (left - move <= 1e-4f)
            {
                TrainAt = goal;
                trainSpeed = 0;
                dwell = Dwell;
                TrainStops++;
            }
            PoseTrain();
        }
        /// <summary>Each car on the rails, turned along the chord under it so it follows the bends; the locos face outwards.</summary>
        void PoseTrain()
        {
            for (int i = 0; i < cars.Length; i++)
            {
                float middle = TrainAt + plan.CarOffset(i), half = plan.CarLengths[i] * .35f;
                var at = plan.OnTrack(middle, out var along);
                var chord = plan.OnTrack(middle + half, out _) - plan.OnTrack(middle - half, out _);
                if (chord.sqrMagnitude > 1e-8f)
                    along = chord.normalized;
                cars[i].localPosition = at + Vector3.up * RailTop;
                cars[i].localRotation = Quaternion.LookRotation(i == 0 ? -along : along);
            }
            // Steam from the leading loco while the train runs.
            float life = Mathf.Repeat(clock * 1.6f, 1f);
            for (int i = 0; i < puffs.Length; i++)
            {
                bool leading = trainSpeed > .05f && (i == 0) == (trainHeading < 0);
                puffs[i].localScale = leading ? Vector3.one * (.02f + Mathf.Sin(life * Mathf.PI) * .05f) : Vector3.zero;
                puffs[i].localPosition = new Vector3(0, .2f + life * .18f, .11f - life * .12f);
            }
        }

        // ---- Animation --------------------------------------------------------------------------------------------------
        /// <summary>Moves everyone on by <paramref name="step"/> game seconds. A zoo off the screen is hidden and skipped.</summary>
        public void Animate(float step, float time)
        {
            if (!OnScreen())
                return;
            clock += step;
            foreach (var w in walkers)
                Walk(w, step);
            foreach (var idler in idlers)
            {
                float beat = idler.bounce > 0 ? Mathf.Abs(Mathf.Sin(clock * idler.rate * 3 + idler.phase)) * idler.bounce : 0;
                idler.view.localPosition = idler.at + Vector3.up * beat;
                idler.view.localRotation = Quaternion.Euler(0, idler.yaw + Mathf.Sin(clock * .6f * idler.rate + idler.phase) * idler.sway, 0);
            }
            AnimateAnimals(step);
            RunTrain(step);
        }
        bool OnScreen()
        {
            var view = Camera.main;
            if (!view)
                return true;
            var v = view.WorldToViewportPoint(root.position);
            bool seen = v.z > 0 && v.x > -Margin && v.x < 1 + Margin && v.y > -Margin && v.y < 1 + Margin;
            if (root.gameObject.activeSelf != seen)
                root.gameObject.SetActive(seen);
            return seen;
        }
    }
}
