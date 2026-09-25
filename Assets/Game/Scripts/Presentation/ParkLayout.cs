using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// Shared measurements of the three city parks, one plan per footprint, in the lot frame: origin on the lot's centre,
    /// x across the frontage, +z towards the street. Every park has the same bones: a clipped hedge with a gate in the middle
    /// of each side, a loop path for joggers, two straight paths crossing at a round fountain plaza, trees in the band between
    /// the loop and the hedge, and four lawns between the paths. The lawns hold:
    /// <list type="bullet">
    /// <item>3×3 town park: a duck pond, a playground (swings and a slide), a picnic lawn and a kickabout lawn with a kite.</item>
    /// <item>4×4 city park: a bigger pond, a bandstand with a band, a playground with a seesaw, and a café kiosk.</item>
    /// <item>5×5 central park: a boating lake with an island and a boathouse, the bandstand, a big playground with a roundabout, and the café.</item>
    /// </list>
    /// <c>ParkArt</c> draws from these numbers and <see cref="ParkLife"/> moves people on them, so they stay in step.
    /// </summary>
    public sealed class ParkLayout
    {
        public struct Tree
        {
            public Vector3 at;
            public float scale;
            public bool round;
        }
        /// <summary>A bench: its centre on the ground and the way a sitter looks.</summary>
        public struct Seat
        {
            public Vector3 at, facing;
        }
        /// <summary>A round flower bed: its centre on the ground and its radius.</summary>
        public struct Bed
        {
            public Vector3 at;
            public float radius;
        }

        public const int MinSize = 3, MaxSize = 5;
        const float BandTreeSpacing = .42f, ArcStep = 30f;
        static readonly ParkLayout[] plans = { null, null, null, TownPark(), CityPark(), CentralPark() };
        /// <summary>The plan for a park of <paramref name="size"/> cells per side (clamped to 3–5).</summary>
        public static ParkLayout For(int size) => plans[Mathf.Clamp(size, MinSize, MaxSize)];

        public readonly int Size;
        public readonly float Half;
        /// <summary>Tops of the lawn, the gravel paths, the plaza, the pond's bank and water, and the playground surface.</summary>
        public readonly float LawnTop = .03f, PathTop = .036f, PlazaTop = .04f, BankTop = .034f, WaterTop = .038f, PadTop = .035f;
        public readonly float PathWidth = .16f, GateHalf = .15f, HedgeInset = .05f, HedgeHeight = .1f, HedgeThick = .07f;
        /// <summary>Half the side of the loop path's centre line.</summary>
        public readonly float Loop;
        /// <summary>Radii of the paved plaza where the paths cross and of the fountain basin in its middle.</summary>
        public readonly float Plaza, Basin;
        /// <summary>People crossing the plaza walk round the fountain at this radius.</summary>
        public float Ring => Basin + .09f;

        // Water: an ellipse filling Pond. The central park's is a boating lake with an island and a boathouse.
        public Rect Pond { get; private set; }
        public int Ducks { get; private set; }
        public bool Lake { get; private set; }
        public int Boats { get; private set; }
        public Vector3 Island { get; private set; }
        public float IslandRadius { get; private set; }
        public Rect Boathouse { get; private set; }
        public Vector3 JettyTip { get; private set; }

        // The playground: a safety surface, a swing frame along x (seats swing along z), a slide along x, and optional extras.
        public Rect Playground { get; private set; }
        public int Swings { get; private set; }
        public Vector3 SwingFrame { get; private set; }
        public readonly float SwingPitch = .13f, SwingBar = .22f, SwingSeat = .07f;
        public Vector3 SlideLadder { get; private set; }
        public Vector3 SlideTop { get; private set; }
        public Vector3 SlideEnd { get; private set; }
        public bool HasSeesaw { get; private set; }
        public Vector3 Seesaw { get; private set; }
        public readonly float SeesawHalf = .17f;
        public bool HasRoundabout { get; private set; }
        public Vector3 Roundabout { get; private set; }
        public readonly float RoundaboutRadius = .12f;

        // The bandstand faces the plaza; its audience sits on the lawn in front of it.
        public bool HasBandstand { get; private set; }
        public Vector3 Bandstand { get; private set; }
        public float BandstandRadius { get; private set; }
        public readonly float StageTop = .08f, BandstandRoof = .36f;
        public Vector3 StageFacing { get; private set; }
        public Vector3[] Audience { get; private set; } = new Vector3[0];

        // The café kiosk serves from a hatch facing the plaza; its tables stand under parasols.
        public bool HasCafe { get; private set; }
        public Rect Kiosk { get; private set; }
        public readonly float KioskHeight = .26f;
        public Vector3 Hatch { get; private set; }
        public Vector3 HatchFacing { get; private set; }
        public Vector3[] Tables { get; private set; } = new Vector3[0];

        // Lawn life.
        public Vector3[] Picnics { get; private set; } = new Vector3[0];
        /// <summary>Two children kicking a ball between these spots, or none.</summary>
        public Vector3[] Kickabout { get; private set; } = new Vector3[0];
        public bool HasKite { get; private set; }
        public Vector3 Kite { get; private set; }

        // Furniture and planting.
        public Seat[] Benches { get; private set; } = new Seat[0];
        public Bed[] Beds { get; private set; } = new Bed[0];
        public Tree[] Trees { get; private set; } = new Tree[0];
        public Vector3[] Lamps { get; private set; } = new Vector3[0];

        // Walks.
        /// <summary>The loop path's corners, walked round and round (the last corner leads back to the first).</summary>
        public Vector3[] LoopRoute { get; private set; }
        /// <summary>Gate-to-gate walks along the straight paths that go round the fountain on the plaza.</summary>
        public Vector3[][] Crossings { get; private set; }

        ParkLayout(int size, float loop, float plaza, float basin)
        {
            Size = size;
            Half = size / 2f;
            Loop = loop;
            Plaza = plaza;
            Basin = basin;
        }

        static Vector3 V(float x, float z) => new Vector3(x, 0, z);
        static Rect Area(float x, float z, float width, float depth) => new Rect(x - width / 2, z - depth / 2, width, depth);
        static Seat Bench(float x, float z, float fx, float fz) => new Seat { at = V(x, z), facing = V(fx, fz) };
        static Bed Flowers(float x, float z, float radius) => new Bed { at = V(x, z), radius = radius };
        static Tree Round(float x, float z, float scale) => new Tree { at = V(x, z), scale = scale, round = true };
        static Tree Pine(float x, float z, float scale) => new Tree { at = V(x, z), scale = scale };
        /// <summary>Unit vector of gate <paramref name="d"/> in Directions order: north (+z, the street), east, south, west.</summary>
        public static Vector3 Heading(int d) => V(d == 1 ? 1 : d == 3 ? -1 : 0, d == 0 ? 1 : d == 2 ? -1 : 0);
        /// <summary>Where the path through gate <paramref name="d"/> meets the pavement.</summary>
        public Vector3 Gate(int d) => Heading(d) * (Half - .02f);

        static ParkLayout TownPark()
        {
            var p = new ParkLayout(3, 1.06f, .3f, .14f);
            p.Pond = Area(-.55f, -.53f, .7f, .56f);
            p.Ducks = 4;
            p.Playground = Area(.53f, -.55f, .78f, .7f);
            p.Swings = 2;
            p.SwingFrame = V(.62f, -.78f);
            p.SlideLadder = V(.26f, -.36f);
            p.SlideTop = new Vector3(.34f, .17f, -.36f);
            p.SlideEnd = V(.8f, -.36f);
            p.Picnics = new[] { V(-.68f, .32f), V(-.36f, .74f) };
            p.Kickabout = new[] { V(.3f, .44f), V(.82f, .44f) };
            p.HasKite = true;
            p.Kite = V(.62f, .8f);
            p.Benches = new[] { Bench(-.55f, -.15f, 0, -1), Bench(.8f, -.14f, 0, -1), Bench(-.15f, .5f, -1, 0) };
            p.Beds = new[] { Flowers(-.27f, .27f, .09f), Flowers(.27f, .27f, .07f), Flowers(-.27f, -.27f, .07f) };
            p.Trees = new[] { Round(-.8f, .8f, .9f), Pine(.3f, .82f, .8f), Round(-.85f, -.12f - .06f, .7f) };
            return p.Finish();
        }
        static ParkLayout CityPark()
        {
            var p = new ParkLayout(4, 1.52f, .42f, .2f);
            p.Pond = Area(-.8f, -.8f, 1f, .8f);
            p.Ducks = 6;
            p.Playground = Area(-.78f, .78f, 1.08f, .96f);
            p.Swings = 3;
            p.SwingFrame = V(-.92f, .44f);
            p.SlideLadder = V(-1.18f, 1.06f);
            p.SlideTop = new Vector3(-1.08f, .19f, 1.06f);
            p.SlideEnd = V(-.52f, 1.06f);
            p.HasSeesaw = true;
            p.Seesaw = V(-.44f, .6f);
            p.AddBandstand(V(.92f, -.92f), .25f, 12);
            p.AddCafe(Area(1.1f, 1.12f, .36f, .3f), new[] { V(.42f, .46f), V(.8f, .4f), V(.44f, .88f) });
            p.Picnics = new[] { V(1.2f, -.3f) };
            p.Benches = new[] { Bench(-.8f, -.15f, 0, -1), Bench(-.78f, .16f, 0, 1), Bench(.15f, -.55f, 1, 0), Bench(-1.3f, -.15f, 0, -1) };
            p.Beds = new[] { Flowers(-.36f, -.36f, .1f), Flowers(.36f, -.36f, .08f), Flowers(1.22f, .52f, .1f), Flowers(.5f, 1.25f, .09f) };
            p.Trees = new[] { Round(-1.3f, -1.3f, .8f), Round(.35f, -1.25f, .9f), Pine(1.3f, -.25f, .9f), Round(.95f, .78f, .75f) };
            return p.Finish();
        }
        static ParkLayout CentralPark()
        {
            var p = new ParkLayout(5, 1.98f, .52f, .25f);
            p.Pond = Area(-1f, -1f, 1.6f, 1.36f);
            p.Ducks = 6;
            p.Lake = true;
            p.Boats = 2;
            p.Island = V(-1.12f, -1.08f);
            p.IslandRadius = .2f;
            p.Boathouse = Area(-.3f, -1.66f, .24f, .2f);
            p.JettyTip = V(-.56f, -1.4f);
            p.Playground = Area(-1f, 1f, 1.4f, 1.24f);
            p.Swings = 4;
            p.SwingFrame = V(-1.22f, .56f);
            p.SlideLadder = V(-1.56f, 1.42f);
            p.SlideTop = new Vector3(-1.46f, .21f, 1.42f);
            p.SlideEnd = V(-.82f, 1.42f);
            p.HasSeesaw = true;
            p.Seesaw = V(-.6f, .6f);
            p.HasRoundabout = true;
            p.Roundabout = V(-.62f, 1.02f);
            p.AddBandstand(V(1.12f, -1.12f), .3f, 16);
            p.AddCafe(Area(1.46f, 1.48f, .42f, .34f), new[] { V(.5f, .5f), V(.9f, .44f), V(.48f, .92f), V(1.3f, .5f), V(.9f, .86f) });
            p.Picnics = new[] { V(1.62f, -.4f), V(.4f, -1.62f) };
            p.Kickabout = new[] { V(.34f, 1.4f), V(.98f, 1.66f) };
            p.HasKite = true;
            p.Kite = V(1.66f, .98f);
            p.Benches = new[] { Bench(-1f, -.15f, 0, -1), Bench(-.15f, -1f, -1, 0), Bench(-1f, .17f, 0, 1), Bench(.15f, -.62f, 1, 0), Bench(-1.6f, -.15f, 0, -1) };
            p.Beds = new[] { Flowers(-.44f, -.44f, .12f), Flowers(.44f, -.44f, .1f), Flowers(1.7f, 1.05f, .1f), Flowers(.3f, .34f, .1f) };
            p.Trees = new[] { Round(-1.7f, -1.72f, .9f), Round(.5f, -1.12f, 1f), Pine(1.72f, -.3f, 1f), Round(.3f, 1.72f, .8f), Round(-1.76f, .22f, .8f) };
            return p.Finish();
        }

        /// <summary>The bandstand at <paramref name="at"/>, facing the plaza, with <paramref name="seats"/> spots for the audience in rows before it.</summary>
        void AddBandstand(Vector3 at, float radius, int seats)
        {
            HasBandstand = true;
            Bandstand = at;
            BandstandRadius = radius;
            StageFacing = (-at).normalized;
            var audience = new List<Vector3>();
            int rows = seats > 12 ? 3 : 2, perRow = Mathf.CeilToInt(seats / (float)rows);
            float yaw = Mathf.Atan2(StageFacing.x, StageFacing.z) * Mathf.Rad2Deg;
            for (int row = 0; row < rows; row++)
            {
                float reach = radius + .2f + row * .13f, spread = 70f - row * 8f;
                for (int i = 0; i < perRow && audience.Count < seats; i++)
                {
                    float angle = (yaw - spread / 2 + spread * (i + .5f * (row % 2)) / Mathf.Max(1, perRow - 1)) * Mathf.Deg2Rad;
                    audience.Add(at + V(Mathf.Sin(angle), Mathf.Cos(angle)) * reach);
                }
            }
            Audience = audience.ToArray();
        }
        /// <summary>The café kiosk in <paramref name="kiosk"/>, serving from its west face, and its tables.</summary>
        void AddCafe(Rect kiosk, Vector3[] tables)
        {
            HasCafe = true;
            Kiosk = kiosk;
            HatchFacing = V(-1, 0);
            Hatch = V(kiosk.xMin, kiosk.center.y);
            Tables = tables;
        }

        /// <summary>Adds the trees round the loop, the lamps and the walks shared by every plan.</summary>
        ParkLayout Finish()
        {
            float band = (Loop + PathWidth / 2 + Half - HedgeInset - HedgeThick / 2) / 2;
            var trees = new List<Tree>(Trees);
            int n = 0;
            for (int side = 0; side < 4; side++)
                for (float t = -band; t <= band + .01f; t += BandTreeSpacing)
                {
                    // Corners belong to the north and south sides; the gates stay clear.
                    bool corner = Mathf.Abs(t) > band - .05f;
                    if (Mathf.Abs(t) < GateHalf + .2f || (corner && side % 2 == 1))
                        continue;
                    var along = side % 2 == 0 ? V(t, 0) : V(0, t);
                    var at = Heading(side) * band + along;
                    trees.Add(new Tree { at = at, scale = corner ? 1.15f : .8f + .1f * (n % 3), round = !corner && n % 3 != 1 });
                    n++;
                }
            Trees = trees.ToArray();
            var lamps = new List<Vector3>();
            float offset = PathWidth / 2 + .05f;
            for (int d = 0; d < 4; d++)
            {
                var heading = Heading(d);
                var across = V(heading.z, -heading.x);
                lamps.Add(heading * (Plaza + .08f) + across * offset);
                lamps.Add(heading * (Half - HedgeInset - .16f) - across * offset);
            }
            Lamps = lamps.ToArray();
            LoopRoute = new[] { V(Loop, Loop), V(Loop, -Loop), V(-Loop, -Loop), V(-Loop, Loop) };
            Crossings = new[] { Via(0, 2, 1), Via(3, 1, 1), Via(0, 3, -1), Via(1, 2, 1), Via(2, 0, 1), Via(1, 3, 1) };
            return this;
        }
        /// <summary>From gate <paramref name="from"/> to gate <paramref name="to"/>, round the fountain the <paramref name="way"/> (+1 clockwise from above).</summary>
        Vector3[] Via(int from, int to, int way)
        {
            var route = new List<Vector3> { Gate(from) };
            float start = from * 90f, end = to * 90f;
            float sweep = Mathf.Repeat((end - start) * way, 360f);
            for (float a = 0; a <= sweep + .01f; a += ArcStep)
            {
                float angle = (start + a * way) * Mathf.Deg2Rad;
                route.Add(V(Mathf.Sin(angle), Mathf.Cos(angle)) * Ring);
            }
            route.Add(Gate(to));
            return route.ToArray();
        }
        /// <summary>A point on the water's ellipse, <paramref name="fraction"/> of the way out from its centre at <paramref name="angle"/> degrees.</summary>
        public Vector3 OnPond(float angle, float fraction, float y = 0)
        {
            float a = angle * Mathf.Deg2Rad;
            return new Vector3(Pond.center.x + Mathf.Sin(a) * Pond.width / 2 * fraction, y, Pond.center.y + Mathf.Cos(a) * Pond.height / 2 * fraction);
        }
        /// <summary>Where swing <paramref name="i"/> hangs from the frame's bar, counted from the west end.</summary>
        public Vector3 SwingPivot(int i) => SwingFrame + new Vector3((i - (Swings - 1) / 2f) * SwingPitch, SwingBar, 0);
    }
}
