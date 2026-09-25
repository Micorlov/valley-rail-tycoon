using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// The three city parks, drawn from their <see cref="ParkLayout"/> plan: a clipped hedge with a gate in each side (an iron
    /// arch over the street gate), gravel paths round a loop and across to a fountain plaza, round flower beds, park trees, a
    /// duck pond (a boating lake with an island and a boathouse in the central park), a playground, and on the bigger parks a
    /// bandstand and a café kiosk. Benches, lamps, reeds and café chairs go in the close-up detail mesh; <see cref="ParkLife"/>
    /// brings the people, ducks and boats.
    /// </summary>
    public sealed partial class WorldView
    {
        // Literal colours only: statics of other partial files may not be initialised yet when this file's run.
        static readonly Color ParkLawn = new Color(.38f, .64f, .3f), ParkHedge = new Color(.2f, .42f, .22f), ParkStone = new Color(.7f, .67f, .6f),
            ParkStoneLight = new Color(.9f, .88f, .82f), ParkIron = new Color(.14f, .24f, .18f), ParkGravel = new Color(.88f, .82f, .66f),
            ParkPaving = new Color(.8f, .76f, .68f), ParkPond = new Color(.26f, .56f, .74f), ParkFountainWater = new Color(.5f, .78f, .92f),
            ParkBank = new Color(.6f, .54f, .38f), ParkSurface = new Color(.84f, .46f, .32f), ParkFrame = new Color(.98f, .8f, .2f),
            ParkSlide = new Color(.2f, .5f, .86f), ParkRed = new Color(.86f, .24f, .2f), ParkWhite = new Color(.96f, .95f, .91f),
            ParkBandRed = new Color(.74f, .18f, .16f), ParkGold = new Color(.95f, .72f, .25f), ParkKiosk = new Color(.6f, .84f, .74f),
            ParkKioskDark = new Color(.2f, .5f, .42f), ParkSoil = new Color(.4f, .29f, .2f), ParkCrown = new Color(.32f, .58f, .26f),
            ParkCrownDark = new Color(.24f, .48f, .24f), ParkPine = new Color(.15f, .38f, .22f), ParkTimber = new Color(.5f, .35f, .22f),
            ParkReed = new Color(.35f, .5f, .25f), ParkLily = new Color(.3f, .6f, .3f);
        static readonly Color[] ParkBlooms = { new Color(.92f, .3f, .38f), new Color(.98f, .8f, .22f), new Color(.72f, .42f, .86f), new Color(1f, .6f, .2f), new Color(.96f, .96f, .94f) };

        float DrawCityPark(Lot lot, int size)
        {
            var P = ParkLayout.For(size);
            ParkGrounds(lot, P);
            ParkPaths(lot, P);
            ParkWater(lot, P);
            ParkPlayground(lot, P);
            float top = ParkPlanting(lot, P);
            if (P.HasBandstand)
                top = Mathf.Max(top, ParkBandstand(lot, P));
            if (P.HasCafe)
                ParkCafe(lot, P);
            return top;
        }
        /// <summary>A flat round slab (an ellipse <paramref name="width"/> by <paramref name="depth"/>) standing on <paramref name="at"/>.</summary>
        void ParkDisc(Lot lot, string name, Vector3 at, float width, float depth, float height, Color color, Transform root = null) =>
            Shape(name, Drum(), lot.at + lot.turn * (at + Vector3.up * height / 2), new Vector3(width, depth, height), color, root ? root : cityRoot, lot.turn * Quaternion.Euler(90, 0, 0));
        /// <summary>A bar from <paramref name="a"/> to <paramref name="b"/> (never straight up), <paramref name="width"/> kept level.</summary>
        void ParkBeam(Lot lot, string name, Vector3 a, Vector3 b, float width, float height, Color color, Transform root = null)
        {
            var along = b - a;
            Box(name, lot.at + lot.turn * ((a + b) / 2), new Vector3(width, height, along.magnitude), color, root ? root : cityRoot, lot.turn * Quaternion.LookRotation(along));
        }

        /// <summary>The lawn, the hedge with a gap and stone pillars at each gate, and the iron arch over the street gate.</summary>
        void ParkGrounds(Lot lot, ParkLayout P)
        {
            Part(lot, "Park lawn", new Vector3(0, P.LawnTop / 2, 0), new Vector3(P.Size - .04f, P.LawnTop, P.Size - .04f), ParkLawn);
            float edge = P.Half - P.HedgeInset, gate = P.GateHalf, length = edge + P.HedgeThick / 2 - gate, pillar = gate + .035f;
            for (int d = 0; d < 4; d++)
            {
                var heading = ParkLayout.Heading(d);
                var across = new Vector3(heading.z, 0, -heading.x);
                var size = d % 2 == 0 ? new Vector3(length, P.HedgeHeight, P.HedgeThick) : new Vector3(P.HedgeThick, P.HedgeHeight, length);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(lot, "Park hedge", heading * edge + across * side * (gate + length / 2) + Vector3.up * P.HedgeHeight / 2, size, ParkHedge);
                    Part(lot, "Gate pillar", heading * edge + across * side * pillar + Vector3.up * .08f, new Vector3(.075f, .16f, .075f), ParkStone);
                    Part(lot, "Pillar cap", heading * edge + across * side * pillar + Vector3.up * .17f, new Vector3(.09f, .02f, .09f), ParkStoneLight);
                }
            }
            var front = ParkLayout.Heading(0) * edge;
            for (int side = -1; side <= 1; side += 2)
            {
                Part(lot, "Arch post", front + new Vector3(side * pillar, .28f, 0), new Vector3(.025f, .2f, .025f), ParkIron);
                ParkBeam(lot, "Arch", front + new Vector3(side * pillar, .38f, 0), front + new Vector3(0, .44f, 0), .025f, .025f, ParkIron);
            }
            Part(lot, "Arch bar", front + new Vector3(0, .37f, 0), new Vector3(2 * pillar, .02f, .02f), ParkIron);
            Part(lot, "Arch crest", front + new Vector3(0, .45f, 0), new Vector3(.06f, .06f, .02f), ParkGold);
        }
        /// <summary>Gravel paths (the loop and the two straight paths), the paved plaza and its fountain.</summary>
        void ParkPaths(Lot lot, ParkLayout P)
        {
            float w = P.PathWidth, t = .006f, y = P.PathTop - t / 2;
            Part(lot, "Park path", new Vector3(0, y, 0), new Vector3(w, t, P.Size), ParkGravel);
            Part(lot, "Park path", new Vector3(0, y, 0), new Vector3(P.Size, t, w), ParkGravel);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(lot, "Loop path", new Vector3(0, y, side * P.Loop), new Vector3(2 * P.Loop + w, t, w), ParkGravel);
                Part(lot, "Loop path", new Vector3(side * P.Loop, y, 0), new Vector3(w, t, 2 * P.Loop + w), ParkGravel);
            }
            float plaza = 2 * P.Plaza, basin = 2 * P.Basin;
            ParkDisc(lot, "Plaza", Vector3.zero, plaza, plaza, P.PlazaTop, ParkPaving);
            // The fountain: a stone basin, its water, a pedestal and an upper bowl; the jet plays when zoomed in.
            ParkDisc(lot, "Fountain basin", Vector3.zero, basin, basin, .09f, ParkStoneLight);
            ParkDisc(lot, "Fountain water", new Vector3(0, .05f, 0), basin - .05f, basin - .05f, .045f, ParkFountainWater);
            Part(lot, "Fountain pedestal", new Vector3(0, .13f, 0), new Vector3(.05f, .1f, .05f), ParkStoneLight);
            ParkDisc(lot, "Fountain bowl", new Vector3(0, .17f, 0), basin * .45f, basin * .45f, .03f, ParkStoneLight);
            Emit(EmitterKind.Fountain, lot, new Vector3(0, .2f, 0), new Color(.72f, .9f, 1f), P.Basin / .15f);
        }
        /// <summary>The pond on its muddy bank; the lake also gets a wooded island, a boathouse and a jetty.</summary>
        void ParkWater(Lot lot, ParkLayout P)
        {
            var pond = P.Pond;
            var centre = new Vector3(pond.center.x, 0, pond.center.y);
            ParkDisc(lot, "Pond bank", centre, pond.width + .1f, pond.height + .1f, P.BankTop, ParkBank);
            ParkDisc(lot, "Pond", centre, pond.width, pond.height, P.WaterTop, ParkPond);
            if (!P.Lake)
                return;
            float island = 2 * P.IslandRadius;
            ParkDisc(lot, "Island shore", P.Island, island + .05f, island + .05f, .041f, ParkBank);
            ParkDisc(lot, "Island", P.Island, island, island, .046f, ParkLawn);
            ParkTreeAt(lot, P.Island + new Vector3(.03f, .046f, .02f), .95f, true, 0);
            var shed = P.Boathouse;
            var at = new Vector3(shed.center.x, 0, shed.center.y);
            ParkBeam(lot, "Jetty", at + Vector3.up * .045f, P.JettyTip + Vector3.up * .045f, .09f, .014f, ParkTimber);
            Part(lot, "Boathouse", at + new Vector3(0, .08f, 0), new Vector3(shed.width, .16f, shed.height), ParkTimber);
            Gable(lot, at + new Vector3(0, .16f, 0), shed.width + .05f, .1f, shed.height + .05f, ParkKioskDark);
        }
        /// <summary>The playground's red safety surface, the swings' A-frames, the slide and the seesaw and roundabout bases.</summary>
        void ParkPlayground(Lot lot, ParkLayout P)
        {
            var pad = P.Playground;
            Part(lot, "Playground surface", new Vector3(pad.center.x, P.PadTop / 2, pad.center.y), new Vector3(pad.width, P.PadTop, pad.height), ParkSurface);
            float halfBar = P.Swings * P.SwingPitch / 2 + .03f;
            var frame = P.SwingFrame;
            Part(lot, "Swing bar", frame + new Vector3(0, P.SwingBar, 0), new Vector3(2 * halfBar + .03f, .022f, .022f), ParkFrame);
            for (int end = -1; end <= 1; end += 2)
                for (int leg = -1; leg <= 1; leg += 2)
                    ParkBeam(lot, "Swing leg", frame + new Vector3(end * halfBar, P.PadTop, leg * .09f), frame + new Vector3(end * halfBar, P.SwingBar, 0), .018f, .018f, ParkSlide);
            // The slide: a ladder up to a platform on four posts, then a walled chute down to the surface.
            var top = P.SlideTop;
            for (int side = -1; side <= 1; side += 2)
                ParkBeam(lot, "Ladder rail", P.SlideLadder + new Vector3(0, 0, side * .035f), top + new Vector3(-.05f, .06f, side * .035f), .014f, .014f, ParkSlide);
            for (int i = 0; i < 4; i++)
                Part(lot, "Slide post", new Vector3(top.x + (i % 2 == 0 ? -.045f : .045f), top.y / 2, top.z + (i < 2 ? -.045f : .045f)), new Vector3(.014f, top.y, .014f), ParkSlide);
            Part(lot, "Slide platform", top + new Vector3(0, -.01f, 0), new Vector3(.11f, .02f, .11f), ParkFrame);
            var foot = P.SlideEnd + Vector3.up * .045f;
            ParkBeam(lot, "Slide chute", top, foot, .08f, .012f, ParkFrame);
            for (int side = -1; side <= 1; side += 2)
                ParkBeam(lot, "Slide wall", top + new Vector3(0, .015f, side * .042f), foot + new Vector3(0, .015f, side * .042f), .01f, .03f, ParkRed);
            if (P.HasSeesaw)
                Part(lot, "Seesaw pivot", P.Seesaw + new Vector3(0, .035f, 0), new Vector3(.04f, .07f, .06f), ParkSlide);
            if (P.HasRoundabout)
                Part(lot, "Roundabout hub", P.Roundabout + new Vector3(0, .025f, 0), new Vector3(.05f, .05f, .05f), ParkIron);
        }
        /// <summary>Park trees and flower beds; returns the tallest treetop.</summary>
        float ParkPlanting(Lot lot, ParkLayout P)
        {
            float top = 0;
            for (int i = 0; i < P.Trees.Length; i++)
            {
                var tree = P.Trees[i];
                top = Mathf.Max(top, ParkTreeAt(lot, tree.at + Vector3.up * P.LawnTop, tree.scale, tree.round, i + lot.hash));
            }
            for (int i = 0; i < P.Beds.Length; i++)
            {
                var bed = P.Beds[i];
                float d = 2 * bed.radius;
                ParkDisc(lot, "Flower bed", bed.at, d, d, .044f, ParkSoil);
                ParkDisc(lot, "Flowers", bed.at, d - .035f, d - .035f, .05f, ParkBlooms[(i + lot.hash) % ParkBlooms.Length]);
                ParkDisc(lot, "Flowers", bed.at, d * .45f, d * .45f, .056f, ParkBlooms[(i + lot.hash + 2) % ParkBlooms.Length]);
            }
            return top;
        }
        /// <summary>A park tree standing on <paramref name="foot"/>: a round crown (a drum between two low cones) or a pine. Returns its top.</summary>
        float ParkTreeAt(Lot lot, Vector3 foot, float s, bool round, int shade)
        {
            Part(lot, "Tree trunk", foot + new Vector3(0, .09f * s, 0), new Vector3(.045f, .18f, .045f) * s, ParkTimber);
            if (!round)
            {
                Spire(lot, foot + new Vector3(0, .1f * s, 0), .15f * s, .5f * s, ParkPine);
                return foot.y + .6f * s;
            }
            var leaves = shade % 2 == 0 ? ParkCrown : ParkCrownDark;
            float radius = .17f * s, bottom = .15f * s, height = .15f * s;
            ParkDisc(lot, "Tree crown", foot + Vector3.up * bottom, 2 * radius, 2 * radius, height, leaves);
            Shape("Tree crown", cone, lot.at + lot.turn * (foot + Vector3.up * (bottom + height)), new Vector3(radius, .13f * s, radius), leaves, cityRoot, lot.turn);
            Shape("Tree crown", cone, lot.at + lot.turn * (foot + Vector3.up * bottom), new Vector3(radius, .07f * s, radius), leaves, cityRoot, lot.turn * Quaternion.Euler(180, 0, 0));
            return foot.y + bottom + height + .13f * s;
        }
        /// <summary>A round white bandstand on a stone plinth, six slim posts and a red roof with a gold finial.</summary>
        float ParkBandstand(Lot lot, ParkLayout P)
        {
            var at = P.Bandstand;
            float r = P.BandstandRadius, d = 2 * r, roof = P.BandstandRoof;
            ParkDisc(lot, "Bandstand plinth", at, d + .05f, d + .05f, P.StageTop - .025f, ParkStone);
            ParkDisc(lot, "Bandstand stage", at, d, d, P.StageTop, ParkWhite);
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                var post = at + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (r - .03f);
                Part(lot, "Bandstand post", post + Vector3.up * (P.StageTop + roof) / 2, new Vector3(.02f, roof - P.StageTop, .02f), ParkWhite);
            }
            ParkDisc(lot, "Bandstand eaves", at + Vector3.up * (roof - .025f), d + .1f, d + .1f, .03f, ParkWhite);
            Shape("Bandstand roof", cone, lot.at + lot.turn * (at + Vector3.up * roof), new Vector3(r + .07f, .2f, r + .07f), ParkBandRed, cityRoot, lot.turn);
            Part(lot, "Bandstand finial", at + Vector3.up * (roof + .22f), new Vector3(.025f, .06f, .025f), ParkGold);
            return roof + .25f;
        }
        /// <summary>The mint café kiosk with a striped awning over its hatch, and tables under red and green parasols.</summary>
        void ParkCafe(Lot lot, ParkLayout P)
        {
            var k = P.Kiosk;
            var at = new Vector3(k.center.x, 0, k.center.y);
            float h = P.KioskHeight;
            Part(lot, "Café kiosk", at + Vector3.up * h / 2, new Vector3(k.width, h, k.height), ParkKiosk);
            Part(lot, "Kiosk roof", at + Vector3.up * (h + .015f), new Vector3(k.width + .06f, .03f, k.height + .06f), ParkWhite);
            Part(lot, "Kiosk sign", at + new Vector3(0, h + .07f, 0), new Vector3(.04f, .08f, k.height * .7f), ParkKioskDark);
            Part(lot, "Kiosk hatch", P.Hatch + new Vector3(-.004f, h * .56f, 0), new Vector3(.01f, h * .34f, k.height * .6f), ParkIron);
            Part(lot, "Kiosk counter", P.Hatch + new Vector3(-.03f, h * .38f, 0), new Vector3(.06f, .015f, k.height * .62f), ParkWhite);
            for (int i = 0; i < 5; i++)
                Part(lot, "Awning", P.Hatch + new Vector3(-.05f, h * .82f, (i - 2) * k.height * .14f), new Vector3(.1f, .015f, k.height * .14f), i % 2 == 0 ? ParkBandRed : ParkWhite);
            for (int i = 0; i < P.Tables.Length; i++)
            {
                var t = P.Tables[i];
                Part(lot, "Café table", t + Vector3.up * .06f, new Vector3(.1f, .012f, .1f), ParkWhite);
                Part(lot, "Table leg", t + Vector3.up * .03f, new Vector3(.015f, .06f, .015f), ParkIron);
                Part(lot, "Parasol pole", t + Vector3.up * .13f, new Vector3(.012f, .2f, .012f), ParkWhite);
                Shape("Café parasol", cone, lot.at + lot.turn * (t + Vector3.up * .2f), new Vector3(.15f, .06f, .15f), i % 2 == 0 ? ParkBandRed : ParkKioskDark, cityRoot, lot.turn);
            }
        }

        /// <summary>Close-up extras: benches, lamps and bins, reeds and lily pads, café chairs, a boat moored at the jetty; and the park's people.</summary>
        void CityParkDetails(Lot lot, int size)
        {
            var P = ParkLayout.For(size);
            Emit(EmitterKind.Park, lot, Vector3.zero, ParkLawn, P.Size); // people, ducks and boats: ParkLife
            foreach (var seat in P.Benches)
            {
                ParkBench(lot, seat);
                var side = new Vector3(seat.facing.z, 0, -seat.facing.x);
                Detail(lot, "Bin", seat.at + side * .13f + new Vector3(0, .04f, 0), new Vector3(.04f, .08f, .04f), ParkIron);
            }
            foreach (var lamp in P.Lamps)
                LampPost(lot, lamp);
            const int Reeds = 16;
            for (int i = 0; i < Reeds; i++)
            {
                var at = P.OnPond(i * 360f / Reeds + (i % 3) * 6f, 1.03f + (i % 2) * .03f);
                Detail(lot, "Reeds", at + new Vector3(0, .07f, 0), new Vector3(.014f, .08f + (i % 3) * .02f, .014f), ParkReed);
                Detail(lot, "Reeds", at + new Vector3(.02f, .06f, .015f), new Vector3(.012f, .07f, .012f), ParkReed);
            }
            for (int i = 0; i < 4; i++)
                ParkDisc(lot, "Lily pad", P.OnPond(40 + i * 83, .5f + .12f * (i % 2), P.WaterTop), .07f, .06f, .005f, ParkLily, cityDetailRoot);
            if (P.Lake)
            {
                var tip = P.JettyTip;
                var shed = new Vector3(P.Boathouse.center.x, 0, P.Boathouse.center.y);
                var across = Vector3.Cross(Vector3.up, (tip - shed).normalized);
                ParkBeam(lot, "Moored boat", tip - (tip - shed) * .35f + across * .1f + Vector3.up * .045f, tip + across * .1f + Vector3.up * .045f, .08f, .035f, ParkWhite, cityDetailRoot);
                for (int side = -1; side <= 1; side += 2)
                    Detail(lot, "Jetty post", tip + across * side * .045f + Vector3.up * .04f, new Vector3(.02f, .08f, .02f), ParkTimber);
            }
            foreach (var t in P.Tables)
                for (int side = -1; side <= 1; side += 2)
                {
                    Detail(lot, "Café chair", t + new Vector3(side * .085f, .03f, 0), new Vector3(.04f, .012f, .04f), ParkIron);
                    Detail(lot, "Chair back", t + new Vector3(side * .105f, .055f, 0), new Vector3(.008f, .045f, .04f), ParkIron);
                }
            if (P.HasCafe)
                Detail(lot, "Bin", P.Hatch + new Vector3(-.05f, .04f, P.Kiosk.height / 2 + .05f), new Vector3(.04f, .08f, .04f), ParkIron);
        }
        /// <summary>A timber bench on iron legs, its back away from where a sitter looks.</summary>
        void ParkBench(Lot lot, ParkLayout.Seat seat)
        {
            var turn = lot.turn * Quaternion.LookRotation(seat.facing);
            var at = lot.at + lot.turn * seat.at;
            Box("Bench seat", at + turn * new Vector3(0, .05f, 0), new Vector3(.17f, .018f, .055f), ParkTimber, cityDetailRoot, turn);
            Box("Bench back", at + turn * new Vector3(0, .085f, -.03f), new Vector3(.17f, .045f, .012f), ParkTimber, cityDetailRoot, turn);
            for (int side = -1; side <= 1; side += 2)
                Box("Bench leg", at + turn * new Vector3(side * .07f, .025f, 0), new Vector3(.015f, .05f, .05f), ParkIron, cityDetailRoot, turn);
        }
    }
}
