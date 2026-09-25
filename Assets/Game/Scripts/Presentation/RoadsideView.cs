using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Roadside service areas as far as they are built (redrawn with the towns, into the town batch): levelled ground,
    /// then the forecourt, pump island and footings, then the buildings and the canopy, and finally the open station with
    /// its shop, the garage with a lift at its open door and the tyre shop with its stacks and roof tyre. Four styles
    /// (Roadside.Kinds) share that plan: a highway filling station, a retro one, a truck stop and an eco station.
    /// <see cref="RoadsideLife"/> drives the cars and the mechanics; measurements come from <see cref="RoadsideLayout"/>.
    /// </summary>
    public sealed partial class WorldView
    {
        /// <summary>The service areas' colours, in their own class so their names never clash with other WorldView partials.</summary>
        static class Forecourt
        {
            internal static readonly Color Dirt = new Color(.55f, .47f, .36f), Concrete = new Color(.76f, .76f, .73f), Drive = new Color(.4f, .41f, .43f),
                Verge = new Color(.4f, .6f, .3f), Kerb = new Color(.86f, .85f, .8f), Canopy = new Color(.97f, .97f, .95f), Column = new Color(.8f, .81f, .82f),
                Pump = new Color(.95f, .95f, .93f), Screen = new Color(.1f, .12f, .14f), Glass = new Color(.34f, .56f, .68f), ShopWall = new Color(.94f, .91f, .83f),
                Roof = new Color(.26f, .28f, .31f), GarageWall = new Color(.66f, .72f, .78f), GarageSign = new Color(.14f, .36f, .72f), Shutter = new Color(.72f, .74f, .76f),
                ShutterLine = new Color(.55f, .57f, .6f), Inside = new Color(.13f, .14f, .16f), TyreWall = new Color(.93f, .76f, .3f), TyreRoof = new Color(.2f, .21f, .23f),
                Rubber = new Color(.09f, .09f, .1f), Hub = new Color(.72f, .74f, .77f), Lift = new Color(.96f, .78f, .12f), Paint = new Color(.97f, .97f, .95f),
                Post = new Color(.24f, .26f, .28f), Lamp = new Color(1f, .93f, .7f), Cone = new Color(.95f, .45f, .1f), Marker = new Color(.95f, .65f, .24f),
                Air = new Color(.2f, .5f, .85f), Bin = new Color(.25f, .45f, .3f), Oil = new Color(.7f, .16f, .14f),
                RetroRed = new Color(.8f, .13f, .13f), RetroCream = new Color(.97f, .93f, .82f), Brick = new Color(.64f, .32f, .25f),
                TruckYellow = new Color(.98f, .76f, .1f), Charcoal = new Color(.2f, .21f, .23f), Steel = new Color(.78f, .8f, .83f), Diesel = new Color(.13f, .38f, .22f),
                EcoGreen = new Color(.3f, .72f, .35f), Grass = new Color(.42f, .64f, .3f), Timber = new Color(.62f, .45f, .28f), Solar = new Color(.1f, .2f, .45f),
                SolarLine = new Color(.55f, .65f, .8f), Charger = new Color(.3f, .9f, .5f);
            // A highway station's brand is a plain colour, so two of them side by side still look different.
            internal static readonly Color[] Brands = { new Color(.86f, .16f, .17f), new Color(.14f, .6f, .36f), new Color(.15f, .4f, .82f), new Color(.96f, .55f, .1f) };
            internal sealed class Look
            {
                public Color brand, trim, shopWall, shopRoof, garageWall, garageSign, tyreWall, tyreRoof;
            }
            // Per style (Roadside.Kinds): highway, retro, truck stop, eco. The highway brand is replaced per station.
            internal static readonly Look[] Looks =
            {
                new Look { brand = Brands[1], trim = Canopy, shopWall = ShopWall, shopRoof = Roof, garageWall = GarageWall, garageSign = GarageSign, tyreWall = TyreWall, tyreRoof = TyreRoof },
                new Look { brand = RetroRed, trim = RetroCream, shopWall = RetroCream, shopRoof = RetroRed, garageWall = Brick, garageSign = RetroCream, tyreWall = RetroCream, tyreRoof = RetroRed },
                new Look { brand = TruckYellow, trim = Charcoal, shopWall = Steel, shopRoof = Charcoal, garageWall = new Color(.45f, .47f, .5f), garageSign = TruckYellow, tyreWall = TruckYellow, tyreRoof = Charcoal },
                new Look { brand = EcoGreen, trim = Canopy, shopWall = Timber, shopRoof = Grass, garageWall = new Color(.93f, .94f, .92f), garageSign = EcoGreen, tyreWall = Timber, tyreRoof = Grass },
            };
        }
        RoadsideLife roadside;
        Mesh drum;

        /// <summary>Every planned service area at its stage.</summary>
        void DrawServiceAreas()
        {
            foreach (var road in game.World.intercityRoads)
                if (Roadside.Planned(road) && Roadside.Shaped(road))
                    DrawServiceArea(road);
        }
        void DrawServiceArea(IntercityRoadState road)
        {
            var f = new RoadsideLayout(road);
            for (int i = 0; i < Roadside.Cells; i++)
                if (trees.TryGetValue(Roadside.SiteCell(road, i).Key, out var tree))
                    tree.SetActive(false);
            int stage = road.service;
            if (stage < 2)
            {
                Slab(f, "Service site", -RoadsideLayout.Half, RoadsideLayout.Half, RoadsideLayout.Near, RoadsideLayout.Far, .006f, .012f, Forecourt.Dirt);
                foreach (float u in new[] { -1.42f, 1.42f })
                    foreach (float v in new[] { .58f, 2.42f })
                    {
                        Part(f, "Survey stake", u, .06f, v, .025f, .12f, .025f, Forecourt.Paint);
                        Part(f, "Stake flag", u, .13f, v, .03f, .03f, .03f, Forecourt.Cone);
                    }
                SiteWorks(f);
                return;
            }
            var look = Forecourt.Looks[f.kind];
            var brand = f.kind == 0 ? Forecourt.Brands[(road.a * 7 + road.b * 3) % Forecourt.Brands.Length] : look.brand;
            DrawForecourt(f);
            if (stage < 3)
            {
                SiteWorks(f);
                return;
            }
            DrawCanopy(f, look, brand);
            DrawShop(f, look, brand);
            DrawGarage(f, look);
            DrawTyreShop(f, look);
            if (stage < Roadside.Steps)
            {
                SiteWorks(f);
                return;
            }
            DrawPumps(f, brand);
            DrawStationSign(f, brand);
            DrawYard(f);
            if (road.serviceOwned)
                DrawOwnerFlag(f);
        }
        void Part(in RoadsideLayout f, string name, float u, float y, float v, float su, float sy, float sv, Color color, Transform root = null) =>
            Box(name, f.At(u, v, y), f.Size(su, sy, sv), color, root ? root : cityRoot);
        void Slab(in RoadsideLayout f, string name, float u0, float u1, float v0, float v1, float y, float height, Color color, Transform root = null) =>
            Part(f, name, (u0 + u1) / 2, y, (v0 + v1) / 2, u1 - u0, height, v1 - v0, color, root);
        /// <summary>A box turned with the frame and then tipped by <paramref name="tilt"/> degrees about the road's direction.</summary>
        void Tipped(in RoadsideLayout f, string name, float u, float y, float v, float su, float sy, float sv, float tilt, Color color) =>
            Box(name, f.At(u, v, y), new Vector3(su, sy, sv), color, cityRoot, Quaternion.AngleAxis(tilt, f.flow) * f.Facing(0, 1));
        /// <summary>A gable roof over u0..u1 × v0..v1 standing on <paramref name="eaves"/>, its ridge along the road.</summary>
        void RoadsideGable(in RoadsideLayout f, float u0, float u1, float v0, float v1, float eaves, float rise, Color color) =>
            Shape("Gable roof", prism, f.At((u0 + u1) / 2, (v0 + v1) / 2, eaves), new Vector3(u1 - u0, rise, v1 - v0), color, cityRoot, f.Facing(0, 1));
        /// <summary>A tyre (or drum) of the given diameter and width, its axle along <paramref name="axle"/>.</summary>
        void TyrePart(string name, Vector3 at, float diameter, float width, Vector3 axle, Color color, Transform root = null)
        {
            // An upright axle cannot be a look direction with the default up, so lay the drum flat by turning it instead.
            var turn = axle == Vector3.up ? Quaternion.Euler(90, 0, 0) : Quaternion.LookRotation(axle);
            Shape(name, Drum(), at, new Vector3(diameter, diameter, width), color, root ? root : cityRoot, turn);
            if (color == Forecourt.Rubber)
                Shape(name + " hub", Drum(), at, new Vector3(diameter * .5f, diameter * .5f, width * 1.08f), Forecourt.Hub, root ? root : cityRoot, turn);
        }
        /// <summary>The shared cylinder: radius .5 and length 1 along z, centred, with flat caps.</summary>
        internal Mesh Drum()
        {
            if (drum)
                return drum;
            const int Sides = 12;
            var triangles = new Vector3[Sides * 12];
            int n = 0;
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2 / Sides, b = (i + 1) * Mathf.PI * 2 / Sides;
                Vector3 p = new Vector3(Mathf.Cos(a) * .5f, Mathf.Sin(a) * .5f, 0), q = new Vector3(Mathf.Cos(b) * .5f, Mathf.Sin(b) * .5f, 0);
                Vector3 front = Vector3.forward * .5f, back = Vector3.back * .5f;
                // Clockwise seen from outside, so every face shows.
                triangles[n++] = p + back; triangles[n++] = q + front; triangles[n++] = p + front;
                triangles[n++] = p + back; triangles[n++] = q + back; triangles[n++] = q + front;
                triangles[n++] = front; triangles[n++] = p + front; triangles[n++] = q + front;
                triangles[n++] = back; triangles[n++] = q + back; triangles[n++] = p + back;
            }
            drum = Faceted(triangles);
            drum.name = "Drum";
            ownedMeshes.Add(drum);
            return drum;
        }
        /// <summary>While the site is being built: cones across both driveways and the builder's board.</summary>
        void SiteWorks(in RoadsideLayout f)
        {
            foreach (float u in new[] { RoadsideLayout.EntryU, RoadsideLayout.ExitU })
                for (int i = -1; i <= 1; i++)
                    Shape("Traffic cone", f.At(u + i * .2f, .6f, .02f), new Vector3(.045f, .12f, .045f), Forecourt.Cone, cityRoot);
            Part(f, "Service area construction", 0, .22f, .56f, .6f, .4f, .12f, Forecourt.Marker);
        }
        /// <summary>Concrete apron, grass verge with kerbs and two driveways, the drive-through lane and the pump island.</summary>
        void DrawForecourt(in RoadsideLayout f)
        {
            float half = RoadsideLayout.Half, drive = RoadsideLayout.DriveWidth / 2, front = .62f;
            Slab(f, "Forecourt", -half, half, front, RoadsideLayout.Far, .015f, .03f, Forecourt.Concrete);
            foreach (float u in new[] { RoadsideLayout.EntryU, RoadsideLayout.ExitU })
                Slab(f, "Driveway", u - drive, u + drive, RoadsideLayout.Near, front, .015f, .03f, Forecourt.Drive);
            // The verge between and beside the driveways, with a kerb along its forecourt edge.
            float[] verge = { -half, RoadsideLayout.EntryU - drive, RoadsideLayout.EntryU + drive, RoadsideLayout.ExitU - drive, RoadsideLayout.ExitU + drive, half };
            for (int i = 0; i < verge.Length; i += 2)
            {
                Slab(f, "Verge", verge[i], verge[i + 1], RoadsideLayout.Near, front, .02f, .04f, Forecourt.Verge);
                Slab(f, "Kerb", verge[i], verge[i + 1], front - .02f, front + .01f, .03f, .03f, Forecourt.Kerb);
            }
            // The drive-through lane under the canopy, darker where the tyres run.
            Slab(f, "Forecourt lane", -1.3f, 1.3f, front + .02f, RoadsideLayout.CanopyFar + .06f, .031f, .002f, Forecourt.Drive);
            float island = RoadsideLayout.IslandLength / 2;
            Slab(f, "Pump island", -island, island, RoadsideLayout.IslandV - .05f, RoadsideLayout.IslandV + .05f, .045f, .03f, Forecourt.Kerb);
            foreach (float u in new[] { -.36f, .36f })
                Part(f, "Canopy column", u, .29f, RoadsideLayout.IslandV, .045f, .46f, .045f, Forecourt.Column);
            // Footings of the three buildings.
            Slab(f, "Footing", -RoadsideLayout.ShopU, RoadsideLayout.ShopU, RoadsideLayout.ShopNear, RoadsideLayout.ShopFar, .034f, .01f, Forecourt.Column);
            Slab(f, "Footing", RoadsideLayout.GarageFrom, RoadsideLayout.GarageTo, RoadsideLayout.GarageNear, RoadsideLayout.GarageFar, .034f, .01f, Forecourt.Column);
            Slab(f, "Footing", RoadsideLayout.TyreFrom, RoadsideLayout.TyreTo, RoadsideLayout.TyreNear, RoadsideLayout.TyreFar, .034f, .01f, Forecourt.Column);
        }
        /// <summary>
        /// The canopy over the pumps: flat and white with a brand band (highway), a red gable (retro), tall and dark with a
        /// yellow band so lorries fit under it (truck stop), or carrying solar panels (eco).
        /// </summary>
        void DrawCanopy(in RoadsideLayout f, Forecourt.Look look, Color brand)
        {
            float u = RoadsideLayout.CanopyU, near = RoadsideLayout.CanopyNear, far = RoadsideLayout.CanopyFar, top = RoadsideLayout.CanopyTop;
            switch (f.kind)
            {
                case 1:
                    Slab(f, "Canopy ceiling", -u + .06f, u - .06f, near + .06f, far - .06f, .4f, .03f, look.trim);
                    RoadsideGable(f, -u, u, near + .02f, far - .02f, .415f, .17f, brand);
                    break;
                case 2:
                    top = RoadsideLayout.TruckCanopyTop;
                    foreach (float c in new[] { -.36f, .36f })
                        Part(f, "Canopy column", c, top / 2, RoadsideLayout.IslandV, .075f, top, .075f, look.trim);
                    Slab(f, "Canopy fascia", -u - .08f, u + .08f, near - .05f, far + .05f, top - .03f, .09f, brand);
                    Slab(f, "Canopy roof", -u - .06f, u + .06f, near - .03f, far + .03f, top + .02f, .02f, look.trim);
                    for (int i = 0; i < 3; i++)
                        Slab(f, "Canopy light", -u + .1f, u - .1f, near + .15f + .26f * i, near + .19f + .26f * i, top - .08f, .01f, Forecourt.Lamp);
                    break;
                case 3:
                    Slab(f, "Canopy frame", -u, u, near, far, top - .02f, .05f, Forecourt.Column);
                    Slab(f, "Canopy stripe", -u - .002f, u + .002f, near - .002f, far + .002f, top - .035f, .015f, brand);
                    // Two rows of four solar panels tipped towards the road.
                    for (int row = 0; row < 2; row++)
                        for (int col = 0; col < 4; col++)
                        {
                            float pu = -u + (col + .5f) * 2 * u / 4, pv = near + (row + .5f) * (far - near) / 2;
                            Tipped(f, "Solar panel", pu, top + .03f, pv, 2 * u / 4 - .03f, .012f, (far - near) / 2 - .03f, -8, Forecourt.Solar);
                            Tipped(f, "Solar cell line", pu, top + .037f, pv, 2 * u / 4 - .04f, .004f, .008f, -8, Forecourt.SolarLine);
                        }
                    break;
                default:
                    Slab(f, "Canopy fascia", -u, u, near, far, top - .03f, .07f, brand);
                    Slab(f, "Canopy roof", -u + .02f, u - .02f, near + .02f, far - .02f, top + .01f, .02f, look.trim);
                    Slab(f, "Canopy stripe", -u - .002f, u + .002f, near - .002f, far + .002f, top - .045f, .015f, look.trim);
                    break;
            }
        }
        /// <summary>
        /// The shop behind the forecourt: a minimart (highway), a diner with a gable roof and a checkered band (retro), a
        /// truckers' diner with a striped awning and a billboard on the roof (truck stop), or a glass café under a grass
        /// roof (eco).
        /// </summary>
        void DrawShop(in RoadsideLayout f, Forecourt.Look look, Color brand)
        {
            float u = RoadsideLayout.ShopU, near = RoadsideLayout.ShopNear, far = RoadsideLayout.ShopFar, h = RoadsideLayout.ShopHeight;
            if (f.kind == 3)
            {
                Slab(f, "Café glass", -u + .04f, u - .04f, near, far, h / 2 + .03f, h, Forecourt.Glass);
                foreach (float end in new[] { -u + .03f, u - .03f })
                    Part(f, "Café timber", end, h / 2 + .03f, (near + far) / 2, .06f, h, far - near, look.shopWall);
                Slab(f, "Café roof edge", -u - .04f, u + .04f, near - .05f, far + .03f, h + .04f, .04f, look.shopWall);
                Slab(f, "Grass roof", -u - .02f, u + .02f, near - .03f, far + .01f, h + .065f, .02f, look.shopRoof);
                foreach (float pu in new[] { -.3f, 0f, .3f })
                    Part(f, "Planter", pu, .07f, near - .06f, .12f, .06f, .06f, Forecourt.Grass);
                return;
            }
            Slab(f, "Shop", -u, u, near, far, h / 2 + .03f, h, look.shopWall);
            Slab(f, "Shop window", -u + .06f, u - .06f, near - .012f, near, .15f, .15f, Forecourt.Glass);
            Slab(f, "Shop door", -.07f, .07f, near - .016f, near, .13f, .19f, f.kind == 1 ? brand : Forecourt.Inside);
            switch (f.kind)
            {
                case 1:
                    RoadsideGable(f, -u - .04f, u + .04f, near - .05f, far + .03f, h + .03f, .16f, look.shopRoof);
                    // The diner's checkered band along the bottom of the front.
                    for (int i = 0; i < 10; i++)
                    {
                        float cu = -u + (i + .5f) * 2 * u / 10;
                        Part(f, "Checker", cu, .06f, near - .008f, 2 * u / 10, .025f, .016f, i % 2 == 0 ? Forecourt.Screen : Forecourt.Paint);
                        Part(f, "Checker", cu, .035f, near - .008f, 2 * u / 10, .025f, .016f, i % 2 == 1 ? Forecourt.Screen : Forecourt.Paint);
                    }
                    break;
                case 2:
                    Slab(f, "Shop roof", -u - .04f, u + .04f, near - .05f, far + .03f, h + .045f, .03f, look.shopRoof);
                    for (int i = 0; i < 6; i++)
                        Slab(f, "Awning", -u + i * 2 * u / 6, -u + (i + 1) * 2 * u / 6, near - .16f, near, h - .02f, .02f, i % 2 == 0 ? Forecourt.RetroRed : Forecourt.Paint);
                    foreach (float leg in new[] { -.3f, .3f })
                        Part(f, "Billboard leg", leg, h + .16f, (near + far) / 2, .025f, .22f, .025f, look.shopRoof);
                    Part(f, "Billboard", 0, h + .3f, (near + far) / 2 - .02f, .86f, .2f, .035f, brand);
                    for (int i = 0; i < 2; i++)
                        Part(f, "Billboard stripe", 0, h + .26f + .08f * i, (near + far) / 2 - .04f, .7f, .03f, .012f, look.shopRoof);
                    break;
                default:
                    Slab(f, "Shop roof", -u - .04f, u + .04f, near - .05f, far + .03f, h + .045f, .03f, look.shopRoof);
                    Slab(f, "Shop sign", -u, u, near - .025f, near, h - .02f, .07f, brand);
                    break;
            }
        }
        /// <summary>A two-bay garage facing the road: one door open onto the lift, the other shuttered; a sign above.</summary>
        void DrawGarage(in RoadsideLayout f, Forecourt.Look look)
        {
            float from = RoadsideLayout.GarageFrom, to = RoadsideLayout.GarageTo, near = RoadsideLayout.GarageNear, far = RoadsideLayout.GarageFar, h = RoadsideLayout.GarageHeight;
            Slab(f, "Garage", from, to, near, far, h / 2 + .03f, h, look.garageWall);
            RoadsideGable(f, from - .03f, to + .03f, near - .03f, far + .03f, h + .03f, .12f, f.kind == 1 ? look.shopRoof : Forecourt.Roof);
            Slab(f, "Garage sign", from + .02f, to - .02f, near - .02f, near, h - .02f, .06f, look.garageSign);
            Slab(f, "Open bay", RoadsideLayout.LiftU - .14f, RoadsideLayout.LiftU + .14f, near - .012f, near, .15f, .24f, Forecourt.Inside);
            Slab(f, "Shutter", RoadsideLayout.ClosedBayU - .14f, RoadsideLayout.ClosedBayU + .14f, near - .012f, near, .15f, .24f, Forecourt.Shutter);
            for (int i = 0; i < 4; i++)
                Slab(f, "Shutter line", RoadsideLayout.ClosedBayU - .14f, RoadsideLayout.ClosedBayU + .14f, near - .016f, near - .012f, .07f + .05f * i, .008f, Forecourt.ShutterLine);
            // The two-post lift in front of the open bay; RoadsideLife raises its arms and the car on them.
            foreach (float side in new[] { -.15f, .15f })
                Part(f, "Lift post", RoadsideLayout.LiftU + side, .2f, RoadsideLayout.LiftV, .035f, .34f, .035f, Forecourt.Lift);
        }
        /// <summary>An open-fronted tyre shop with racks of tyres inside and a big tyre on the roof.</summary>
        void DrawTyreShop(in RoadsideLayout f, Forecourt.Look look)
        {
            float from = RoadsideLayout.TyreFrom, to = RoadsideLayout.TyreTo, near = RoadsideLayout.TyreNear, far = RoadsideLayout.TyreFar, h = RoadsideLayout.TyreHeight;
            Slab(f, "Tyre shop floor", from, to, near, far, .036f, .012f, Forecourt.Inside);
            Slab(f, "Tyre shop back", from, to, far - .04f, far, h / 2 + .03f, h, look.tyreWall);
            Slab(f, "Tyre shop side", from, from + .04f, near, far, h / 2 + .03f, h, look.tyreWall);
            Slab(f, "Tyre shop side", to - .04f, to, near, far, h / 2 + .03f, h, look.tyreWall);
            Slab(f, "Tyre shop roof", from - .03f, to + .03f, near - .08f, far + .02f, h + .045f, .03f, look.tyreRoof);
            Slab(f, "Tyre shop fascia", from - .03f, to + .03f, near - .1f, near - .06f, h + .02f, .06f, look.tyreWall);
            // Tyres standing in a rack along the back wall.
            for (int i = 0; i < 5; i++)
                TyrePart("Racked tyre", f.At(from + .12f + i * (to - from - .24f) / 4, far - .1f, .11f), .14f, .045f, f.away, Forecourt.Rubber);
            // The shop's sign: a big tyre standing on the roof, facing the road.
            TyrePart("Roof tyre", f.At((from + to) / 2, near + .12f, h + .2f), .3f, .08f, f.away, Forecourt.Rubber);
        }
        /// <summary>
        /// The two pumps on the island: modern pumps (highway), tall red pumps with glass globes (retro), diesel pumps
        /// (truck stop), or a pump and an electric car charger (eco).
        /// </summary>
        void DrawPumps(in RoadsideLayout f, Color brand)
        {
            float v = RoadsideLayout.IslandV;
            foreach (float u in new[] { -RoadsideLayout.PumpU, RoadsideLayout.PumpU })
            {
                switch (f.kind)
                {
                    case 1:
                        Part(f, "Retro pump", u, .16f, v, .065f, .2f, .055f, brand);
                        Part(f, "Pump face", u, .18f, v, .045f, .06f, .062f, Forecourt.Paint, cityDetailRoot);
                        TyrePart("Pump globe", f.At(u, v, .29f), .07f, .06f, Vector3.up, Forecourt.Paint);
                        TyrePart("Globe cap", f.At(u, v, .325f), .075f, .015f, Vector3.up, brand);
                        continue;
                    case 2:
                        Part(f, "Diesel pump", u, .17f, v, .08f, .22f, .07f, Forecourt.Diesel);
                        Part(f, "Pump head", u, .295f, v, .085f, .03f, .075f, brand);
                        Part(f, "Pump screen", u, .2f, v, .05f, .045f, .075f, Forecourt.Screen, cityDetailRoot);
                        continue;
                    case 3 when u > 0:
                        Part(f, "Charger", u, .17f, v, .05f, .24f, .045f, Forecourt.Pump);
                        Part(f, "Charger light", u, .22f, v, .054f, .07f, .049f, Forecourt.Charger);
                        Part(f, "Charger cable", u + .03f, .14f, v, .012f, .1f, .05f, Forecourt.Screen, cityDetailRoot);
                        continue;
                }
                Part(f, "Pump", u, .14f, v, .07f, .17f, .06f, Forecourt.Pump);
                Part(f, "Pump head", u, .235f, v, .075f, .03f, .065f, brand);
                Part(f, "Pump screen", u, .17f, v, .045f, .04f, .065f, Forecourt.Screen, cityDetailRoot);
                Part(f, "Pump hose", u + .042f, .13f, v, .012f, .08f, .07f, Forecourt.Screen, cityDetailRoot);
            }
            Part(f, "Bin", -.36f, .09f, v, .05f, .06f, .05f, Forecourt.Bin, cityDetailRoot);
        }
        /// <summary>
        /// The sign on the verge at the entrance, read by drivers coming up the road: a price pylon (highway), a round disc
        /// on a pole (retro), a tall pylon (truck stop) or a low green sign beside the wind turbine (eco).
        /// </summary>
        void DrawStationSign(in RoadsideLayout f, Color brand)
        {
            float u = -1.46f, v = .56f;
            switch (f.kind)
            {
                case 1:
                    Part(f, "Sign pole", u, .34f, v, .03f, .62f, .03f, Forecourt.Post);
                    TyrePart("Round sign", f.At(u, v, .82f), .34f, .04f, f.flow, brand);
                    TyrePart("Round sign centre", f.At(u, v, .82f), .24f, .046f, f.flow, Forecourt.RetroCream);
                    Part(f, "Round sign bar", u, .82f, v, .05f, .06f, .3f, brand);
                    return;
                case 2:
                    Part(f, "Price sign post", u, .5f, v, .05f, .96f, .05f, Forecourt.Charcoal);
                    Part(f, "Price sign", u, 1.12f, v, .06f, .36f, .24f, brand);
                    for (int i = 0; i < 3; i++)
                        Part(f, "Price", u, .99f + i * .08f, v, .066f, .035f, .18f, Forecourt.Charcoal);
                    return;
                case 3:
                    Part(f, "Eco sign", u, .2f, v, .06f, .36f, .2f, brand);
                    Part(f, "Eco sign stripe", u, .28f, v, .066f, .04f, .16f, Forecourt.Paint);
                    // The wind turbine's mast and nacelle; RoadsideLife turns its rotor.
                    Part(f, "Turbine mast", RoadsideLayout.TurbineU, RoadsideLayout.HubHeight / 2, RoadsideLayout.TurbineV, .04f, RoadsideLayout.HubHeight, .04f, Forecourt.Paint);
                    Part(f, "Turbine nacelle", RoadsideLayout.TurbineU, RoadsideLayout.HubHeight, RoadsideLayout.TurbineV + .02f, .06f, .06f, .12f, Forecourt.Paint);
                    return;
            }
            Part(f, "Price sign post", u, .3f, v, .035f, .56f, .035f, Forecourt.Post);
            Part(f, "Price sign", u, .72f, v, .05f, .34f, .2f, brand);
            Part(f, "Price sign top", u, .84f, v, .055f, .07f, .21f, Forecourt.Paint);
            for (int i = 0; i < 3; i++)
                Part(f, "Price", u, .6f + i * .07f, v, .056f, .035f, .15f, i == 2 ? Forecourt.Paint : Forecourt.Screen);
        }
        /// <summary>Tyre stacks, the air pump, oil drums and lamps.</summary>
        void DrawYard(in RoadsideLayout f)
        {
            // Stacks of new tyres beside the tyre shop, where RoadsideLife rolls them to and from the car on the jack.
            for (int s = 0; s < RoadsideLayout.StackV.Length; s++)
                for (int k = 0; k < 5 - s; k++)
                    TyrePart("Tyre stack", f.At(RoadsideLayout.StackU, RoadsideLayout.StackV[s], .053f + .047f * k), .15f, .045f, Vector3.up, Forecourt.Rubber);
            Part(f, "Air pump", .72f, .12f, 1.68f, .05f, .18f, .05f, Forecourt.Air);
            Part(f, "Air pump head", .72f, .23f, 1.68f, .06f, .04f, .06f, Forecourt.Paint, cityDetailRoot);
            // Oil drums by the garage wall.
            for (int i = 0; i < 3; i++)
                TyrePart("Oil drum", f.At(RoadsideLayout.GarageTo + .07f, RoadsideLayout.GarageFar - .1f - .1f * i, .1f), .08f, .13f, Vector3.up, Forecourt.Oil, cityDetailRoot);
            // Lamps on the verge at both ends of the canopy.
            foreach (float u in new[] { -.75f, .75f })
            {
                Part(f, "Lamp post", u, .28f, .56f, .025f, .5f, .025f, Forecourt.Post);
                Part(f, "Lamp", u, .54f, .56f, .07f, .035f, .07f, Forecourt.Lamp);
            }
        }
    }
}
