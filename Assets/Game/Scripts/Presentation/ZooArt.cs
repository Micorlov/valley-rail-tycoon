using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// The city zoo, drawn from <see cref="ZooLayout.Plan"/>: a sandstone wall with a green "ZOO" arch and ticket booths at
    /// the gate, gravel aisles and a paved entrance plaza with an elephant statue and an ice-cream kiosk, the pens (the
    /// penguins' ice and pool, the flamingo lagoon, the elephants' sand yard and bathing pool, the monkey island in its moat
    /// with poles, platforms and ropes, the lions' rock and the savanna with acacias, a watering hole and a feeding deck) and
    /// the miniature railway round them with its two platforms. Fences, sleepers, benches and lamps go in the close-up
    /// detail mesh; <see cref="ZooLife"/> brings the animals, the visitors and the train.
    /// </summary>
    public sealed partial class WorldView
    {
        // Literal colours only: statics of other partial files may not be initialised yet when this file's run.
        static readonly Color ZooLawn = new Color(.4f, .64f, .32f), ZooStone = new Color(.78f, .64f, .46f), ZooStoneLight = new Color(.9f, .82f, .68f),
            ZooIron = new Color(.14f, .28f, .2f), ZooGravel = new Color(.9f, .84f, .7f), ZooPaving = new Color(.82f, .77f, .68f), ZooBrick = new Color(.72f, .4f, .3f),
            ZooIce = new Color(.88f, .93f, .96f), ZooWater = new Color(.22f, .52f, .78f), ZooShallow = new Color(.45f, .74f, .8f), ZooMud = new Color(.52f, .42f, .3f),
            ZooSand = new Color(.88f, .76f, .52f), ZooDry = new Color(.8f, .7f, .44f), ZooSavanna = new Color(.74f, .74f, .4f), ZooGrass = new Color(.46f, .68f, .34f),
            ZooRock = new Color(.58f, .56f, .52f), ZooRockLight = new Color(.72f, .71f, .68f), ZooTimber = new Color(.5f, .35f, .22f), ZooHay = new Color(.93f, .8f, .42f),
            ZooRope = new Color(.8f, .68f, .46f), ZooBallast = new Color(.56f, .53f, .5f), ZooSteel = new Color(.42f, .42f, .46f), ZooSleeper = new Color(.38f, .28f, .2f),
            ZooPlatform = new Color(.8f, .76f, .7f), ZooCanopy = new Color(.2f, .46f, .3f), ZooSign = new Color(.1f, .36f, .2f), ZooGold = new Color(.98f, .8f, .2f),
            ZooBooth = new Color(.97f, .92f, .78f), ZooRed = new Color(.86f, .24f, .2f), ZooWhite = new Color(.96f, .95f, .91f), ZooBronze = new Color(.5f, .4f, .26f),
            ZooLeaf = new Color(.5f, .6f, .26f), ZooLeafDark = new Color(.4f, .52f, .22f), ZooIsland = new Color(.5f, .64f, .3f), ZooGlass = new Color(.72f, .88f, .92f),
            ZooPink = new Color(.98f, .62f, .72f), ZooMint = new Color(.62f, .88f, .76f), ZooReed = new Color(.36f, .52f, .26f), ZooDark = new Color(.16f, .16f, .18f);
        static readonly Color[] ZooPenGround = { new Color(.88f, .93f, .96f), new Color(.46f, .68f, .34f), new Color(.88f, .76f, .52f), new Color(.46f, .68f, .34f), new Color(.8f, .7f, .44f), new Color(.74f, .74f, .4f) };
        static readonly Color[] ZooBlooms = { new Color(.92f, .3f, .38f), new Color(.98f, .8f, .22f), new Color(.72f, .42f, .86f), new Color(1f, .6f, .2f) };

        float DrawZoo(Lot lot)
        {
            var Z = ZooLayout.Plan;
            float top = ZooGrounds(lot, Z);
            ZooPaths(lot, Z);
            ZooRailway(lot, Z);
            for (int i = 0; i < Z.Pens.Length; i++)
            {
                var pen = Z.Pens[i];
                Part(lot, "Pen ground", new Vector3(pen.center.x, Z.PenTop / 2, pen.center.y), new Vector3(pen.width, Z.PenTop, pen.height), ZooPenGround[i]);
            }
            ZooPenguinPool(lot, Z);
            top = Mathf.Max(top, ZooLagoonAndYard(lot, Z));
            top = Mathf.Max(top, ZooMonkeyIsland(lot, Z));
            top = Mathf.Max(top, ZooLionRock(lot, Z));
            top = Mathf.Max(top, ZooSavannaGrounds(lot, Z));
            ZooPlaza(lot, Z);
            return top;
        }
        void ZooBlock(Lot lot, string name, ZooLayout.Block block, float bottom, Color color) =>
            Part(lot, name, block.at + Vector3.up * (bottom + block.size.y / 2), block.size, color);
        /// <summary>A box from <paramref name="a"/> to <paramref name="b"/> on the ground, <paramref name="width"/> wide, stretched a little so bends close up.</summary>
        void ZooRun(Lot lot, string name, Vector3 a, Vector3 b, float width, float bottom, float height, Color color, Transform root = null)
        {
            var along = b - a;
            along.y = 0;
            var turn = lot.turn * Quaternion.LookRotation(along);
            var middle = (a + b) / 2;
            middle.y = bottom + height / 2;
            Box(name, lot.at + lot.turn * middle, new Vector3(width, height, along.magnitude + .02f), color, root ? root : cityRoot, turn);
        }

        /// <summary>The lawn, the wall round it with the gate in the street side, the "ZOO" arch and the ticket booths; returns the arch's top.</summary>
        float ZooGrounds(Lot lot, ZooLayout Z)
        {
            Part(lot, "Zoo lawn", new Vector3(0, Z.GroundTop / 2, 0), new Vector3(ZooLayout.Size - .04f, Z.GroundTop, ZooLayout.Size - .04f), ZooLawn);
            float line = Z.WallLine, outer = line + Z.WallThick / 2, piece = outer - Z.GateHalf;
            for (int d = 0; d < 4; d++)
            {
                var heading = ParkLayout.Heading(d);
                bool alongX = d % 2 == 0;
                float length = alongX ? 2 * outer : 2 * outer - 2 * Z.WallThick;
                var size = alongX ? new Vector3(length, Z.WallHeight, Z.WallThick) : new Vector3(Z.WallThick, Z.WallHeight, length);
                var cap = alongX ? new Vector3(length + .02f, .025f, Z.WallThick + .02f) : new Vector3(Z.WallThick + .02f, .025f, length + .02f);
                if (d != 0)
                {
                    Part(lot, "Zoo wall", heading * line + Vector3.up * Z.WallHeight / 2, size, ZooStone);
                    Part(lot, "Wall coping", heading * line + Vector3.up * Z.WallHeight, cap, ZooStoneLight);
                    continue;
                }
                // The street side: two lengths either side of the gate.
                for (int side = -1; side <= 1; side += 2)
                {
                    var at = new Vector3(side * (Z.GateHalf + piece / 2), 0, line);
                    Part(lot, "Zoo wall", at + Vector3.up * Z.WallHeight / 2, new Vector3(piece, Z.WallHeight, Z.WallThick), ZooStone);
                    Part(lot, "Wall coping", at + Vector3.up * Z.WallHeight, new Vector3(piece + .02f, .025f, Z.WallThick + .02f), ZooStoneLight);
                }
            }
            float top = ZooArch(lot, Z);
            for (int i = 0; i < Z.Booths.Length; i++)
            {
                var booth = Z.Booths[i];
                var at = new Vector3(booth.center.x, 0, booth.center.y);
                float h = Z.BoothHeight, inward = booth.center.x < 0 ? 1 : -1;
                Part(lot, "Ticket booth", at + Vector3.up * h / 2, new Vector3(booth.width, h, booth.height), ZooBooth);
                Part(lot, "Booth window", at + new Vector3(inward * (booth.width / 2 + .004f), h * .6f, 0), new Vector3(.01f, h * .32f, booth.height * .6f), ZooDark);
                Part(lot, "Booth counter", at + new Vector3(inward * (booth.width / 2 + .02f), h * .42f, 0), new Vector3(.04f, .012f, booth.height * .62f), ZooWhite);
                for (int s = 0; s < 4; s++)
                    Part(lot, "Booth roof", at + new Vector3(0, h + .012f, (s - 1.5f) * booth.height / 4 * 1.1f), new Vector3(booth.width + .06f, .024f, booth.height / 4 * 1.1f), s % 2 == 0 ? ZooRed : ZooWhite);
            }
            return top;
        }
        /// <summary>Two stone pillars over the gate carrying a green board with "ZOO" in gold, readable from the street and from inside.</summary>
        float ZooArch(Lot lot, ZooLayout Z)
        {
            float line = Z.WallLine, pillar = Z.GateHalf + .06f, height = .78f, board = .34f, middle = .6f;
            for (int side = -1; side <= 1; side += 2)
            {
                Part(lot, "Gate pillar", new Vector3(side * pillar, height / 2, line), new Vector3(.11f, height, .11f), ZooStone);
                Part(lot, "Pillar cap", new Vector3(side * pillar, height + .015f, line), new Vector3(.14f, .03f, .14f), ZooStoneLight);
                ParkDisc(lot, "Pillar ball", new Vector3(side * pillar, height + .03f, line), .08f, .08f, .07f, ZooGold);
            }
            float width = 2 * pillar + .2f;
            Part(lot, "Zoo sign", new Vector3(0, middle, line), new Vector3(width, board, .04f), ZooSign);
            Part(lot, "Sign frame", new Vector3(0, middle + board / 2, line), new Vector3(width + .02f, .02f, .05f), ZooGold);
            Part(lot, "Sign frame", new Vector3(0, middle - board / 2, line), new Vector3(width + .02f, .02f, .05f), ZooGold);
            const string Text = "ZOO";
            float pixel = .038f, left = -PixelFont.Measure(Text) * pixel / 2, glyphTop = middle + PixelFont.Height * pixel / 2;
            for (int c = 0; c < Text.Length; c++)
            {
                var glyph = PixelFont.Rows(Text[c]);
                for (int y = 0; y < PixelFont.Height; y++)
                    for (int x = 0; x < PixelFont.Width; x++)
                    {
                        if (!PixelFont.Lit(glyph, x, y))
                            continue;
                        float px = left + (c * PixelFont.Advance + x + .5f) * pixel, py = glyphTop - (y + .5f) * pixel;
                        var size = new Vector3(pixel * .92f, pixel * .92f, .012f);
                        // Seen from inside the zoo the letters run the other way, so the back face mirrors them.
                        Part(lot, "Sign letter", new Vector3(px, py, line + .026f), size, ZooGold);
                        Part(lot, "Sign letter", new Vector3(-px, py, line - .026f), size, ZooGold);
                    }
            }
            return height + .1f;
        }
        /// <summary>Gravel aisles, the entrance path through the gate, and the paved plaza with a brick walk down its middle.</summary>
        void ZooPaths(Lot lot, ZooLayout Z)
        {
            float w = Z.PathWidth, t = .006f, y = Z.PathTop - t / 2, a = Z.Aisle, end = Z.AisleEnd;
            for (int side = -1; side <= 1; side += 2)
            {
                Part(lot, "Zoo aisle", new Vector3(0, y, side * a), new Vector3(2 * end, t, w), ZooGravel);
                float south = -a - w / 2, length = Z.PlatformEdge - south;
                Part(lot, "Zoo aisle", new Vector3(side * a, y, south + length / 2), new Vector3(w, t, length), ZooGravel);
            }
            Part(lot, "Entrance path", new Vector3(0, y, (a + Z.Half) / 2), new Vector3(w, t, Z.Half - a), ZooGravel);
            var plaza = Z.Plaza;
            Part(lot, "Zoo plaza", new Vector3(plaza.center.x, Z.PlazaTop / 2, plaza.center.y), new Vector3(plaza.width, Z.PlazaTop, plaza.height), ZooPaving);
            Part(lot, "Plaza walk", new Vector3(0, Z.PlazaTop + .001f, plaza.center.y), new Vector3(w, .004f, plaza.height), ZooBrick);
        }
        /// <summary>The miniature railway: ballast and rails round the pens, buffer stops, and a platform with a shelter at each end.</summary>
        void ZooRailway(Lot lot, ZooLayout Z)
        {
            var track = Z.Track;
            for (int i = 0; i + 1 < track.Length; i++)
            {
                Vector3 a = track[i], b = track[i + 1];
                var across = Vector3.Cross(Vector3.up, (b - a).normalized);
                ZooRun(lot, "Ballast", a, b, Z.Ballast, 0, .04f, ZooBallast);
                for (int side = -1; side <= 1; side += 2)
                    ZooRun(lot, "Rail", a + across * side * Z.Gauge / 2, b + across * side * Z.Gauge / 2, .014f, .04f, .016f, ZooSteel);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var stop = new Vector3(side * (Z.StubEnd - .02f), 0, Z.TrackLine);
                Part(lot, "Buffer stop", stop + Vector3.up * .07f, new Vector3(.03f, .06f, .12f), ZooRed);
                Part(lot, "Buffer stop", stop + Vector3.up * .075f, new Vector3(.032f, .015f, .122f), ZooWhite);
                var platform = Z.Platforms[side < 0 ? 0 : 1];
                var middle = new Vector3(platform.center.x, 0, platform.center.y);
                Part(lot, "Platform", middle + Vector3.up * Z.PlatformTop / 2, new Vector3(platform.width, Z.PlatformTop, platform.height), ZooPlatform);
                // A little shelter: four posts and a green roof.
                var shelter = middle + new Vector3(side * .1f, 0, 0);
                for (int p = 0; p < 4; p++)
                    Part(lot, "Shelter post", shelter + new Vector3((p % 2 == 0 ? -1 : 1) * .2f, Z.PlatformTop + .12f, (p < 2 ? -1 : 1) * .04f), new Vector3(.015f, .24f, .015f), ZooIron);
                Part(lot, "Shelter roof", shelter + Vector3.up * (Z.PlatformTop + .25f), new Vector3(.48f, .025f, .14f), ZooCanopy);
            }
        }
        /// <summary>The penguins' pool with an icy rim, and their rocks.</summary>
        void ZooPenguinPool(Lot lot, ZooLayout Z)
        {
            var pool = Z.PenguinPool;
            var centre = new Vector3(pool.center.x, 0, pool.center.y);
            ParkDisc(lot, "Pool rim", centre, pool.width + .08f, pool.height + .08f, Z.BankTop, ZooWhite);
            ParkDisc(lot, "Penguin pool", centre, pool.width, pool.height, Z.WaterTop, ZooWater);
            foreach (var rock in Z.PenguinRocks)
            {
                ZooBlock(lot, "Penguin rock", rock, Z.PenTop, ZooRockLight);
                Part(lot, "Snow cap", rock.at + Vector3.up * (Z.PenTop + rock.size.y + .01f), new Vector3(rock.size.x * .7f, .02f, rock.size.z * .7f), ZooWhite);
            }
        }
        /// <summary>The flamingo lagoon and its tree, and the elephants' bathing pool, hay rack, shade tree and boulder.</summary>
        float ZooLagoonAndYard(Lot lot, ZooLayout Z)
        {
            var lagoon = Z.Lagoon;
            var centre = new Vector3(lagoon.center.x, 0, lagoon.center.y);
            ParkDisc(lot, "Lagoon bank", centre, lagoon.width + .1f, lagoon.height + .1f, Z.BankTop, ZooSand);
            ParkDisc(lot, "Lagoon", centre, lagoon.width, lagoon.height, Z.WaterTop, ZooShallow);
            float top = ParkTreeAt(lot, Z.FlamingoTree.at + Vector3.up * Z.PenTop, Z.FlamingoTree.scale, true, lot.hash);
            var pool = Z.ElephantPool;
            var middle = new Vector3(pool.center.x, 0, pool.center.y);
            ParkDisc(lot, "Wallow", middle, pool.width + .1f, pool.height + .1f, Z.BankTop, ZooMud);
            ParkDisc(lot, "Bathing pool", middle, pool.width, pool.height, Z.WaterTop, ZooWater);
            var rack = Z.HayRack;
            for (int end = -1; end <= 1; end += 2)
                Part(lot, "Rack post", rack.at + new Vector3(0, Z.PenTop + .1f, end * rack.size.z / 2), new Vector3(.025f, .2f, .025f), ZooTimber);
            ZooBlock(lot, "Hay rack", rack, Z.PenTop + .08f, ZooTimber);
            Part(lot, "Hay", rack.at + Vector3.up * (Z.PenTop + .08f + rack.size.y + .02f), new Vector3(rack.size.x + .04f, .05f, rack.size.z - .02f), ZooHay);
            ZooBlock(lot, "Boulder", Z.Boulder, Z.PenTop, ZooRock);
            return Mathf.Max(top, ParkTreeAt(lot, Z.ElephantTree.at + Vector3.up * Z.PenTop, Z.ElephantTree.scale, true, lot.hash + 1));
        }
        /// <summary>The moat, the island, three climbing poles with platforms and ropes between them, and the monkeys' hut.</summary>
        float ZooMonkeyIsland(Lot lot, ZooLayout Z)
        {
            float moat = 2 * Z.MoatRadius, island = 2 * Z.IslandRadius, ground = Z.IslandTop;
            ParkDisc(lot, "Moat", Vector3.zero, moat, moat, Z.WaterTop, ZooWater);
            ParkDisc(lot, "Island shore", Vector3.zero, island + .06f, island + .06f, ground - .012f, ZooMud);
            ParkDisc(lot, "Monkey island", Vector3.zero, island, island, ground, ZooIsland);
            float top = 0;
            for (int i = 0; i < Z.Poles.Length; i++)
            {
                var pole = Z.Poles[i];
                var foot = new Vector3(pole.x, ground, pole.z);
                Part(lot, "Climbing pole", foot + Vector3.up * pole.y / 2, new Vector3(.03f, pole.y, .03f), ZooTimber);
                ParkDisc(lot, "Pole platform", foot + Vector3.up * pole.y, .16f, .16f, .02f, ZooTimber);
                var next = Z.Poles[(i + 1) % Z.Poles.Length];
                ParkBeam(lot, "Rope", foot + Vector3.up * (pole.y - .02f), new Vector3(next.x, ground + next.y - .02f, next.z), .01f, .01f, ZooRope);
                top = Mathf.Max(top, ground + pole.y + .02f);
            }
            var hut = Z.MonkeyHouse;
            ZooBlock(lot, "Monkey hut", hut, ground, ZooTimber);
            Gable(lot, hut.at + Vector3.up * (ground + hut.size.y), hut.size.x + .04f, .06f, hut.size.z + .04f, ZooCanopy);
            return top;
        }
        /// <summary>The lions' rock, their log and shade tree.</summary>
        float ZooLionRock(Lot lot, ZooLayout Z)
        {
            float bottom = Z.PenTop;
            for (int i = 0; i < Z.LionRock.Length; i++)
            {
                ZooBlock(lot, "Lion rock", Z.LionRock[i], bottom, i == 0 ? ZooRock : ZooRockLight);
                bottom += Z.LionRock[i].size.y;
            }
            ZooRun(lot, "Log", Z.LogFrom, Z.LogTo, .06f, Z.PenTop, .05f, ZooTimber);
            return ParkTreeAt(lot, Z.LionTree.at + Vector3.up * Z.PenTop, Z.LionTree.scale, true, lot.hash + 2);
        }
        /// <summary>The savanna's watering hole, acacias and rocks, and the timber feeding deck on its fence.</summary>
        float ZooSavannaGrounds(Lot lot, ZooLayout Z)
        {
            var hole = Z.Waterhole;
            var centre = new Vector3(hole.center.x, 0, hole.center.y);
            ParkDisc(lot, "Waterhole bank", centre, hole.width + .1f, hole.height + .1f, Z.BankTop, ZooMud);
            ParkDisc(lot, "Waterhole", centre, hole.width, hole.height, Z.WaterTop, ZooWater);
            float top = 0;
            foreach (var tree in Z.Acacias)
                top = Mathf.Max(top, ZooAcacia(lot, tree.at + Vector3.up * Z.PenTop, tree.scale));
            foreach (var rock in Z.SavannaRocks)
                ZooBlock(lot, "Rock", rock, Z.PenTop, ZooRock);
            var deck = Z.Deck;
            var at = new Vector3(deck.center.x, 0, deck.center.y);
            Part(lot, "Feeding deck", at + Vector3.up * (Z.DeckTop - .015f), new Vector3(deck.width, .03f, deck.height), ZooTimber);
            for (int i = 0; i < 4; i++)
                Part(lot, "Deck post", at + new Vector3((i % 2 == 0 ? -1 : 1) * (deck.width / 2 - .02f), Z.DeckTop / 2, (i < 2 ? -1 : 1) * (deck.height / 2 - .02f)), new Vector3(.025f, Z.DeckTop, .025f), ZooTimber);
            return top;
        }
        /// <summary>A flat-topped acacia: a slim trunk with a bough and two layers of wide, flat crown. Returns its top.</summary>
        float ZooAcacia(Lot lot, Vector3 foot, float s)
        {
            Part(lot, "Acacia trunk", foot + Vector3.up * .26f * s, new Vector3(.035f, .52f, .035f) * s, ZooTimber);
            ParkBeam(lot, "Acacia bough", foot + Vector3.up * .36f * s, foot + new Vector3(.13f, .5f, .06f) * s, .022f * s, .022f * s, ZooTimber);
            ParkDisc(lot, "Acacia crown", foot + Vector3.up * .5f * s, .62f * s, .5f * s, .06f * s, ZooLeaf);
            ParkDisc(lot, "Acacia crown", foot + new Vector3(.08f, .55f, .05f) * s, .38f * s, .32f * s, .05f * s, ZooLeafDark);
            return foot.y + .6f * s;
        }
        /// <summary>The plaza's bronze elephant on its plinth, the ice-cream kiosk and two flower beds.</summary>
        void ZooPlaza(Lot lot, ZooLayout Z)
        {
            var plinth = Z.Plinth;
            ZooBlock(lot, "Statue plinth", plinth, Z.PlazaTop, ZooStoneLight);
            // The statue faces the gate, greeting people coming in.
            float s = .5f, y = Z.PlazaTop + plinth.size.y;
            var at = plinth.at + Vector3.up * y;
            Part(lot, "Statue body", at + new Vector3(0, .3f, 0) * s, new Vector3(.22f, .2f, .36f) * s, ZooBronze);
            for (int i = 0; i < 4; i++)
                Part(lot, "Statue leg", at + new Vector3((i % 2 == 0 ? -.07f : .07f), .1f, (i < 2 ? -.12f : .12f)) * s, new Vector3(.07f, .2f, .07f) * s, ZooBronze);
            Part(lot, "Statue head", at + new Vector3(0, .38f, .22f) * s, new Vector3(.16f, .16f, .12f) * s, ZooBronze);
            for (int side = -1; side <= 1; side += 2)
                Part(lot, "Statue ear", at + new Vector3(side * .1f, .38f, .2f) * s, new Vector3(.03f, .15f, .12f) * s, ZooBronze);
            ParkBeam(lot, "Statue trunk", at + new Vector3(0, .34f, .28f) * s, at + new Vector3(0, .5f, .4f) * s, .05f * s, .05f * s, ZooBronze);
            var k = Z.Kiosk;
            var kiosk = new Vector3(k.center.x, 0, k.center.y);
            float h = Z.KioskHeight;
            Part(lot, "Ice-cream kiosk", kiosk + Vector3.up * h / 2, new Vector3(k.width, h, k.height), ZooWhite);
            for (int i = 0; i < 4; i++)
                Part(lot, "Kiosk roof", kiosk + new Vector3(0, h + .012f, (i - 1.5f) * k.height / 4 * 1.1f), new Vector3(k.width + .06f, .024f, k.height / 4 * 1.1f), i % 2 == 0 ? ZooPink : ZooWhite);
            Part(lot, "Kiosk hatch", Z.Hatch + new Vector3(-.004f, h * .56f, 0), new Vector3(.01f, h * .34f, k.height * .6f), ZooDark);
            Part(lot, "Kiosk counter", Z.Hatch + new Vector3(-.03f, h * .38f, 0), new Vector3(.06f, .015f, k.height * .62f), ZooMint);
            // A giant cone on the roof.
            var cone0 = kiosk + Vector3.up * (h + .2f);
            Shape("Giant cone", cone, lot.at + lot.turn * cone0, new Vector3(.05f, .15f, .05f), ZooHay, cityRoot, lot.turn * Quaternion.Euler(180, 0, 0));
            ParkDisc(lot, "Giant scoop", cone0 - Vector3.up * .01f, .1f, .1f, .07f, ZooPink);
            ParkDisc(lot, "Giant scoop", cone0 + Vector3.up * .05f, .07f, .07f, .05f, ZooWhite);
            for (int i = 0; i < Z.Beds.Length; i++)
            {
                var bed = Z.Beds[i].at;
                ParkDisc(lot, "Flower bed", bed, .2f, .2f, Z.PlazaTop + .012f, ParkSoil);
                ParkDisc(lot, "Flowers", bed, .17f, .17f, Z.PlazaTop + .018f, ZooBlooms[(i + lot.hash) % ZooBlooms.Length]);
                ParkDisc(lot, "Flowers", bed, .08f, .08f, Z.PlazaTop + .024f, ZooBlooms[(i + lot.hash + 2) % ZooBlooms.Length]);
            }
        }

        /// <summary>Close-up extras: pen fences and the wall's railing, sleepers, benches, lamps, reeds and hay; and the zoo's life.</summary>
        void ZooDetails(Lot lot)
        {
            var Z = ZooLayout.Plan;
            Emit(EmitterKind.Zoo, lot, Vector3.zero, ZooLawn, ZooLayout.Size); // animals, visitors and the train: ZooLife
            for (int i = 0; i < Z.Pens.Length; i++)
                ZooFence(lot, Z.Pens[i], Z.PenTop, Z.FenceHeights[i], (ZooLayout.Exhibit)i == ZooLayout.Exhibit.Penguins);
            ZooWallRailing(lot, Z);
            // Sleepers every tenth of a cell along the line.
            for (float d = .05f; d < Z.TrackLength; d += .1f)
            {
                var at = Z.OnTrack(d, out var along);
                Box("Sleeper", lot.at + lot.turn * (at + Vector3.up * .044f), new Vector3(.13f, .008f, .03f), ZooSleeper, cityDetailRoot, lot.turn * Quaternion.LookRotation(along));
            }
            foreach (var platform in Z.Platforms)
                Detail(lot, "Platform edge", new Vector3(platform.center.x, Z.PlatformTop + .002f, platform.yMax - .012f), new Vector3(platform.width, .004f, .02f), ZooWhite);
            foreach (var seat in Z.Benches)
                ParkBench(lot, new ParkLayout.Seat { at = seat.at + Vector3.up * (seat.at.z > Z.PlatformEdge ? Z.PlatformTop : Z.PlazaTop), facing = seat.facing });
            foreach (var lamp in Z.Lamps)
                LampPost(lot, lamp);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 2; i++)
                    Detail(lot, "Turnstile", new Vector3(side * (.12f + i * .07f), .06f, Z.WallLine - .12f), new Vector3(.025f, .09f, .06f), ZooSteel);
            const int Reeds = 14;
            for (int i = 0; i < Reeds; i++)
            {
                var at = ZooLayout.OnOval(Z.Lagoon, 200 + i * 160f / Reeds, 1.02f + (i % 2) * .04f);
                Detail(lot, "Reeds", at + new Vector3(0, .08f, 0), new Vector3(.014f, .1f + (i % 3) * .02f, .014f), ZooReed);
                Detail(lot, "Reeds", at + new Vector3(.02f, .07f, .015f), new Vector3(.012f, .08f, .012f), ZooReed);
            }
            foreach (var bale in new[] { new Vector3(-1.9f, 0, -1.3f), new Vector3(1.55f, 0, -2.2f), new Vector3(-2.26f, 0, .1f) })
                Detail(lot, "Hay bale", bale + Vector3.up * (Z.PenTop + .03f), new Vector3(.1f, .06f, .07f), ZooHay);
            var deck = Z.Deck;
            Detail(lot, "Deck rail", new Vector3(deck.center.x, Z.DeckTop + .07f, deck.yMin + .01f), new Vector3(deck.width, .015f, .015f), ZooTimber);
            for (int side = -1; side <= 1; side += 2)
                Detail(lot, "Deck rail", new Vector3(deck.center.x + side * (deck.width / 2 - .01f), Z.DeckTop + .07f, deck.center.y), new Vector3(.015f, .015f, deck.height), ZooTimber);
        }
        /// <summary>A pen's fence round <paramref name="pen"/>: posts and two rails, or a glass screen with a rail on top.</summary>
        void ZooFence(Lot lot, Rect pen, float ground, float height, bool glass)
        {
            for (int d = 0; d < 4; d++)
            {
                bool alongX = d % 2 == 0;
                float length = alongX ? pen.width : pen.height;
                var from = new Vector3(d == 1 ? pen.xMax : pen.xMin, 0, d == 0 ? pen.yMax : pen.yMin);
                var dir = alongX ? Vector3.right : Vector3.forward;
                var middle = from + dir * length / 2;
                var rail = alongX ? new Vector3(length, .012f, .012f) : new Vector3(.012f, .012f, length);
                if (glass)
                    Detail(lot, "Glass screen", middle + Vector3.up * (ground + height / 2), alongX ? new Vector3(length, height, .008f) : new Vector3(.008f, height, length), ZooGlass);
                else
                    Detail(lot, "Fence rail", middle + Vector3.up * (ground + height * .5f), rail, ZooIron);
                Detail(lot, "Fence rail", middle + Vector3.up * (ground + height), rail, glass ? ZooSteel : ZooIron);
                int posts = Mathf.Max(2, Mathf.RoundToInt(length / .2f));
                for (int p = 0; p <= posts; p++)
                    Detail(lot, "Fence post", from + dir * (length * p / posts) + Vector3.up * (ground + height / 2), new Vector3(.014f, height, .014f), glass ? ZooSteel : ZooIron);
            }
        }
        /// <summary>Iron bars along the top of the boundary wall.</summary>
        void ZooWallRailing(Lot lot, ZooLayout Z)
        {
            float line = Z.WallLine, top = Z.WallHeight + .012f, bar = .07f;
            for (int d = 0; d < 4; d++)
            {
                var heading = ParkLayout.Heading(d);
                var across = new Vector3(heading.z, 0, -heading.x);
                for (float t = -line; t <= line + .01f; t += .12f)
                {
                    if (d == 0 && Mathf.Abs(t) < Z.GateHalf + .1f)
                        continue;
                    Detail(lot, "Wall bar", heading * line + across * t + Vector3.up * (top + bar / 2), new Vector3(.01f, bar, .01f), ZooIron);
                }
            }
        }
    }
}
