using System.Collections.Generic;
using UnityEngine;
namespace ValleyRail
{
    /// <summary>
    /// Stadium dressing: a mown pitch with its markings (drawn with the building), goals and corner flags (close-up
    /// details), and the list of grounds handed to <see cref="StadiumMatch"/> for the crowd and the live match.
    /// </summary>
    public sealed partial class WorldView
    {
        // Literal colours only: statics of other partial files may not be initialised yet when this file's run.
        static readonly Color MownStripe = new Color(.25f, .58f, .28f), PitchLine = new Color(.95f, .96f, .93f),
            Net = new Color(.85f, .87f, .88f), CornerFlag = new Color(.98f, .82f, .15f),
            SeatRed = new Color(.5f, .16f, .15f), SeatRedShade = new Color(.42f, .13f, .12f), SeatBlue = new Color(.2f, .3f, .5f), SeatBlueShade = new Color(.16f, .25f, .42f);
        const int CentreCircleSegments = 20;
        const float CarLength = .3f, CoachLength = .8f;
        readonly List<StadiumMatch.Venue> venues = new List<StadiumMatch.Venue>();
        StadiumMatch matches;

        /// <summary>Share of the lot's depth the bowl takes; big grounds leave the strip along the street for the car park.</summary>
        static float BowlShare(int size) => size >= 4 ? .78f : 1;
        /// <summary>The bowl's measurements for a stadium building of this size.</summary>
        static ArenaLayout ArenaFor(float w, float d, float h, int size) => new ArenaLayout(w, d * BowlShare(size), h, size);
        /// <summary>The bowl's frame: the lot frame pushed back from the street to make room for the car park.</summary>
        static Lot ArenaBowl(Lot lot, float d, int size) =>
            new Lot { at = lot.at + lot.turn * new Vector3(0, 0, -d * (1 - BowlShare(size)) / 2), turn = lot.turn, hash = lot.hash };

        /// <summary>
        /// Match-day parking between a big stadium and its street: two double rows of bays, noses meeting on each middle
        /// line, nearly every bay taken, and both team coaches by the entrance in their club colours.
        /// </summary>
        void ArenaCarPark(Lot lot, float w, float d, int size)
        {
            float depth = d * (1 - BowlShare(size)), mid = d / 2 - depth / 2, left = -w * .3f, right = w * .47f;
            Part(lot, "Stadium car park", new Vector3(0, .012f, mid), new Vector3(w * .97f, .02f, depth - .04f), Asphalt);
            int bays = Mathf.FloorToInt((right - left) / (CarLength * .87f));
            float bay = (right - left) / bays;
            for (int pair = 0; pair < 2; pair++)
            {
                float line = mid + (pair == 0 ? -1 : 1) * depth * .26f;
                Part(lot, "Bay line", new Vector3((left + right) / 2, .025f, line), new Vector3(right - left, .01f, .02f), White);
                for (int i = 0; i <= bays; i++)
                    Part(lot, "Bay line", new Vector3(left + i * bay, .025f, line), new Vector3(.02f, .01f, CarLength * 2 + .04f), White);
                for (int row = 0; row < 2; row++)
                    for (int i = 0; i < bays; i++)
                    {
                        int pick = i * 7 + row * 3 + pair * 5 + lot.hash;
                        if (pick % 10 >= 8)
                            continue; // a few empty bays
                        // Nose to nose across the line; a car sits a touch off-centre in its bay now and then.
                        float offset = ((i * 13 + row * 7 + pair) % 5 - 2) * .006f;
                        var at = new Vector3(left + (i + .5f) * bay + offset, .022f, line + (row == 0 ? -1 : 1) * (CarLength / 2 + .02f));
                        ParkedCar(lot, at, CarLength, CarPaint[(pick / 3) % CarPaint.Length], row == 0 ? 0 : 180);
                    }
            }
            for (int team = 0; team < 2; team++)
                Coach(lot, new Vector3(-w * .44f + team * w * .075f, 0, mid), StadiumMatch.TeamShirt(team));
        }
        /// <summary>A team coach parked nose to the street: body in club colours, a window band, a white roof and wheels.</summary>
        void Coach(Lot lot, Vector3 ground, Color club)
        {
            float width = .2f, high = .24f;
            Part(lot, "Coach", ground + new Vector3(0, .03f + high / 2, 0), new Vector3(width, high, CoachLength), club);
            Part(lot, "Coach windows", ground + new Vector3(0, .03f + high * .68f, .01f), new Vector3(width + .006f, high * .26f, CoachLength * .9f), CarGlass);
            Part(lot, "Coach windscreen", ground + new Vector3(0, .03f + high * .6f, CoachLength / 2 + .003f), new Vector3(width * .86f, high * .4f, .006f), CarGlass);
            Part(lot, "Coach roof", ground + new Vector3(0, .03f + high + .008f, 0), new Vector3(width - .01f, .016f, CoachLength - .02f), White);
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                    Detail(lot, "Wheel", ground + new Vector3(side * (width / 2 - .01f), .04f, end * CoachLength * .32f), new Vector3(.03f, .08f, .08f), Tyre);
        }
        /// <summary>Lamps down the car park's middle aisle and a blue P sign at the entrance.</summary>
        void ArenaCarParkDetails(Lot lot, float w, float d, int size)
        {
            float depth = d * (1 - BowlShare(size)), mid = d / 2 - depth / 2;
            for (int i = 0; i < 4; i++)
                LampPost(lot, new Vector3(-w * .2f + i * w * .2f, 0, mid));
            var sign = new Vector3(-w * .3f + .08f, 0, d / 2 - .06f);
            Detail(lot, "Sign post", sign + new Vector3(0, .2f, 0), new Vector3(.025f, .4f, .025f), Slate);
            Detail(lot, "Parking sign", sign + new Vector3(0, .42f, .015f), new Vector3(.18f, .18f, .02f), PoliceBlue);
            Detail(lot, "P", sign + new Vector3(-.03f, .42f, .027f), new Vector3(.025f, .12f, .006f), White);
            Detail(lot, "P", sign + new Vector3(.005f, .46f, .027f), new Vector3(.05f, .025f, .006f), White);
            Detail(lot, "P", sign + new Vector3(.005f, .415f, .027f), new Vector3(.05f, .025f, .006f), White);
            Detail(lot, "P", sign + new Vector3(.03f, .4375f, .027f), new Vector3(.02f, .045f, .006f), White);
        }

