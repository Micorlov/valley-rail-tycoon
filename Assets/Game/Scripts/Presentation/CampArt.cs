using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Campsites as far as they are built (redrawn with the towns, into the town batch): cleared ground with survey
    /// stakes, then grass, the gravel lane, pitches, hedges and the gate, and finally the reception, the shower block,
    /// the campfire ring, trees and the pitches' tents, caravans or log cabins (Campsites.Kinds). <see cref="CampLife"/>
    /// lights the fire and brings the campers; measurements come from <see cref="CampLayout"/>.
    /// </summary>
    public sealed partial class WorldView
    {
        /// <summary>The campsites' colours, in their own class so their names never clash with other WorldView partials.</summary>
        static class Camping
        {
            internal static readonly Color Dirt = new Color(.55f, .47f, .36f), Grass = new Color(.43f, .66f, .31f), Pitch = new Color(.5f, .72f, .35f),
                Gravel = new Color(.74f, .68f, .57f), Hedge = new Color(.2f, .43f, .21f), Timber = new Color(.62f, .43f, .25f), DarkTimber = new Color(.42f, .28f, .16f),
                OfficeRoof = new Color(.26f, .4f, .25f), ShowerWall = new Color(.93f, .92f, .86f), ShowerRoof = new Color(.3f, .32f, .35f), Door = new Color(.2f, .45f, .7f),
                Stone = new Color(.62f, .62f, .6f), Ash = new Color(.18f, .17f, .16f), Log = new Color(.46f, .31f, .18f), Caravan = new Color(.96f, .96f, .93f),
                CaravanRoof = new Color(.82f, .83f, .82f), Window = new Color(.2f, .3f, .38f), Rubber = new Color(.09f, .09f, .1f), Post = new Color(.3f, .3f, .32f),
                Hookup = new Color(.2f, .55f, .9f), Stake = new Color(.95f, .95f, .92f), Flag = new Color(.95f, .45f, .1f), Leaf = new Color(.2f, .44f, .22f),
                Trunk = new Color(.36f, .25f, .15f), Awning = new Color(.93f, .86f, .66f), Board = new Color(.95f, .65f, .24f), Chair = new Color(.2f, .5f, .75f),
                Table = new Color(.6f, .45f, .3f), Cone = new Color(.95f, .45f, .1f), Footing = new Color(.72f, .72f, .7f);
            internal static readonly Color[] Tents = { new Color(.9f, .3f, .2f), new Color(.2f, .55f, .85f), new Color(.95f, .72f, .15f), new Color(.3f, .68f, .4f), new Color(.6f, .35f, .75f), new Color(.95f, .5f, .15f) };
            internal static readonly Color[] Stripes = { new Color(.85f, .2f, .2f), new Color(.15f, .45f, .8f), new Color(.2f, .6f, .35f), new Color(.95f, .6f, .1f) };
            internal static readonly Color[] CabinRoofs = { new Color(.55f, .2f, .15f), new Color(.25f, .4f, .25f), new Color(.32f, .32f, .36f) };
        }
        CampLife campLife;

        /// <summary>Every planned campsite at its stage.</summary>
        void DrawCampsites()
        {
            foreach (var road in game.World.intercityRoads)
                if (Campsites.Planned(road) && Campsites.Shaped(road))
                    DrawCampsite(road);
        }
        void DrawCampsite(IntercityRoadState road)
        {
            var f = new CampLayout(road);
            for (int i = 0; i < Campsites.Cells; i++)
                if (trees.TryGetValue(Campsites.SiteCell(road, i).Key, out var tree))
                    tree.SetActive(false);
            if (road.camp < 2)
            {
                CampSlab(f, "Campsite ground", CampLayout.U0, CampLayout.U1, CampLayout.Near, CampLayout.Far, .006f, .012f, Camping.Dirt);
                foreach (float u in new[] { CampLayout.U0 + .08f, CampLayout.U1 - .08f })
                    foreach (float v in new[] { CampLayout.Near + .08f, CampLayout.Far - .08f })
                    {
                        CampPart(f, "Survey stake", u, .06f, v, .025f, .12f, .025f, Camping.Stake);
                        CampPart(f, "Stake flag", u, .13f, v, .03f, .03f, .03f, Camping.Flag);
                    }
                CampWorks(f);
                return;
            }
            DrawCampGround(f);
            if (road.camp < Campsites.Steps)
            {
                CampSlab(f, "Footing", CampLayout.OfficeU - .22f, CampLayout.OfficeU + .22f, CampLayout.OfficeV - .19f, CampLayout.OfficeV + .19f, .03f, .012f, Camping.Footing);
                CampSlab(f, "Footing", CampLayout.ShowerU - .25f, CampLayout.ShowerU + .25f, CampLayout.ShowerV - .21f, CampLayout.ShowerV + .21f, .03f, .012f, Camping.Footing);
                // Timber stacked for the reception and the pitches marked out with stakes.
                for (int i = 0; i < 3; i++)
                    CampPart(f, "Timber stack", CampLayout.OfficeU, .045f + .03f * i, CampLayout.OfficeV + .45f, .36f, .025f, .12f - .02f * i, Camping.Timber);
                for (int p = 0; p < Campsites.Pitches; p++)
                    CampPart(f, "Pitch stake", CampLayout.PitchU[p], .05f, CampLayout.PitchV(p), .025f, .1f, .025f, Camping.Stake);
                CampWorks(f);
                return;
            }
            DrawCampOffice(f);
            DrawShowerBlock(f);
            DrawCampfireRing(f);
            DrawCampTrees(f);
            for (int p = 0; p < Campsites.Pitches; p++)
                DrawPitch(f, p, road.a + road.b);
        }
        void CampPart(in CampLayout f, string name, float u, float y, float v, float su, float sy, float sv, Color color, Transform root = null) =>
            Box(name, f.At(u, v, y), f.Size(su, sy, sv), color, root ? root : cityRoot);
        void CampSlab(in CampLayout f, string name, float u0, float u1, float v0, float v1, float y, float height, Color color, Transform root = null) =>
            CampPart(f, name, (u0 + u1) / 2, y, (v0 + v1) / 2, u1 - u0, height, v1 - v0, color, root);
        /// <summary>A gable roof centred on (u, v) standing on <paramref name="eaves"/>, its ridge along the road.</summary>
        void CampGable(in CampLayout f, float u, float v, float su, float sv, float eaves, float rise, Color color) =>
            Shape("Gable roof", prism, f.At(u, v, eaves), new Vector3(su, rise, sv), color, cityRoot, f.Facing(0, 1));
        /// <summary>While the site is being built: cones across the gate and the builder's board.</summary>
        void CampWorks(in CampLayout f)
        {
            for (int i = -1; i <= 1; i++)
                Shape("Traffic cone", f.At(CampLayout.GateU + i * .16f, .6f, .02f), new Vector3(.045f, .12f, .045f), Camping.Cone, cityRoot);
            CampPart(f, "Campsite construction", CampLayout.GateU + .5f, .22f, .58f, .5f, .36f, .1f, Camping.Board);
        }
        /// <summary>Grass, the pitches and their car pads, the gravel lane, the hedge round the site and the gate.</summary>
        void DrawCampGround(in CampLayout f)
        {
            float u0 = CampLayout.U0, u1 = CampLayout.U1, near = CampLayout.Near, far = CampLayout.Far, half = CampLayout.LaneWidth / 2;
            CampSlab(f, "Campsite grass", u0, u1, near, far, .01f, .02f, Camping.Grass);
            for (int p = 0; p < Campsites.Pitches; p++)
            {
                float u = CampLayout.PitchU[p], v = CampLayout.PitchV(p);
                CampSlab(f, "Pitch", u - CampLayout.PitchWidth / 2 + .02f, u + CampLayout.PitchWidth / 2 - .02f, v - CampLayout.PitchDepth / 2 + .02f, v + CampLayout.PitchDepth / 2 - .02f, .021f, .004f, Camping.Pitch);
                var pad = f.CarSpot(p);
                Box("Car pad", new Vector3(pad.x, .024f, pad.z), f.Size(.2f, .006f, .3f), Camping.Gravel, cityRoot);
            }
            CampSlab(f, "Gravel lane", CampLayout.GateU - half, CampLayout.LaneEnd, CampLayout.LaneV - half, CampLayout.LaneV + half, .024f, .008f, Camping.Gravel);
            CampSlab(f, "Gravel lane", CampLayout.GateU - half, CampLayout.GateU + half, near, CampLayout.LaneV, .024f, .008f, Camping.Gravel);
            // The hedge round the site, open at the gate.
            const float Hedge = .07f, High = .1f;
            CampSlab(f, "Hedge", u0, CampLayout.GateU - half - .06f, near, near + Hedge, High / 2 + .02f, High, Camping.Hedge);
            CampSlab(f, "Hedge", CampLayout.GateU + half + .06f, u1, near, near + Hedge, High / 2 + .02f, High, Camping.Hedge);
            CampSlab(f, "Hedge", u0, u1, far - Hedge, far, High / 2 + .02f, High, Camping.Hedge);
            CampSlab(f, "Hedge", u0, u0 + Hedge, near, far, High / 2 + .02f, High, Camping.Hedge);
            CampSlab(f, "Hedge", u1 - Hedge, u1, near, far, High / 2 + .02f, High, Camping.Hedge);
            // The gate: two timber posts and a beam with the site's board across the top.
            foreach (float u in new[] { CampLayout.GateU - half - .03f, CampLayout.GateU + half + .03f })
                CampPart(f, "Gate post", u, .17f, near + .04f, .04f, .34f, .04f, Camping.DarkTimber);
            CampPart(f, "Gate beam", CampLayout.GateU, .33f, near + .04f, CampLayout.LaneWidth + .14f, .035f, .035f, Camping.DarkTimber);
            CampPart(f, "Gate board", CampLayout.GateU, .4f, near + .04f, CampLayout.LaneWidth + .02f, .09f, .015f, Camping.Board);
        }
        /// <summary>The timber reception hut by the gate, its door and window on the lane side.</summary>
        void DrawCampOffice(in CampLayout f)
        {
            float u = CampLayout.OfficeU, v = CampLayout.OfficeV;
            CampPart(f, "Reception", u, .13f, v, .4f, .22f, .34f, Camping.Timber);
            CampGable(f, u, v, .46f, .4f, .24f, .12f, Camping.OfficeRoof);
            // The door faces the gate lane, a window the road and one the pitches.
            CampPart(f, "Reception door", u - .202f, .09f, v - .06f, .01f, .14f, .08f, Camping.DarkTimber);
            CampPart(f, "Reception step", u - .23f, .025f, v - .06f, .06f, .02f, .12f, Camping.DarkTimber);
            CampPart(f, "Reception window", u + .06f, .15f, v - .172f, .12f, .07f, .01f, Camping.Window);
            CampPart(f, "Reception window", u, .15f, v + .172f, .12f, .07f, .01f, Camping.Window);
        }
        /// <summary>The white shower and toilet block behind the lane: two doors facing it, a flat roof and a vent.</summary>
        void DrawShowerBlock(in CampLayout f)
        {
            float u = CampLayout.ShowerU, v = CampLayout.ShowerV;
            CampPart(f, "Shower block", u, .13f, v, .46f, .22f, .38f, Camping.ShowerWall);
            CampPart(f, "Shower block roof", u, .25f, v, .5f, .025f, .42f, Camping.ShowerRoof);
            CampPart(f, "Roof vent", u + .12f, .28f, v + .08f, .06f, .05f, .06f, Camping.ShowerRoof);
            foreach (float du in new[] { -.11f, .11f })
                CampPart(f, "Shower door", u + du, .09f, v - .192f, .08f, .15f, .01f, Camping.Door);
            CampPart(f, "Shower window", u, .19f, v - .192f, .06f, .03f, .01f, Camping.Window);
        }
        /// <summary>A ring of stones round the ashes, with log benches round it; CampLife lights the fire.</summary>
        void DrawCampfireRing(in CampLayout f)
        {
            var centre = f.At(CampLayout.FireU, CampLayout.FireV, .026f);
            Box("Ashes", centre, f.Size(.2f, .006f, .2f), Camping.Ash, cityRoot);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var at = centre + f.Offset(Mathf.Cos(a), Mathf.Sin(a)) * CampLayout.FireRing;
                Box("Fire stone", at + Vector3.up * .01f, new Vector3(.05f, .035f, .05f), Camping.Stone, cityRoot, Quaternion.AngleAxis(i * 25, Vector3.up));
            }
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.PI * (.25f + i * .6f);
                var dir = f.Offset(Mathf.Cos(a), Mathf.Sin(a));
                Box("Log bench", centre + dir * .32f + Vector3.up * .02f, new Vector3(.06f, .045f, .2f), Camping.Log, cityRoot, Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir)));
            }
        }
        /// <summary>Broadleaf trees in the corners, so the site reads as a campsite in the country.</summary>
        void DrawCampTrees(in CampLayout f)
        {
            var spots = new[] { new Vector2(CampLayout.U0 + .14f, CampLayout.Far - .16f), new Vector2(CampLayout.U1 - .14f, CampLayout.Far - .16f),
                new Vector2(CampLayout.U1 - .14f, CampLayout.Near + .22f), new Vector2(CampLayout.ShowerU + .45f, CampLayout.Far - .14f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var at = f.At(spots[i].x, spots[i].y, .02f);
                float h = .5f + .08f * (i % 3);
                Box("Camp tree trunk", at + Vector3.up * h * .3f, new Vector3(.05f, h * .6f, .05f), Camping.Trunk, cityRoot);
                Shape("Camp tree crown", at + Vector3.up * h * .35f, new Vector3(.17f, h * .75f, .17f), Camping.Leaf, cityRoot);
                Box("Camp tree crown", at + Vector3.up * h * .55f, new Vector3(.22f, .16f, .22f), Camping.Leaf, cityRoot, Quaternion.AngleAxis(45, Vector3.up));
            }
        }
        /// <summary>A tent on its own root for CampLife to pitch and strike on the visitor pitch; the root stands on the ground, ridge along its x.</summary>
        internal Transform VisitorTent(Transform parent, Color colour)
        {
            var root = new GameObject("Visitor tent").transform;
            root.SetParent(parent, false);
            Shape("Tent", prism, Vector3.zero, new Vector3(.3f, .17f, .26f), colour, root);
            return root;
        }
        /// <summary>A pitch's tent, caravan or cabin in the site's style. The visitor pitch waits empty for CampLife's arrivals (tents, caravans).</summary>
        void DrawPitch(in CampLayout f, int pitch, int seed)
        {
            float toLane = CampLayout.ToLane(pitch);
            if (f.kind == 2)
            {
                DrawCabin(f, pitch, seed);
                return;
            }
            // Every pitch has its hook-up post by the car pad.
            var post = f.At(CampLayout.PitchU[pitch] + .3f, CampLayout.PitchV(pitch) + toLane * .38f, .06f);
            Box("Hook-up post", post, new Vector3(.035f, .09f, .035f), Camping.Post, cityRoot);
            Box("Hook-up socket", post + Vector3.up * .05f, new Vector3(.04f, .025f, .04f), Camping.Hookup, cityDetailRoot);
            if (pitch == CampLayout.Visitor)
                return;
            if (f.kind == 1)
                DrawCaravan(f, pitch, seed);
            else
                DrawTent(f, pitch, seed);
            // Two chairs and a table on the grass in front.
            var front = f.At(CampLayout.PitchU[pitch] - .12f, CampLayout.PitchV(pitch) + toLane * .3f, .025f);
            Box("Camp table", front + Vector3.up * .035f, new Vector3(.08f, .01f, .08f), Camping.Table, cityDetailRoot);
            foreach (float du in new[] { -.08f, .08f })
                Box("Camp chair", front + f.Offset(du, 0) + Vector3.up * .02f, new Vector3(.035f, .04f, .035f), Camping.Chair, cityDetailRoot);
        }
        void DrawTent(in CampLayout f, int pitch, int seed)
        {
            var colour = Camping.Tents[(pitch + seed) % Camping.Tents.Length];
            bool family = (pitch + seed) % 3 == 0;
            float su = family ? .4f : .3f, sv = family ? .34f : .26f, rise = family ? .22f : .17f;
            var at = f.Unit(pitch, .024f);
            Shape("Tent", prism, at, new Vector3(su, rise, sv), colour, cityRoot, f.Facing(0, 1));
            // The door: a dark flap on the lane side.
            float toLane = CampLayout.ToLane(pitch);
            Box("Tent door", at + f.Offset(0, toLane) * (sv / 2 - .04f) + Vector3.up * rise * .3f, f.Size(.07f, rise * .55f, .01f), Camping.Ash, cityDetailRoot);
            Box("Tent groundsheet", at + Vector3.up * .002f, f.Size(su + .04f, .004f, sv + .06f), Camping.DarkTimber, cityRoot);
        }
        void DrawCaravan(in CampLayout f, int pitch, int seed)
        {
            var stripe = Camping.Stripes[(pitch + seed) % Camping.Stripes.Length];
            float toLane = CampLayout.ToLane(pitch);
            var at = f.Unit(pitch);
            const float Length = .46f, Width = .22f, Bottom = .06f, Height = .2f;
            Box("Caravan", at + Vector3.up * (Bottom + Height / 2), f.Size(Length, Height, Width), Camping.Caravan, cityRoot);
            Box("Caravan roof", at + Vector3.up * (Bottom + Height + .008f), f.Size(Length - .04f, .016f, Width - .02f), Camping.CaravanRoof, cityRoot);
            Box("Caravan stripe", at + Vector3.up * (Bottom + .07f), f.Size(Length + .004f, .025f, Width + .004f), stripe, cityRoot);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Caravan window", at + f.Offset(0, side) * (Width / 2 + .002f) + f.Offset(-.1f, 0) + Vector3.up * (Bottom + .14f), f.Size(.14f, .05f, .006f), Camping.Window, cityRoot);
                Box("Caravan wheel", at + f.Offset(0, side) * (Width / 2 - .01f) + Vector3.up * .04f, f.Size(.07f, .07f, .03f), Camping.Rubber, cityRoot);
            }
            Box("Caravan door", at + f.Offset(0, toLane) * (Width / 2 + .004f) + f.Offset(.13f, 0) + Vector3.up * (Bottom + .08f), f.Size(.06f, .13f, .006f), stripe, cityDetailRoot);
            Box("Tow hitch", at + f.Offset(Length / 2 + .06f, 0) + Vector3.up * .05f, f.Size(.12f, .02f, .03f), Camping.Post, cityRoot);
            // An awning over the door on the lane side, on two poles.
            var awning = at + f.Offset(0, toLane) * (Width / 2 + .1f) + f.Offset(.05f, 0);
            Box("Awning", awning + Vector3.up * (Bottom + Height - .02f), f.Size(.3f, .012f, .2f), Camping.Awning, cityRoot);
            foreach (float du in new[] { -.14f, .14f })
                Box("Awning pole", awning + f.Offset(du, toLane * .09f) + Vector3.up * (Bottom + Height) / 2, new Vector3(.012f, Bottom + Height, .012f), Camping.Post, cityDetailRoot);
        }
        void DrawCabin(in CampLayout f, int pitch, int seed)
        {
            float toLane = CampLayout.ToLane(pitch);
            var at = f.Unit(pitch);
            const float Length = .42f, Depth = .34f, Height = .22f;
            Box("Log cabin", at + Vector3.up * (.024f + Height / 2), f.Size(Length, Height, Depth), Camping.Timber, cityRoot);
            for (int i = 1; i <= 3; i++)
                Box("Cabin log line", at + Vector3.up * (.024f + Height * i / 4), f.Size(Length + .006f, .012f, Depth + .006f), Camping.DarkTimber, cityRoot);
            Shape("Gable roof", prism, at + Vector3.up * (.024f + Height), new Vector3(Length + .08f, .14f, Depth + .1f), Camping.CabinRoofs[(pitch + seed) % Camping.CabinRoofs.Length], cityRoot, f.Facing(0, 1));
            var face = at + f.Offset(0, toLane) * (Depth / 2 + .004f);
            Box("Cabin door", face + f.Offset(-.08f, 0) + Vector3.up * .1f, f.Size(.07f, .14f, .008f), Camping.DarkTimber, cityRoot);
            Box("Cabin window", face + f.Offset(.09f, 0) + Vector3.up * .14f, f.Size(.1f, .06f, .008f), Camping.Window, cityRoot);
            Box("Cabin porch", at + f.Offset(0, toLane) * (Depth / 2 + .07f) + Vector3.up * .03f, f.Size(Length, .018f, .14f), Camping.DarkTimber, cityRoot);
            Box("Cabin chimney", at + f.Offset(.13f, -toLane * .06f) + Vector3.up * (.024f + Height + .1f), new Vector3(.05f, .12f, .05f), Camping.Stone, cityRoot);
        }
    }
}
