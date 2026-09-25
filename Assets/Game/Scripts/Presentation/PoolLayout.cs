using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// Shared measurements of the four open-air pools, one plan per footprint, in the lot frame: origin on the lot's
    /// centre, x across the frontage, +z towards the street (rect y is lot z). A car park runs along the street (one row of
    /// bays on the small lots, two on the big ones); behind its fence lie the changing rooms, loungers, lawn and pools:
    /// <list type="bullet">
    /// <item>2×2 splash pool: two lanes, a changing hut and towels on the deck.</item>
    /// <item>3×3 community pool: three lanes, a paddling pool, loungers, a lawn and a lifeguard.</item>
    /// <item>4×4 lido: five lanes, a diving board, a paddling pool, loungers and a lawn.</item>
    /// <item>5×5 water park: six lanes and a diving board, a paddling pool, and a slide tower whose chute drops into a splash pool.</item>
    /// </list>
    /// <c>PoolArt</c> draws from these numbers and <see cref="PoolLife"/> moves cars and people on them. Every member is an
    /// instance member so both read one plan without caring which it is.
    /// </summary>
    public sealed class PoolLayout
    {
        public struct Bay
        {
            public Vector3 at, facing;
        }
        public const int MinSize = 2, MaxSize = 5;
        static readonly PoolLayout[] plans = { null, null, Splash(), Community(), Lido(), WaterPark() };
        /// <summary>The plan for a pool of <paramref name="size"/> cells per side (clamped to 2–5).</summary>
        public static PoolLayout For(int size) => plans[Mathf.Clamp(size, MinSize, MaxSize)];

        public readonly int Size;
        public readonly float Half;
        /// <summary>Tops of the car park asphalt, the pool deck, the water and the coping round each pool, and of the street.</summary>
        public readonly float ParkTop = .025f, DeckTop = .03f, WaterTop = .036f, CopingTop = .05f, StreetTop = .026f;
        public readonly float LoungerTop = .075f, BoardTop = .13f, ChairTop = .36f;

        // The car park, between the street hedge and the pool fence.
        public readonly bool TwoRows;
        public readonly float Hedge, Fence, BackRow, Aisle, FrontRow;
        public readonly float BayDepth = .46f, BayPitch = .3f, GateHalf = .3f, WalkHalf = .2f;
        public readonly int BaysPerRow;
        /// <summary>Cars keep to the lot side of the street: a lane this far from the lot's middle.</summary>
        public readonly float StreetLane;

        // The enclosure behind the fence.
        /// <summary>The path from the car park through the fence (and the zebra across the car park) at this x.</summary>
        public float EntranceX { get; private set; }
        /// <summary>The walk by the lawn and the walk along the pool; equal on the splash pool, which has one walk.</summary>
        public float Promenade { get; private set; }
        public float PoolSide { get; private set; }
        public Rect Building { get; private set; }
        public Rect Lawn { get; private set; }
        public Rect Pool { get; private set; }
        public int Lanes { get; private set; }
        public readonly float Coping = .05f, LaneWidth = .2f;
        public float SwimFrom { get; private set; }
        public float SwimTo { get; private set; }
        public float[] Ladders { get; private set; }
        public Rect Paddling { get; private set; }
        public bool HasBoard { get; private set; }
        public float BoardBack { get; private set; }
        public float BoardTip { get; private set; }
        public float BoardFoot { get; private set; }
        public float[] Loungers { get; private set; }
        public float LoungerRow { get; private set; }
        public readonly float LoungerPitch = .28f, LoungerLength = .24f;
        public Vector3[] Parasols { get; private set; }
        public bool HasChair { get; private set; }
        public Vector3 Chair { get; private set; }
        public Vector3 ChairFacing { get; private set; }
        public Vector3[] Towels { get; private set; }
        /// <summary>Gaps between the loungers where people cross from one walk to the other.</summary>
        public float[] CrossWalks { get; private set; }
        // The water park's slide: the splash pool it drops into, its ladder, the tower, and the chute (with heights).
        public Rect Leisure { get; private set; }
        public float LeisureLadder { get; private set; }
        public Rect Tower { get; private set; }
        public float TowerTop { get; private set; }
        public Vector3 SlideFoot { get; private set; }
        public Vector3[] Chute { get; private set; }

        public bool HasLawn => Lawn.width > 0;
        public bool HasPaddling => Paddling.width > 0;
        public bool HasSlide => Chute != null;

        PoolLayout(int size, bool twoRows, int baysPerRow)
        {
            Size = size;
            Half = size / 2f;
            TwoRows = twoRows;
            BaysPerRow = baysPerRow;
            Hedge = Half - .05f;
            // Two rows: back row, aisle, front row. One row: back row and the aisle along the hedge.
            Fence = Half - (twoRows ? 1.4f : .9f);
            BackRow = Fence + .04f + BayDepth / 2;
            Aisle = Fence + .04f + BayDepth + .17f;
            FrontRow = twoRows ? Aisle + .17f + BayDepth / 2 : float.NaN;
            StreetLane = Half + .5f - .2f;
            Ladders = new float[0];
            Loungers = new float[0];
            Parasols = new Vector3[0];
            Towels = new Vector3[0];
            CrossWalks = new float[0];
        }
        /// <summary>Loungers every pitch from <paramref name="start"/>; a parasol shades each pair.</summary>
        void LoungerRun(float start, int count, float row)
        {
            LoungerRow = row;
            Loungers = new float[count];
            for (int i = 0; i < count; i++)
                Loungers[i] = start + i * LoungerPitch;
            Parasols = new Vector3[count / 2];
            for (int pair = 0; pair < count / 2; pair++)
                Parasols[pair] = new Vector3(Loungers[2 * pair] + LoungerPitch / 2, 0, row + .02f);
        }
        void Lanes_(int lanes, float xMin, float xMax, float top)
        {
            Lanes = lanes;
            Pool = Rect.MinMaxRect(xMin, top - lanes * LaneWidth - .08f, xMax, top);
            SwimFrom = xMin + .12f;
            SwimTo = xMax - .12f;
            Ladders = new[] { xMin + .17f, xMax - .17f };
        }
        void Board(float back, float tip)
        {
            HasBoard = true;
            BoardBack = back;
            BoardTip = tip;
            BoardFoot = back + .09f;
        }

        static PoolLayout Splash()
        {
            var p = new PoolLayout(2, false, 6) { EntranceX = -.45f, Promenade = -.24f, PoolSide = -.24f };
            p.Lanes_(2, -.86f, .42f, -.38f);
            p.Building = Rect.MinMaxRect(.55f, -.9f, .92f, -.3f);
            p.Towels = new[] { new Vector3(-.8f, 0, -.07f), new Vector3(-.1f, 0, -.07f), new Vector3(.25f, 0, -.07f) };
            p.Parasols = new[] { new Vector3(-.62f, 0, -.02f), new Vector3(.08f, 0, -.02f) };
            return p;
        }
        static PoolLayout Community()
        {
            var p = new PoolLayout(3, false, 9) { EntranceX = -.6f, Promenade = 0, PoolSide = -.46f };
            p.Building = Rect.MinMaxRect(-1.42f, .1f, -.76f, .56f);
            p.Lawn = Rect.MinMaxRect(-.44f, .1f, 1.42f, .56f);
            p.Towels = new[] { new Vector3(0, 0, .33f), new Vector3(.5f, 0, .3f), new Vector3(1f, 0, .34f) };
            p.LoungerRun(-1.02f, 6, -.22f);
            p.Lanes_(3, -1.3f, .5f, -.56f);
            p.Paddling = Rect.MinMaxRect(.78f, -1.2f, 1.32f, -.66f);
            p.HasChair = true;
            p.Chair = new Vector3(1.36f, 0, -.3f);
            p.ChairFacing = Vector3.back;
            p.CrossWalks = new[] { -1.3f, -.6f, .7f };
            return p;
        }
        static PoolLayout Lido()
        {
            var p = new PoolLayout(4, true, 12) { EntranceX = -.47f, Promenade = -.1f, PoolSide = -.56f };
            p.Building = Rect.MinMaxRect(-1.88f, 0, -.72f, .56f);
            p.Lawn = Rect.MinMaxRect(-.28f, 0, 1.9f, .56f);
            p.Towels = new[] { new Vector3(.05f, 0, .3f), new Vector3(.5f, 0, .24f), new Vector3(.95f, 0, .32f), new Vector3(1.4f, 0, .26f) };
            p.LoungerRun(-1.45f, 8, -.32f);
            p.Lanes_(5, -1.62f, .92f, -.66f);
            p.Board(-1.86f, -1.46f);
            p.Paddling = Rect.MinMaxRect(1.18f, -1.62f, 1.78f, -.86f);
            p.HasChair = true;
            p.Chair = new Vector3(1f, 0, -.3f);
            p.ChairFacing = Vector3.back;
            p.CrossWalks = new[] { -1.65f, -.47f, .8f };
            return p;
        }
        static PoolLayout WaterPark()
        {
            var p = new PoolLayout(5, true, 16) { EntranceX = -.75f, Promenade = .4f, PoolSide = -.06f };
            p.Building = Rect.MinMaxRect(-2.4f, .5f, -.95f, 1.06f);
            p.Lawn = Rect.MinMaxRect(-.55f, .5f, 2.42f, 1.06f);
            p.Towels = new[] { new Vector3(-.25f, 0, .8f), new Vector3(.25f, 0, .76f), new Vector3(.75f, 0, .82f), new Vector3(1.25f, 0, .76f), new Vector3(1.75f, 0, .8f) };
            p.LoungerRun(-2.29f, 14, .18f);
            p.Lanes_(6, -2.1f, .5f, -.16f);
            p.Board(-2.42f, -2f);
            p.Paddling = Rect.MinMaxRect(1.35f, -.8f, 2.25f, -.26f);
            p.HasChair = true;
            p.Chair = new Vector3(.7f, 0, -.62f);
            p.ChairFacing = Vector3.left;
            p.CrossWalks = new[] { -.75f, .93f, 2.38f };
            p.Leisure = Rect.MinMaxRect(.85f, -2.25f, 1.95f, -1.05f);
            p.LeisureLadder = 1f;
            p.Tower = Rect.MinMaxRect(2.01f, -2.05f, 2.31f, -1.75f);
            p.TowerTop = 1f;
            p.SlideFoot = new Vector3(2.38f, 0, -1.9f);
            p.Chute = new[]
            {
                new Vector3(2.16f, 1f, -1.9f), new Vector3(2.16f, .95f, -1.66f), new Vector3(2.3f, .75f, -1.25f), new Vector3(2.05f, .55f, -.98f),
                new Vector3(1.62f, .33f, -1.02f), new Vector3(1.35f, .15f, -1.3f), new Vector3(1.25f, .06f, -1.55f),
            };
            return p;
        }

        public float LaneZ(int lane) => Pool.center.y + (lane - (Lanes - 1) / 2f) * LaneWidth;
        /// <summary>Column centres of the lot: the street cells in front of the lot sit at these x.</summary>
        public float Column(int k) => k - (Size - 1) / 2f;
        public Vector3 Ladder(int i) => new Vector3(Ladders[i], 0, Pool.yMax);
        public float BayX(int i) => (i - (BaysPerRow - 1) / 2f) * BayPitch;

        /// <summary>
        /// Every bay: the back row (noses to the pool) leaves the zebra walkway free; on big lots the front row (noses to the
        /// street) leaves the driveway at <paramref name="gateX"/> free.
        /// </summary>
        public List<Bay> Bays(float gateX)
        {
            var bays = new List<Bay>(2 * BaysPerRow);
            for (int i = 0; i < BaysPerRow; i++)
            {
                float x = BayX(i);
                if (Mathf.Abs(x - EntranceX) >= WalkHalf)
                    bays.Add(new Bay { at = new Vector3(x, ParkTop, BackRow), facing = Vector3.back });
                if (TwoRows && Mathf.Abs(x - gateX) >= GateHalf)
                    bays.Add(new Bay { at = new Vector3(x, ParkTop, FrontRow), facing = Vector3.forward });
            }
            return bays;
        }
    }
}