        /// <summary>
        /// The four stands and the floodlight masts; returns the building's height. In the town pass every stand is its own
        /// mesh in two versions, full and cut down to its front rows, so <see cref="StadiumMatch"/> can lower the stands
        /// between the camera and the pitch when zoomed in. A doomed ghost (station upgrade) gets plain parts instead.
        /// </summary>
        float ArenaStands(Lot lot, ArenaLayout arena)
        {
            float mast = arena.h * (arena.size >= 4 ? 1.8f : 1f) + .4f * arena.size;
            if (batch == null || batchTarget != cityRoot)
            {
                for (int side = 0; side < 4; side++)
                    DrawStand(null, lot, arena, side, true);
                for (int corner = 0; corner < 4; corner++)
                    DrawMast(null, lot, arena, corner, mast);
                return mast + .2f;
            }
            GameObject[] full = new GameObject[4], cut = new GameObject[4], masts = new GameObject[4];
            for (int side = 0; side < 4; side++)
            {
                full[side] = StandObject(lot, "Stand", builder => DrawStand(builder, lot, arena, side, true));
                cut[side] = StandObject(lot, "Stand cut away", builder => DrawStand(builder, lot, arena, side, false));
                cut[side].SetActive(false);
            }
            for (int corner = 0; corner < 4; corner++)
                masts[corner] = StandObject(lot, "Floodlight mast", builder => DrawMast(builder, lot, arena, corner, mast));
            venues.Add(new StadiumMatch.Venue(lot.at, lot.turn, arena, lot.hash, full, cut, masts));
            return mast + .2f;
        }
        /// <summary>
        /// One stand: rows of seats stepping up away from the pitch (the fans sit on these), a solid back behind them and,
        /// on big grounds, an upper tier with glass boxes; only the long stands get a roof. Cut away, just the front rows remain.
        /// </summary>
        void DrawStand(TrainMeshBuilder builder, Lot lot, ArenaLayout arena, int side, bool full)
        {
            var stand = arena.Stand(side);
            bool alongX = stand.alongX;
            int rows = full ? stand.rows : stand.CutRows;
            float back = stand.thick - stand.rake, height = stand.RowTop(rows - 1);
            StandPart(builder, lot, "Stand", stand.centre + stand.normal * (stand.thick - back) / 2 + Vector3.up * height / 2, stand.Span(back, height, stand.length), alongX ? Stone : Cream);
            // Darker seats than the kits, alternating by row, so the fans stand out and every row reads.
            for (int row = 0; row < rows; row++)
            {
                float from = stand.Front + row * stand.Step, depth = stand.rake - row * stand.Step, top = stand.RowTop(row);
                var seats = alongX ? row % 2 == 0 ? SeatRed : SeatRedShade : row % 2 == 0 ? SeatBlue : SeatBlueShade;
                StandPart(builder, lot, "Seat row", stand.centre + stand.normal * (from + depth / 2) + Vector3.up * top / 2, stand.Span(depth, top, stand.length), seats);
            }
            if (!full)
                return;
            if (arena.size >= 4)
            {
                StandPart(builder, lot, "Upper tier", stand.centre + stand.normal * stand.thick * .3f + Vector3.up * height * 1.35f,
                    stand.Span(stand.thick * .5f, height * .7f, alongX ? arena.w * .86f : arena.d * .56f), alongX ? White : Stone);
                StandPart(builder, lot, "Executive boxes", stand.centre + stand.normal * (stand.thick * .05f - .012f) + Vector3.up * height * 1.3f,
                    stand.Span(.02f, height * .22f, stand.length * .86f), Glass, Finish.Glass);
            }
            if (alongX)
                StandPart(builder, lot, "Roof canopy", stand.centre + Vector3.up * (arena.RoofTop - .02f), new Vector3(arena.w * .92f, .04f, stand.thick * 1.3f), White);
        }
        /// <summary>A floodlight mast on corner 0–3: x side from bit 0 (-x first), z side from bit 1 (-z first).</summary>
        void DrawMast(TrainMeshBuilder builder, Lot lot, ArenaLayout arena, int corner, float mast)
        {
            int size = arena.size;
            var at = new Vector3((corner % 2 == 0 ? -1 : 1) * arena.w * .46f, 0, (corner < 2 ? -1 : 1) * arena.d * .44f);
            StandPart(builder, lot, "Floodlight mast", at + Vector3.up * mast / 2, new Vector3(.05f * size, mast, .05f * size), Slate);
            StandPart(builder, lot, "Floodlights", at + Vector3.up * mast, new Vector3(.16f * size, .1f * size, .06f * size), Cream);
        }
        /// <summary>A part of a stand: into its own mesh (lot frame) when building one, otherwise a plain town part.</summary>
        void StandPart(TrainMeshBuilder builder, Lot lot, string name, Vector3 local, Vector3 size, Color color, Finish finish = Finish.Matte)
        {
            if (builder != null)
                builder.Box(local, size, new Paint(color, finish));
            else
                Part(lot, name, local, size, color);
        }
        /// <summary>A stand-alone town object on the lot; clearing the town frees its mesh like the batched ones.</summary>
        GameObject StandObject(Lot lot, string name, System.Action<TrainMeshBuilder> draw)
        {
            var builder = PaletteBuilder();
            draw(builder);
            var mesh = BuildPaletteMesh(builder, name, out var materials);
            ownedMeshes.Add(mesh);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(cityRoot, false);
            go.transform.localPosition = lot.at;
            go.transform.localRotation = lot.turn;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        /// <summary>The grass with mowing stripes, touchlines, halfway line, centre circle and both penalty areas.</summary>
        void ArenaPitch(Lot lot, ArenaLayout arena, Color grass)
        {
            float length = arena.PitchLength, width = arena.PitchWidth, line = arena.Line, top = ArenaLayout.PitchTop;
            Part(lot, "Pitch", new Vector3(0, top / 2 + .005f, 0), new Vector3(length, top - .01f, width), grass);
            int stripes = 2 * Mathf.Max(3, arena.size);
            for (int i = 1; i < stripes; i += 2)
                Part(lot, "Mown stripe", new Vector3(-length / 2 + (i + .5f) * length / stripes, top + .001f, 0), new Vector3(length / stripes, .004f, width), MownStripe);
            float y = top + .003f, thin = .006f;
            for (int side = -1; side <= 1; side += 2)
            {
                Part(lot, "Touchline", new Vector3(0, y, side * (width - line) / 2), new Vector3(length, thin, line), PitchLine);
                Part(lot, "Goal line", new Vector3(side * (length - line) / 2, y, 0), new Vector3(line, thin, width), PitchLine);
                PenaltyArea(lot, side, length * .16f, width * .58f, arena);
                PenaltyArea(lot, side, length * .055f, width * .28f, arena);
                Part(lot, "Penalty spot", new Vector3(side * length * .39f, y, 0), new Vector3(line * 1.6f, thin, line * 1.6f), PitchLine);
            }
            Part(lot, "Halfway line", new Vector3(0, y, 0), new Vector3(line, thin, width), PitchLine);
            Part(lot, "Centre spot", new Vector3(0, y, 0), new Vector3(line * 2, thin, line * 2), PitchLine);
            float radius = width * .13f, arc = 2 * Mathf.PI * radius / CentreCircleSegments * 1.1f;
            for (int i = 0; i < CentreCircleSegments; i++)
            {
                float angle = (i + .5f) * 2 * Mathf.PI / CentreCircleSegments;
                var at = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                Box("Centre circle", lot.at + lot.turn * at, new Vector3(line, thin, arc), PitchLine, cityRoot, lot.turn * Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0));
            }
        }
        /// <summary>A box marked out from the goal line at end <paramref name="side"/>: front edge and two sides.</summary>
        void PenaltyArea(Lot lot, int side, float depth, float span, ArenaLayout arena)
        {
            float end = arena.PitchLength / 2, line = arena.Line, y = ArenaLayout.PitchTop + .003f;
            Part(lot, "Penalty area", new Vector3(side * (end - depth), y, 0), new Vector3(line, .006f, span), PitchLine);
            for (int edge = -1; edge <= 1; edge += 2)
                Part(lot, "Penalty area", new Vector3(side * (end - depth / 2), y, edge * span / 2), new Vector3(depth, .006f, line), PitchLine);
        }
        /// <summary>Goals with nets at both ends and a flag at every corner; the match plays between them.</summary>
        void ArenaGoals(Lot lot, ArenaLayout arena)
        {
            float end = arena.PitchLength / 2, half = arena.GoalWidth / 2, high = arena.GoalHeight, deep = arena.GoalDepth, top = ArenaLayout.PitchTop;
            float post = Mathf.Max(.012f, .0125f * arena.Player), mesh = .008f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int edge = -1; edge <= 1; edge += 2)
                {
                    Detail(lot, "Goal post", new Vector3(side * end, top + high / 2, edge * half), new Vector3(post, high, post), White);
                    Detail(lot, "Side net", new Vector3(side * (end + deep / 2), top + high / 2, edge * half), new Vector3(deep, high, mesh), Net);
                    Detail(lot, "Corner flag", new Vector3(side * end, top + high * .35f, edge * arena.PitchWidth / 2), new Vector3(post * .7f, high * .7f, post * .7f), White);
                    Detail(lot, "Flag", new Vector3(side * end, top + high * .62f, edge * arena.PitchWidth / 2 + post * 2), new Vector3(mesh, high * .16f, post * 3), CornerFlag);
                }
                Detail(lot, "Crossbar", new Vector3(side * end, top + high, 0), new Vector3(post, post, half * 2 + post), White);
                Detail(lot, "Back net", new Vector3(side * (end + deep), top + high / 2, 0), new Vector3(mesh, high, half * 2), Net);
                for (int bar = 0; bar < 3; bar++)
                    Detail(lot, "Roof net", new Vector3(side * (end + deep * (bar + .5f) / 3), top + high, 0), new Vector3(mesh, mesh, half * 2), Net);
            }
        }
        internal TrainMeshBuilder PaletteBuilder() => new TrainMeshBuilder(PaletteTexel);
        /// <summary>Builds a palette-coloured mesh (as train cars are) so a whole crowd draws with one material per finish.</summary>
        internal Mesh BuildPaletteMesh(TrainMeshBuilder builder, string name, out Material[] materials)
        {
            var mesh = builder.Build(name, false, out var finishes);
            if (paletteDirty)
            {
                trainPalette.Apply(false);
                paletteDirty = false;
            }
            var all = TrainFinishes();
            materials = new Material[finishes.Length];
            for (int i = 0; i < finishes.Length; i++)
                materials[i] = all[(int)finishes[i]];
            return mesh;
        }
    }
}
