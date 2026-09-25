using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// The drive-in cinema: a gravel lot with ramps on curved rows facing a big screen on a steel frame, a snack bar with
    /// the projection booth on its roof, a ticket booth on the way in and a bulb-lit DRIVE-IN sign by the street. In the
    /// town pass it is drawn into two stand-alone meshes (everything, and the close-up bits) that <see cref="DriveInCinema"/>
    /// turns as a whole so the screen always faces the camera; the screen picture, beam and cars are the cinema's own.
    /// </summary>
    public sealed partial class WorldView
    {
        // Literal colours only: statics of other partial files may not be initialised yet when this file's run.
        static readonly Color DriveInGravel = new Color(.44f, .42f, .39f), DriveInLane = new Color(.27f, .27f, .29f), DriveInRamp = new Color(.53f, .51f, .47f),
            ScreenFrame = new Color(.94f, .93f, .9f), ScreenBack = new Color(.25f, .33f, .29f), Girder = new Color(.4f, .42f, .46f),
            FencePlank = new Color(.56f, .41f, .27f), SignBoard = new Color(.98f, .95f, .86f), SignRed = new Color(.88f, .13f, .1f),
            SignBulb = new Color(1f, .86f, .42f), SnackWall = new Color(.96f, .94f, .88f), SnackStripe = new Color(.85f, .16f, .14f),
            BoothGrey = new Color(.55f, .57f, .6f), BoothGlass = new Color(.1f, .12f, .16f), SpeakerGrey = new Color(.33f, .35f, .38f),
            DriveInLamp = new Color(1f, .93f, .62f), DriveInPost = new Color(.3f, .33f, .38f);
        readonly List<DriveInCinema.Site> driveIns = new List<DriveInCinema.Site>();
        DriveInCinema cinemas;

        /// <summary>Draws a drive-in on a <paramref name="size"/> × <paramref name="size"/> lot and returns its height.</summary>
        float DrawDriveIn(Lot lot, int size)
        {
            var layout = new DriveInLayout(size);
            float top = layout.screenBottom + layout.screenHeight + .08f;
            if (batch == null || batchTarget != cityRoot)
            {
                // A doomed ghost (station upgrade): plain parts in the lot's own frame.
                DriveInParts(null, lot, layout);
                DriveInDetails(null, lot, layout);
                return top;
            }
            var art = StandObject(lot, "Drive-in", builder => DriveInParts(builder, lot, layout));
            var details = StandObject(lot, "Drive-in details", builder => DriveInDetails(builder, lot, layout));
            details.SetActive(ZoomedIn);
            var street = lot.turn * Vector3.forward;
            int side = (int)Mathf.Repeat(Mathf.Round(Mathf.Atan2(street.x, street.z) * Mathf.Rad2Deg / 90), 4);
            driveIns.Add(new DriveInCinema.Site(lot.at, size, lot.hash, side, art, details));
            return top;
        }
        /// <summary>A part of the drive-in: into its mesh (lot frame) when building one, otherwise a plain town part.</summary>
        void DriveInPart(TrainMeshBuilder builder, Lot lot, string name, Vector3 local, Vector3 size, Color color, Quaternion? turn = null)
        {
            var rotation = turn ?? Quaternion.identity;
            if (builder != null)
                builder.Box(local, size, new Paint(color, Finish.Matte), rotation);
            else
                Box(name, lot.at + lot.turn * local, size, color, cityRoot, lot.turn * rotation);
        }
        void DriveInParts(TrainMeshBuilder b, Lot lot, DriveInLayout layout)
        {
            float edge = layout.half - .03f;
            DriveInPart(b, lot, "Gravel", new Vector3(0, .012f, 0), new Vector3(2 * edge, .024f, 2 * edge), DriveInGravel);
            DriveInLanes(b, lot, layout);
            foreach (var bay in layout.bays)
            {
                // A low ramp under the front wheels of every bay; the strips join up along each row.
                var ramp = layout.Point(bay.radius - .1f, bay.angle);
                DriveInPart(b, lot, "Ramp", new Vector3(ramp.x, .03f, ramp.z), new Vector3(DriveInLayout.BayPitch + .01f, .024f, .16f), DriveInRamp, bay.Facing);
            }
            DriveInScreen(b, lot, layout);
            SnackBar(b, lot, layout);
            TicketBooth(b, lot, layout);
            DriveInSign(b, lot, layout);
            DriveInFence(b, lot, layout);
        }
        /// <summary>Asphalt lanes: in past the ticket booth down the right side, out up the left side.</summary>
        void DriveInLanes(TrainMeshBuilder b, Lot lot, DriveInLayout layout)
        {
            float half = layout.half, top = layout.ticketStop.z - .8f, bottom = layout.screenZ + .15f;
            for (int side = -1; side <= 1; side += 2)
            {
                float gate = side * layout.gateIn.x, lane = side * layout.laneX;
                DriveInPart(b, lot, "Lane", new Vector3(gate, .015f, (half + top) / 2), new Vector3(.3f, .03f, half - top), DriveInLane);
                DriveInPart(b, lot, "Lane", new Vector3((gate + lane) / 2, .015f, top), new Vector3(Mathf.Abs(lane - gate) + .3f, .03f, .3f), DriveInLane);
                DriveInPart(b, lot, "Lane", new Vector3(lane, .015f, (top + bottom) / 2), new Vector3(.28f, .03f, top - bottom), DriveInLane);
            }
        }
        /// <summary>The screen: a white frame on a steel trestle with a painted back; the picture itself is the cinema's quad.</summary>
        void DriveInScreen(TrainMeshBuilder b, Lot lot, DriveInLayout layout)
        {
            float w = layout.screenWidth, h = layout.screenHeight, bottom = layout.screenBottom, z = layout.screenZ, mid = bottom + h / 2;
            DriveInPart(b, lot, "Screen frame", new Vector3(0, mid, z - .04f), new Vector3(w + .14f, h + .14f, .08f), ScreenFrame);
            DriveInPart(b, lot, "Screen back", new Vector3(0, mid, z - .1f), new Vector3(w + .1f, h + .1f, .04f), ScreenBack);
            float back = z - .17f, top = bottom + h + .02f;
            DriveInPart(b, lot, "Screen footing", new Vector3(0, .04f, back), new Vector3(w + .3f, .08f, .26f), Stone);
            float[] posts = { -w * .46f, -w * .16f, w * .16f, w * .46f };
            foreach (float x in posts)
                DriveInPart(b, lot, "Screen post", new Vector3(x, top / 2, back), new Vector3(.07f, top, .07f), Girder);
            foreach (float y in new[] { bottom * .5f, mid, top - .04f })
                DriveInPart(b, lot, "Screen beam", new Vector3(0, y, back), new Vector3(w * .96f, .045f, .045f), Girder);
            // Cross bracing between each pair of posts, below the screen and behind it.
            for (int i = 0; i + 1 < posts.Length; i++)
            {
                float x0 = posts[i], x1 = posts[i + 1], span = x1 - x0;
                foreach (var (y0, y1) in new[] { (bottom * .5f, mid), (mid, top - .04f) })
                {
                    float rise = y1 - y0, length = Mathf.Sqrt(span * span + rise * rise), tilt = Mathf.Atan2(rise, span) * Mathf.Rad2Deg;
                    for (int d = -1; d <= 1; d += 2)
                        DriveInPart(b, lot, "Screen brace", new Vector3((x0 + x1) / 2, (y0 + y1) / 2, back - .01f), new Vector3(length, .025f, .025f), Girder, Quaternion.Euler(0, 0, d * tilt));
                }
            }
            DriveInPart(b, lot, "Verge", new Vector3(0, .02f, z + .12f), new Vector3(w + .2f, .03f, .2f), Grass);
        }
        /// <summary>The snack bar: striped walls, an awning over the counter facing the back rows, a SNACKS board and the projection booth.</summary>
        void SnackBar(TrainMeshBuilder b, Lot lot, DriveInLayout layout)
        {
            var at = layout.snackBar;
            var size = layout.snackBarSize;
            float front = at.z + size.z / 2;
            DriveInPart(b, lot, "Snack bar", at + new Vector3(0, size.y / 2, 0), size, SnackWall);
            DriveInPart(b, lot, "Snack bar stripe", at + new Vector3(0, size.y * .72f, 0), new Vector3(size.x + .01f, .045f, size.z + .01f), SnackStripe);
            DriveInPart(b, lot, "Snack bar roof", at + new Vector3(0, size.y + .02f, 0), new Vector3(size.x + .08f, .04f, size.z + .08f), Slate);
            DriveInPart(b, lot, "Counter window", new Vector3(0, .14f, front + .004f), new Vector3(size.x * .66f, .09f, .01f), BoothGlass);
            for (int i = 0; i < 6; i++)
                DriveInPart(b, lot, "Awning", new Vector3(-size.x * .35f + i * size.x * .14f, .215f, front + .07f), new Vector3(size.x * .14f, .025f, .14f), i % 2 == 0 ? SnackStripe : SnackWall);
            float boardY = size.y + .13f, boardZ = front - .05f;
            DriveInPart(b, lot, "Snacks board", new Vector3(0, boardY, boardZ), new Vector3(size.x * .86f, .17f, .03f), SignBoard);
            PixelWord(b, lot, "SNACKS", new Vector3(0, boardY, boardZ + .02f), .02f, .012f, SignRed);
            // The projection booth faces the screen through a dark port; the cinema's beam starts there.
            var booth = new Vector3(0, size.y + .1f, at.z - size.z / 2 + .1f);
            DriveInPart(b, lot, "Projection booth", booth, new Vector3(.36f, .2f, .2f), BoothGrey);
            DriveInPart(b, lot, "Projection booth roof", booth + new Vector3(0, .11f, 0), new Vector3(.42f, .03f, .26f), Slate);
            DriveInPart(b, lot, "Projector port", new Vector3(0, layout.projector.y, booth.z - .1f - .004f), new Vector3(.12f, .06f, .01f), BoothGlass);
        }
        void TicketBooth(TrainMeshBuilder b, Lot lot, DriveInLayout layout)
        {
            var at = layout.ticketBooth;
            DriveInPart(b, lot, "Ticket booth", at + new Vector3(0, .13f, 0), new Vector3(.22f, .26f, .22f), SnackWall);
            DriveInPart(b, lot, "Ticket booth band", at + new Vector3(0, .05f, 0), new Vector3(.23f, .06f, .23f), SnackStripe);
            DriveInPart(b, lot, "Ticket window", at + new Vector3(.112f, .17f, 0), new Vector3(.01f, .08f, .14f), BoothGlass);
            DriveInPart(b, lot, "Ticket booth roof", at + new Vector3(0, .28f, 0), new Vector3(.32f, .04f, .32f), SnackStripe);
        }
        /// <summary>The street sign: DRIVE-IN in red letters on a cream board ringed with bulbs, on two posts.</summary>
        void DriveInSign(TrainMeshBuilder b, Lot lot, DriveInLayout layout)
        {
            var at = layout.sign;
            float width = 1.8f * layout.scale, height = .44f, bottom = .56f, mid = bottom + height / 2;
            for (int side = -1; side <= 1; side += 2)
                DriveInPart(b, lot, "Sign post", at + new Vector3(side * width * .4f, mid / 2, -.02f), new Vector3(.04f, mid, .04f), DriveInPost);
            DriveInPart(b, lot, "Sign board", at + new Vector3(0, mid, 0), new Vector3(width, height, .05f), SignBoard);
            DriveInPart(b, lot, "Sign frame", at + new Vector3(0, bottom + height, 0), new Vector3(width + .04f, .04f, .07f), SignRed);
            DriveInPart(b, lot, "Sign frame", at + new Vector3(0, bottom, 0), new Vector3(width + .04f, .04f, .07f), SignRed);
            for (int side = -1; side <= 1; side += 2)
                DriveInPart(b, lot, "Sign frame", at + new Vector3(side * width / 2, mid, 0), new Vector3(.04f, height + .04f, .07f), SignRed);
            int bulbs = Mathf.RoundToInt(width / .1f);
            for (int i = 0; i <= bulbs; i++)
                foreach (float y in new[] { bottom, bottom + height })
                    DriveInPart(b, lot, "Sign bulb", at + new Vector3(-width / 2 + i * width / bulbs, y, .04f), new Vector3(.025f, .025f, .02f), SignBulb);
            PixelWord(b, lot, "DRIVE-IN", at + new Vector3(0, mid, .03f), width * .88f / PixelFont.Measure("DRIVE-IN"), .016f, SignRed);
        }
        /// <summary>A low timber fence round the lot with gaps for the ways in and out.</summary>
        void DriveInFence(TrainMeshBuilder b, Lot lot, DriveInLayout layout)
        {
            float edge = layout.half - .06f, gap = .34f;
            for (int side = 0; side < 4; side++)
            {
                var along = side % 2 == 0 ? Vector3.right : Vector3.forward;
                var line = new Vector3(Directions.Dx[side], 0, Directions.Dz[side]) * edge;
                float from = -edge;
                // The street side (+z) has gaps for the ways in and out.
                if (line.z > 0)
                    foreach (float gate in new[] { layout.gateOut.x, layout.gateIn.x })
                    {
                        FenceRun(b, lot, line, along, from, gate - gap);
                        from = gate + gap;
                    }
                FenceRun(b, lot, line, along, from, edge);
            }
        }
        void FenceRun(TrainMeshBuilder b, Lot lot, Vector3 line, Vector3 along, float from, float to)
        {
            float length = to - from;
            if (length <= .02f)
                return;
            var centre = line + along * (from + to) / 2;
            var size = along.x != 0 ? new Vector3(length, .03f, .02f) : new Vector3(.02f, .03f, length);
            DriveInPart(b, lot, "Fence rail", centre + Vector3.up * .06f, size, FencePlank);
            DriveInPart(b, lot, "Fence rail", centre + Vector3.up * .12f, size, FencePlank);
            int posts = Mathf.Max(1, Mathf.RoundToInt(length / .45f));
            for (int i = 0; i <= posts; i++)
                DriveInPart(b, lot, "Fence post", line + along * (from + i * length / posts) + Vector3.up * .075f, new Vector3(.035f, .15f, .035f), FencePlank);
        }
        /// <summary>Close-up bits: a speaker post between every other pair of bays, lamps along the lanes and picnic tables at the snack bar.</summary>
        void DriveInDetails(TrainMeshBuilder b, Lot lot, DriveInLayout layout)
        {
            for (int i = 0; i + 1 < layout.bays.Count; i += 2)
            {
                var a = layout.bays[i];
                var next = layout.bays[i + 1];
                if (next.row != a.row || Mathf.Abs(next.angle - a.angle) > 1.5f * DriveInLayout.BayPitch / a.radius)
                    continue;
                var post = layout.Point(a.radius - .08f, (a.angle + next.angle) / 2);
                DriveInPart(b, lot, "Speaker post", new Vector3(post.x, .07f, post.z), new Vector3(.02f, .14f, .02f), SpeakerGrey);
                DriveInPart(b, lot, "Speaker", new Vector3(post.x, .15f, post.z), new Vector3(.07f, .04f, .03f), SpeakerGrey, a.Facing);
            }
            foreach (var z in new[] { .6f, -1.1f })
                for (int side = -1; side <= 1; side += 2)
                {
                    var at = new Vector3(side * (layout.laneX + .2f), 0, z * layout.scale);
                    DriveInPart(b, lot, "Lamp post", at + new Vector3(0, .25f, 0), new Vector3(.03f, .5f, .03f), DriveInPost);
                    DriveInPart(b, lot, "Lamp", at + new Vector3(-side * .03f, .5f, 0), new Vector3(.1f, .03f, .06f), DriveInLamp);
                }
            float benchZ = layout.snackBar.z + layout.snackBarSize.z / 2 + .12f;
            for (int side = -1; side <= 1; side += 2)
            {
                var at = new Vector3(side * .26f, 0, benchZ);
                DriveInPart(b, lot, "Picnic table", at + new Vector3(0, .06f, 0), new Vector3(.18f, .02f, .09f), FencePlank);
                DriveInPart(b, lot, "Picnic bench", at + new Vector3(0, .035f, .07f), new Vector3(.18f, .015f, .03f), FencePlank);
                DriveInPart(b, lot, "Picnic bench", at + new Vector3(0, .035f, -.07f), new Vector3(.18f, .015f, .03f), FencePlank);
            }
        }
        /// <summary>
        /// Block letters from the pixel font on a face looking along +z, centred on <paramref name="centre"/>: each run of lit
        /// pixels in a row is one box <paramref name="depth"/> deep. They read left to right for someone facing the letters.
        /// </summary>
        void PixelWord(TrainMeshBuilder b, Lot lot, string text, Vector3 centre, float pixel, float depth, Color color)
        {
            int width = PixelFont.Measure(text);
            for (int i = 0; i < text.Length; i++)
            {
                var glyph = PixelFont.Rows(text[i]);
                if (glyph == null)
                    continue;
                for (int row = 0; row < PixelFont.Height; row++)
                    for (int col = 0; col < PixelFont.Width; col++)
                    {
                        if (!PixelFont.Lit(glyph, col, row))
                            continue;
                        int start = col;
                        while (col + 1 < PixelFont.Width && PixelFont.Lit(glyph, col + 1, row))
                            col++;
                        // Facing the letters you look along -z, so your left is +x.
                        float run = col - start + 1, middle = i * PixelFont.Advance + start + run / 2;
                        var at = centre + new Vector3((width / 2f - middle) * pixel, (PixelFont.Height / 2f - row - .5f) * pixel, 0);
                        DriveInPart(b, lot, "Sign letter", at, new Vector3(run * pixel, pixel, depth), color);
                    }
            }
        }
    }
}
