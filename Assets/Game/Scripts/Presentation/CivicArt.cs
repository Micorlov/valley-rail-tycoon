using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>Municipal buildings: one landmark silhouette each, readable from the default zoom.</summary>
    public sealed partial class WorldView
    {
        static readonly Color PoliceBlue = new Color(.2f, .33f, .6f), Grass = new Color(.36f, .62f, .3f), Pitch = new Color(.3f, .66f, .32f);
        float DrawCivic(BuildingDefinition def, Lot lot, float w, float d, float h)
        {
            float front = d / 2;
            switch (def.style)
            {
                case BuildingStyle.TownHall:
                    Part(lot, "Hall", new Vector3(0, h / 2, -.06f), new Vector3(w, h, d - .12f), Sand);
                    Part(lot, "Cornice", new Vector3(0, h + .03f, -.06f), new Vector3(w + .06f, .06f, d - .06f), Cream);
                    for (int i = -2; i <= 2; i++)
                        Part(lot, "Column", new Vector3(i * w * .16f, h * .4f, front - .02f), new Vector3(.05f, h * .8f, .05f), Cream);
                    Gable(lot, new Vector3(0, h * .8f, front - .04f), w * .78f, .2f, .14f, Cream);
                    Part(lot, "Clock tower", new Vector3(0, h + .3f, -.1f), new Vector3(.24f, .6f, .24f), Sand);
                    Part(lot, "Clock", new Vector3(0, h + .42f, .03f), new Vector3(.14f, .14f, .02f), Cream);
                    Hip(lot, new Vector3(0, h + .6f, -.1f), .3f, .26f, .3f, Moss);
                    Part(lot, "Flag pole", new Vector3(0, h + 1.02f, -.1f), new Vector3(.02f, .3f, .02f), Slate);
                    Emit(EmitterKind.Flag, lot, new Vector3(0, h + 1.1f, -.1f), Red, .85f);
                    return h + 1.2f;
                case BuildingStyle.Chapel:
                    Part(lot, "Nave", new Vector3(0, h / 2, -.1f), new Vector3(w, h, d - .2f), White);
                    Gable(lot, new Vector3(0, h, -.1f), w + .06f, .34f, d - .14f, Slate, true);
                    Part(lot, "Bell tower", new Vector3(0, h * .75f, front - .1f), new Vector3(.2f, h * 1.5f, .2f), White);
                    Spire(lot, new Vector3(0, h * 1.5f, front - .1f), .14f, .7f, Slate);
                    Part(lot, "Rose window", new Vector3(0, h * .7f, front + .005f), new Vector3(.1f, .1f, .02f), Gold);
                    Door(lot, 0, front, Timber, .1f, .2f);
                    return h * 1.5f + .7f;
                case BuildingStyle.School:
                    Part(lot, "Classrooms", new Vector3(0, h / 2, -.08f), new Vector3(w, h, d - .16f), new Color(.9f, .8f, .5f));
                    Part(lot, "Roof", new Vector3(0, h + .03f, -.08f), new Vector3(w + .06f, .06f, d - .1f), Terracotta);
                    Part(lot, "Bell cote", new Vector3(0, h + .16f, -.08f), new Vector3(.14f, .22f, .1f), White);
                    Part(lot, "Bell", new Vector3(0, h + .14f, -.02f), new Vector3(.06f, .06f, .02f), Gold);
                    Part(lot, "Playground", new Vector3(0, .015f, front + .01f), new Vector3(w, .03f, .14f), Stone);
                    Door(lot, 0, front - .16f, PoliceBlue, .14f, .24f);
                    Windows(lot, new Vector3(0, 0, -.08f), w, d - .16f, .08f, h, 2, Navy);
                    return h + .3f;
                case BuildingStyle.FireStation:
                    Part(lot, "Station", new Vector3(-w * .1f, h / 2, 0), new Vector3(w * .8f, h, d), Red);
                    Part(lot, "Roof", new Vector3(-w * .1f, h + .03f, 0), new Vector3(w * .8f + .06f, .06f, d + .06f), White);
                    for (int i = 0; i < 2; i++)
                    {
                        float x = -w * .3f + i * w * .36f;
                        Part(lot, "Engine door frame", new Vector3(x, h * .36f, front + .01f), new Vector3(w * .3f, h * .72f, .02f), White);
                        Part(lot, "Engine door", new Vector3(x, h * .34f, front + .02f), new Vector3(w * .24f, h * .62f, .02f), Slate);
                    }
                    Part(lot, "Hose tower", new Vector3(w * .38f, h * .9f, -d * .2f), new Vector3(.18f, h * 1.8f, .18f), Brick);
                    Hip(lot, new Vector3(w * .38f, h * 1.8f, -d * .2f), .24f, .16f, .24f, Slate);
                    return h * 1.8f + .16f;
                case BuildingStyle.MedicalCentre:
                    Part(lot, "Clinic", new Vector3(0, h / 2, 0), new Vector3(w, h, d), White);
                    Part(lot, "Roof", new Vector3(0, h + .03f, 0), new Vector3(w + .04f, .06f, d + .04f), PaleBlue);
                    Part(lot, "Cross", new Vector3(w * .28f, h * .62f, front + .012f), new Vector3(.2f, .06f, .02f), Red);
                    Part(lot, "Cross", new Vector3(w * .28f, h * .62f, front + .012f), new Vector3(.06f, .2f, .02f), Red);
                    Part(lot, "Canopy", new Vector3(-w * .15f, .3f, front + .08f), new Vector3(.34f, .03f, .16f), PaleBlue);
                    Door(lot, -w * .15f, front, Glass, .16f, .26f);
                    Windows(lot, Vector3.zero, w, d, .36f, h, 1, Glass);
                    return h + .06f;
                case BuildingStyle.PostOffice:
                    Part(lot, "Lower walls", new Vector3(0, h * .3f, 0), new Vector3(w, h * .6f, d), PoliceBlue);
                    Part(lot, "Upper walls", new Vector3(0, h * .8f, 0), new Vector3(w, h * .4f, d), Cream);
                    Part(lot, "Roof", new Vector3(0, h + .03f, 0), new Vector3(w + .06f, .06f, d + .06f), Slate);
                    Part(lot, "Sign", new Vector3(0, h * .68f, front + .015f), new Vector3(w * .7f, .08f, .02f), Gold);
                    Part(lot, "Post box", new Vector3(w * .38f, .09f, front + .08f), new Vector3(.07f, .18f, .07f), Red);
                    Door(lot, 0, front, Glass, .14f, .24f);
                    return h + .06f;
                case BuildingStyle.Park:
                    Part(lot, "Lawn", new Vector3(0, .02f, 0), new Vector3(w, .04f, d), Grass);
                    Part(lot, "Path", new Vector3(0, .045f, 0), new Vector3(w, .01f, .12f), Sand);
                    Part(lot, "Path", new Vector3(0, .045f, 0), new Vector3(.12f, .01f, d), Sand);
                    Part(lot, "Pond", new Vector3(0, .05f, 0), new Vector3(.22f, .02f, .22f), Water);
                    for (int i = 0; i < 4; i++)
                    {
                        var at = new Vector3((i % 2 == 0 ? -1 : 1) * w * .28f, 0, (i < 2 ? -1 : 1) * d * .28f);
                        Part(lot, "Trunk", at + new Vector3(0, .1f, 0), new Vector3(.05f, .2f, .05f), Timber);
                        Spire(lot, at + new Vector3(0, .16f, 0), .15f, .42f + .06f * i, i % 2 == 0 ? Leaf : Moss);
                    }
                    Part(lot, "Bench", new Vector3(w * .2f, .05f, front - .08f), new Vector3(.16f, .05f, .05f), Timber);
                    return .7f;
                case BuildingStyle.Police:
                    Part(lot, "Station", new Vector3(0, h / 2, 0), new Vector3(w, h, d), PoliceBlue);
                    Part(lot, "White band", new Vector3(0, h * .55f, 0), new Vector3(w + .02f, .05f, d + .02f), White);
                    Part(lot, "Roof", new Vector3(0, h + .03f, 0), new Vector3(w + .06f, .06f, d + .06f), Navy);
                    Part(lot, "Blue lamp", new Vector3(0, h * .8f, front + .03f), new Vector3(.08f, .08f, .06f), new Color(.3f, .6f, 1f));
                    Part(lot, "Radio mast", new Vector3(-w * .35f, h + .3f, -d * .3f), new Vector3(.03f, .6f, .03f), Slate);
                    Emit(EmitterKind.Beacon, lot, new Vector3(-w * .35f, h + .62f, -d * .3f), Red);
                    Door(lot, 0, front, Glass, .14f, .24f);
                    Windows(lot, Vector3.zero, w, d, .05f, h * .5f, 1, Navy);
                    return h + .6f;
                case BuildingStyle.Library:
                    Part(lot, "Steps", new Vector3(0, .03f, front - .02f), new Vector3(w * .5f, .06f, .12f), Stone);
                    Part(lot, "Reading room", new Vector3(0, h / 2, -.06f), new Vector3(w, h, d - .12f), Brick);
                    Hip(lot, new Vector3(0, h, -.06f), w + .06f, .22f, d - .06f, Slate);
                    for (int i = -2; i <= 2; i++)
                        Part(lot, "Tall window", new Vector3(i * w * .18f, h * .5f, front - .115f), new Vector3(.08f, h * .6f, .02f), Navy);
                    for (int i = -1; i <= 1; i += 2)
                        Part(lot, "Column", new Vector3(i * w * .1f, h * .4f, front - .04f), new Vector3(.05f, h * .8f, .05f), Cream);
                    return h + .22f;
                case BuildingStyle.Hospital:
                    Part(lot, "Ward block", new Vector3(0, h / 2, -.06f), new Vector3(w, h, d - .12f), White);
                    Part(lot, "Wing", new Vector3(-w * .2f, .3f, front - .02f), new Vector3(w * .6f, .6f, .16f), White);
                    Part(lot, "Helipad", new Vector3(0, h + .02f, -.06f), new Vector3(w * .7f, .04f, d * .6f), Slate);
                    Part(lot, "H", new Vector3(-.08f, h + .045f, -.06f), new Vector3(.04f, .01f, .22f), White);
                    Part(lot, "H", new Vector3(.08f, h + .045f, -.06f), new Vector3(.04f, .01f, .22f), White);
                    Part(lot, "H", new Vector3(0, h + .045f, -.06f), new Vector3(.16f, .01f, .04f), White);
                    Part(lot, "Cross", new Vector3(w * .3f, h * .8f, front - .115f), new Vector3(.24f, .07f, .02f), Red);
                    Part(lot, "Cross", new Vector3(w * .3f, h * .8f, front - .115f), new Vector3(.07f, .24f, .02f), Red);
                    Windows(lot, new Vector3(0, 0, -.06f), w, d - .12f, .7f, h, 4, Glass);
                    return h + .1f;
                case BuildingStyle.Museum:
                    Part(lot, "Plinth", new Vector3(0, .05f, 0), new Vector3(w, .1f, d), Stone);
                    Part(lot, "Gallery", new Vector3(0, h / 2 + .05f, -.08f), new Vector3(w * .9f, h, d - .2f), Cream);
                    for (int i = -3; i <= 3; i++)
                        Part(lot, "Column", new Vector3(i * w * .13f, h * .45f + .05f, front - .06f), new Vector3(.05f, h * .9f, .05f), White);
                    Gable(lot, new Vector3(0, h * .9f + .05f, front - .08f), w * .92f, .2f, .16f, White);
                    Spire(lot, new Vector3(0, h + .05f, -.12f), .26f, .3f, new Color(.4f, .62f, .56f));
                    return h + .4f;
                case BuildingStyle.Stadium:
                    Part(lot, "Pitch", new Vector3(0, .03f, 0), new Vector3(w * .7f, .06f, d * .6f), Pitch);
                    Part(lot, "Halfway line", new Vector3(0, .065f, 0), new Vector3(.02f, .01f, d * .6f), White);
                    for (int side = 0; side < 4; side++)
                    {
                        bool alongX = side % 2 == 0;
                        var offset = new Vector3(Directions.Dx[side] * w * .42f, 0, Directions.Dz[side] * d * .38f);
                        Part(lot, "Stand", offset + new Vector3(0, h / 2, 0), alongX ? new Vector3(w * .9f, h, .12f) : new Vector3(.12f, h, d * .8f), alongX ? Cream : Stone);
                        Part(lot, "Seats", offset * .92f + new Vector3(0, h * .6f, 0), alongX ? new Vector3(w * .8f, .04f, .08f) : new Vector3(.08f, .04f, d * .7f), Red);
                    }
                    for (int i = 0; i < 4; i++)
                        Part(lot, "Floodlight", new Vector3((i % 2 == 0 ? -1 : 1) * w * .46f, (h + .7f) / 2, (i < 2 ? -1 : 1) * d * .44f), new Vector3(.04f, h + .7f, .04f), Slate);
                    return h + .7f;
                case BuildingStyle.DriveIn:
                    return DrawDriveIn(lot, def.size);
                case BuildingStyle.PoliceHeadquarters:
                case BuildingStyle.ShoppingMall:
                case BuildingStyle.GeneralHospital:
                case BuildingStyle.Arena:
                case BuildingStyle.University:
                    return DrawLandmark(def, lot, w, d, h);
                case BuildingStyle.Lido:
                    return DrawLido(lot, def.size, h);
                case BuildingStyle.CityPark:
                    return DrawCityPark(lot, def.size);
                case BuildingStyle.Zoo:
                    return DrawZoo(lot);
                default:
                    Part(lot, "Walls", new Vector3(0, h / 2, 0), new Vector3(w, h, d), Walls[Mathf.Clamp(def.wall, 0, Walls.Length - 1)]);
                    return h;
            }
        }
    }
}
