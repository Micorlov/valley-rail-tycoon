using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// People in a city park, so it is never empty: strollers (some with a dog or a pram) and joggers on the loop, people
    /// crossing by the fountain, children on the swings, slide, seesaw and roundabout with parents watching, picnics, a
    /// kickabout and a kite on the lawns, a band playing to an audience, café customers, bench sitters, ducks on the pond and
    /// rowing boats on the central park's lake. Built by <see cref="CityLife"/> from the park emitter (size = cells per side);
    /// everything sits in the lot frame and follows the <see cref="ParkLayout"/> plan, so nobody walks through a tree or a
    /// table. A park off the screen sleeps.
    /// </summary>
    public sealed class ParkLife
    {
        sealed class Walker
        {
            public Transform view, companion;
            public Vector3[] route;
            public float[] speeds;
            public bool loop;
            public float at, length, pace, wait, bob, stride = 40, lane = Lane, beside, ahead;
            public int heading = 1, slide = -1;
            public Vector3 facing = Vector3.forward;
        }
        sealed class Idler
        {
            public Transform view;
            public Vector3 at;
            public float yaw, phase, sway, bounce, rate = 1;
        }
        sealed class Mover
        {
            public Transform view;
            public Transform[] oars;
            public float angle, rate, fraction, phase;
        }
        const float Adult = 1.6f, Child = 1.15f, Lane = .04f, WalkBob = .008f, JogBob = .02f, Margin = .35f;
        static readonly Color[] Sportswear = { new Color(.95f, .2f, .45f), new Color(.1f, .7f, .8f), new Color(.98f, .55f, .1f), new Color(.45f, .85f, .2f) };
        static readonly Color[] Fabrics = { new Color(.86f, .28f, .26f), new Color(.24f, .5f, .82f), new Color(.96f, .82f, .3f), new Color(.6f, .4f, .8f) };
        static readonly Color[] Coats = { new Color(.55f, .38f, .22f), new Color(.12f, .12f, .12f), new Color(.93f, .9f, .84f), new Color(.85f, .62f, .3f) };
        static readonly Color[] Hulls = { new Color(.86f, .22f, .18f), new Color(.2f, .42f, .78f), new Color(.95f, .94f, .9f) };
        static readonly Color Chain = new Color(.7f, .72f, .75f), SeatRed = new Color(.86f, .24f, .2f), Brass = new Color(.95f, .76f, .3f),
            DuckWhite = new Color(.96f, .95f, .9f), DuckBrown = new Color(.55f, .42f, .3f), DuckGreen = new Color(.15f, .45f, .25f), Beak = new Color(.98f, .6f, .12f),
            Wicker = new Color(.72f, .55f, .32f), Ball = new Color(.97f, .97f, .97f), Pram = new Color(.2f, .28f, .45f), Oar = new Color(.6f, .45f, .28f),
            DiscYellow = new Color(.98f, .8f, .2f), KiteString = new Color(.95f, .95f, .95f);
        readonly List<Walker> walkers = new List<Walker>();
        readonly List<Idler> idlers = new List<Idler>();
        readonly List<Transform> swings = new List<Transform>();
        readonly List<Mover> ducks = new List<Mover>(), boats = new List<Mover>();
        readonly System.Random random;
        readonly WorldView world;
        readonly ParkLayout plan;
        readonly Transform root;
        Transform seesaw, roundabout, ball, kite, line;
        Transform[] kickers;
        float[] kickerYaw;
        float clock;
        int children, sliders;

        public int Size => plan.Size;
        /// <summary>Everyone in the park: walking, sitting, playing, rowing or playing in the band.</summary>
        public int People => walkers.Count + idlers.Count + children + 2 * boats.Count;
        public int Walkers => walkers.Count;
        public int Joggers { get; private set; }
        public int Dogs { get; private set; }
        /// <summary>Children on the swings, the slide, the seesaw and the roundabout, or kicking a ball.</summary>
        public int Playing => children + sliders;
        public int Musicians { get; private set; }
        public int Listeners { get; private set; }
        public int Ducks => ducks.Count;
        public int Boats => boats.Count;
        public Transform Root => root;

        public ParkLife(WorldView world, Transform parent, WorldView.Emitter spot)
        {
            this.world = world;
            plan = ParkLayout.For(Mathf.RoundToInt(spot.size));
            // Seeded by the lot, so each park looks the same after every redraw and never touches the town's own randomness.
            random = new System.Random(Mathf.RoundToInt(spot.at.x) * 7919 + Mathf.RoundToInt(spot.at.z) * 104729 + 31);
            root = new GameObject("Park").transform;
            root.SetParent(parent, false);
            root.localPosition = spot.at;
            root.localRotation = spot.turn;
            AddWalks();
            AddPlayground();
            AddLawns();
            if (plan.HasBandstand)
                AddBand();
            if (plan.HasCafe)
                AddCafe();
            AddBenchSitters();
            AddWater();
        }

        float Next() => (float)random.NextDouble();
        float Spread(float degrees) => (Next() * 2 - 1) * degrees;
        static float Toward(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
        Color Clothes() => CityLife.Shirts[random.Next(CityLife.Shirts.Length)];
        Transform Standing(string name, float scale, Color shirt) => CityLife.Figure(world, root, name, shirt, scale);

        /// <summary>A seated person on a seat <paramref name="seat"/> above <paramref name="at"/> (0 = on the grass), looking along <paramref name="yaw"/>.</summary>
        Transform Seated(Transform parent, string name, Vector3 at, float seat, float yaw, float s, Color shirt)
        {
            var view = new GameObject(name).transform;
            view.SetParent(parent, false);
            view.localPosition = at;
            view.localRotation = Quaternion.Euler(0, yaw, 0);
            world.Box("Legs", new Vector3(0, seat + .0125f * s, .035f * s), new Vector3(.035f, .025f, .07f) * s, CityLife.Trousers, view);
            if (seat > 0)
                world.Box("Shins", new Vector3(0, seat / 2, .07f * s), new Vector3(.03f * s, seat, .02f * s), CityLife.Trousers, view);
            world.Box("Body", new Vector3(0, seat + .055f * s, 0), new Vector3(.05f, .06f, .03f) * s, shirt, view);
            world.Box("Head", new Vector3(0, seat + .1f * s, 0), Vector3.one * .03f * s, CityLife.Skin, view);
            return view;
        }
        Idler Idle(Transform view, Vector3 at, float yaw, float sway, float bounce = 0, float rate = 1)
        {
            var idler = new Idler { view = view, at = at, yaw = yaw, phase = Next() * 6, sway = sway, bounce = bounce, rate = rate };
            idlers.Add(idler);
            return idler;
        }

        // ---- Walks ------------------------------------------------------------------------------------------------------
        void AddWalks()
        {
            int strollers = plan.Size, joggers = plan.Size - 2, crossers = Mathf.Min(plan.Crossings.Length, plan.Size + 1);
            for (int i = 0; i < strollers; i++)
            {
                var w = AddWalker(plan.LoopRoute, true, (i + .4f * Next()) / strollers, Next() < .2f ? Child : Adult, Clothes(), .16f + .06f * Next());
                w.heading = i % 2 == 0 ? 1 : -1;
                if (i % 3 == 0)
                    AddDog(w);
                else if (i % 4 == 1)
                    AddPram(w);
            }
            for (int i = 0; i < joggers; i++)
            {
                var w = AddWalker(plan.LoopRoute, true, (i + .5f) / joggers, Adult, Sportswear[random.Next(Sportswear.Length)], .5f + .12f * Next());
                w.heading = i % 2 == 0 ? -1 : 1;
                w.bob = JogBob;
                w.stride = 28;
                Joggers++;
            }
            for (int i = 0; i < crossers; i++)
                AddWalker(plan.Crossings[i], false, Next(), Next() < .25f ? Child : Adult, Clothes(), .18f + .06f * Next());
        }
        Walker AddWalker(Vector3[] path, bool loop, float start, float scale, Color shirt, float pace, float lift = -1)
        {
            var route = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++)
                route[i] = path[i] + Vector3.up * (lift >= 0 ? lift : plan.PathTop);
            float length = 0;
            for (int i = 0; i < route.Length - (loop ? 0 : 1); i++)
                length += Vector3.Distance(route[i], route[(i + 1) % route.Length]);
            var w = new Walker { view = Standing(scale < Adult ? "Park child" : "Park walker", scale, shirt), route = route, loop = loop, length = length, at = start * length, pace = pace, bob = WalkBob };
            walkers.Add(w);
            Pose(w);
            return w;
        }
        void AddDog(Walker w)
        {
            var dog = new GameObject("Dog").transform;
            dog.SetParent(root, false);
            var coat = Coats[random.Next(Coats.Length)];
            world.Box("Dog body", new Vector3(0, .04f, 0), new Vector3(.03f, .03f, .07f), coat, dog);
            world.Box("Dog head", new Vector3(0, .065f, .045f), new Vector3(.028f, .028f, .03f), coat, dog);
            for (int i = 0; i < 4; i++)
                world.Box("Dog leg", new Vector3(i % 2 == 0 ? -.01f : .01f, .0125f, i < 2 ? -.025f : .025f), new Vector3(.01f, .025f, .01f), coat, dog);
            world.Box("Dog tail", new Vector3(0, .06f, -.04f), new Vector3(.008f, .025f, .008f), coat, dog, Quaternion.Euler(-30, 0, 0));
            w.companion = dog;
            w.ahead = .09f;
            w.beside = .045f;
            Dogs++;
        }
        void AddPram(Walker w)
        {
            var pram = new GameObject("Pram").transform;
            pram.SetParent(root, false);
            world.Box("Pram body", new Vector3(0, .05f, 0), new Vector3(.045f, .04f, .07f), Pram, pram);
            world.Box("Pram hood", new Vector3(0, .08f, -.02f), new Vector3(.045f, .03f, .03f), Pram, pram);
            for (int i = 0; i < 4; i++)
                world.Box("Pram wheel", new Vector3(i % 2 == 0 ? -.025f : .025f, .012f, i < 2 ? -.025f : .025f), new Vector3(.008f, .024f, .024f), CityLife.Trousers, pram);
            world.Box("Pram handle", new Vector3(0, .085f, -.05f), new Vector3(.04f, .008f, .008f), Chain, pram);
            w.companion = pram;
            w.ahead = .1f;
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
        /// <summary>Places a walker (and its dog or pram) on its route, keeping to its right so people passing do not overlap.</summary>
        static void Pose(Walker w)
        {
            Locate(w, w.at, out var point, out var along, out int segment);
            var flat = new Vector3(along.x, 0, along.z) * w.heading;
            if (flat.sqrMagnitude > 1e-6f)
                w.facing = flat.normalized;
            var right = new Vector3(w.facing.z, 0, -w.facing.x);
            float bob = w.wait > 0 ? 0 : Mathf.Abs(Mathf.Sin(w.at * w.stride)) * w.bob;
            var turn = Quaternion.LookRotation(w.facing);
            w.view.localPosition = point + right * w.lane + Vector3.up * bob;
            // Sliding down: sit back.
            w.view.localRotation = segment == w.slide ? turn * Quaternion.Euler(-35, 0, 0) : turn;
            if (!w.companion)
                return;
            Locate(w, w.at + w.heading * w.ahead, out var lead, out _, out _);
            w.companion.localPosition = lead + right * (w.lane + w.beside) + Vector3.up * bob * .5f;
            w.companion.localRotation = turn;
        }

        // ---- Playground -------------------------------------------------------------------------------------------------
        void AddPlayground()
        {
            var P = plan;
            float drop = P.SwingBar - P.SwingSeat;
            for (int i = 0; i < P.Swings; i++)
            {
                var pivot = new GameObject("Swing").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = P.SwingPivot(i);
                for (int side = -1; side <= 1; side += 2)
                    world.Box("Chain", new Vector3(side * .028f, -drop / 2, 0), new Vector3(.006f, drop, .006f), Chain, pivot);
                world.Box("Swing seat", new Vector3(0, -drop, 0), new Vector3(.07f, .012f, .04f), SeatRed, pivot);
                // One swing in three stands empty and barely moves.
                if (i % 3 != 2)
                {
                    Seated(pivot, "Swinging child", new Vector3(0, -drop + .006f, -.02f), 0, 0, Child, Clothes());
                    children++;
                }
                swings.Add(pivot);
            }
            AddSlider(0);
            if (P.Size >= 5)
                AddSlider(.5f);
            if (P.HasSeesaw)
            {
                seesaw = new GameObject("Seesaw").transform;
                seesaw.SetParent(root, false);
                seesaw.localPosition = P.Seesaw + Vector3.up * .08f;
                world.Box("Seesaw beam", Vector3.zero, new Vector3(2 * P.SeesawHalf, .014f, .035f), SeatRed, seesaw);
                for (int side = -1; side <= 1; side += 2)
                {
                    world.Box("Handle", new Vector3(side * (P.SeesawHalf - .06f), .025f, 0), new Vector3(.008f, .04f, .03f), Chain, seesaw);
                    Seated(seesaw, "Seesaw child", new Vector3(side * (P.SeesawHalf - .025f), .007f, 0), 0, -side * 90, Child, Clothes());
                    children++;
                }
            }
            if (P.HasRoundabout)
            {
                roundabout = new GameObject("Roundabout").transform;
                roundabout.SetParent(root, false);
                roundabout.localPosition = P.Roundabout + Vector3.up * .05f;
                float d = 2 * P.RoundaboutRadius;
                world.Box("Roundabout deck", Vector3.zero, new Vector3(d, .02f, d) * .92f, DiscYellow, roundabout);
                world.Box("Roundabout deck", Vector3.zero, new Vector3(d, .02f, d) * .92f, DiscYellow, roundabout, Quaternion.Euler(0, 45, 0));
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 90 * Mathf.Deg2Rad;
                    world.Box("Roundabout rail", new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * .06f + Vector3.up * .04f, new Vector3(.01f, .07f, .01f), SeatRed, roundabout);
                }
                for (int i = 0; i < 3; i++)
                {
                    float a = (i * 120 + 45) * Mathf.Deg2Rad;
                    var at = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * .085f + Vector3.up * .01f;
                    var kid = CityLife.Figure(world, roundabout, "Roundabout child", Clothes(), Child);
                    kid.localPosition = at;
                    kid.localRotation = Quaternion.Euler(0, i * 120 + 45 + 90, 0);
                    children++;
                }
            }
            // Parents watch from the playground's edge nearest the cross path.
            var pad = P.Playground;
            float edge = pad.center.y < 0 ? pad.yMax + .07f : pad.yMin - .07f;
            for (int i = 0; i < (P.Size >= 4 ? 2 : 1); i++)
            {
                var at = new Vector3(pad.center.x + (i == 0 ? -.25f : .25f) * pad.width, plan.LawnTop, edge);
                var parent = Standing("Parent", Adult, Clothes());
                Idle(parent, at, Toward(at, new Vector3(pad.center.x, 0, pad.center.y)), 12);
            }
        }
        /// <summary>A child going round the slide: off the end, back along its side, up the ladder and down the chute.</summary>
        void AddSlider(float start)
        {
            var P = plan;
            Vector3 end = P.SlideEnd + Vector3.up * .01f, top = P.SlideTop, ladder = P.SlideLadder;
            var route = new[]
            {
                end, end + new Vector3(.06f, -.01f, .1f), new Vector3(ladder.x - .06f, 0, ladder.z + .1f), ladder + new Vector3(-.03f, 0, 0),
                new Vector3(top.x - .05f, top.y - P.PadTop, top.z), top - Vector3.up * P.PadTop,
            };
            var w = AddWalker(route, true, start, Child, Clothes(), .2f, P.PadTop);
            w.speeds = new[] { 1f, 1f, 1f, .45f, .6f, 2.6f };
            w.slide = route.Length - 1;
            w.lane = 0;
            w.view.name = "Slide child";
            sliders++;
        }

        // ---- Lawns ------------------------------------------------------------------------------------------------------
        void AddLawns()
        {
            float lawn = plan.LawnTop;
            foreach (var spot in plan.Picnics)
            {
                var at = spot + Vector3.up * lawn;
                float turn = Spread(40);
                world.Box("Picnic blanket", at + Vector3.up * .003f, new Vector3(.26f, .006f, .2f), Fabrics[random.Next(Fabrics.Length)], root, Quaternion.Euler(0, turn, 0));
                world.Box("Picnic basket", at + new Vector3(.03f, .025f, 0), new Vector3(.05f, .04f, .035f), Wicker, root, Quaternion.Euler(0, turn, 0));
                int sitters = 2 + random.Next(2);
                for (int i = 0; i < sitters; i++)
                {
                    float a = (turn + 90 + i * 360f / sitters) * Mathf.Deg2Rad;
                    var seat = at + new Vector3(Mathf.Sin(a) * .09f, .006f, Mathf.Cos(a) * .07f);
                    Idle(Seated(root, "Picnicker", seat, 0, 0, i == 2 ? Child : Adult, Clothes()), seat, Toward(seat, at), 5, .004f, 1.5f);
                }
            }
            if (plan.Kickabout.Length == 2)
            {
                Vector3 a = plan.Kickabout[0] + Vector3.up * lawn, b = plan.Kickabout[1] + Vector3.up * lawn;
                kickers = new[] { Standing("Kicker", Child, Clothes()), Standing("Kicker", Child, Clothes()) };
                kickers[0].localPosition = a;
                kickers[1].localPosition = b;
                kickerYaw = new[] { Toward(a, b), Toward(b, a) };
                ball = world.Box("Ball", a, Vector3.one * .03f, Ball, root).transform;
                children += 2;
            }
            if (plan.HasKite)
            {
                var at = plan.Kite + Vector3.up * lawn;
                var flyer = Standing("Kite flyer", Adult, Clothes());
                var outward = new Vector3(at.x, 0, at.z).normalized;
                Idle(flyer, at, Toward(Vector3.zero, outward), 6);
                kite = new GameObject("Kite").transform;
                kite.SetParent(root, false);
                var colour = Fabrics[random.Next(Fabrics.Length)];
                world.Box("Kite sail", Vector3.zero, new Vector3(.09f, .09f, .008f), colour, kite, Quaternion.Euler(0, 0, 45));
                for (int i = 1; i <= 3; i++)
                    world.Box("Kite tail", new Vector3(0, -.06f * i - .02f, 0), new Vector3(.02f, .015f, .005f), Fabrics[(i + 1) % Fabrics.Length], kite);
                line = world.Box("Kite string", at, new Vector3(.004f, .004f, .1f), KiteString, root).transform;
            }
        }

        // ---- Bandstand and café -----------------------------------------------------------------------------------------
        void AddBand()
        {
            var P = plan;
            float facing = Toward(Vector3.zero, P.StageFacing);
            int players = P.Size >= 5 ? 4 : 3;
            for (int i = 0; i < players; i++)
            {
                float a = (facing + 180 + (i - (players - 1) / 2f) * 55f) * Mathf.Deg2Rad;
                var at = P.Bandstand + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * P.BandstandRadius * .45f + Vector3.up * P.StageTop;
                var player = Standing("Musician", Adult, i % 2 == 0 ? new Color(.7f, .12f, .14f) : new Color(.12f, .16f, .32f));
                bool tuba = i == 1;
                world.Box(tuba ? "Tuba" : "Trumpet", new Vector3(0, .1f, .04f) * Adult, tuba ? new Vector3(.05f, .06f, .04f) * Adult : new Vector3(.012f, .012f, .05f) * Adult, Brass, player);
                Idle(player, at, facing + Spread(15), 8, .01f, 2.2f);
                Musicians++;
            }
            foreach (var seat in P.Audience)
            {
                var at = seat + Vector3.up * P.LawnTop;
                bool stands = Next() < .15f;
                var view = stands ? Standing("Listener", Adult, Clothes()) : Seated(root, "Listener", at, 0, 0, Next() < .25f ? Child : Adult, Clothes());
                Idle(view, at, Toward(at, P.Bandstand) + Spread(10), 7, .003f, 2.2f);
                Listeners++;
            }
        }
        void AddCafe()
        {
            var P = plan;
            foreach (var table in P.Tables)
                for (int side = -1; side <= 1; side += 2)
                {
                    if (Next() < .3f)
                        continue;
                    var at = table + new Vector3(side * .085f, 0, 0);
                    Idle(Seated(root, "Café customer", at, .036f, 0, Adult, Clothes()), at, side > 0 ? -90 : 90, 6, .002f, 1.3f);
                }
            for (int i = 0; i < 2; i++)
            {
                var at = P.Hatch + P.HatchFacing * (.1f + i * .11f) + Vector3.up * P.LawnTop;
                Idle(Standing("Queueing", i == 1 && Next() < .5f ? Child : Adult, Clothes()), at, Toward(P.HatchFacing, Vector3.zero), 10);
            }
        }
        void AddBenchSitters()
        {
            foreach (var bench in plan.Benches)
            {
                if (Next() < .35f)
                    continue;
                var right = new Vector3(bench.facing.z, 0, -bench.facing.x);
                int sitters = Next() < .4f ? 2 : 1;
                for (int i = 0; i < sitters; i++)
                {
                    var at = bench.at + right * (sitters == 1 ? Spread(.03f) : (i == 0 ? -.045f : .045f)) + Vector3.up * .002f;
                    Idle(Seated(root, "Bench sitter", at, .055f, 0, Adult, Clothes()), at, Toward(Vector3.zero, bench.facing), 5, 0, .8f);
                }
            }
        }

        // ---- Water ------------------------------------------------------------------------------------------------------
        void AddWater()
        {
            var P = plan;
            for (int i = 0; i < P.Ducks; i++)
            {
                var duck = new GameObject("Duck").transform;
                duck.SetParent(root, false);
                bool mallard = i % 2 == 0;
                world.Box("Duck body", new Vector3(0, .012f, 0), new Vector3(.035f, .022f, .055f), mallard ? DuckBrown : DuckWhite, duck);
                world.Box("Duck head", new Vector3(0, .032f, .024f), Vector3.one * .02f, mallard ? DuckGreen : DuckWhite, duck);
                world.Box("Duck beak", new Vector3(0, .03f, .04f), new Vector3(.01f, .006f, .014f), Beak, duck);
                float fraction = P.Lake ? .84f + .08f * (i % 2) : .3f + .45f * i / Mathf.Max(1, P.Ducks - 1);
                ducks.Add(new Mover { view = duck, angle = Next() * 360, rate = (i % 3 == 0 ? -1 : 1) * (10 + 8 * Next()), fraction = fraction, phase = Next() * 6 });
            }
            for (int i = 0; i < P.Boats; i++)
            {
                var boat = new GameObject("Rowing boat").transform;
                boat.SetParent(root, false);
                var hull = Hulls[i % Hulls.Length];
                world.Box("Hull", new Vector3(0, .012f, 0), new Vector3(.09f, .03f, .2f), hull, boat);
                world.Box("Gunwale", new Vector3(0, .03f, 0), new Vector3(.1f, .008f, .21f), new Color(.95f, .94f, .9f), boat);
                world.Box("Thwart", new Vector3(0, .026f, 0), new Vector3(.08f, .008f, .03f), Oar, boat);
                // The rower faces the stern and pulls; a passenger sits in the stern.
                Seated(boat, "Rower", new Vector3(0, .02f, .01f), .012f, 180, Adult, Clothes());
                Seated(boat, "Passenger", new Vector3(0, .02f, -.06f), .012f, 0, Adult, Clothes());
                var oars = new Transform[2];
                for (int side = 0; side < 2; side++)
                {
                    var oarlock = new GameObject("Oarlock").transform;
                    oarlock.SetParent(boat, false);
                    oarlock.localPosition = new Vector3(side == 0 ? -.05f : .05f, .035f, .01f);
                    world.Box("Oar", new Vector3((side == 0 ? -1 : 1) * .06f, -.01f, 0), new Vector3(.12f, .008f, .014f), Oar, oarlock, Quaternion.Euler(0, 0, side == 0 ? 12 : -12));
                    oars[side] = oarlock;
                }
                boats.Add(new Mover { view = boat, oars = oars, angle = i * 360f / P.Boats, rate = 7, fraction = .62f, phase = Next() * 6 });
            }
        }
        /// <summary>A duck or boat on its ellipse round the water, facing the way it swims.</summary>
        void Swim(Mover m, float bob)
        {
            var at = plan.OnPond(m.angle, m.fraction, plan.WaterTop + bob);
            var ahead = plan.OnPond(m.angle + Mathf.Sign(m.rate) * 2, m.fraction, plan.WaterTop);
            m.view.localPosition = at;
            var along = ahead - at;
            along.y = 0;
            if (along.sqrMagnitude > 1e-8f)
                m.view.localRotation = Quaternion.LookRotation(along);
        }

        // ---- Animation --------------------------------------------------------------------------------------------------
        /// <summary>Moves everyone on by <paramref name="step"/> game seconds. Parks off the screen are hidden and skipped.</summary>
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
            for (int i = 0; i < swings.Count; i++)
            {
                float reach = i % 3 == 2 ? 4 : 28 + 8 * (i % 2);
                swings[i].localRotation = Quaternion.Euler(Mathf.Sin(clock * 2.3f + i * 1.7f) * reach, 0, 0);
            }
            if (seesaw)
                seesaw.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(clock * 1.8f) * 13);
            if (roundabout)
                roundabout.localRotation = Quaternion.Euler(0, clock * 70, 0);
            if (ball)
                Kick();
            if (kite)
                Fly();
            foreach (var duck in ducks)
            {
                duck.angle += step * duck.rate;
                Swim(duck, Mathf.Sin(clock * 3 + duck.phase) * .002f);
            }
            foreach (var boat in boats)
            {
                boat.angle += step * boat.rate;
                Swim(boat, 0);
                float stroke = clock * 3 + boat.phase;
                for (int side = 0; side < 2; side++)
                    boat.oars[side].localRotation = Quaternion.Euler(0, (side == 0 ? 1 : -1) * Mathf.Sin(stroke) * 30, (side == 0 ? 1 : -1) * Mathf.Max(0, Mathf.Cos(stroke)) * 12);
            }
        }
        void Walk(Walker w, float step)
        {
            if (w.wait > 0)
            {
                // Paused at a gate, then heads back the other way.
                w.wait -= step;
                if (w.wait <= 0)
                    w.heading = -w.heading;
            }
            else
            {
                Locate(w, w.at, out _, out _, out int segment);
                float factor = w.speeds == null ? 1 : w.speeds[segment];
                w.at += step * w.pace * factor * w.heading;
                if (w.loop)
                    w.at = Mathf.Repeat(w.at, w.length);
                else if (w.at >= w.length || w.at <= 0)
                {
                    w.at = Mathf.Clamp(w.at, 0, w.length);
                    w.wait = 1 + Next() * 2;
                }
            }
            Pose(w);
        }
        /// <summary>The ball flies from one child to the other in a low arc; each kicker leans into the kick.</summary>
        void Kick()
        {
            const float Flight = 1.3f;
            Vector3 a = kickers[0].localPosition, b = kickers[1].localPosition;
            float t = Mathf.Repeat(clock / Flight, 2);
            bool outward = t < 1;
            float u = outward ? t : t - 1;
            Vector3 from = outward ? a : b, to = outward ? b : a;
            ball.localPosition = Vector3.Lerp(from, to, u) + Vector3.up * (.015f + Mathf.Sin(u * Mathf.PI) * .12f);
            for (int i = 0; i < 2; i++)
            {
                bool kicking = (i == 0) == outward && u < .15f;
                kickers[i].localRotation = Quaternion.Euler(kicking ? 12 : 0, kickerYaw[i], 0);
            }
        }
        /// <summary>The kite dances downwind of its flyer, its string running from the flyer's hand.</summary>
        void Fly()
        {
            var at = plan.Kite + Vector3.up * plan.LawnTop;
            var outward = new Vector3(at.x, 0, at.z).normalized;
            var across = new Vector3(outward.z, 0, -outward.x);
            var hand = at + outward * .03f + Vector3.up * .15f;
            var sail = at + outward * .45f + Vector3.up * (.8f + Mathf.Sin(clock * 1.3f) * .06f) + across * Mathf.Sin(clock * .7f) * .14f;
            kite.localPosition = sail;
            kite.localRotation = Quaternion.LookRotation(outward) * Quaternion.Euler(-20, 0, Mathf.Sin(clock * 1.1f) * 20);
            var span = sail - hand;
            line.localPosition = (sail + hand) / 2;
            line.localRotation = Quaternion.LookRotation(span);
            line.localScale = new Vector3(.004f, .004f, span.magnitude);
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
