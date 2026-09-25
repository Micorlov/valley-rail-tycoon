using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Close-up town details: yards, rooftop plant, lit windows, vehicles and street furniture. Everything here batches
    /// into the detail meshes, which only show when the camera is zoomed in, so the far view stays cheap.
    /// </summary>
    public sealed partial class WorldView
    {
        static readonly Color LampGlow = new Color(1f, .93f, .62f), LitWindow = new Color(1f, .84f, .45f), Bin = new Color(.2f, .36f, .26f),
            Hedge = new Color(.22f, .45f, .25f), ConeOrange = new Color(.98f, .5f, .12f), SchoolBus = new Color(.98f, .78f, .15f);
        void Detail(Lot lot, string name, Vector3 local, Vector3 size, Color color) =>
            Box(name, lot.at + lot.turn * local, size, color, cityDetailRoot, lot.turn);
        void DetailTree(Lot lot, Vector3 local, float scale, Color leaves)
        {
            Detail(lot, "Tree trunk", local + new Vector3(0, .06f * scale, 0), new Vector3(.04f, .12f, .04f) * scale, Timber);
            Shape("Tree", cone, lot.at + lot.turn * (local + new Vector3(0, .1f * scale, 0)), new Vector3(.12f, .34f, .12f) * scale, leaves, cityDetailRoot, lot.turn);
        }
        void LampPost(Lot lot, Vector3 local)
        {
            Detail(lot, "Lamp post", local + new Vector3(0, .25f, 0), new Vector3(.03f, .5f, .03f), Slate);
            Detail(lot, "Lamp", local + new Vector3(0, .5f, 0), new Vector3(.08f, .03f, .06f), LampGlow);
        }
        void Bench(Lot lot, Vector3 local, bool alongX = true)
        {
            Detail(lot, "Bench", local + new Vector3(0, .04f, 0), alongX ? new Vector3(.16f, .03f, .05f) : new Vector3(.05f, .03f, .16f), Timber);
            Detail(lot, "Bench back", local + new Vector3(alongX ? 0 : -.025f, .07f, alongX ? -.025f : 0), alongX ? new Vector3(.16f, .04f, .01f) : new Vector3(.01f, .04f, .16f), Timber);
        }
        void Vehicle(Lot lot, Vector3 local, Vector3 size, Color body, Color stripe, bool alongX)
        {
            var box = alongX ? size : new Vector3(size.z, size.y, size.x);
            Detail(lot, "Vehicle", local + new Vector3(0, size.y / 2 + .03f, 0), box, body);
            Detail(lot, "Vehicle stripe", local + new Vector3(0, size.y * .55f + .03f, 0), box + new Vector3(.01f, -size.y * .7f, .01f), stripe);
        }
        /// <summary>Details for one finished building; the lot frame faces its street.</summary>
        void DrawDetails(BuildingDefinition def, Lot lot)
        {
            float w = def.width / 100f, d = def.depth / 100f, h = def.height / 100f, front = d / 2, edge = def.size / 2f - .06f;
            if (def.category == BuildingCategory.Residential)
            {
                if (def.level <= 2)
                    HomeYard(def, lot, w, d, h, front, edge);
                else
                    FlatsDetails(def, lot, w, d, h, front, edge);
            }
            else if (def.category == BuildingCategory.Commercial)
                ShopDetails(def, lot, w, d, h, front, edge);
            else if (def.size > 1)
                LandmarkDetails(def, lot, w, d, h, front);
            else
                CivicDetails(def, lot, w, d, h, front, edge);
        }
        void HomeYard(BuildingDefinition def, Lot lot, float w, float d, float h, float front, float edge)
        {
            int hash = lot.hash;
            Detail(lot, "Door step", new Vector3(0, .02f, front + .05f), new Vector3(.16f, .04f, .08f), Stone);
            Detail(lot, "Porch lamp", new Vector3(.1f, .22f, front + .02f), new Vector3(.03f, .04f, .03f), LampGlow);
            // Front boundary with a gap for the path: picket fence, hedge or low stone wall.
            int kind = (hash / 3) % 3;
            Color boundary = kind == 0 ? White : kind == 1 ? Hedge : Stone;
            float height = kind == 1 ? .1f : .07f;
            for (int side = -1; side <= 1; side += 2)
                Detail(lot, "Front boundary", new Vector3(side * (edge + .1f) / 2, height / 2, edge), new Vector3(edge - .1f, height, kind == 1 ? .08f : .025f), boundary);
            Detail(lot, "Mailbox post", new Vector3(-.16f, .05f, edge - .04f), new Vector3(.02f, .1f, .02f), Slate);
            Detail(lot, "Mailbox", new Vector3(-.16f, .11f, edge - .04f), new Vector3(.05f, .04f, .06f), (hash & 1) == 0 ? Red : PoliceBlue);
            Detail(lot, "Wheelie bin", new Vector3(w / 2 + .05f, .05f, -d * .2f), new Vector3(.05f, .1f, .05f), Bin);
            DetailTree(lot, new Vector3(-(w / 2 + .07f), 0, d * .1f), .7f + (hash % 3) * .1f, (hash & 2) == 0 ? Leaf : Moss);
            for (int i = 0; i < 3; i++)
                Detail(lot, "Flowers", new Vector3(.14f + i * .06f, .025f, front + .07f), new Vector3(.04f, .05f, .04f), i == 1 ? Rose : Gold);
            if (hash % 3 == 0 && def.style != BuildingStyle.Townhouses)
                ParkedCar(lot, new Vector3(-.2f, .03f, (front + edge) / 2 - .02f), .28f, CarPaint[hash % CarPaint.Length], (hash & 4) == 0 ? 90 : -90, closeUpOnly: true);
            if (hash % 4 == 1)
                Detail(lot, "Satellite dish", new Vector3(w / 2 + .015f, h * .8f, 0), new Vector3(.02f, .07f, .07f), Stone);
        }
        void FlatsDetails(BuildingDefinition def, Lot lot, float w, float d, float h, float front, float edge)
        {
            bool tower = def.style == BuildingStyle.Tower || def.style == BuildingStyle.HighRise;
            if (!tower)
            {
                for (int i = 0; i < 2; i++)
                    Detail(lot, "Rooftop plant", new Vector3(-w * .2f + i * w * .35f, h + .09f, -d * .15f), new Vector3(.14f, .1f, .12f), Stone);
                Detail(lot, "Rooftop vent", new Vector3(w * .3f, h + .08f, d * .2f), new Vector3(.05f, .1f, .05f), Slate);
            }
            LitWindows(lot, w, d, tower ? Mathf.Min(h, 3f) : h, front, 5);
            for (int side = -1; side <= 1; side += 2)
                Detail(lot, "Planter", new Vector3(side * .18f, .05f, front + .06f), new Vector3(.1f, .1f, .08f), Hedge);
            Detail(lot, "Bike rack", new Vector3(w * .35f, .04f, front + .08f), new Vector3(.18f, .06f, .02f), Slate);
            LampPost(lot, new Vector3(-edge + .04f, 0, edge - .02f));
        }
        void LitWindows(Lot lot, float w, float d, float h, float front, int count)
        {
            int hash = lot.hash;
            int rows = Mathf.Max(2, (int)(h / .35f));
            for (int i = 0; i < count; i++)
            {
                int r = (hash / (7 + i * 3)) % rows;
                float y = .3f + (h - .3f) * (r + .5f) / rows;
                float x = ((hash / (11 + i * 5)) % 5 - 2) * w * .14f;
                bool side = i % 2 == 1;
                var at = side ? new Vector3(w / 2 + .018f, y, ((hash / (13 + i)) % 3 - 1) * d * .2f) : new Vector3(x, y, front + .018f);
                Detail(lot, "Lit window", at, side ? new Vector3(.01f, .06f, .07f) : new Vector3(.07f, .06f, .01f), LitWindow);
            }
        }
        void ShopDetails(BuildingDefinition def, Lot lot, float w, float d, float h, float front, float edge)
        {
            if (def.style == BuildingStyle.CommercialTower)
            {
                LampPost(lot, new Vector3(-edge + .04f, 0, edge - .02f));
                Bench(lot, new Vector3(.2f, 0, edge - .05f));
                return;
            }
            Detail(lot, "Sign light", new Vector3(0, h - .02f, front + .04f), new Vector3(w * .5f, .02f, .02f), LampGlow);
            Detail(lot, "Planter", new Vector3(-w * .42f, .05f, front + .08f), new Vector3(.08f, .1f, .08f), Hedge);
            Detail(lot, "Planter", new Vector3(w * .42f, .05f, front + .08f), new Vector3(.08f, .1f, .08f), Hedge);
            switch (def.style)
            {
                case BuildingStyle.Cafe:
                    Detail(lot, "Menu board", new Vector3(.2f, .06f, edge - .04f), new Vector3(.05f, .12f, .02f), Navy);
                    break;
                case BuildingStyle.MarketHall:
                    for (int i = 0; i < 4; i++)
                        Detail(lot, "Produce crate", new Vector3(-.24f + i * .16f, .04f, front + .08f), new Vector3(.1f, .06f, .08f), i % 2 == 0 ? Red : Leaf);
                    break;
                case BuildingStyle.Hotel:
                    ParkedCar(lot, new Vector3(-.15f, .03f, edge + .15f), .28f, SchoolBus, 90, closeUpOnly: true); // a taxi waiting at the door
                    Detail(lot, "Entrance light", new Vector3(0, .34f, front + .12f), new Vector3(.2f, .02f, .02f), LampGlow);
                    LitWindows(lot, w, d, h, front, 4);
                    break;
                case BuildingStyle.Office:
                    Detail(lot, "Rooftop plant", new Vector3(w * .2f, h + .1f, 0), new Vector3(.16f, .12f, .14f), Stone);
                    LitWindows(lot, w, d, h, front, 3);
                    break;
                default:
                    Bench(lot, new Vector3(-.22f, 0, edge - .05f));
                    break;
            }
        }
        void CivicDetails(BuildingDefinition def, Lot lot, float w, float d, float h, float front, float edge)
        {
            float street = def.size / 2f + .28f; // kerbside parking on the street the building faces
            switch (def.style)
            {
                case BuildingStyle.TownHall:
                    LampPost(lot, new Vector3(-edge + .03f, 0, edge - .02f));
                    LampPost(lot, new Vector3(edge - .03f, 0, edge - .02f));
                    for (int side = -1; side <= 1; side += 2)
                        Detail(lot, "Flower bed", new Vector3(side * w * .3f, .02f, front + .06f), new Vector3(w * .25f, .04f, .07f), Rose);
                    break;
                case BuildingStyle.Chapel:
                    for (int i = 0; i < 3; i++)
                        Detail(lot, "Gravestone", new Vector3(w / 2 + .08f, .05f, -d * .3f + i * .14f), new Vector3(.03f, .1f, .07f), Stone);
                    Detail(lot, "Bell", new Vector3(0, h * 1.35f, front - .1f), new Vector3(.08f, .08f, .08f), Gold);
                    break;
                case BuildingStyle.School:
                    Vehicle(lot, new Vector3(0, 0, street), new Vector3(.5f, .16f, .15f), SchoolBus, Navy, true);
                    Detail(lot, "Slide", new Vector3(-w * .3f, .08f, -d / 2 - .06f), new Vector3(.06f, .03f, .2f), Red);
                    Detail(lot, "Swing frame", new Vector3(w * .25f, .12f, -d / 2 - .06f), new Vector3(.2f, .02f, .02f), PoliceBlue);
                    break;
                case BuildingStyle.FireStation:
                    Vehicle(lot, new Vector3(-w * .12f, 0, street), new Vector3(.46f, .16f, .16f), Red, White, true);
                    Detail(lot, "Ladder", new Vector3(-w * .12f, .21f, street), new Vector3(.42f, .02f, .06f), Stone);
                    break;
                case BuildingStyle.MedicalCentre:
                    Vehicle(lot, new Vector3(.1f, 0, street), new Vector3(.3f, .14f, .15f), White, Red, true);
                    Emit(EmitterKind.Beacon, lot, new Vector3(.1f, .2f, street), Red);
                    Bench(lot, new Vector3(-w * .3f, 0, edge - .05f));
                    break;
                case BuildingStyle.PostOffice:
                    Vehicle(lot, new Vector3(-.1f, 0, street), new Vector3(.3f, .14f, .15f), Red, Gold, true);
                    break;
                case BuildingStyle.Park:
                    for (int i = 0; i < 4; i++)
                        LampPost(lot, new Vector3((i % 2 == 0 ? -1 : 1) * w * .44f, 0, (i < 2 ? -1 : 1) * d * .44f));
                    Bench(lot, new Vector3(-w * .2f, 0, -.1f));
                    Bench(lot, new Vector3(w * .2f, 0, .1f));
                    for (int i = 0; i < 3; i++)
                        Detail(lot, "Duck", new Vector3(-.05f + i * .05f, .065f, .03f * (i - 1)), new Vector3(.025f, .02f, .035f), White);
                    break;
                case BuildingStyle.Library:
                case BuildingStyle.Museum:
                    for (int side = -1; side <= 1; side += 2)
                        Detail(lot, "Banner", new Vector3(side * w * .32f, h * .55f, front + .015f), new Vector3(.08f, h * .45f, .01f), def.style == BuildingStyle.Museum ? Red : PoliceBlue);
                    Bench(lot, new Vector3(w * .3f, 0, edge - .05f));
                    LampPost(lot, new Vector3(-edge + .03f, 0, edge - .02f));
                    break;
                default:
                    LampPost(lot, new Vector3(-edge + .03f, 0, edge - .02f));
                    break;
            }
        }
        void LandmarkDetails(BuildingDefinition def, Lot lot, float w, float d, float h, float front)
        {
            int size = def.size;
            switch (def.style)
            {
                case BuildingStyle.PoliceHeadquarters:
                    Detail(lot, "Gate barrier", new Vector3(w * .07f, .1f, front - .03f), new Vector3(w * .12f, .025f, .025f), Red);
                    for (int i = 0; i < 4; i++)
                        Shape("Traffic cone", cone, lot.at + lot.turn * new Vector3(-w * .1f + i * .12f, 0, front + .12f), new Vector3(.04f, .09f, .04f), ConeOrange, cityDetailRoot, lot.turn);
                    break;
                case BuildingStyle.ShoppingMall:
                    for (int i = 0; i < size; i++)
                        Detail(lot, "Trolley bay", new Vector3(-w * .44f + i * w * .88f / Mathf.Max(1, size - 1), .05f, d * .22f), new Vector3(.18f, .08f, .06f), Stone);
                    Detail(lot, "Rooftop logo", new Vector3(0, h + .35f + .06f * size, -d * .35f), new Vector3(w * .24f, .3f, .05f), Red);
                    Bench(lot, new Vector3(-w * .25f, 0, d * .22f));
                    Bench(lot, new Vector3(w * .25f, 0, d * .22f));
                    break;
                case BuildingStyle.GeneralHospital:
                    for (int i = 0; i < Mathf.Max(1, size - 1); i++)
                        Emit(EmitterKind.Beacon, lot, new Vector3(w * .3f + (i - (size - 2) * .5f) * .22f, .22f, front - .08f), i % 2 == 0 ? Red : new Color(.3f, .6f, 1f));
                    Detail(lot, "Entrance lights", new Vector3(w * .3f, .37f, front - .1f), new Vector3(w * .3f, .02f, .02f), LampGlow);
                    Bench(lot, new Vector3(-w * .4f, 0, d * .22f));
                    break;
                case BuildingStyle.University:
                    Detail(lot, "Statue plinth", new Vector3(0, .08f, d * .12f), new Vector3(.18f, .16f, .18f), Stone);
                    Shape("Statue", cone, lot.at + lot.turn * new Vector3(0, .16f, d * .12f), new Vector3(.06f, .3f, .06f), new Color(.35f, .5f, .45f), cityDetailRoot, lot.turn);
                    Detail(lot, "Fountain basin", new Vector3(0, .04f, -d * .08f), new Vector3(.5f, .06f, .5f), Stone);
                    Emit(EmitterKind.Fountain, lot, new Vector3(0, .06f, -d * .08f), new Color(.45f, .7f, .9f), 1.4f);
                    Emit(EmitterKind.Campus, lot, new Vector3(0, .03f, 0), Grass, w); // students on the quad: CampusLife
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Bench(lot, new Vector3(side * w * .18f, 0, d * .2f));
                        LampPost(lot, new Vector3(side * w * .1f, 0, d * .28f));
                    }
                    break;
                case BuildingStyle.Arena:
                    ArenaDetails(lot, w, d, h, size);
                    break;
                case BuildingStyle.Lido:
                    LidoDetails(lot, size);
                    break;
                case BuildingStyle.CityPark:
                    CityParkDetails(lot, size);
                    break;
                case BuildingStyle.Zoo:
                    ZooDetails(lot);
                    break;
            }
        }
        /// <summary>Goals and corner flags; the fans, the match and its scoreboard come alive in <see cref="StadiumMatch"/>.</summary>
        void ArenaDetails(Lot lot, float w, float d, float h, int size)
        {
            ArenaGoals(ArenaBowl(lot, d, size), ArenaFor(w, d, h, size));
            if (size >= 4)
                ArenaCarParkDetails(lot, w, d, size);
        }
        /// <summary>Street furniture for one town street cell: lamps, trees, crossings, traffic lights, bus stops and hydrants.</summary>
        void DrawStreetDetails(Cell cell, int mask)
        {
            var at = new Vector3(cell.x, 0, cell.z);
            int arms = Directions.Count(mask), pavement = -1, hash = (cell.x * 73856093 ^ cell.z * 19349663) & 0x7fffffff;
            for (int d = 0; d < 4 && pavement < 0; d++)
                if ((mask & 1 << d) == 0)
                    pavement = d;
            if (arms >= 3)
            {
                // Zebra crossings on every arm; CityTraffic puts up working traffic lights where a junction has them.
                for (int d = 0; d < 4; d++)
                {
                    if ((mask & 1 << d) == 0)
                        continue;
                    var along = new Vector3(Directions.Dx[d], 0, Directions.Dz[d]);
                    var across = new Vector3(-along.z, 0, along.x);
                    for (int s = 0; s < 4; s++)
                        Box("Crossing stripe", at + along * .38f + across * (-.3f + s * .2f) + Vector3.up * .028f,
                            d % 2 == 0 ? new Vector3(.1f, .01f, .16f) : new Vector3(.16f, .01f, .1f), White, cityDetailRoot);
                }
                return;
            }
            if (pavement < 0)
                return;
            var side = new Vector3(Directions.Dx[pavement], 0, Directions.Dz[pavement]);
            var run = new Vector3(side.z, 0, -side.x);
            var flat = new Vector3(Mathf.Abs(side.x), 0, Mathf.Abs(side.z));
            int pick = hash % 13;
            if (pick % 3 == 0)
            {
                Box("Street lamp post", at + side * .44f + run * .3f + Vector3.up * .25f, new Vector3(.03f, .5f, .03f), Slate, cityDetailRoot);
                Box("Street lamp", at + side * .38f + run * .3f + Vector3.up * .5f, new Vector3(.12f, .03f, .06f), LampGlow, cityDetailRoot);
            }
            if (pick == 4 || pick == 10)
            {
                var spot = at + side * .44f - run * .25f;
                Box("Street tree trunk", spot + Vector3.up * .07f, new Vector3(.04f, .14f, .04f), Timber, cityDetailRoot);
                Shape("Street tree", cone, spot + Vector3.up * .12f, new Vector3(.13f, .36f, .13f), Leaf, cityDetailRoot);
            }
            if (pick == 11 && Directions.Straight(mask))
            {
                var stop = at + side * .44f;
                Box("Bus shelter roof", stop + Vector3.up * .26f, new Vector3(.3f, .02f, .3f) - flat * .2f, White, cityDetailRoot);
                Box("Bus shelter back", stop + side * .04f + Vector3.up * .13f, new Vector3(.3f, .26f, .3f) - flat * .28f, Glass, cityDetailRoot);
            }
            if (pick == 7)
                Box("Hydrant", at + side * .44f + run * .1f + Vector3.up * .04f, new Vector3(.04f, .08f, .04f), Red, cityDetailRoot);
        }
        /// <summary>Plaza extras: benches and lamps round the fountain, and its water jet.</summary>
        void DrawPlazaDetails(Cell cell)
        {
            var lot = new Lot { at = new Vector3(cell.x, 0, cell.z), turn = Quaternion.identity, hash = 0 };
            Bench(lot, new Vector3(-.32f, 0, 0), false);
            Bench(lot, new Vector3(.32f, 0, 0), false);
            LampPost(lot, new Vector3(-.4f, 0, .4f));
            LampPost(lot, new Vector3(.4f, 0, -.4f));
            Emit(EmitterKind.Fountain, lot, new Vector3(0, .26f, 0), new Color(.45f, .7f, .9f));
        }
    }
}
