using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// The east coast's static art: the shoreline past the map edge (drawn once with the land) and every beach car park as far as
    /// it is built (redrawn with the towns, into the town batch). <see cref="BeachLife"/> moves the waves, cars and people.
    /// Measurements come from <see cref="BeachLayout"/>.
    /// </summary>
    public sealed partial class WorldView
    {
        /// <summary>The coast's colours, in their own class so their names never clash with other WorldView partials.</summary>
        static class Seaside
        {
            internal static readonly Color SeaShallow = new Color(.28f, .6f, .7f), SeaEdge = new Color(.42f, .73f, .76f),
                WetSand = new Color(.8f, .71f, .5f), Gravel = new Color(.6f, .55f, .46f), Surface = new Color(.29f, .3f, .32f),
                Kerb = new Color(.74f, .73f, .68f), Paint = new Color(.94f, .94f, .9f), SignBlue = new Color(.12f, .35f, .75f),
                Planks = new Color(.63f, .46f, .29f), PlankGap = new Color(.46f, .33f, .2f), Post = new Color(.24f, .26f, .28f),
                Glow = new Color(1f, .93f, .7f), KioskWall = new Color(.95f, .93f, .86f), KioskMint = new Color(.36f, .78f, .72f),
                Stripe = new Color(.9f, .22f, .2f), Guard = new Color(.93f, .92f, .88f), GuardRed = new Color(.85f, .16f, .14f),
                Timber = new Color(.52f, .38f, .24f), ConeOrange = new Color(.95f, .45f, .1f), Wafer = new Color(.85f, .65f, .38f),
                Scoop = new Color(.98f, .75f, .82f), Barrier = new Color(.95f, .65f, .24f);
            internal static readonly Color[] Canopies = { new Color(.92f, .2f, .22f), new Color(.98f, .78f, .14f), new Color(.18f, .5f, .86f), new Color(.2f, .72f, .6f), new Color(.96f, .5f, .15f), new Color(.95f, .95f, .93f), new Color(.9f, .35f, .6f) };
            internal static readonly Color[] Cushions = { new Color(.16f, .4f, .78f), new Color(.2f, .72f, .7f), new Color(.97f, .97f, .95f), new Color(.98f, .8f, .2f) };
            internal static readonly Color[] Towels = { new Color(.96f, .55f, .7f), new Color(.35f, .7f, .95f), new Color(.98f, .85f, .3f), new Color(.5f, .85f, .45f), new Color(.95f, .95f, .93f) };
        }
        BeachLife beach;

        /// <summary>
        /// The shoreline along the whole east edge: wet sand at the waterline and a narrow strip of shallow water where the
        /// waves roll in (BeachLife). No open sea beyond it: the map ends at the shallows.
        /// </summary>
        void BuildSea(Transform ground)
        {
            float mid = (MapDefinition.Size - 1) / 2f, length = MapDefinition.Size, edge = BeachLayout.Waterline, width = BeachLayout.WaterWidth;
            Box("Shallows", new Vector3(edge + width / 2, -.034f, mid), new Vector3(width, .02f, length), Seaside.SeaShallow, ground);
            Box("Shallows edge", new Vector3(edge + .6f, -.028f, mid), new Vector3(1.2f, .02f, length), Seaside.SeaEdge, ground);
            Box("Wet sand", new Vector3(edge - .15f, .003f, mid), new Vector3(.3f, .01f, length), Seaside.WetSand, ground);
        }

        void DrawBeachParks()
        {
            foreach (var road in game.World.intercityRoads)
                if (road.ToBeach && road.park > 0)
                    DrawBeachPark(Coast.Entrance(road), road.park);
        }
        /// <summary>
        /// A beach car park at its construction stage: levelled ground, then the surface and kerbs, the painted bays and
        /// parking sign, lamps with the boardwalk and ice-cream kiosk, and finally umbrellas, towels and the lifeguard tower.
        /// </summary>
        void DrawBeachPark(Cell entrance, int stage)
        {
            float south = BeachLayout.South(entrance), north = BeachLayout.North(entrance), mid = BeachLayout.Middle(entrance);
            float width = BeachLayout.East - BeachLayout.West, length = Coast.ParkLength;
            var centre = new Vector3(BeachLayout.Aisle, 0, mid);
            Box("Car park ground", centre + Vector3.up * .01f, new Vector3(width, .02f, length), Seaside.Gravel, cityRoot);
            if (stage >= 2)
                DrawParkSurface(entrance, south, north, width);
            if (stage >= 3)
                DrawParkBays(entrance);
            if (stage >= 4)
                DrawParkFacilities(entrance, south, north);
            if (stage >= Coast.ParkSteps)
            {
                DrawBeachDay(entrance);
                return;
            }
            // Still being built: the road is closed at the gate with a barrier and cones, and the site marker stands.
            Box("Car park barrier", new Vector3(BeachLayout.West - .05f, .1f, entrance.z), new Vector3(.05f, .06f, .7f), Seaside.Barrier, cityRoot);
            for (int i = -1; i <= 1; i += 2)
                Box("Barrier leg", new Vector3(BeachLayout.West - .05f, .05f, entrance.z + i * .3f), new Vector3(.04f, .1f, .04f), Seaside.Paint, cityRoot);
            for (int i = 0; i < 3; i++)
                Shape("Traffic cone", new Vector3(BeachLayout.Aisle - .4f + .4f * i, .02f, mid - .2f + .3f * (i % 2)), new Vector3(.045f, .12f, .045f), Seaside.ConeOrange, cityRoot);
            Box("Car park construction", centre + Vector3.up * .22f, new Vector3(.6f, .4f, .12f), Seaside.Barrier, cityRoot);
        }
        void DrawParkSurface(Cell entrance, float south, float north, float width)
        {
            float mid = (south + north) / 2, row = BeachLayout.WalkRow(entrance);
            Box("Car park surface", new Vector3(BeachLayout.Aisle, .02f, mid), new Vector3(width - .04f, .02f, north - south - .04f), Seaside.Surface, cityRoot);
            // Kerbs on the ends and the seaward side; the inland side opens where the road comes in, the seaward side at the boardwalk.
            Box("Kerb", new Vector3(BeachLayout.Aisle, .04f, south + .025f), new Vector3(width, .03f, .05f), Seaside.Kerb, cityRoot);
            Box("Kerb", new Vector3(BeachLayout.Aisle, .04f, north - .025f), new Vector3(width, .03f, .05f), Seaside.Kerb, cityRoot);
            KerbRun(BeachLayout.East - .025f, south, row - .2f);
            KerbRun(BeachLayout.East - .025f, row + .2f, north);
            KerbRun(BeachLayout.West + .025f, south, entrance.z - .45f);
            KerbRun(BeachLayout.West + .025f, entrance.z + .45f, north);
        }
        void KerbRun(float x, float from, float to) =>
            Box("Kerb", new Vector3(x, .04f, (from + to) / 2), new Vector3(.05f, .03f, to - from), Seaside.Kerb, cityRoot);
        void DrawParkBays(Cell entrance)
        {
            for (int k = 0; k <= BeachLayout.BaysPerRow; k++)
            {
                float z = BeachLayout.BayLine(entrance, k);
                Box("Bay line", new Vector3(BeachLayout.East - BeachLayout.BayDepth / 2, .032f, z), new Vector3(BeachLayout.BayDepth - .08f, .004f, .025f), Seaside.Paint, cityRoot);
                if (Mathf.Abs(z - entrance.z) > .45f)
                    Box("Bay line", new Vector3(BeachLayout.West + BeachLayout.BayDepth / 2, .032f, z), new Vector3(BeachLayout.BayDepth - .08f, .004f, .025f), Seaside.Paint, cityRoot);
            }
            ParkingSign(new Vector3(BeachLayout.West - .08f, 0, entrance.z - .62f));
        }
        /// <summary>A blue "P" board on a post, read from the road coming in.</summary>
        void ParkingSign(Vector3 at)
        {
            Box("Sign post", at + new Vector3(0, .18f, 0), new Vector3(.025f, .36f, .025f), Seaside.Post, cityRoot);
            var board = at + new Vector3(-.02f, .36f, 0);
            Box("Parking sign", board, new Vector3(.02f, .2f, .2f), Seaside.SignBlue, cityRoot);
            // The letter faces the incoming road (west); from that side +z is on the left.
            var face = board + new Vector3(-.012f, 0, 0);
            Box("P", face + new Vector3(0, 0, .045f), new Vector3(.006f, .15f, .028f), Seaside.Paint, cityRoot);
            Box("P", face + new Vector3(0, .061f, -.005f), new Vector3(.006f, .028f, .1f), Seaside.Paint, cityRoot);
            Box("P", face + new Vector3(0, .002f, -.005f), new Vector3(.006f, .028f, .1f), Seaside.Paint, cityRoot);
            Box("P", face + new Vector3(0, .032f, -.045f), new Vector3(.006f, .06f, .028f), Seaside.Paint, cityRoot);
        }
        void DrawParkFacilities(Cell entrance, float south, float north)
        {
            // Lamps at the four corners.
            foreach (float x in new[] { BeachLayout.West + .1f, BeachLayout.East - .1f })
                foreach (float z in new[] { south + .1f, north - .1f })
                {
                    Box("Lamp post", new Vector3(x, .26f, z), new Vector3(.025f, .5f, .025f), Seaside.Post, cityRoot);
                    Box("Lamp", new Vector3(x, .52f, z), new Vector3(.07f, .035f, .07f), Seaside.Glow, cityRoot);
                }
            // The boardwalk from the car park over the dunes onto the sand.
            float row = BeachLayout.WalkRow(entrance), walk = BeachLayout.WalkwayEnd - BeachLayout.East;
            Box("Boardwalk", new Vector3(BeachLayout.East + walk / 2, .018f, row), new Vector3(walk, .026f, .34f), Seaside.Planks, cityRoot);
            for (int i = 1; i < 6; i++)
                Box("Board gap", new Vector3(BeachLayout.East + walk * i / 6f, .032f, row), new Vector3(.012f, .004f, .34f), Seaside.PlankGap, cityRoot);
            // The ice-cream kiosk opens towards the boardwalk: a back wall, a counter, a striped awning and a cone on top.
            var k = BeachLayout.Kiosk(entrance);
            Box("Kiosk floor", k + new Vector3(0, .015f, 0), new Vector3(.4f, .03f, .42f), Seaside.Planks, cityRoot);
            Box("Kiosk back", k + new Vector3(.16f, .17f, 0), new Vector3(.06f, .34f, .4f), Seaside.KioskWall, cityRoot);
            Box("Kiosk side", k + new Vector3(0, .17f, .18f), new Vector3(.34f, .34f, .04f), Seaside.KioskMint, cityRoot);
            Box("Kiosk side", k + new Vector3(0, .17f, -.18f), new Vector3(.34f, .34f, .04f), Seaside.KioskMint, cityRoot);
            Box("Kiosk counter", k + new Vector3(-.13f, .08f, 0), new Vector3(.08f, .16f, .32f), Seaside.KioskMint, cityRoot);
            for (int i = 0; i < 4; i++)
                Box("Awning", k + new Vector3(-.05f, .36f, -.18f + .12f * i), new Vector3(.5f, .03f, .12f), i % 2 == 0 ? Seaside.Stripe : Seaside.KioskWall, cityRoot);
            Shape("Ice-cream cone", k + new Vector3(.02f, .52f, 0), new Vector3(.05f, .14f, .05f), Seaside.Wafer, cityRoot, Quaternion.Euler(180, 0, 0));
            Box("Ice cream", k + new Vector3(.02f, .55f, 0), new Vector3(.08f, .07f, .08f), Seaside.Scoop, cityRoot);
            // Benches beside the boardwalk.
            foreach (float z in new[] { row - .45f, row + .45f })
            {
                Box("Bench seat", new Vector3(BeachLayout.East + .3f, .07f, z), new Vector3(.28f, .025f, .09f), Seaside.Timber, cityRoot);
                Box("Bench back", new Vector3(BeachLayout.East + .3f, .12f, z + (z > row ? .04f : -.04f)), new Vector3(.28f, .08f, .02f), Seaside.Timber, cityRoot);
            }
        }
        /// <summary>
        /// The finished beach: rows of umbrellas with a pair of sun loungers under each, more umbrellas with towels further
        /// along, and the lifeguard tower with its flag.
        /// </summary>
        void DrawBeachDay(Cell entrance)
        {
            var club = BeachLayout.ClubUmbrellas(entrance);
            for (int i = 0; i < club.Count; i++)
            {
                // Each row of loungers has its own cushion colour; the umbrellas alternate along the row.
                Umbrella(club[i], Seaside.Canopies[(i / 2 + i % 2 * 3) % Seaside.Canopies.Length]);
                for (int side = -1; side <= 1; side += 2)
                    SunLounger(club[i] + new Vector3(0, 0, side * BeachLayout.LoungerGap), Seaside.Cushions[i / 2 % Seaside.Cushions.Length]);
            }
            var spots = BeachLayout.UmbrellaSpots(entrance);
            for (int i = 0; i < spots.Count; i++)
            {
                Umbrella(spots[i], Seaside.Canopies[(entrance.z + i) % Seaside.Canopies.Length]);
                Box("Towel", BeachLayout.Towel(spots[i]) + new Vector3(0, .005f, 0), new Vector3(.15f, .008f, .32f), Seaside.Towels[(entrance.z * 3 + i) % Seaside.Towels.Length], cityRoot);
            }
            var g = BeachLayout.Lifeguard(entrance);
            foreach (float dx in new[] { -.12f, .12f })
                foreach (float dz in new[] { -.12f, .12f })
                    Box("Tower leg", g + new Vector3(dx, .22f, dz), new Vector3(.03f, .44f, .03f), Seaside.Guard, cityRoot);
            Box("Tower deck", g + new Vector3(0, .45f, 0), new Vector3(.34f, .03f, .34f), Seaside.Guard, cityRoot);
            Box("Tower hut", g + new Vector3(.03f, .56f, 0), new Vector3(.22f, .18f, .28f), Seaside.GuardRed, cityRoot);
            Box("Tower roof", g + new Vector3(.03f, .67f, 0), new Vector3(.3f, .03f, .36f), Seaside.Guard, cityRoot);
            Box("Tower ladder", g + new Vector3(-.2f, .22f, 0), new Vector3(.02f, .44f, .1f), Seaside.Timber, cityRoot);
            Box("Flag pole", g + new Vector3(.14f, .85f, .14f), new Vector3(.015f, .36f, .015f), Seaside.Paint, cityRoot);
            Box("Flag", g + new Vector3(.14f, .96f, .23f), new Vector3(.01f, .1f, .16f), Seaside.GuardRed, cityRoot);
        }
        void Umbrella(Vector3 at, Color canopy)
        {
            Box("Umbrella pole", at + new Vector3(0, .21f, 0), new Vector3(.018f, .42f, .018f), Seaside.Paint, cityRoot);
            Shape("Umbrella", at + new Vector3(0, .34f, 0), new Vector3(.34f, .12f, .34f), canopy, cityRoot);
        }
        /// <summary>A sun lounger lying across the beach: white frame on four legs, a cushion, and a back rest raised at the landward end.</summary>
        void SunLounger(Vector3 at, Color cushion)
        {
            Box("Lounger frame", at + new Vector3(0, .045f, 0), new Vector3(.34f, .018f, .13f), Seaside.Paint, cityRoot);
            foreach (float dx in new[] { -.15f, .15f })
                foreach (float dz in new[] { -.055f, .055f })
                    Box("Lounger leg", at + new Vector3(dx, .02f, dz), new Vector3(.018f, .04f, .018f), Seaside.Paint, cityRoot);
            Box("Lounger cushion", at + new Vector3(.05f, .062f, 0), new Vector3(.22f, .018f, .115f), cushion, cityRoot);
            Box("Lounger back", at + new Vector3(-.12f, .095f, 0), new Vector3(.13f, .02f, .115f), cushion, cityRoot, Quaternion.Euler(0, 0, -35));
        }
    }
}
