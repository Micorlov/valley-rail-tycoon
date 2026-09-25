using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// Students on a university quad, so the campus is never empty: walkers on the paths between the gate, the clock tower
    /// and the wings, chatting groups on the lawn, readers on the grass and on the benches. Built by <see cref="CityLife"/>
    /// from the campus emitter, in the lot frame (+z towards the street, origin on the lawn) and scaled by the lot span.
    /// </summary>
    public sealed class CampusLife
    {
        sealed class Walker
        {
            public Transform view;
            public Vector3[] route;
            public float at, length, pace, wait;
            public int heading = 1;
        }
        sealed class Idler
        {
            public Transform view;
            public Vector3 at;
            public float yaw, phase, sway;
            public bool talks;
        }
        const float FigureScale = 1.5f, Lane = .045f, WalkBob = .008f, PathTop = .01f, BenchSeat = .025f;
        // Fractions of the lot span. They follow LandmarkArt (quad ±.28 × ±.25, paths .18 wide, gatehouses from +.33, tower face at −.26)
        // and DetailArt (statue at +.12 and fountain at −.08 on the path, trees at ±.15/±.12, benches at ±.18/+.2), so nobody walks through them.
        static readonly Vector2[] CrossPath = { new Vector2(-.25f, 0), new Vector2(.25f, 0) };
        static readonly Vector2[] GateToTower =
        {
            new Vector2(0, .44f), new Vector2(0, .17f), new Vector2(.045f, .15f), new Vector2(.045f, .09f), new Vector2(0, .05f), new Vector2(0, 0),
            new Vector2(.07f, -.02f), new Vector2(.07f, -.14f), new Vector2(0, -.18f), new Vector2(0, -.25f),
        };
        static readonly Vector2[] Groups = { new Vector2(-.19f, -.07f), new Vector2(.2f, .07f), new Vector2(-.09f, .2f) };
        static readonly Vector2[] Readers = { new Vector2(-.2f, .15f), new Vector2(.08f, .21f), new Vector2(-.08f, -.2f) };
        static readonly Vector2 Picnic = new Vector2(.19f, -.19f);
        static readonly Vector2[] Benches = { new Vector2(-.18f, .2f), new Vector2(.18f, .2f) };
        static readonly Color[] Hoodies = { new Color(.55f, .14f, .2f), new Color(.17f, .22f, .42f), new Color(.6f, .6f, .62f), new Color(.2f, .45f, .35f) };
        static readonly Color[] Covers = { new Color(.8f, .2f, .2f), new Color(.2f, .35f, .75f), new Color(.95f, .92f, .85f), new Color(.2f, .5f, .3f) };
        static readonly Color Blanket = new Color(.85f, .3f, .3f), Backpack = new Color(.22f, .24f, .28f);
        readonly List<Walker> walkers = new List<Walker>();
        readonly List<Idler> idlers = new List<Idler>();
        readonly System.Random random;
        readonly WorldView world;
        readonly Transform root;
        readonly float span;

        /// <summary>Everyone on this campus, walking or not.</summary>
        public int People => walkers.Count + idlers.Count;
        public int Walking => walkers.Count;

        public CampusLife(WorldView world, Transform parent, WorldView.Emitter spot)
        {
            this.world = world;
            span = spot.size;
            // Seeded by the lot, so each campus looks the same after every redraw and never touches the town's own randomness.
            random = new System.Random(Mathf.RoundToInt(spot.at.x) * 7919 + Mathf.RoundToInt(spot.at.z) * 104729);
            root = new GameObject("Campus").transform;
            root.SetParent(parent, false);
            root.localPosition = spot.at;
            root.localRotation = spot.turn;
            for (int i = 0; i < 3; i++)
                AddWalker(CrossPath, false, i / 3f);
            for (int i = 0; i < 4; i++)
                AddWalker(GateToTower, i % 2 == 1, (i / 2 + (i % 2) * .5f) / 2f);
            foreach (var g in Groups)
                AddGroup(Local(g), 3 + random.Next(2));
            foreach (var r in Readers)
                AddSitter(Local(r), 0, Toward(Local(r), Vector3.zero) + Spread(40), random.Next(3) > 0);
            var picnic = Local(Picnic);
            world.Box("Picnic blanket", picnic + new Vector3(0, .003f, 0), new Vector3(.24f, .006f, .18f), Blanket, root);
            AddSitter(picnic + new Vector3(-.06f, .006f, 0), 0, 90 + Spread(15), false);
            AddSitter(picnic + new Vector3(.06f, .006f, 0), 0, -90 + Spread(15), true);
            foreach (var b in Benches)
                AddSitter(Local(b), BenchSeat, 0, random.Next(2) == 0);
        }

        Vector3 Local(Vector2 fraction) => new Vector3(fraction.x * span, 0, fraction.y * span);
        float Spread(float degrees) => ((float)random.NextDouble() * 2 - 1) * degrees;
        static float Toward(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
        Color Clothes() => random.Next(2) == 0 ? Hoodies[random.Next(Hoodies.Length)] : CityLife.Shirts[random.Next(CityLife.Shirts.Length)];

        void AddWalker(Vector2[] path, bool mirrored, float start)
        {
            var route = new Vector3[path.Length];
            float length = 0;
            for (int i = 0; i < path.Length; i++)
            {
                route[i] = Local(mirrored ? new Vector2(-path[i].x, path[i].y) : path[i]) + Vector3.up * PathTop;
                if (i > 0)
                    length += Vector3.Distance(route[i - 1], route[i]);
            }
            var view = CityLife.Figure(world, root, "Student", Clothes(), FigureScale);
            world.Box("Backpack", new Vector3(0, .09f, -.022f) * FigureScale, new Vector3(.04f, .045f, .015f) * FigureScale, Backpack, view);
            var walker = new Walker { view = view, route = route, length = length, at = start * length, pace = .22f + (float)random.NextDouble() * .1f, heading = random.Next(2) * 2 - 1 };
            walkers.Add(walker);
            Pose(walker);
        }
        void AddGroup(Vector3 centre, int members)
        {
            float radius = .06f + .012f * members, turn = Spread(180);
            for (int i = 0; i < members; i++)
            {
                float angle = (turn + i * 360f / members + Spread(15)) * Mathf.Deg2Rad;
                var at = centre + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                var view = CityLife.Figure(world, root, "Student", Clothes(), FigureScale);
                idlers.Add(new Idler { view = view, at = at, yaw = Toward(at, centre), phase = (float)random.NextDouble() * 6, sway = 10, talks = true });
            }
        }
        void AddSitter(Vector3 at, float seat, float yaw, bool reading)
        {
            float s = FigureScale;
            var view = new GameObject("Seated student").transform;
            view.SetParent(root, false);
            world.Box("Legs", new Vector3(0, seat + .0125f * s, .035f * s), new Vector3(.035f, .025f, .07f) * s, CityLife.Trousers, view);
            if (seat > 0)
                world.Box("Shins", new Vector3(0, seat / 2, .07f * s), new Vector3(.03f * s, seat, .02f * s), CityLife.Trousers, view);
            world.Box("Body", new Vector3(0, seat + .055f * s, 0), new Vector3(.05f, .06f, .03f) * s, Clothes(), view);
            world.Box("Head", new Vector3(0, seat + .1f * s, 0), Vector3.one * .03f * s, CityLife.Skin, view);
            if (reading)
                world.Box("Book", new Vector3(0, seat + .06f * s, .032f * s), new Vector3(.045f, .032f, .006f) * s, Covers[random.Next(Covers.Length)], view, Quaternion.Euler(-35, 0, 0));
            idlers.Add(new Idler { view = view, at = at, yaw = yaw, phase = (float)random.NextDouble() * 6, sway = 4 });
        }

        /// <summary>Moves the students on by <paramref name="step"/> game seconds; <paramref name="time"/> drives the idle sway.</summary>
        public void Animate(float step, float time)
        {
            foreach (var w in walkers)
            {
                if (w.wait > 0)
                {
                    // Paused at a doorway or the gate, then heads back the other way.
                    w.wait -= step;
                    if (w.wait <= 0)
                        w.heading = -w.heading;
                }
                else
                {
                    w.at += step * w.pace * w.heading;
                    if (w.at >= w.length || w.at <= 0)
                    {
                        w.at = Mathf.Clamp(w.at, 0, w.length);
                        w.wait = 1 + (float)random.NextDouble() * 2;
                    }
                }
                Pose(w);
            }
            foreach (var idler in idlers)
            {
                // Groups take turns talking: the speaker nods while the others turn a little towards them.
                float bob = idler.talks && Mathf.Repeat(time * .25f + idler.phase, 3) < 1 ? Mathf.Abs(Mathf.Sin(time * 7 + idler.phase)) * .006f : 0;
                idler.view.localPosition = idler.at + Vector3.up * bob;
                idler.view.localRotation = Quaternion.Euler(0, idler.yaw + Mathf.Sin(time * .6f + idler.phase) * idler.sway, 0);
            }
        }
        /// <summary>Places a walker along its route, keeping to its right so people passing each other do not overlap.</summary>
        static void Pose(Walker w)
        {
            var route = w.route;
            float left = w.at;
            int i = 1;
            while (i < route.Length - 1 && left > Vector3.Distance(route[i - 1], route[i]))
            {
                left -= Vector3.Distance(route[i - 1], route[i]);
                i++;
            }
            var along = route[i] - route[i - 1];
            float leg = along.magnitude;
            var point = route[i - 1] + (leg > 0 ? along * Mathf.Clamp01(left / leg) : Vector3.zero);
            var facing = (leg > 0 ? along / leg : Vector3.forward) * w.heading;
            var right = new Vector3(facing.z, 0, -facing.x);
            float bob = w.wait > 0 ? 0 : Mathf.Abs(Mathf.Sin(w.at * 40)) * WalkBob;
            w.view.localPosition = point + right * Lane + Vector3.up * bob;
            w.view.localRotation = Quaternion.LookRotation(facing);
        }
    }
}
