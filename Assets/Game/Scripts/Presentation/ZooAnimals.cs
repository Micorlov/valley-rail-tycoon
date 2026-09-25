using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// The zoo's animals, built from boxes in each pen's own spot of <see cref="ZooLayout.Plan"/>: elephants (one a calf)
    /// ambling round their yard with swinging trunks and flapping ears, giraffes browsing the acacias or taking leaves at the
    /// feeding deck, zebras grazing, a lion pacing past the visitors while the lionesses doze (one on the rock) and a cub
    /// plays, penguins waddling, swimming and diving off their rock, flamingos wading and standing on one leg, and monkeys
    /// climbing, swinging on the ropes and scampering round their island. Roaming animals turn on the spot, then walk straight
    /// to a new spot that keeps them clear of the pen's rocks and trees and of each other.
    /// </summary>
    public sealed partial class ZooLife
    {
        public enum Species { Elephant, Giraffe, Zebra, Lion, Penguin, Flamingo, Monkey }
        /// <summary>An animal and the pen it lives in.</summary>
        public struct Resident
        {
            public Transform view;
            public Species species;
            public ZooLayout.Exhibit pen;
        }
        sealed class Beast
        {
            public Transform view, neck, trunk, tail;
            public Transform[] legs = new Transform[0], ears = new Transform[0];
            public Species species;
            public ZooLayout.Exhibit pen;
            public Rect yard;
            public bool oval;
            public Vector3[] beat;
            public int beatIndex;
            public Vector3 at, target, face;
            public float yaw, pace, wait, gait, phase, radius, ground, stride = 30, swing = 25, bob, idleMin = 2, idleMax = 6, turnRate = 90;
            public float neckPitch, neckRest, neckLow;
            public bool moving, browsing;
        }
        /// <summary>Something a roaming animal keeps clear of: a rock, a trunk, a rack, a keeper or a resting animal.</summary>
        struct Blocker
        {
            public Vector3 at;
            public float radius;
            public ZooLayout.Exhibit pen;
        }
        sealed class Lounger
        {
            public Transform view, head, tail;
            public float phase;
        }
        sealed class Climber
        {
            public Transform view;
            public Vector3 foot, outward;
            public float height, speed, phase;
        }
        sealed class Swimmer
        {
            public Transform view;
            public Rect water;
            public float angle, rate, fraction, y, phase;
        }
        static readonly Color ElephantGrey = new Color(.56f, .56f, .58f), Ivory = new Color(.95f, .93f, .85f), Eye = new Color(.08f, .08f, .08f),
            GiraffeTan = new Color(.93f, .72f, .36f), GiraffePatch = new Color(.56f, .34f, .16f), ZebraWhite = new Color(.95f, .95f, .93f), ZebraBlack = new Color(.1f, .1f, .1f),
            LionTan = new Color(.84f, .64f, .32f), LionLight = new Color(.93f, .8f, .58f), Mane = new Color(.5f, .3f, .12f),
            PenguinBlack = new Color(.1f, .11f, .13f), PenguinWhite = new Color(.96f, .96f, .95f), Beak = new Color(.98f, .6f, .12f),
            FlamingoPink = new Color(.97f, .52f, .6f), FlamingoDeep = new Color(.9f, .36f, .46f),
            MonkeyBrown = new Color(.46f, .3f, .17f), MonkeyFace = new Color(.86f, .68f, .5f);
        readonly List<Beast> beasts = new List<Beast>();
        readonly List<Blocker> blockers = new List<Blocker>();
        readonly List<Resident> residents = new List<Resident>();
        readonly List<Lounger> loungers = new List<Lounger>();
        readonly List<Climber> climbers = new List<Climber>();
        readonly List<Swimmer> swimmers = new List<Swimmer>();
        readonly List<Transform> flamingoNecks = new List<Transform>();
        Walker diver;
        Transform swingPivot, runner;
        Vector3 ropeFrom, ropeTo;

        public IReadOnlyList<Resident> Residents => residents;
        public int Count(Species species)
        {
            int n = 0;
            foreach (var r in residents)
                if (r.species == species)
                    n++;
            return n;
        }

        void AddAnimals()
        {
            AddBlockers();
            AddElephants();
            AddSavanna();
            AddLions();
            AddPenguins();
            AddFlamingos();
            AddMonkeys();
        }
        void Block(ZooLayout.Exhibit pen, Vector3 at, float radius) => blockers.Add(new Blocker { at = new Vector3(at.x, 0, at.z), radius = radius, pen = pen });
        void AddBlockers()
        {
            var P = plan;
            Block(ZooLayout.Exhibit.Elephants, P.HayRack.at, .16f);
            Block(ZooLayout.Exhibit.Elephants, P.ElephantTree.at, .08f);
            Block(ZooLayout.Exhibit.Elephants, P.Boulder.at, .1f);
            foreach (var tree in P.Acacias)
                Block(ZooLayout.Exhibit.Savanna, tree.at, .07f);
            foreach (var rock in P.SavannaRocks)
                Block(ZooLayout.Exhibit.Savanna, rock.at, .12f);
            foreach (var rock in P.PenguinRocks)
                Block(ZooLayout.Exhibit.Penguins, rock.at, Mathf.Max(rock.size.x, rock.size.z) / 2 + .02f);
            Block(ZooLayout.Exhibit.Penguins, P.PenguinKeeper.at, .07f);
        }
        Transform Add(Transform view, Species species, ZooLayout.Exhibit pen)
        {
            residents.Add(new Resident { view = view, species = species, pen = pen });
            return view;
        }
        /// <summary>A leg hanging from a hip pivot, so it can swing.</summary>
        Transform Leg(Transform body, Vector3 hip, Vector3 size, Color color)
        {
            var pivot = Node("Leg", body);
            pivot.localPosition = hip;
            Box("Leg", new Vector3(0, -size.y / 2, 0), size, color, pivot);
            return pivot;
        }
        Transform[] Legs(Transform body, float hip, float x, float z, Vector3 size, Color color) => new[]
        {
            Leg(body, new Vector3(-x, hip, z), size, color), Leg(body, new Vector3(x, hip, z), size, color),
            Leg(body, new Vector3(-x, hip, -z), size, color), Leg(body, new Vector3(x, hip, -z), size, color),
        };
        Beast Roamer(Transform view, Species species, ZooLayout.Exhibit pen, Rect yard, float radius, float pace)
        {
            var b = new Beast { view = view, species = species, pen = pen, yard = yard, radius = radius, pace = pace, phase = Next() * 6, ground = plan.PenTop };
            beasts.Add(b);
            Add(view, species, pen);
            return b;
        }
        /// <summary>Puts a roaming animal down on a clear spot of its yard, facing somewhere random, resting for a moment.</summary>
        void Settle(Beast b)
        {
            b.at = b.beat != null ? b.beat[0] : ClearSpot(b);
            b.target = b.at;
            b.yaw = Next() * 360;
            b.wait = Next() * 3;
            b.neckPitch = b.neckRest;
            PoseBeast(b, 0);
        }
        Vector3 ClearSpot(Beast b)
        {
            var spot = Vector3.zero;
            for (int tries = 0; tries < 40; tries++)
            {
                spot = RandomIn(b);
                if (Free(b, spot))
                    return spot;
            }
            return spot;
        }
        Vector3 RandomIn(Beast b)
        {
            for (int tries = 0; ; tries++)
            {
                var spot = new Vector3(b.yard.xMin + Next() * b.yard.width, 0, b.yard.yMin + Next() * b.yard.height);
                if (!b.oval || ZooLayout.InOval(b.yard, spot) || tries > 20)
                    return spot;
            }
        }
        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        /// <summary>Clear of the pen's blockers and of where every other animal in the pen stands or is heading.</summary>
        bool Free(Beast b, Vector3 spot)
        {
            foreach (var o in blockers)
                if (o.pen == b.pen && Flat(spot, o.at) < o.radius + b.radius)
                    return false;
            foreach (var other in beasts)
                if (other != b && other.pen == b.pen && (Flat(spot, other.target) < other.radius + b.radius || Flat(spot, other.at) < other.radius + b.radius))
                    return false;
            return true;
        }
        /// <summary>A new spot to walk to; false when none of a dozen tries leads somewhere clear.</summary>
        bool PickTarget(Beast b)
        {
            if (b.beat != null)
            {
                b.beatIndex = (b.beatIndex + 1) % b.beat.Length;
                b.target = b.beat[b.beatIndex];
                return true;
            }
            for (int tries = 0; tries < 12; tries++)
            {
                var spot = b.species == Species.Giraffe && Next() < .3f ? FeedingSpot() : RandomIn(b);
                if (Flat(spot, b.at) < .12f)
                    continue;
                bool clear = true;
                for (int i = 1; i <= 6 && clear; i++)
                    clear = Free(b, Vector3.Lerp(b.at, spot, i / 6f));
                if (!clear)
                    continue;
                b.target = spot;
                return true;
            }
            return false;
        }
        Vector3 FeedingSpot() => plan.FeedingSpot.at;

        // ---- Builders -----------------------------------------------------------------------------------------------
        void AddElephants()
        {
            var yard = plan.ElephantYard;
            float[] sizes = { 1, .92f, .6f };
            foreach (float s in sizes)
            {
                var view = Node("Elephant");
                Box("Body", new Vector3(0, .3f, 0) * s, new Vector3(.22f, .2f, .36f) * s, ElephantGrey, view);
                Box("Head", new Vector3(0, .36f, .22f) * s, new Vector3(.16f, .16f, .12f) * s, ElephantGrey, view);
                for (int side = -1; side <= 1; side += 2)
                    Box("Eye", new Vector3(side * .081f, .39f, .25f) * s, Vector3.one * .012f * s, Eye, view);
                var ears = new Transform[2];
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? -1 : 1;
                    ears[i] = Node("Ear", view);
                    ears[i].localPosition = new Vector3(side * .08f, .4f, .2f) * s;
                    Box("Ear", new Vector3(side * .012f, -.04f, -.05f) * s, new Vector3(.02f, .15f, .12f) * s, ElephantGrey, ears[i]);
                }
                var trunk = Node("Trunk", view);
                trunk.localPosition = new Vector3(0, .33f, .28f) * s;
                Box("Trunk", new Vector3(0, -.1f, 0) * s, new Vector3(.05f, .2f, .05f) * s, ElephantGrey, trunk);
                Box("Trunk tip", new Vector3(0, -.2f, .015f) * s, new Vector3(.045f, .03f, .05f) * s, ElephantGrey, trunk);
                if (s > .8f)
                    for (int side = -1; side <= 1; side += 2)
                        Box("Tusk", new Vector3(side * .045f, .27f, .3f) * s, new Vector3(.016f, .016f, .08f) * s, Ivory, view, Quaternion.Euler(-25, 0, 0));
                var tail = Node("Tail", view);
                tail.localPosition = new Vector3(0, .36f, -.18f) * s;
                Box("Tail", new Vector3(0, -.06f, -.01f) * s, new Vector3(.015f, .12f, .015f) * s, ElephantGrey, tail);
                var b = Roamer(view, Species.Elephant, ZooLayout.Exhibit.Elephants, yard, .16f * s + .02f, .07f);
                b.legs = Legs(view, .2f * s, .07f * s, .12f * s, new Vector3(.07f, .2f, .07f) * s, ElephantGrey);
                b.ears = ears;
                b.trunk = trunk;
                b.tail = tail;
                b.stride = 22 / s;
                b.swing = 18;
                b.idleMin = 3;
                b.idleMax = 9;
                b.turnRate = 50;
                Settle(b);
            }
        }
        void AddSavanna()
        {
            var yard = plan.SavannaYard;
            for (int i = 0; i < 3; i++)
            {
                var view = Node("Giraffe");
                Box("Body", new Vector3(0, .36f, 0), new Vector3(.11f, .13f, .26f), GiraffeTan, view);
                for (int side = -1; side <= 1; side += 2)
                    for (int k = -1; k <= 1; k++)
                        Box("Patch", new Vector3(side * .056f, .37f + (k == 0 ? .03f : -.02f), k * .08f), new Vector3(.004f, .04f, .05f), GiraffePatch, view);
                for (int k = -1; k <= 1; k += 2)
                    Box("Patch", new Vector3(k * .02f, .426f, k * .05f), new Vector3(.04f, .004f, .05f), GiraffePatch, view);
                var neck = Node("Neck", view);
                neck.localPosition = new Vector3(0, .4f, .1f);
                Box("Neck", new Vector3(0, .18f, 0), new Vector3(.05f, .36f, .05f), GiraffeTan, neck);
                Box("Mane", new Vector3(0, .18f, -.028f), new Vector3(.012f, .34f, .012f), GiraffePatch, neck);
                for (int k = 0; k < 3; k++)
                    Box("Patch", new Vector3(0, .07f + k * .1f, .026f), new Vector3(.03f, .035f, .004f), GiraffePatch, neck);
                Box("Head", new Vector3(0, .37f, .035f), new Vector3(.05f, .055f, .11f), GiraffeTan, neck);
                Box("Muzzle", new Vector3(0, .36f, .09f), new Vector3(.045f, .045f, .03f), GiraffePatch, neck);
                for (int side = -1; side <= 1; side += 2)
                    Box("Ossicone", new Vector3(side * .015f, .41f, 0), new Vector3(.01f, .03f, .01f), GiraffePatch, neck);
                var tail = Node("Tail", view);
                tail.localPosition = new Vector3(0, .41f, -.13f);
                Box("Tail", new Vector3(0, -.06f, -.01f), new Vector3(.01f, .12f, .01f), GiraffeTan, tail);
                Box("Tuft", new Vector3(0, -.13f, -.01f), new Vector3(.018f, .03f, .018f), GiraffePatch, tail);
                var b = Roamer(view, Species.Giraffe, ZooLayout.Exhibit.Savanna, yard, .14f, .1f);
                b.legs = Legs(view, .3f, .04f, .09f, new Vector3(.03f, .3f, .03f), GiraffeTan);
                b.neck = neck;
                b.tail = tail;
                b.neckRest = 18;
                b.neckLow = 38;
                b.stride = 26;
                b.swing = 22;
                b.idleMin = 3;
                b.idleMax = 8;
                b.turnRate = 60;
                Settle(b);
            }
            for (int i = 0; i < 5; i++)
            {
                var view = Node("Zebra");
                Box("Body", new Vector3(0, .19f, 0), new Vector3(.09f, .1f, .22f), ZebraWhite, view);
                for (int k = 0; k < 5; k++)
                    Box("Stripe", new Vector3(0, .19f, -.09f + k * .042f), new Vector3(.094f, .104f, .016f), ZebraBlack, view);
                var neck = Node("Neck", view);
                neck.localPosition = new Vector3(0, .21f, .09f);
                Box("Neck", new Vector3(0, .06f, .01f), new Vector3(.04f, .13f, .055f), ZebraWhite, neck);
                Box("Stripe", new Vector3(0, .05f, .01f), new Vector3(.044f, .02f, .058f), ZebraBlack, neck);
                Box("Mane", new Vector3(0, .07f, -.02f), new Vector3(.012f, .12f, .02f), ZebraBlack, neck);
                Box("Head", new Vector3(0, .13f, .04f), new Vector3(.045f, .05f, .1f), ZebraWhite, neck);
                Box("Muzzle", new Vector3(0, .125f, .09f), new Vector3(.047f, .035f, .03f), ZebraBlack, neck);
                var tail = Node("Tail", view);
                tail.localPosition = new Vector3(0, .22f, -.11f);
                Box("Tail", new Vector3(0, -.04f, -.01f), new Vector3(.01f, .08f, .01f), ZebraBlack, tail);
                var b = Roamer(view, Species.Zebra, ZooLayout.Exhibit.Savanna, yard, .11f, .12f);
                b.legs = Legs(view, .14f, .035f, .08f, new Vector3(.028f, .14f, .028f), ZebraWhite);
                b.neck = neck;
                b.tail = tail;
                b.neckRest = 25;
                b.neckLow = 110;
                b.stride = 40;
                b.idleMin = 2;
                b.idleMax = 7;
                Settle(b);
            }
        }
        /// <summary>A lion standing on four legs (<paramref name="mane"/> for the male), its head and tail on pivots.</summary>
        Transform Lion(string name, float s, bool mane, out Transform head, out Transform tail, out Transform[] legs)
        {
            var view = Node(name);
            Box("Body", new Vector3(0, .12f, 0) * s, new Vector3(.09f, .08f, .24f) * s, LionTan, view);
            head = LionHead(view, new Vector3(0, .15f, .13f) * s, s, mane);
            tail = LionTail(view, new Vector3(0, .14f, -.12f) * s, s, -10);
            legs = Legs(view, .08f * s, .035f * s, .08f * s, new Vector3(.03f, .08f, .03f) * s, LionTan);
            return view;
        }
        Transform LionHead(Transform body, Vector3 at, float s, bool mane)
        {
            var head = Node("Head", body);
            head.localPosition = at;
            if (mane)
                Box("Mane", Vector3.zero, new Vector3(.12f, .12f, .07f) * s, Mane, head);
            Box("Head", new Vector3(0, 0, .03f) * s, new Vector3(.07f, .07f, .07f) * s, LionTan, head);
            Box("Muzzle", new Vector3(0, -.015f, .07f) * s, new Vector3(.04f, .03f, .03f) * s, LionLight, head);
            for (int side = -1; side <= 1; side += 2)
                Box("Ear", new Vector3(side * .026f, .04f, .02f) * s, new Vector3(.018f, .018f, .01f) * s, LionTan, head);
            return head;
        }
        Transform LionTail(Transform body, Vector3 at, float s, float droop)
        {
            var tail = Node("Tail", body);
            tail.localPosition = at;
            Box("Tail", new Vector3(0, 0, -.06f) * s, new Vector3(.012f, .012f, .12f) * s, LionTan, tail, Quaternion.Euler(-droop, 0, 0));
            Box("Tuft", new Vector3(0, -droop * .0015f, -.125f) * s, new Vector3(.022f, .022f, .025f) * s, Mane, tail);
            return tail;
        }
        /// <summary>A lioness lying down, head up, at <paramref name="at"/> looking along <paramref name="facing"/>.</summary>
        void Lying(Vector3 at, Vector3 facing)
        {
            var view = Node("Lion");
            view.localPosition = at;
            view.localRotation = Quaternion.LookRotation(facing);
            Box("Body", new Vector3(0, .04f, 0), new Vector3(.1f, .07f, .24f), LionTan, view);
            for (int side = -1; side <= 1; side += 2)
                Box("Paw", new Vector3(side * .03f, .014f, .14f), new Vector3(.03f, .025f, .08f), LionTan, view);
            var head = LionHead(view, new Vector3(0, .1f, .13f), 1, false);
            var tail = LionTail(view, new Vector3(0, .02f, -.12f), 1, 0);
            loungers.Add(new Lounger { view = view, head = head, tail = tail, phase = Next() * 6 });
            Add(view, Species.Lion, ZooLayout.Exhibit.Lions);
        }
        void AddLions()
        {
            var male = Lion("Lion", 1.05f, true, out var head, out var tail, out var legs);
            var pacer = Roamer(male, Species.Lion, ZooLayout.Exhibit.Lions, plan.LionYard, .1f, .12f);
            pacer.beat = plan.LionBeat;
            pacer.legs = legs;
            pacer.tail = tail;
            pacer.idleMin = .8f;
            pacer.idleMax = 2;
            pacer.stride = 45;
            pacer.turnRate = 120;
            pacer.bob = .006f;
            Settle(pacer);
            Lying(plan.LionPerch, Vector3.left);
            foreach (var bed in plan.LionBeds)
                Lying(bed.at + Vector3.up * plan.PenTop, bed.facing);
            // The cub trots round its mother on the second bed.
            var mother = plan.LionBeds[1].at;
            var ring = new Vector3[8];
            for (int i = 0; i < ring.Length; i++)
            {
                float a = i * 45 * Mathf.Deg2Rad;
                ring[i] = mother + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * plan.CubRing;
            }
            var cubView = Lion("Lion cub", .55f, false, out _, out var cubTail, out var cubLegs);
            var cub = Roamer(cubView, Species.Lion, ZooLayout.Exhibit.Lions, plan.LionYard, .06f, .1f);
            cub.beat = ring;
            cub.legs = cubLegs;
            cub.tail = cubTail;
            cub.idleMin = 0;
            cub.idleMax = 1.5f;
            cub.stride = 70;
            cub.turnRate = 200;
            cub.bob = .008f;
            Settle(cub);
        }
        Transform Penguin(string name)
        {
            var view = Node(name);
            Box("Body", new Vector3(0, .045f, 0), new Vector3(.045f, .075f, .04f), PenguinBlack, view);
            Box("Belly", new Vector3(0, .04f, .021f), new Vector3(.036f, .06f, .004f), PenguinWhite, view);
            Box("Head", new Vector3(0, .095f, 0), new Vector3(.034f, .03f, .034f), PenguinBlack, view);
            Box("Beak", new Vector3(0, .092f, .024f), new Vector3(.01f, .008f, .016f), Beak, view);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Foot", new Vector3(side * .012f, .004f, .012f), new Vector3(.014f, .006f, .02f), Beak, view);
                Box("Flipper", new Vector3(side * .026f, .05f, 0), new Vector3(.006f, .045f, .02f), PenguinBlack, view, Quaternion.Euler(0, 0, side * 12));
            }
            return view;
        }
        void AddPenguins()
        {
            for (int i = 0; i < 4; i++)
            {
                var b = Roamer(Penguin("Penguin"), Species.Penguin, ZooLayout.Exhibit.Penguins, plan.PenguinShore, .035f, .08f);
                b.stride = 60;
                b.swing = 0;
                b.idleMin = 1;
                b.idleMax = 5;
                b.turnRate = 160;
                Settle(b);
            }
            for (int i = 0; i < 3; i++)
            {
                var view = Add(Penguin("Penguin"), Species.Penguin, ZooLayout.Exhibit.Penguins);
                swimmers.Add(new Swimmer { view = view, water = plan.PenguinPool, angle = i * 120, rate = (i % 2 == 0 ? 1 : -1) * (40 + 15 * Next()), fraction = .45f + .15f * i, y = plan.PenguinSwim, phase = Next() * 6 });
            }
            var rock = plan.PenguinRocks[1];
            var lookout = Add(Penguin("Penguin"), Species.Penguin, ZooLayout.Exhibit.Penguins);
            lookout.localPosition = rock.at + Vector3.up * (plan.PenTop + rock.size.y);
            lookout.localRotation = Quaternion.Euler(0, 200, 0);
            // The diver: a walker whose lap climbs the big rock, dives in, swims and climbs out again.
            var route = plan.DiveRoute;
            float length = 0;
            for (int i = 0; i < route.Length; i++)
                length += Vector3.Distance(route[i], route[(i + 1) % route.Length]);
            diver = new Walker { view = Add(Penguin("Penguin"), Species.Penguin, ZooLayout.Exhibit.Penguins), route = route, loop = true, length = length, pace = 1, speeds = plan.DiveSpeeds, lane = 0, bob = .01f, stride = 60 };
            Pose(diver);
        }
        Transform Flamingo(bool oneLeg, out Transform neck)
        {
            var view = Node("Flamingo");
            Box("Leg", new Vector3(-.01f, .05f, 0), new Vector3(.008f, .1f, .008f), FlamingoPink, view);
            if (oneLeg)
                Box("Tucked leg", new Vector3(.012f, .085f, -.01f), new Vector3(.008f, .05f, .008f), FlamingoPink, view, Quaternion.Euler(60, 0, 0));
            else
                Box("Leg", new Vector3(.01f, .05f, 0), new Vector3(.008f, .1f, .008f), FlamingoPink, view);
            Box("Body", new Vector3(0, .12f, 0), new Vector3(.045f, .045f, .08f), FlamingoPink, view);
            Box("Wings", new Vector3(0, .143f, -.005f), new Vector3(.046f, .006f, .06f), FlamingoDeep, view);
            Box("Tail", new Vector3(0, .125f, -.045f), new Vector3(.03f, .02f, .015f), PenguinBlack, view);
            neck = Node("Neck", view);
            neck.localPosition = new Vector3(0, .13f, .035f);
            Box("Neck", new Vector3(0, .04f, -.005f), new Vector3(.012f, .08f, .012f), FlamingoPink, neck);
            Box("Neck", new Vector3(0, .09f, .008f), new Vector3(.012f, .04f, .012f), FlamingoPink, neck, Quaternion.Euler(30, 0, 0));
            Box("Head", new Vector3(0, .112f, .02f), new Vector3(.018f, .018f, .03f), FlamingoPink, neck);
            Box("Beak", new Vector3(0, .105f, .04f), new Vector3(.01f, .012f, .02f), PenguinBlack, neck);
            return view;
        }
        void AddFlamingos()
        {
            var lagoon = plan.Lagoon;
            for (int i = 0; i < 4; i++)
            {
                var view = Add(Flamingo(true, out var neck), Species.Flamingo, ZooLayout.Exhibit.Flamingos);
                var at = ZooLayout.OnOval(lagoon, 30 + i * 85 + Range(-15, 15), .35f + .25f * (i % 2), plan.WaterTop - .01f);
                view.localPosition = at;
                view.localRotation = Quaternion.Euler(0, Next() * 360, 0);
                flamingoNecks.Add(neck);
                Block(ZooLayout.Exhibit.Flamingos, at, .05f);
            }
            var shallows = new Rect(lagoon.x + lagoon.width * .15f, lagoon.y + lagoon.height * .15f, lagoon.width * .7f, lagoon.height * .7f);
            for (int i = 0; i < 4; i++)
            {
                var view = Flamingo(false, out var neck);
                var b = Roamer(view, Species.Flamingo, ZooLayout.Exhibit.Flamingos, shallows, .05f, .05f);
                b.oval = true;
                b.ground = plan.WaterTop - .01f;
                b.neck = neck;
                b.neckRest = 0;
                b.neckLow = 120;
                b.swing = 0;
                b.idleMin = 2;
                b.idleMax = 6;
                b.turnRate = 120;
                Settle(b);
            }
        }
        Transform Monkey(bool armsUp)
        {
            var view = Node("Monkey");
            Box("Body", new Vector3(0, .06f, 0), new Vector3(.04f, .05f, .032f), MonkeyBrown, view);
            Box("Head", new Vector3(0, .1f, .005f), new Vector3(.036f, .034f, .032f), MonkeyBrown, view);
            Box("Face", new Vector3(0, .098f, .019f), new Vector3(.026f, .022f, .006f), MonkeyFace, view);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Arm", armsUp ? new Vector3(side * .022f, .12f, .005f) : new Vector3(side * .028f, .065f, .004f), new Vector3(.012f, .045f, .012f), MonkeyBrown, view);
                Box("Leg", new Vector3(side * .014f, .02f, .004f), new Vector3(.014f, .04f, .014f), MonkeyBrown, view);
            }
            Box("Tail", new Vector3(0, .07f, -.04f), new Vector3(.008f, .008f, .06f), MonkeyBrown, view, Quaternion.Euler(-40, 0, 0));
            return view;
        }
        void AddMonkeys()
        {
            float ground = plan.IslandTop;
            foreach (var pole in plan.Poles)
            {
                var foot = new Vector3(pole.x, ground, pole.z);
                var outward = new Vector3(pole.x, 0, pole.z).normalized;
                climbers.Add(new Climber { view = Add(Monkey(true), Species.Monkey, ZooLayout.Exhibit.Monkeys), foot = foot, outward = outward, height = pole.y - .08f, speed = .08f + .04f * Next(), phase = Next() * 10 });
            }
            for (int i = 0; i < 2; i++)
            {
                var pole = plan.Poles[i];
                var sitter = Add(Monkey(false), Species.Monkey, ZooLayout.Exhibit.Monkeys);
                sitter.localPosition = new Vector3(pole.x, ground + pole.y + .01f, pole.z);
                Idle(sitter, sitter.localPosition, Next() * 360, 40, .004f, 1.5f);
            }
            // The swinger hangs from the rope between the third pole and the first.
            Vector3 a = plan.Poles[2], b = plan.Poles[0];
            ropeFrom = new Vector3(a.x, ground + a.y - .02f, a.z);
            ropeTo = new Vector3(b.x, ground + b.y - .02f, b.z);
            swingPivot = Node("Rope swing");
            var hanger = Add(Monkey(true), Species.Monkey, ZooLayout.Exhibit.Monkeys);
            hanger.SetParent(swingPivot, false);
            hanger.localPosition = new Vector3(0, -.14f, 0);
            runner = Add(Monkey(false), Species.Monkey, ZooLayout.Exhibit.Monkeys);
        }

        // ---- Animation ----------------------------------------------------------------------------------------------
        void AnimateAnimals(float step)
        {
            foreach (var b in beasts)
                Roam(b, step);
            foreach (var l in loungers)
            {
                l.head.localRotation = Quaternion.Euler(Mathf.Sin(clock * .4f + l.phase) * 6, Mathf.Sin(clock * .23f + l.phase) * 35, 0);
                l.tail.localRotation = Quaternion.Euler(0, Mathf.Sin(clock * 1.7f + l.phase) * 25, 0);
                l.view.localScale = new Vector3(1, 1 + Mathf.Sin(clock * 1.2f + l.phase) * .03f, 1);
            }
            foreach (var s in swimmers)
            {
                s.angle += step * s.rate;
                var at = ZooLayout.OnOval(s.water, s.angle, s.fraction, s.y + Mathf.Sin(clock * 3 + s.phase) * .004f);
                var ahead = ZooLayout.OnOval(s.water, s.angle + Mathf.Sign(s.rate) * 3, s.fraction, s.y);
                s.view.localPosition = at;
                var along = ahead - at;
                along.y = 0;
                if (along.sqrMagnitude > 1e-8f)
                    s.view.localRotation = Quaternion.LookRotation(along) * Quaternion.Euler(50, 0, 0);
            }
            Dive(step);
            for (int i = 0; i < flamingoNecks.Count; i++)
            {
                // Now and then a standing flamingo dips its head into the water.
                float dip = Mathf.Clamp01(Mathf.Sin(clock * .35f + i * 1.9f) * 3 - 2);
                flamingoNecks[i].localRotation = Quaternion.Euler(dip * 120, 0, 0);
            }
            foreach (var c in climbers)
                Climb(c);
            SwingOnTheRope();
            float lap = clock * .9f;
            var round = new Vector3(Mathf.Sin(lap), 0, Mathf.Cos(lap)) * plan.RunRing;
            runner.localPosition = round + Vector3.up * (plan.IslandTop + Mathf.Abs(Mathf.Sin(clock * 9)) * .02f);
            runner.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(lap), 0, -Mathf.Sin(lap)));
        }
        void Roam(Beast b, float step)
        {
            if (b.wait > 0)
            {
                b.wait -= step;
                b.moving = false;
                if (b.face != Vector3.zero)
                    b.yaw = Mathf.MoveTowardsAngle(b.yaw, Toward(b.at, b.face), b.turnRate * step);
                if (b.wait <= 0)
                {
                    b.face = Vector3.zero;
                    b.browsing = false;
                    if (PickTarget(b))
                        b.moving = true;
                    else
                        b.wait = .5f + Next();
                }
            }
            else if (b.moving)
            {
                var to = b.target - b.at;
                to.y = 0;
                float distance = to.magnitude;
                if (distance > 1e-4f)
                {
                    float delta = Mathf.DeltaAngle(b.yaw, Toward(b.at, b.target));
                    b.yaw += Mathf.Clamp(delta, -b.turnRate * step, b.turnRate * step);
                    // Turn on the spot first, then walk straight along the line PickTarget checked.
                    if (Mathf.Abs(delta) < 12)
                    {
                        float move = Mathf.Min(distance, b.pace * step);
                        b.at += to / distance * move;
                        b.gait += move * b.stride;
                        distance -= move;
                    }
                }
                if (distance <= 1e-3f)
                    Arrive(b);
            }
            PoseBeast(b, step);
        }
        void Arrive(Beast b)
        {
            b.at = b.target;
            b.moving = false;
            b.wait = Range(b.idleMin, b.idleMax);
            if (b.species != Species.Giraffe)
                return;
            // Giraffes turn to the deck's visitors or to the nearest acacia and browse.
            if (Flat(b.at, FeedingSpot()) < .02f)
            {
                b.face = b.at + plan.FeedingSpot.facing;
                b.browsing = true;
                b.wait += 3;
                return;
            }
            foreach (var tree in plan.Acacias)
                if (Flat(b.at, tree.at) < .5f)
                {
                    b.face = tree.at;
                    b.browsing = true;
                }
        }
        void PoseBeast(Beast b, float step)
        {
            float swing = b.moving ? Mathf.Sin(b.gait) * b.swing : 0;
            for (int i = 0; i < b.legs.Length; i++)
                b.legs[i].localRotation = Quaternion.Euler(i == 0 || i == 3 ? swing : -swing, 0, 0);
            float bob = b.moving ? Mathf.Abs(Mathf.Sin(b.gait)) * b.bob : 0;
            float roll = b.species == Species.Penguin && b.moving ? Mathf.Sin(b.gait) * 12 : 0;
            b.view.localPosition = b.at + Vector3.up * (b.ground + bob);
            b.view.localRotation = Quaternion.Euler(0, b.yaw, roll);
            float t = clock + b.phase;
            if (b.neck)
            {
                bool low = !b.moving && (b.species != Species.Giraffe || b.browsing) && Mathf.Sin(t * .5f) > -.3f;
                float goal = low ? b.neckLow : b.neckRest + (b.moving ? Mathf.Sin(b.gait) * 4 : Mathf.Sin(t * .8f) * 3);
                b.neckPitch = Mathf.MoveTowards(b.neckPitch, goal, 70 * step);
                b.neck.localRotation = Quaternion.Euler(b.neckPitch, 0, 0);
            }
            if (b.trunk)
                b.trunk.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 18 - 8, 0, Mathf.Sin(t * .9f) * 10);
            for (int i = 0; i < b.ears.Length; i++)
                b.ears[i].localRotation = Quaternion.Euler(0, (i == 0 ? -1 : 1) * (8 + Mathf.Sin(t * 2.1f) * 14), 0);
            if (b.tail)
                b.tail.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 2.3f) * 20, 0);
        }
        void Dive(float step)
        {
            Locate(diver, diver.at, out _, out _, out int segment);
            diver.at = Mathf.Repeat(diver.at + step * diver.speeds[segment] * diver.pace, diver.length);
            Pose(diver);
            // Head first into the water, flat while swimming.
            if (segment == 1)
                diver.view.localRotation *= Quaternion.Euler(70, 0, 0);
            else if (segment == 2 || segment == 3)
                diver.view.localRotation *= Quaternion.Euler(50, 0, 0);
        }
        /// <summary>Up the pole, a rest at the top, back down, a rest at the bottom.</summary>
        void Climb(Climber c)
        {
            float up = c.height / c.speed, rest = 2.5f, cycle = 2 * up + 2 * rest, t = Mathf.Repeat(clock + c.phase, cycle);
            float y = t < up ? t / up : t < up + rest ? 1 : t < 2 * up + rest ? 1 - (t - up - rest) / up : 0;
            c.view.localPosition = c.foot + c.outward * .035f + Vector3.up * (.02f + y * c.height);
            c.view.localRotation = Quaternion.LookRotation(-c.outward);
        }
        /// <summary>The swinger works along the rope hand over hand, swaying under it.</summary>
        void SwingOnTheRope()
        {
            float u = (Mathf.Sin(clock * .5f) + 1) / 2;
            var along = ropeTo - ropeFrom;
            swingPivot.localPosition = Vector3.Lerp(ropeFrom, ropeTo, u);
            var flat = new Vector3(along.x, 0, along.z);
            swingPivot.localRotation = Quaternion.LookRotation(flat) * Quaternion.Euler(0, 0, Mathf.Sin(clock * 3.2f) * 22);
        }
    }
}
