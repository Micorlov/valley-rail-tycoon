using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>Homes and businesses. Each style has its own silhouette; colours vary per lot so a street never repeats exactly.</summary>
    public sealed partial class WorldView
    {
        static readonly Color Brick = new Color(.66f, .34f, .26f), Sand = new Color(.86f, .76f, .56f), PaleBlue = new Color(.64f, .75f, .8f), Sage = new Color(.66f, .74f, .6f),
            White = new Color(.94f, .94f, .92f), Stone = new Color(.74f, .71f, .64f), Timber = new Color(.47f, .32f, .21f), Rose = new Color(.86f, .62f, .58f),
            Terracotta = new Color(.72f, .36f, .22f), Slate = new Color(.3f, .33f, .38f), Moss = new Color(.25f, .42f, .3f), Glass = new Color(.18f, .43f, .56f),
            Leaf = new Color(.3f, .6f, .32f), Red = new Color(.8f, .18f, .14f);
        // Instance palettes: static initializers of a partial class run in an unspecified order across files, so a
        // static array here could copy Cream or Gold (declared in WorldView.cs) before they are set, as black.
        readonly Color[] HomeWalls = { Cream, Sand, PaleBlue, Sage, Rose, White };
        readonly Color[] HomeRoofs = { Terracotta, Slate, new Color(.57f, .26f, .16f), Moss };
        readonly Color[] Awnings = { Red, new Color(.2f, .5f, .35f), new Color(.2f, .38f, .66f), Gold };
        /// <summary>Draws one building and returns its height, for the town label.</summary>
        float DrawBuilding(BuildingDefinition def, Lot lot)
        {
            float w = def.width / 100f, d = def.depth / 100f, h = def.height / 100f, front = d / 2;
            Color wall = Pick(HomeWalls, lot.hash), roof = Pick(HomeRoofs, lot.hash, 1);
            switch (def.style)
            {
                case BuildingStyle.House:
                    Part(lot, "Walls", new Vector3(0, h / 2, 0), new Vector3(w, h, d), wall);
                    Hip(lot, new Vector3(0, h, 0), w + .08f, .34f, d + .08f, roof);
                    Door(lot, 0, front, Timber);
                    Windows(lot, Vector3.zero, w, d, .1f, h, 1, Navy);
                    return h + .34f;
                case BuildingStyle.LargeHouse:
                    Part(lot, "Walls", new Vector3(0, h / 2, 0), new Vector3(w, h, d), wall);
                    Gable(lot, new Vector3(0, h, 0), w + .08f, .36f, d + .08f, roof);
                    Chimney(lot, w * .3f, 0, h + .42f, Brick);
                    Part(lot, "Porch roof", new Vector3(0, .3f, front + .08f), new Vector3(.34f, .04f, .16f), roof);
                    Door(lot, 0, front, Timber);
                    Windows(lot, Vector3.zero, w, d, .05f, h, 2, Navy);
                    return h + .36f;
                case BuildingStyle.Cottage:
                    Part(lot, "Stone walls", new Vector3(0, h / 2, 0), new Vector3(w, h, d), Stone);
                    Gable(lot, new Vector3(0, h, 0), w + .08f, .42f, d + .08f, Slate, true);
                    Chimney(lot, -w * .3f, -d * .2f, h + .38f, Brick);
                    Part(lot, "Flower box", new Vector3(w * .22f, .2f, front + .03f), new Vector3(.16f, .05f, .05f), Leaf);
                    Door(lot, -w * .15f, front, Timber, .1f, .22f);
                    return h + .42f;
                case BuildingStyle.Bungalow:
                    Part(lot, "Walls", new Vector3(-w * .1f, h / 2, 0), new Vector3(w * .8f, h, d), wall);
                    Hip(lot, new Vector3(-w * .1f, h, 0), w * .8f + .08f, .2f, d + .08f, roof);
                    Part(lot, "Garage", new Vector3(w * .4f, h * .4f, d * .05f), new Vector3(w * .24f, h * .8f, d * .9f), White);
                    Part(lot, "Garage door", new Vector3(w * .4f, h * .3f, d * .5f + .02f), new Vector3(w * .18f, h * .6f, .02f), Slate);
                    Door(lot, -w * .2f, front, Timber, .1f, .22f);
                    Windows(lot, new Vector3(-w * .1f, 0, 0), w * .8f, d, .08f, h, 1, Navy);
                    return h + .2f;
                case BuildingStyle.Farmhouse:
                    Part(lot, "Timber walls", new Vector3(0, h / 2, -.04f), new Vector3(w, h, d - .08f), Timber);
                    Gable(lot, new Vector3(0, h, -.04f), w + .1f, .38f, d, new Color(.6f, .2f, .16f));
                    Part(lot, "Porch", new Vector3(0, .26f, front + .02f), new Vector3(w * .9f, .04f, .18f), Cream);
                    Part(lot, "Porch post", new Vector3(-w * .4f, .13f, front + .08f), new Vector3(.03f, .26f, .03f), Cream);
                    Part(lot, "Porch post", new Vector3(w * .4f, .13f, front + .08f), new Vector3(.03f, .26f, .03f), Cream);
                    Chimney(lot, w * .32f, -.04f, h + .44f, Stone);
                    Door(lot, 0, front - .08f, Cream);
                    return h + .38f;
                case BuildingStyle.Villa:
                    Part(lot, "Walls", new Vector3(-w * .12f, h / 2, 0), new Vector3(w * .76f, h, d), wall);
                    Hip(lot, new Vector3(-w * .12f, h, 0), w * .76f + .08f, .32f, d + .08f, roof);
                    Part(lot, "Wing", new Vector3(w * .36f, h * .32f, -d * .1f), new Vector3(w * .28f, h * .64f, d * .8f), Cream);
                    Hip(lot, new Vector3(w * .36f, h * .64f, -d * .1f), w * .28f + .06f, .18f, d * .8f + .06f, roof);
                    Part(lot, "Balcony", new Vector3(-w * .12f, h * .55f, front + .05f), new Vector3(w * .4f, .03f, .1f), White);
                    Door(lot, -w * .12f, front, Timber);
                    Windows(lot, new Vector3(-w * .12f, 0, 0), w * .76f, d, .05f, h, 2, Navy);
                    return h + .32f;
                case BuildingStyle.Duplex:
                    Part(lot, "Walls", new Vector3(0, h / 2, 0), new Vector3(w, h, d), wall);
                    Part(lot, "Party wall", new Vector3(0, h / 2, 0), new Vector3(.04f, h + .02f, d + .02f), White);
                    Gable(lot, new Vector3(0, h, 0), w + .08f, .34f, d + .08f, roof);
                    Chimney(lot, -w * .3f, 0, h + .4f, Brick);
                    Chimney(lot, w * .3f, 0, h + .4f, Brick);
                    Door(lot, -w * .25f, front, Timber);
                    Door(lot, w * .25f, front, Timber);
                    Windows(lot, Vector3.zero, w, d, .05f, h, 2, Navy);
                    return h + .34f;
                case BuildingStyle.Townhouses:
                    for (int i = -1; i <= 1; i++)
                    {
                        float x = i * w / 3, unit = w / 3 - .01f, top = h * (1 - .08f * ((lot.hash + i + 1) % 2));
                        Part(lot, "Townhouse", new Vector3(x, top / 2, 0), new Vector3(unit, top, d), Pick(HomeWalls, lot.hash, i + 1));
                        Gable(lot, new Vector3(x, top, 0), unit + .02f, .3f, d + .06f, roof, true);
                        Door(lot, x, front, Timber, .08f, .22f);
                        Part(lot, "Windows", new Vector3(x, top * .7f, 0), new Vector3(unit * .55f, .1f, d + .03f), Navy);
                    }
                    return h + .3f;
                case BuildingStyle.Apartments:
                    Part(lot, "Walls", new Vector3(0, h / 2, 0), new Vector3(w, h, d), Walls[2]);
                    Part(lot, "Parapet", new Vector3(0, h + .03f, 0), new Vector3(w + .04f, .06f, d + .04f), Slate);
                    Part(lot, "Entrance canopy", new Vector3(0, .3f, front + .06f), new Vector3(.3f, .03f, .12f), Navy);
                    Door(lot, 0, front, Glass, .16f, .26f);
                    Windows(lot, Vector3.zero, w, d, .3f, h, 3, Navy);
                    return h + .06f;
                case BuildingStyle.WalkUp:
                    Part(lot, "Brick walls", new Vector3(0, h / 2, 0), new Vector3(w, h, d), Brick);
                    for (int floor = 1; floor <= 3; floor++)
                        Part(lot, "Balcony", new Vector3(0, h * floor / 4f, front + .05f), new Vector3(w * .8f, .03f, .1f), Cream);
                    Part(lot, "Water tank", new Vector3(w * .2f, h + .12f, -d * .15f), new Vector3(.14f, .24f, .14f), Timber);
                    Door(lot, 0, front, Timber, .14f, .24f);
                    Windows(lot, Vector3.zero, w, d, .28f, h, 4, Navy);
                    return h + .24f;
                case BuildingStyle.GardenFlats:
                    Part(lot, "Walls", new Vector3(0, h * .42f, 0), new Vector3(w, h * .84f, d), White);
                    Part(lot, "Top floor", new Vector3(0, h * .92f, -d * .1f), new Vector3(w * .7f, h * .16f, d * .7f), Sand);
                    Part(lot, "Roof garden", new Vector3(0, h * .84f + .02f, 0), new Vector3(w * .96f, .04f, d * .96f), Leaf);
                    Spire(lot, new Vector3(w * .36f, h * .84f + .04f, d * .3f), .09f, .3f, Moss);
                    Spire(lot, new Vector3(-w * .36f, h * .84f + .04f, d * .3f), .09f, .26f, Moss);
                    Door(lot, 0, front, Glass, .18f, .26f);
                    Windows(lot, Vector3.zero, w, d, .28f, h * .84f, 4, Glass);
                    return h;
                case BuildingStyle.HighRise:
                    Part(lot, "Base", new Vector3(0, h * .25f, 0), new Vector3(w, h * .5f, d), Sand);
                    Part(lot, "Middle", new Vector3(0, h * .65f, 0), new Vector3(w * .78f, h * .3f, d * .78f), Sand);
                    Part(lot, "Top", new Vector3(0, h * .9f, 0), new Vector3(w * .56f, h * .2f, d * .56f), Cream);
                    Windows(lot, Vector3.zero, w, d, .3f, h * .5f, 6, Navy);
                    Windows(lot, Vector3.zero, w * .78f, d * .78f, h * .5f, h * .8f, 3, Navy);
                    Windows(lot, Vector3.zero, w * .56f, d * .56f, h * .8f, h, 2, Gold);
                    Part(lot, "Mast", new Vector3(0, h + .25f, 0), new Vector3(.04f, .5f, .04f), Slate);
                    Emit(EmitterKind.Beacon, lot, new Vector3(0, h + .52f, 0), Red);
                    return h + .5f;
                case BuildingStyle.Shop:
                case BuildingStyle.Store:
                    {
                        Color awning = Pick(Awnings, lot.hash);
                        Part(lot, "Walls", new Vector3(0, h / 2, 0), new Vector3(w, h, d), Walls[4]);
                        Part(lot, "Roof", new Vector3(0, h + .03f, 0), new Vector3(w + .06f, .06f, d + .06f), Navy);
                        Part(lot, "Shop window", new Vector3(0, .16f, front + .01f), new Vector3(w * .8f, .16f, .02f), Glass);
                        Part(lot, "Awning", new Vector3(0, .3f, front + .08f), new Vector3(w * .86f, .03f, .16f), awning);
                        Part(lot, "Sign", new Vector3(0, h - .08f, front + .015f), new Vector3(w * .6f, .08f, .02f), Gold);
                        if (def.style == BuildingStyle.Store)
                            Windows(lot, Vector3.zero, w, d, .36f, h - .1f, 1, Navy);
                        return h + .06f;
                    }
                case BuildingStyle.Office:
                    Part(lot, "Glass block", new Vector3(0, h / 2, 0), new Vector3(w, h, d), Walls[5]);
                    Windows(lot, Vector3.zero, w, d, .15f, h, 4, Glass);
                    Part(lot, "Roof plant", new Vector3(-w * .2f, h + .08f, 0), new Vector3(w * .3f, .16f, d * .4f), Stone);
                    Door(lot, 0, front, Navy, .18f, .26f);
                    return h + .16f;
                case BuildingStyle.Cafe:
                    Part(lot, "Walls", new Vector3(0, h / 2, -.06f), new Vector3(w, h, d - .12f), Pick(HomeWalls, lot.hash, 2));
                    Part(lot, "Roof", new Vector3(0, h + .03f, -.06f), new Vector3(w + .04f, .06f, d - .08f), Timber);
                    for (int i = 0; i < 4; i++)
                        Part(lot, "Awning stripe", new Vector3((i - 1.5f) * w / 4, .32f, front + .02f), new Vector3(w / 4, .03f, .2f), i % 2 == 0 ? Red : White);
                    Part(lot, "Table", new Vector3(-w * .25f, .07f, front + .1f), new Vector3(.08f, .14f, .08f), White);
                    Part(lot, "Table", new Vector3(w * .25f, .07f, front + .1f), new Vector3(.08f, .14f, .08f), White);
                    Part(lot, "Shop window", new Vector3(0, .17f, front - .11f), new Vector3(w * .7f, .14f, .02f), Glass);
                    return h + .06f;
                case BuildingStyle.MarketHall:
                    Part(lot, "Hall", new Vector3(0, h / 2, 0), new Vector3(w, h, d), Brick);
                    Gable(lot, new Vector3(0, h, 0), w + .08f, .3f, d + .08f, Glass, true);
                    for (int i = -1; i <= 1; i++)
                        Part(lot, "Arch", new Vector3(i * w * .3f, h * .35f, front + .01f), new Vector3(w * .18f, h * .6f, .02f), Navy);
                    Part(lot, "Sign", new Vector3(0, h * .82f, front + .015f), new Vector3(w * .5f, .08f, .02f), Gold);
                    return h + .3f;
                case BuildingStyle.Hotel:
                    Part(lot, "Walls", new Vector3(0, h / 2, 0), new Vector3(w, h, d), Cream);
                    Part(lot, "Cornice", new Vector3(0, h + .03f, 0), new Vector3(w + .06f, .06f, d + .06f), Terracotta);
                    Part(lot, "Canopy", new Vector3(0, .3f, front + .1f), new Vector3(w * .5f, .03f, .2f), Red);
                    Part(lot, "Hotel sign", new Vector3(w * .42f, h * .6f, front + .05f), new Vector3(.05f, h * .5f, .08f), Gold);
                    Door(lot, 0, front, Glass, .2f, .26f);
                    Windows(lot, Vector3.zero, w, d, .36f, h, 5, Navy);
                    return h + .06f;
                default:
                    return DrawCivic(def, lot, w, d, h);
            }
        }
    }
}
