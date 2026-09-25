using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// The four open-air pools, drawn from their <see cref="PoolLayout"/> plan: a car park along the street, then behind a
    /// white fence the changing rooms, loungers under parasols, a lawn with towels, the lane pool (with a diving board on the
    /// big ones), a paddling pool with a mushroom fountain, a lifeguard's chair and, at the water park, a slide tower whose
    /// chute drops into a splash pool. <see cref="PoolLife"/> moves the cars and swimmers on the same plan; each pool built
    /// in the town pass is handed on as a venue.
    /// </summary>
    public sealed partial class WorldView
    {
        // Literal colours only: statics of other partial files may not be initialised yet when this file's run.
        static readonly Color PoolWater = new Color(.24f, .7f, .86f), PaddlingWater = new Color(.46f, .83f, .9f), LeisureWater = new Color(.3f, .78f, .9f),
            PoolLine = new Color(.1f, .4f, .64f), PoolTile = new Color(.88f, .86f, .8f), PoolCoping = new Color(.97f, .97f, .95f), RopeRed = new Color(.86f, .16f, .14f),
            RopeWhite = new Color(.96f, .96f, .96f), RopeBlue = new Color(.16f, .36f, .78f), PoolBand = new Color(.16f, .55f, .74f), Chrome = new Color(.78f, .8f, .83f),
            BoardOrange = new Color(.96f, .58f, .16f), ChuteYellow = new Color(.99f, .78f, .16f), ChuteRed = new Color(.9f, .3f, .18f);
        static readonly Color[] Cushions = { new Color(.16f, .42f, .78f), new Color(.98f, .78f, .2f) };
        static readonly Color[] Parasols = { new Color(.86f, .18f, .16f), new Color(.16f, .5f, .8f), new Color(.98f, .72f, .16f), new Color(.2f, .66f, .52f) };
        static readonly Color[] Towels = { new Color(.95f, .4f, .6f), new Color(.2f, .7f, .8f), new Color(.98f, .8f, .22f), new Color(.6f, .4f, .85f) };
        const float FenceHeight = .12f, FenceThick = .025f, ChuteWidth = .14f;
        readonly List<PoolLife.Venue> lidos = new List<PoolLife.Venue>();
        PoolLife pools;

        float DrawLido(Lot lot, int size, float h)
        {
            var L = PoolLayout.For(size);
            float gate = LidoGate(lot, L, out bool fromWest, out bool toEast);
            LidoCarPark(lot, L, gate);
            LidoEnclosure(lot, L);
            LidoPools(lot, L);
            LidoDeck(lot, L);
            float top = LidoBuilding(lot, L, h);
            if (L.HasSlide)
                top = Mathf.Max(top, LidoSlide(lot, L));
            // Only the town pass brings a pool to life, not a ghost drawn for a station upgrade.
            if (batch != null && batchTarget == cityRoot)
                lidos.Add(new PoolLife.Venue(lot.at, lot.turn, lot.hash, L.Size, gate, fromWest, toEast));
            return top;
        }
        /// <summary>
        /// The front column whose street cell cars can best reach: one with street on both sides beats a dead end, ties go
        /// east. Also reports whether cars can come in from the west along the street and drive on east afterwards.
        /// </summary>
        float LidoGate(Lot lot, PoolLayout L, out bool fromWest, out bool toEast)
        {
            int best = L.Size - 1, bestScore = -1;
            for (int k = 0; k < L.Size; k++)
            {
                float x = L.Column(k);
                if (!StreetAhead(lot, L, x))
                    continue;
                int score = (StreetAhead(lot, L, x - 1) ? 2 : 0) + (StreetAhead(lot, L, x + 1) ? 2 : 0);
                if (score >= bestScore)
                {
                    best = k;
                    bestScore = score;
                }
            }
            float gate = L.Column(best);
            fromWest = StreetAhead(lot, L, gate - 1);
            toEast = StreetAhead(lot, L, gate + 1);
            return gate;
        }
        /// <summary>True when the cell just past the lot's street edge at lot-frame <paramref name="x"/> is a town street.</summary>
        bool StreetAhead(Lot lot, PoolLayout L, float x)
        {
            var p = lot.at + lot.turn * new Vector3(x, 0, L.Half + .5f);
            int cx = Mathf.RoundToInt(p.x), cz = Mathf.RoundToInt(p.z);
            return cx >= 0 && cz >= 0 && cx < MapDefinition.Size && cz < MapDefinition.Size && townStreets.Contains(new Cell(cx, cz).Key);
        }

        /// <summary>Asphalt with painted bays, a zebra from the pool gate, a hedge along the street and the driveway through it.</summary>
        void LidoCarPark(Lot lot, PoolLayout L, float gate)
        {
            float from = L.Fence, to = L.Hedge, span = L.Size - .08f;
            Part(lot, "Pool car park", new Vector3(0, L.ParkTop / 2, (from + to) / 2), new Vector3(span, L.ParkTop, to - from), Asphalt);
            Part(lot, "Driveway", new Vector3(gate, L.ParkTop / 2, (to + L.Half) / 2), new Vector3(2 * L.GateHalf, L.ParkTop, L.Half - to + .02f), Asphalt);
            float y = L.ParkTop + .002f, half = L.BayPitch * L.BaysPerRow / 2;
            for (int i = 0; i <= L.BaysPerRow; i++)
            {
                float x = -half + i * L.BayPitch;
                Part(lot, "Bay line", new Vector3(x, y, L.BackRow), new Vector3(.02f, .004f, L.BayDepth - .04f), White);
                // In the front row a line goes where it edges a real bay: none inside the driveway or past the row's end.
                if (L.TwoRows && !(GateBay(L, x - L.BayPitch / 2, gate, half) && GateBay(L, x + L.BayPitch / 2, gate, half)))
                    Part(lot, "Bay line", new Vector3(x, y, L.FrontRow), new Vector3(.02f, .004f, L.BayDepth - .04f), White);
            }
            for (float z = L.Fence + .1f; z < L.Aisle + .17f; z += .13f)
                Part(lot, "Walkway stripe", new Vector3(L.EntranceX, y, z), new Vector3(.24f, .004f, .06f), White);
            // Arrows down the aisle, pointing from the driveway both ways.
            for (int i = -1; i <= 1; i += 2)
                if (Mathf.Abs(gate + i * .55f) < L.Half - .2f)
                    Part(lot, "Aisle arrow", new Vector3(gate + i * .55f, y, L.Aisle), new Vector3(.18f, .004f, .03f), White);
            float hedgeZ = L.Hedge + .025f, edge = L.Half - .04f;
            if (gate - L.GateHalf > -edge)
                Part(lot, "Car park hedge", new Vector3((-edge + gate - L.GateHalf) / 2, .05f, hedgeZ), new Vector3(gate - L.GateHalf + edge, .1f, .06f), Hedge);
            if (gate + L.GateHalf < edge)
                Part(lot, "Car park hedge", new Vector3((edge + gate + L.GateHalf) / 2, .05f, hedgeZ), new Vector3(edge - gate - L.GateHalf, .1f, .06f), Hedge);
            for (int side = -1; side <= 1; side += 2)
                Part(lot, "Car park hedge", new Vector3(side * edge, .05f, (from + to) / 2), new Vector3(.06f, .1f, to - from), Hedge);
        }
        /// <summary>True when a front-row bay centred at <paramref name="x"/> is left out for the driveway (as PoolLayout.Bays does) or lies past the row.</summary>
        static bool GateBay(PoolLayout L, float x, float gate, float half) => Mathf.Abs(x - gate) < L.GateHalf || Mathf.Abs(x) > half;
        /// <summary>The tiled deck, the lawn and the path in, all behind a white railing with a gap for the path.</summary>
        void LidoEnclosure(Lot lot, PoolLayout L)
        {
            float back = -L.Half + .04f, edge = L.Half - .04f;
            Part(lot, "Pool deck", new Vector3(0, L.DeckTop / 2, (back + L.Fence) / 2), new Vector3(2 * edge, L.DeckTop, L.Fence - back), PoolTile);
            if (L.HasLawn)
            {
                var lawn = L.Lawn;
                Part(lot, "Sunbathing lawn", new Vector3(lawn.center.x, L.DeckTop + .003f, lawn.center.y), new Vector3(lawn.width, .006f, lawn.height), Grass);
            }
            Part(lot, "Path", new Vector3(L.EntranceX, L.DeckTop + .002f, (L.Promenade + L.Fence) / 2), new Vector3(.24f, .004f, L.Fence - L.Promenade), Stone);
            float y = FenceHeight / 2;
            Part(lot, "Pool fence", new Vector3(0, y, back), new Vector3(2 * edge, FenceHeight, FenceThick), PoolCoping);
            for (int side = -1; side <= 1; side += 2)
                Part(lot, "Pool fence", new Vector3(side * edge, y, (back + L.Fence) / 2), new Vector3(FenceThick, FenceHeight, L.Fence - back), PoolCoping);
            float gapWest = L.EntranceX - .15f, gapEast = L.EntranceX + .15f;
            Part(lot, "Pool fence", new Vector3((-edge + gapWest) / 2, y, L.Fence), new Vector3(gapWest + edge, FenceHeight, FenceThick), PoolCoping);
            Part(lot, "Pool fence", new Vector3((edge + gapEast) / 2, y, L.Fence), new Vector3(edge - gapEast, FenceHeight, FenceThick), PoolCoping);
            for (int i = 0; i < L.Towels.Length; i++)
                Part(lot, "Towel", L.Towels[i] + new Vector3(0, L.DeckTop + .008f, 0), new Vector3(.13f, .006f, .27f), Towels[(i + lot.hash) % Towels.Length]);
        }
        /// <summary>The pools: water inside a white coping, dark lane lines, the diving board and the paddling pool's mushroom.</summary>
        void LidoPools(Lot lot, PoolLayout L)
        {
            Basin(lot, L, L.Pool, PoolWater);
            float y = L.WaterTop + .001f;
            for (int lane = 0; lane < L.Lanes; lane++)
            {
                float z = L.LaneZ(lane);
                Part(lot, "Lane line", new Vector3((L.SwimFrom + L.SwimTo) / 2, y, z), new Vector3(L.SwimTo - L.SwimFrom, .002f, .025f), PoolLine);
                for (int end = -1; end <= 1; end += 2)
                    Part(lot, "Lane line", new Vector3(end < 0 ? L.SwimFrom : L.SwimTo, y, z), new Vector3(.025f, .002f, .09f), PoolLine);
            }
            if (L.HasBoard)
            {
                // A stand on the deck at the west end and a springy orange plank over the water.
                float mid = L.Pool.center.y, stand = L.Pool.xMin - L.Coping;
                Part(lot, "Diving stand", new Vector3((L.BoardBack + stand) / 2, .06f, mid), new Vector3(stand - L.BoardBack + .02f, .12f, .12f), Stone);
                Part(lot, "Diving board", new Vector3((L.BoardBack + L.BoardTip) / 2, L.BoardTop - .01f, mid), new Vector3(L.BoardTip - L.BoardBack, .02f, .09f), BoardOrange);
            }
            if (L.HasPaddling)
            {
                Basin(lot, L, L.Paddling, PaddlingWater);
                // The mushroom: a stem and a cap; its water jet plays when zoomed in.
                var mushroom = new Vector3(L.Paddling.center.x, 0, L.Paddling.center.y);
                Part(lot, "Mushroom stem", mushroom + new Vector3(0, .08f, 0), new Vector3(.025f, .16f, .025f), PoolCoping);
                Shape("Mushroom cap", cone, lot.at + lot.turn * (mushroom + new Vector3(0, .14f, 0)), new Vector3(.12f, .05f, .12f), RopeBlue, cityRoot, lot.turn);
                Emit(EmitterKind.Fountain, lot, mushroom + new Vector3(0, .19f, 0), new Color(.7f, .9f, 1f), .6f);
            }
            if (L.HasSlide)
                Basin(lot, L, L.Leisure, LeisureWater);
        }
        /// <summary>Water filling <paramref name="water"/> and four coping strips round it.</summary>
        void Basin(Lot lot, PoolLayout L, Rect water, Color color)
        {
            float c = L.Coping, top = L.CopingTop;
            Part(lot, "Pool water", new Vector3(water.center.x, L.WaterTop / 2, water.center.y), new Vector3(water.width, L.WaterTop, water.height), color);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(lot, "Coping", new Vector3(water.center.x, top / 2, water.center.y + side * (water.height + c) / 2), new Vector3(water.width + 2 * c, top, c), PoolCoping);
                Part(lot, "Coping", new Vector3(water.center.x + side * (water.width + c) / 2, top / 2, water.center.y), new Vector3(c, top, water.height), PoolCoping);
            }
        }
        /// <summary>Loungers in pairs under parasols along the pool, and the lifeguard's tall chair.</summary>
        void LidoDeck(Lot lot, PoolLayout L)
        {
            for (int i = 0; i < L.Loungers.Length; i++)
            {
                var at = new Vector3(L.Loungers[i], 0, L.LoungerRow);
                var cushion = Cushions[i / 2 % Cushions.Length];
                Part(lot, "Lounger", at + new Vector3(0, .05f, 0), new Vector3(.11f, .02f, L.LoungerLength), PoolCoping);
                Part(lot, "Lounger cushion", at + new Vector3(0, L.LoungerTop - .007f, -.02f), new Vector3(.09f, .014f, L.LoungerLength - .06f), cushion);
                Part(lot, "Lounger back", at + new Vector3(0, .09f, L.LoungerLength / 2 - .03f), new Vector3(.09f, .07f, .03f), cushion);
            }
            for (int i = 0; i < L.Parasols.Length; i++)
            {
                var at = L.Parasols[i];
                Part(lot, "Parasol pole", at + new Vector3(0, .11f, 0), new Vector3(.016f, .22f, .016f), PoolCoping);
                Shape("Parasol", cone, lot.at + lot.turn * (at + new Vector3(0, .19f, 0)), new Vector3(.2f, .07f, .2f), Parasols[(i + lot.hash) % Parasols.Length], cityRoot, lot.turn);
            }
            if (!L.HasChair)
                return;
            var chair = L.Chair;
            for (int i = 0; i < 4; i++)
                Part(lot, "Chair leg", chair + new Vector3((i % 2 == 0 ? -1 : 1) * .05f, L.ChairTop / 2, (i < 2 ? -1 : 1) * .05f), new Vector3(.02f, L.ChairTop, .02f), PoolCoping);
            Part(lot, "Chair seat", chair + new Vector3(0, L.ChairTop, 0), new Vector3(.14f, .025f, .14f), PoolCoping);
            bool alongZ = Mathf.Abs(L.ChairFacing.z) > .5f;
            Part(lot, "Chair back", chair - L.ChairFacing * .06f + new Vector3(0, L.ChairTop + .07f, 0), alongZ ? new Vector3(.14f, .12f, .02f) : new Vector3(.02f, .12f, .14f), RopeRed);
        }
        /// <summary>The changing rooms: white block with a blue band and a flag; a café hatch under a striped awning on a big one.</summary>
        float LidoBuilding(Lot lot, PoolLayout L, float h)
        {
            var r = L.Building;
            var centre = new Vector3(r.center.x, 0, r.center.y);
            Part(lot, "Changing rooms", centre + new Vector3(0, h / 2, 0), new Vector3(r.width, h, r.height), PoolCoping);
            Part(lot, "Pool band", centre + new Vector3(0, h * .72f, 0), new Vector3(r.width + .01f, h * .16f, r.height + .01f), PoolBand);
            Part(lot, "Flat roof", centre + new Vector3(0, h + .02f, 0), new Vector3(r.width + .05f, .04f, r.height + .05f), PaleBlue);
            Windows(lot, centre, r.width, r.height, .08f, h * .55f, 1, Glass);
            if (r.width >= .9f)
            {
                // Café hatch and a striped awning on the pool side.
                float face = r.yMin;
                Part(lot, "Café hatch", new Vector3(r.xMax - .3f, h * .35f, face - .005f), new Vector3(.3f, .14f, .01f), Navy);
                for (int i = 0; i < 5; i++)
                    Part(lot, "Awning", new Vector3(r.xMax - .42f + i * .06f, h * .52f, face - .06f), new Vector3(.06f, .02f, .12f), i % 2 == 0 ? RopeRed : RopeWhite);
            }
            // The door faces the path in.
            bool east = L.EntranceX > r.xMax;
            Part(lot, "Door", new Vector3(east ? r.xMax + .005f : r.xMin - .005f, .13f, r.center.y), new Vector3(.01f, .26f, .14f), PoolBand);
            var pole = new Vector3(east ? r.xMin + .15f : r.xMax - .15f, h, r.center.y);
            Part(lot, "Flag pole", pole + new Vector3(0, .25f, 0), new Vector3(.02f, .5f, .02f), Slate);
            Emit(EmitterKind.Flag, lot, pole + new Vector3(0, .46f, 0), PoolBand, .8f);
            return h + .55f;
        }
        /// <summary>The water park's slide: a striped tower with a canopy, and a yellow chute on poles winding down into the splash pool.</summary>
        float LidoSlide(Lot lot, PoolLayout L)
        {
            var t = L.Tower;
            var foot = new Vector3(t.center.x, 0, t.center.y);
            Part(lot, "Slide tower", foot + new Vector3(0, L.TowerTop / 2, 0), new Vector3(t.width, L.TowerTop, t.height), PoolBand);
            for (int band = 1; band < 4; band++)
                Part(lot, "Tower band", foot + new Vector3(0, L.TowerTop * band / 4, 0), new Vector3(t.width + .01f, .03f, t.height + .01f), PoolCoping);
            Part(lot, "Tower deck", foot + new Vector3(0, L.TowerTop + .015f, 0), new Vector3(t.width + .08f, .03f, t.height + .08f), PoolCoping);
            Shape("Tower canopy", cone, lot.at + lot.turn * (foot + new Vector3(0, L.TowerTop + .2f, 0)), new Vector3(.34f, .14f, .34f), ChuteRed, cityRoot, lot.turn);
            for (int i = 0; i < 4; i++)
                Part(lot, "Canopy post", foot + new Vector3((i % 2 == 0 ? -1 : 1) * (t.width / 2 + .02f), L.TowerTop + .11f, (i < 2 ? -1 : 1) * (t.height / 2 + .02f)), new Vector3(.015f, .2f, .015f), PoolCoping);
            var chute = L.Chute;
            for (int i = 0; i + 1 < chute.Length; i++)
            {
                Vector3 a = chute[i], b = chute[i + 1], along = b - a;
                var turn = lot.turn * Quaternion.LookRotation(along);
                var mid = lot.at + lot.turn * ((a + b) / 2);
                Box("Chute", mid, new Vector3(ChuteWidth, .025f, along.magnitude + .05f), ChuteYellow, cityRoot, turn);
                for (int side = -1; side <= 1; side += 2)
                    Box("Chute wall", mid + turn * new Vector3(side * ChuteWidth / 2, .03f, 0), new Vector3(.02f, .06f, along.magnitude + .05f), ChuteRed, cityRoot, turn);
                if (i > 0 && b.y > .1f)
                    Part(lot, "Chute post", new Vector3(b.x, b.y / 2, b.z), new Vector3(.025f, b.y, .025f), PoolCoping);
            }
            return L.TowerTop + .34f;
        }

        /// <summary>Close-up extras: lane ropes, pool ladders, lamps and the P sign by the driveway, trees on the lawn.</summary>
        void LidoDetails(Lot lot, int size)
        {
            var L = PoolLayout.For(size);
            float gate = LidoGate(lot, L, out _, out _);
            // Lane ropes between lanes: red and white floats, blue near each wall.
            const float Float = .1f;
            int floats = Mathf.FloorToInt(L.Pool.width / Float);
            for (int rope = 1; rope < L.Lanes; rope++)
            {
                float z = (L.LaneZ(rope - 1) + L.LaneZ(rope)) / 2;
                for (int i = 0; i < floats; i++)
                {
                    bool nearWall = i < 2 || i >= floats - 2;
                    var color = nearWall ? RopeBlue : i % 2 == 0 ? RopeRed : RopeWhite;
                    Detail(lot, "Lane rope", new Vector3(L.Pool.xMin + (i + .5f) * L.Pool.width / floats, L.WaterTop + .006f, z), new Vector3(Float * .8f, .012f, .018f), color);
                }
            }
            for (int i = 0; i < L.Ladders.Length; i++)
                for (int side = -1; side <= 1; side += 2)
                    Detail(lot, "Pool ladder", L.Ladder(i) + new Vector3(side * .035f, L.CopingTop + .04f, 0), new Vector3(.01f, .08f, .06f), Chrome);
            if (L.HasSlide)
            {
                for (int side = -1; side <= 1; side += 2)
                    Detail(lot, "Pool ladder", new Vector3(L.LeisureLadder + side * .035f, L.CopingTop + .04f, L.Leisure.yMax), new Vector3(.01f, .08f, .06f), Chrome);
                // Rungs up the tower's east face, where people climb.
                for (int rung = 1; rung < 10; rung++)
                    Detail(lot, "Tower rung", new Vector3(L.Tower.xMax + .012f, rung * L.TowerTop / 10, L.SlideFoot.z), new Vector3(.012f, .012f, .1f), Chrome);
            }
            if (L.HasBoard)
                for (int side = -1; side <= 1; side += 2)
                    Detail(lot, "Board rail", new Vector3(L.BoardBack + .06f, L.BoardTop + .05f, L.Pool.center.y + side * .05f), new Vector3(.1f, .01f, .01f), Chrome);
            if (L.HasChair)
                Detail(lot, "Life ring", L.Chair + new Vector3(.12f, .1f, .07f), new Vector3(.08f, .08f, .015f), RopeRed);
            var sign = new Vector3(gate + L.GateHalf + .06f, 0, L.Hedge + .03f);
            for (int k = 0; k + 1 < L.Size; k++)
            {
                float x = L.Column(k) + .5f;
                if (Mathf.Abs(x - gate) > L.GateHalf + .1f && Mathf.Abs(x - sign.x) > .2f)
                    LampPost(lot, new Vector3(x, 0, L.Hedge - .03f));
            }
            Detail(lot, "Sign post", sign + new Vector3(0, .2f, 0), new Vector3(.025f, .4f, .025f), Slate);
            Detail(lot, "Parking sign", sign + new Vector3(0, .42f, .015f), new Vector3(.18f, .18f, .02f), PoliceBlue);
            // A P on both faces. Seen from its own side, a face's right-hand side is its local -x, so the stem sits at +x.
            for (int face = -1; face <= 1; face += 2)
            {
                float z = .015f + face * .012f, m = -face;
                Detail(lot, "P", sign + new Vector3(-.03f * m, .42f, z), new Vector3(.025f, .12f, .006f), White);
                Detail(lot, "P", sign + new Vector3(.005f * m, .46f, z), new Vector3(.05f, .025f, .006f), White);
                Detail(lot, "P", sign + new Vector3(.005f * m, .415f, z), new Vector3(.05f, .025f, .006f), White);
                Detail(lot, "P", sign + new Vector3(.03f * m, .4375f, z), new Vector3(.02f, .045f, .006f), White);
            }
            if (L.HasLawn)
            {
                var lawn = L.Lawn;
                DetailTree(lot, new Vector3(lawn.xMax - .1f, L.DeckTop, lawn.yMin + .1f), 1.1f, Leaf);
                DetailTree(lot, new Vector3(lawn.xMax - .1f, L.DeckTop, lawn.yMax - .1f), .9f, Moss);
                Bench(lot, new Vector3(lawn.xMin + .18f, L.DeckTop, lawn.yMax - .06f));
            }
            if (L.Size >= 3)
                DetailTree(lot, new Vector3(-L.Half + .1f, 0, -L.Half + .1f), 1f, Leaf);
        }
    }
}
