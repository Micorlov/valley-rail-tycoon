using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// Shared measurements of the 6×6 city zoo, in the lot frame: origin on the lot's centre, x across the frontage, +z
    /// towards the street. A sandstone wall runs round it with the gate (a "ZOO" arch and two ticket booths) in the street
    /// side. Inside, four gravel aisles, two across and two along at ±<see cref="Aisle"/>, frame the monkey island in the
    /// middle and give every pen a path to look from:
    /// <list type="bullet">
    /// <item>street side: the penguin pool, the entrance plaza (an elephant statue and an ice-cream kiosk), the flamingo lagoon;</item>
    /// <item>middle: the elephant yard, the monkey island in its moat, the lions' rock;</item>
    /// <item>far side: the savanna, with giraffes and zebras, acacias, a watering hole and a giraffe-feeding deck.</item>
    /// </list>
    /// A miniature train runs round the outside of the pens between two platforms either side of the plaza.
    /// <c>ZooArt</c> draws from these numbers and <see cref="ZooLife"/> moves the animals, the visitors and the train on them.
    /// </summary>
    public sealed class ZooLayout
    {
        public enum Exhibit { Penguins, Flamingos, Elephants, Monkeys, Lions, Savanna }
        /// <summary>A box standing on <see cref="at"/> (its bottom centre).</summary>
        public struct Block
        {
            public Vector3 at, size;
        }
        /// <summary>A place to stand or sit and the way to look from it.</summary>
        public struct Spot
        {
            public Vector3 at, facing;
        }
        public struct Tree
        {
            public Vector3 at;
            public float scale;
        }

        public const int Size = 6;
        /// <summary>The only zoo plan: the catalog's zoo is 6×6.</summary>
        public static readonly ZooLayout Plan = new ZooLayout();

        public readonly float Half = Size / 2f;
        /// <summary>Tops of the lawn, the pens' ground, the aisles, the plaza, water banks and water.</summary>
        public readonly float GroundTop = .03f, PenTop = .033f, PathTop = .036f, PlazaTop = .038f, BankTop = .036f, WaterTop = .04f;
        public readonly float WallInset = .04f, WallThick = .07f, WallHeight = .12f, GateHalf = .22f;
        /// <summary>The centre line of the boundary wall.</summary>
        public float WallLine => Half - WallInset - WallThick / 2;
        public readonly float PathWidth = .22f, Aisle = .95f, AisleEnd = 2.4f, PlatformEdge = 2.44f;
        /// <summary>Where people stop to look over a pen's fence: this far from an aisle's centre line.</summary>
        public float Railside => PathWidth / 2 + .015f;

        public readonly Rect[] Pens =
        {
            Span(-2.4f, -1.1f, 1.1f, 2.4f), Span(1.1f, 2.4f, 1.1f, 2.4f), Span(-2.4f, -1.1f, -.8f, .8f),
            Span(-.8f, .8f, -.8f, .8f), Span(1.1f, 2.4f, -.8f, .8f), Span(-2.4f, 2.4f, -2.4f, -1.1f),
        };
        /// <summary>Fence heights, in <see cref="Exhibit"/> order: glass for the penguins, a tall mesh for the lions.</summary>
        public readonly float[] FenceHeights = { .08f, .07f, .12f, .06f, .15f, .1f };
        public Rect Pen(Exhibit e) => Pens[(int)e];
        public readonly Rect Plaza;

        // ---- The zoo train: a U-shaped line round the pens, from a platform west of the plaza to one east of it. -------
        public readonly float TrackLine = 2.64f, TrackCorner = .42f, StubEnd = .88f, Gauge = .07f, Ballast = .17f, PlatformTop = .07f;
        public readonly Rect[] Platforms = { Span(-2.18f, -.86f, 2.44f, 2.55f), Span(.86f, 2.18f, 2.44f, 2.55f) };
        /// <summary>Loco, two open coaches, loco; the gap between cars.</summary>
        public readonly float[] CarLengths = { .28f, .3f, .3f, .28f };
        public readonly float CarGap = .03f;
        public readonly Vector3[] Track;
        public readonly float TrackLength, TrainLength;
        /// <summary>Distances along the track of the train's west end when it stands at the west and at the east platform.</summary>
        public readonly float WestStop, EastStop;

        // ---- Penguins: an ice-white pen, a pool and a diving rock. ------------------------------------------------------
        public readonly Rect PenguinPool = Area(-1.72f, 1.52f, .86f, .5f), PenguinShore = Area(-1.72f, 2.06f, 1.1f, .42f);
        public readonly Block[] PenguinRocks = { Box(-2.08f, 2.12f, .34f, .14f, .28f), Box(-1.42f, 2.2f, .24f, .08f, .2f) };
        public float PenguinSwim => WaterTop - .035f;
        public readonly Spot PenguinKeeper = Look(-1.25f, 1.95f, -1, 0);
        /// <summary>The diver's lap: off the big rock, a swim, out onto the shore and back up the rock.</summary>
        public readonly Vector3[] DiveRoute;
        public readonly float[] DiveSpeeds = { .5f, .7f, .22f, .22f, .1f, .08f, .08f, .1f };

        // ---- Flamingos ---------------------------------------------------------------------------------------------
        public readonly Rect Lagoon = Area(1.74f, 1.66f, 1f, .74f);
        public readonly Tree FlamingoTree = Grow(2.22f, 2.24f, .8f);

        // ---- Elephants ---------------------------------------------------------------------------------------------
        public readonly Rect ElephantPool = Area(-2f, -.4f, .52f, .52f), ElephantYard = Span(-2.16f, -1.34f, -.56f, .56f);
        public readonly Block HayRack = Box(-2.2f, .5f, .1f, .14f, .28f), Boulder = Box(-1.36f, -.52f, .16f, .1f, .14f);
        public readonly Tree ElephantTree = Grow(-1.42f, .5f, 1.1f);

        // ---- Monkeys: a grass island in a round moat, three climbing poles with platforms and ropes, a hut. ------------
        public readonly float MoatRadius = .72f, IslandRadius = .46f, IslandTop = .07f, RunRing = .36f;
        /// <summary>The climbing poles: x and z of the foot, y the height of its platform above the island.</summary>
        public readonly Vector3[] Poles = { Pole(30, .5f), Pole(150, .42f), Pole(270, .56f) };
        public readonly Block MonkeyHouse = Box(0, 0, .14f, .1f, .12f);

        // ---- Lions -------------------------------------------------------------------------------------------------
        public readonly Rect LionYard = Span(1.26f, 2.24f, -.64f, .64f);
        public readonly Block[] LionRock = { Box(2.02f, .3f, .56f, .12f, .46f), Box(2.06f, .34f, .36f, .1f, .3f) };
        public Vector3 LionPerch => new Vector3(2.06f, PenTop + .22f, .34f);
        public readonly Vector3 LogFrom = V(1.45f, -.46f), LogTo = V(1.8f, -.58f);
        public readonly Tree LionTree = Grow(2.2f, -.52f, .9f);
        /// <summary>The lion pacing up and down in front of the visitors on the east aisle.</summary>
        public readonly Vector3[] LionBeat = { V(1.3f, -.56f), V(1.3f, .56f) };
        public readonly Spot[] LionBeds = { Look(1.55f, .28f, -1, 0), Look(1.66f, -.2f, -.6f, .8f) };
        /// <summary>The cub plays round its mother on the second bed.</summary>
        public readonly float CubRing = .19f;

        // ---- The savanna -------------------------------------------------------------------------------------------
        public readonly Rect SavannaYard = Span(-2.16f, 2.16f, -2.16f, -1.34f), Waterhole = Area(1f, -1.9f, .7f, .38f);
        public readonly Tree[] Acacias = { Grow(-1.72f, -1.95f, 1.1f), Grow(1.9f, -1.55f, .95f), Grow(-.35f, -2.05f, 1f) };
        public readonly Block[] SavannaRocks = { Box(-1.05f, -2.18f, .18f, .08f, .14f), Box(2.12f, -2.14f, .2f, .1f, .16f) };
        /// <summary>The raised timber deck on the savanna's fence where visitors feed the giraffes.</summary>
        public readonly Rect Deck = Area(0, -1.2f, .6f, .24f);
        public readonly float DeckTop = .14f;
        /// <summary>Where a giraffe stands to take leaves from the deck.</summary>
        public readonly Spot FeedingSpot = Look(0, -1.52f, 0, 1);

        // ---- The entrance plaza ------------------------------------------------------------------------------------
        public readonly Block Plinth = Box(-.5f, 1.62f, .3f, .1f, .22f);
        public readonly Rect Kiosk = Area(.55f, 1.62f, .22f, .26f);
        public readonly float KioskHeight = .24f, BoothHeight = .22f;
        public Vector3 Hatch => V(Kiosk.xMin, Kiosk.center.y);
        public readonly Vector3 HatchFacing = V(-1, 0);
        public readonly Rect[] Booths = { Area(-.5f, 2.64f, .26f, .22f), Area(.5f, 2.64f, .26f, .22f) };
        public readonly Spot[] Beds = { Look(-.52f, 2.2f, 0, 1), Look(.52f, 2.2f, 0, 1) };

        // ---- Furniture and the visitors' places --------------------------------------------------------------------
        public readonly Spot[] Benches = { Look(-.62f, 1.95f, 1, 0), Look(.62f, 1.95f, -1, 0), Look(-1.9f, 2.47f, 0, 1), Look(1.9f, 2.47f, 0, 1) };
        public readonly Vector3[] Lamps =
        {
            V(-1.75f, .82f), V(1.75f, .82f), V(-1.75f, -1.08f), V(1.75f, -1.08f), V(-.45f, -1.08f), V(.45f, -1.08f), V(-.2f, 1.3f), V(.2f, 1.3f),
        };
        /// <summary>Spots at the pens' railings where visitors stop to watch, facing the animals.</summary>
        public readonly Spot[] Viewpoints;
        /// <summary>Visitors feeding the giraffes from the deck.</summary>
        public readonly Spot[] Feeders;
        /// <summary>Where people wait for the train on each platform.</summary>
        public readonly Spot[] Waiting = { Look(-1.2f, 2.5f, 0, 1), Look(-1.62f, 2.5f, 0, 1), Look(1.3f, 2.5f, 0, 1), Look(1.7f, 2.5f, 0, 1) };

        // ---- Walks -------------------------------------------------------------------------------------------------
        /// <summary>Walks along the aisles, followed there and back with a pause at each end.</summary>
        public readonly Vector3[][] Walks;
        /// <summary>The aisles round the monkey island, walked round and round.</summary>
        public readonly Vector3[] Round;
        public Vector3 Gate => V(0, Half - .04f);

        ZooLayout()
        {
            Plaza = Span(-.84f, .84f, Aisle + PathWidth / 2, WallLine - WallThick / 2);
            Track = TrackRoute();
            for (int i = 0; i + 1 < Track.Length; i++)
                TrackLength += Vector3.Distance(Track[i], Track[i + 1]);
            TrainLength = CarGap * (CarLengths.Length - 1);
            foreach (float car in CarLengths)
                TrainLength += car;
            WestStop = .02f;
            EastStop = TrackLength - .02f - TrainLength;
            float top = PenTop + PenguinRocks[0].size.y;
            DiveRoute = new[]
            {
                new Vector3(-2.02f, top, 2f), new Vector3(-1.96f, top + .09f, 1.86f), new Vector3(-1.9f, PenguinSwim, 1.66f), new Vector3(-1.5f, PenguinSwim, 1.48f),
                new Vector3(-1.5f, PenguinSwim, 1.68f), new Vector3(-1.52f, PenTop, 1.86f), new Vector3(-1.8f, PenTop, 1.9f), new Vector3(-1.87f, PenTop, 2.08f),
            };
            Viewpoints = BuildViewpoints();
            Feeders = new[] { Stand(-.18f, -1.24f), Stand(.02f, -1.2f), Stand(.16f, -1.27f) };
            float a = Aisle, end = AisleEnd - .05f, north = PlatformEdge - .02f;
            var gate = Gate;
            Walks = new[]
            {
                new[] { gate, V(0, a), V(-a, a), V(-a, -a), V(-end, -a) },
                new[] { gate, V(0, a), V(a, a), V(a, -a), V(end, -a) },
                new[] { V(-end, a), V(end, a) },
                new[] { V(-end, -a), V(end, -a) },
                new[] { V(-a, north), V(-a, -a), V(a, -a), V(a, north) },
                new[] { gate, V(0, a), V(-a, a), V(-a, north) },
                new[] { gate, V(0, a), V(a, a), V(a, north) },
            };
            Round = new[] { V(a, a), V(a, -a), V(-a, -a), V(-a, a) };
        }

        static Vector3 V(float x, float z) => new Vector3(x, 0, z);
        static Rect Span(float x0, float x1, float z0, float z1) => new Rect(x0, z0, x1 - x0, z1 - z0);
        static Rect Area(float x, float z, float width, float depth) => new Rect(x - width / 2, z - depth / 2, width, depth);
        static Block Box(float x, float z, float width, float height, float depth) => new Block { at = V(x, z), size = new Vector3(width, height, depth) };
        static Spot Look(float x, float z, float fx, float fz) => new Spot { at = V(x, z), facing = V(fx, fz).normalized };
        static Tree Grow(float x, float z, float scale) => new Tree { at = V(x, z), scale = scale };
        static Vector3 Pole(float degrees, float height)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * .25f, height, Mathf.Cos(a) * .25f);
        }
        Spot Stand(float x, float z) => new Spot { at = new Vector3(x, DeckTop, z), facing = V(0, -1) };

        Spot[] BuildViewpoints()
        {
            float near = Aisle - Railside, far = Aisle + Railside;
            return new[]
            {
                // Penguins and flamingos, from the north aisle and the aisles along.
                Look(-1.5f, far, 0, 1), Look(-1.9f, far, 0, 1), Look(-far, 1.62f, -1, 0),
                Look(1.45f, far, 0, 1), Look(1.9f, far, 0, 1), Look(far, 1.85f, 1, 0),
                // Elephants.
                Look(-1.6f, near, 0, -1), Look(-2.05f, near, 0, -1), Look(-1.8f, -near, 0, 1), Look(-far, -.1f, -1, 0),
                // Monkeys, from all four sides of their island.
                Look(-.4f, near, 0, -1), Look(.35f, near, 0, -1), Look(.25f, -near, 0, 1), Look(-.45f, -near, 0, 1), Look(near, .3f, -1, 0), Look(-near, -.2f, 1, 0),
                // Lions.
                Look(far, .3f, 1, 0), Look(far, -.4f, 1, 0), Look(1.85f, near, 0, -1), Look(1.6f, -near, 0, 1),
                // The savanna, along the far aisle.
                Look(-1.9f, -far, 0, -1), Look(-1.2f, -far, 0, -1), Look(.85f, -far, 0, -1), Look(1.6f, -far, 0, -1),
            };
        }

        /// <summary>The track's centre line: west from the west platform, round three sides of the zoo and back east to the east platform.</summary>
        Vector3[] TrackRoute()
        {
            float t = TrackLine, r = TrackCorner;
            var points = new List<Vector3> { V(-StubEnd, t) };
            Corner(points, V(-t + r, t - r), r, 90, 180);
            Corner(points, V(-t + r, -t + r), r, 180, 270);
            Corner(points, V(t - r, -t + r), r, 270, 360);
            Corner(points, V(t - r, t - r), r, 0, 90);
            points.Add(V(StubEnd, t));
            return points.ToArray();
        }
        static void Corner(List<Vector3> points, Vector3 centre, float radius, float from, float to)
        {
            for (float a = from; a <= to + .01f; a += 15)
                points.Add(centre + V(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * radius);
        }
        /// <summary>The point <paramref name="distance"/> along the track (clamped to its ends) and the way the track runs there.</summary>
        public Vector3 OnTrack(float distance, out Vector3 along)
        {
            float left = Mathf.Clamp(distance, 0, TrackLength);
            for (int i = 0; i + 1 < Track.Length; i++)
            {
                Vector3 a = Track[i], b = Track[i + 1];
                float leg = Vector3.Distance(a, b);
                if (left <= leg || i + 2 == Track.Length)
                {
                    along = (b - a).normalized;
                    return a + along * Mathf.Min(left, leg);
                }
                left -= leg;
            }
            along = Vector3.left;
            return Track[0];
        }
        /// <summary>Distance from the train's west end to the middle of car <paramref name="car"/>.</summary>
        public float CarOffset(int car)
        {
            float offset = CarLengths[car] / 2;
            for (int i = 0; i < car; i++)
                offset += CarLengths[i] + CarGap;
            return offset;
        }
        /// <summary>How far <paramref name="at"/> lies from the track's centre line, on the ground.</summary>
        public float FromTrack(Vector3 at)
        {
            float best = float.MaxValue;
            var flat = V(at.x, at.z);
            for (int i = 0; i + 1 < Track.Length; i++)
            {
                Vector3 a = Track[i], b = Track[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(flat - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector3.Distance(flat, a + ab * t));
            }
            return best;
        }

        /// <summary>On an aisle, the entrance path or the plaza, with <paramref name="slack"/> of verge.</summary>
        public bool OnPath(Vector3 at, float slack = .01f)
        {
            float half = PathWidth / 2 + slack, x = Mathf.Abs(at.x), z = at.z;
            bool across = Mathf.Abs(Mathf.Abs(z) - Aisle) < half && x <= AisleEnd + half;
            bool along = Mathf.Abs(x - Aisle) < half && z >= -Aisle - half && z <= PlatformEdge + half;
            bool entrance = x < half && z >= Aisle - half && z <= Half;
            return across || along || entrance || Plaza.Contains(new Vector2(at.x, at.z));
        }
        /// <summary>Inside the ellipse that fills <paramref name="area"/>, shrunk to <paramref name="fraction"/> of its size.</summary>
        public static bool InOval(Rect area, Vector3 at, float fraction = 1)
        {
            float dx = (at.x - area.center.x) / (area.width / 2 * fraction), dz = (at.z - area.center.y) / (area.height / 2 * fraction);
            return dx * dx + dz * dz <= 1;
        }
        /// <summary>A point on the ellipse filling <paramref name="area"/>, <paramref name="fraction"/> of the way out at <paramref name="degrees"/>.</summary>
        public static Vector3 OnOval(Rect area, float degrees, float fraction, float y = 0)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(area.center.x + Mathf.Sin(a) * area.width / 2 * fraction, y, area.center.y + Mathf.Cos(a) * area.height / 2 * fraction);
        }
    }
}
