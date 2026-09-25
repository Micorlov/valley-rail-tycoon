using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>Landmarks from 2×2 to 8×8 cells. The lot frame is centred on the footprint; details repeat with its size.</summary>
    public sealed partial class WorldView
    {
        float DrawLandmark(BuildingDefinition def, Lot lot, float w, float d, float h)
        {
            int size = def.size;
            float front = d / 2;
            switch (def.style)
            {
                case BuildingStyle.PoliceHeadquarters:
                    {
                        var hq = new Vector3(-w * .18f, 0, -d * .18f);
                        Part(lot, "Headquarters", hq + new Vector3(0, h / 2, 0), new Vector3(w * .64f, h, d * .56f), PoliceBlue);
                        Part(lot, "White band", hq + new Vector3(0, h * .62f, 0), new Vector3(w * .64f + .02f, .06f, d * .56f + .02f), White);
                        Part(lot, "Roof", hq + new Vector3(0, h + .03f, 0), new Vector3(w * .66f, .06f, d * .58f), Navy);
                        Windows(lot, hq, w * .64f, d * .56f, .1f, h, 2 + size, Navy);
                        Part(lot, "Blue lamp", hq + new Vector3(0, .5f, d * .28f + .02f), new Vector3(.08f, .08f, .04f), new Color(.3f, .6f, 1f));
                        Part(lot, "Vehicle garage", new Vector3(w * .34f, .25f, -d * .2f), new Vector3(w * .3f, .5f, d * .5f), Stone);
                        Part(lot, "Garage door", new Vector3(w * .34f, .2f, -d * .2f + d * .25f + .012f), new Vector3(w * .22f, .34f, .02f), Slate);
                        Part(lot, "Courtyard", new Vector3(w * .05f, .012f, d * .26f), new Vector3(w * .9f, .02f, d * .44f), Stone);
                        Part(lot, "Fence", new Vector3(-w * .28f, .07f, front - .03f), new Vector3(w * .42f, .14f, .04f), White);
                        Part(lot, "Fence", new Vector3(w * .33f, .07f, front - .03f), new Vector3(w * .32f, .14f, .04f), White);
                        // Patrol cars in marked bays on an asphalt strip, nose out towards the gate.
                        int cars = Mathf.Max(3, 2 * size - 1);
                        float row = d * .33f, bay = w * .8f / cars;
                        Part(lot, "Car park", new Vector3(w * .04f, .02f, row), new Vector3(w * .84f, .02f, .46f), Asphalt);
                        for (int i = 0; i <= cars; i++)
                            Part(lot, "Bay line", new Vector3(-w * .36f + i * bay, .033f, row), new Vector3(.02f, .01f, .4f), White);
                        for (int i = 0; i < cars; i++)
                        {
                            var bayCentre = new Vector3(-w * .36f + (i + .5f) * bay, .03f, row);
                            float top = ParkedCar(lot, bayCentre, .32f, White, patrol: true);
                            Emit(EmitterKind.Beacon, lot, new Vector3(bayCentre.x, top + .02f, bayCentre.z), i % 2 == 0 ? BeaconBlue : Red);
                        }
                        Part(lot, "Radio mast", hq + new Vector3(-w * .22f, h + .45f * size, -d * .17f), new Vector3(.04f, .9f * size, .04f), Slate);
                        Emit(EmitterKind.Beacon, lot, hq + new Vector3(-w * .22f, h + .9f * size + .02f, -d * .17f), Red);
                        Part(lot, "Flag pole", new Vector3(w * .38f, .45f, front - .12f), new Vector3(.025f, .9f, .025f), Slate);
                        Emit(EmitterKind.Flag, lot, new Vector3(w * .38f, .82f, front - .12f), PoliceBlue);
                        return h + .9f * size;
                    }
                case BuildingStyle.ShoppingMall:
                    {
                        Part(lot, "Mall", new Vector3(0, h / 2, -d * .2f), new Vector3(w, h, d * .6f), Sand);
                        Part(lot, "Sign band", new Vector3(0, h * .8f, d * .1f + .012f), new Vector3(w * .9f, .12f, .02f), Red);
                        Part(lot, "Roof", new Vector3(0, h + .03f, -d * .2f), new Vector3(w + .04f, .06f, d * .6f + .04f), Stone);
                        int skylights = size + 1;
                        for (int i = 0; i < skylights; i++)
                            Part(lot, "Skylight", new Vector3(-w * .4f + i * w * .8f / (skylights - 1), h + .08f, -d * .2f), new Vector3(.2f, .06f, d * .4f), Glass);
                        Part(lot, "Atrium", new Vector3(0, h * .42f, d * .16f), new Vector3(w * .3f, h * .84f, d * .16f), Glass);
                        Gable(lot, new Vector3(0, h * .84f, d * .16f), w * .32f, .22f + .06f * size, d * .18f, PaleBlue, true);
                        Part(lot, "Mall sign", new Vector3(0, h + .2f, d * .1f - .02f), new Vector3(w * .3f, .2f, .04f), Gold);
                        Part(lot, "Car park", new Vector3(0, .012f, d * .37f), new Vector3(w, .02f, d * .26f), Asphalt);
                        int bays = size * 3, rows = size >= 4 ? 2 : 1;
                        float bay = w * .94f / bays, line = Mathf.Max(d * .1f, .3f);
                        for (int r = 0; r < rows; r++)
                        {
                            float z = d * (rows == 1 ? .37f : r == 0 ? .31f : .43f);
                            for (int i = 0; i <= bays; i++)
                                Part(lot, "Bay line", new Vector3(-w * .47f + i * bay, .025f, z), new Vector3(.02f, .01f, line), White);
                            for (int i = 0; i < bays; i++)
                            {
                                if ((i * 7 + r * 3) % 5 >= 3)
                                    continue;
                                // Two rows face each other nose to nose across the middle line; now and then a driver reversed in. Cars sit a little off-centre.
                                int heading = (rows > 1 && r == 0 ? 0 : 180) + ((i * 3 + r) % 5 == 0 ? 180 : 0);
                                float offset = ((i * 13 + r * 7) % 5 - 2) * .006f;
                                var paint = CarPaint[(i * 5 + r * 3 + lot.hash % 7) % CarPaint.Length];
                                ParkedCar(lot, new Vector3(-w * .47f + (i + .5f) * bay + offset, .022f, z), .3f, paint, heading);
                            }
                        }
                        return h + .3f + .06f * size;
                    }
                case BuildingStyle.GeneralHospital:
                    {
                        var tower = new Vector3(-w * .15f, 0, -d * .15f);
                        float face = -d * .15f + d * .225f + .012f;
                        Part(lot, "Ward tower", tower + new Vector3(0, h / 2, 0), new Vector3(w * .5f, h, d * .45f), White);
                        Windows(lot, tower, w * .5f, d * .45f, .75f, h, 5 + size, Glass);
                        Part(lot, "Clinic wing", new Vector3(w * .12f, .35f, d * .05f), new Vector3(w * .76f, .7f, d * .5f), White);
                        Windows(lot, new Vector3(w * .12f, 0, d * .05f), w * .76f, d * .5f, .1f, .7f, 1, Glass);
                        if (size >= 3)
                        {
                            Part(lot, "Research wing", new Vector3(-w * .3f, .5f, -d * .38f), new Vector3(w * .36f, 1f, d * .2f), PaleBlue);
                            Windows(lot, new Vector3(-w * .3f, 0, -d * .38f), w * .36f, d * .2f, .1f, 1f, 2, Navy);
                        }
                        Part(lot, "Cross", new Vector3(-w * .15f, h * .82f, face), new Vector3(.32f, .1f, .02f), Red);
                        Part(lot, "Cross", new Vector3(-w * .15f, h * .82f, face), new Vector3(.1f, .32f, .02f), Red);
                        Part(lot, "Helipad", tower + new Vector3(0, h + .02f, 0), new Vector3(w * .46f, .04f, d * .41f), Slate);
                        Part(lot, "H", tower + new Vector3(-.09f, h + .045f, 0), new Vector3(.04f, .01f, .24f), White);
                        Part(lot, "H", tower + new Vector3(.09f, h + .045f, 0), new Vector3(.04f, .01f, .24f), White);
                        Part(lot, "H", tower + new Vector3(0, h + .045f, 0), new Vector3(.18f, .01f, .04f), White);
                        Part(lot, "Ambulance bay", new Vector3(w * .3f, .34f, front - .1f), new Vector3(w * .32f, .04f, .24f), Red);
                        for (int i = 0; i < Mathf.Max(1, size - 1); i++)
                        {
                            float x = w * .3f + (i - (size - 2) * .5f) * .22f;
                            Part(lot, "Ambulance", new Vector3(x, .1f, front - .08f), new Vector3(.16f, .15f, .3f), White);
                            Part(lot, "Ambulance stripe", new Vector3(x, .12f, front - .08f), new Vector3(.17f, .03f, .31f), Red);
                        }
                        Part(lot, "Garden", new Vector3(-w * .3f, .015f, d * .34f), new Vector3(w * .34f, .03f, d * .26f), Grass);
                        for (int i = 0; i < size + 1; i++)
                            Spire(lot, new Vector3(-w * .44f + i * w * .28f / size, .03f, d * (i % 2 == 0 ? .38f : .3f)), .12f, .36f, i % 2 == 0 ? Leaf : Moss);
                        return h + .1f;
                    }
                case BuildingStyle.Arena:
                    {
                        var arena = ArenaFor(w, d, h, size);
                        var bowl = ArenaBowl(lot, d, size);
                        Part(bowl, "Running track", new Vector3(0, .02f, 0), new Vector3(w * .7f, .04f, arena.d * .58f), size >= 4 ? Terracotta : Pitch);
                        ArenaPitch(bowl, arena, Pitch);
                        if (size >= 4)
                            ArenaCarPark(lot, w, d, size);
                        return ArenaStands(bowl, arena);
                    }
                case BuildingStyle.University:
                    {
                        // Four halls round a lawn quad, a clock tower on the main hall and a gatehouse facing the street.
                        Part(lot, "Quad", new Vector3(0, .015f, 0), new Vector3(w * .56f, .03f, d * .5f), Grass);
                        Part(lot, "Path", new Vector3(0, .035f, 0), new Vector3(.18f, .01f, d * .5f), Sand);
                        Part(lot, "Path", new Vector3(0, .035f, 0), new Vector3(w * .56f, .01f, .18f), Sand);
                        Part(lot, "Main hall", new Vector3(0, h / 2, -d * .37f), new Vector3(w * .8f, h, d * .2f), Brick);
                        Hip(lot, new Vector3(0, h, -d * .37f), w * .82f, .5f, d * .22f, Slate);
                        Windows(lot, new Vector3(0, 0, -d * .37f), w * .8f, d * .2f, .1f, h, 3, Cream);
                        Part(lot, "Clock tower", new Vector3(0, h * .9f, -d * .3f), new Vector3(.5f, h * 1.8f, .5f), Brick);
                        Part(lot, "Clock", new Vector3(0, h * 1.5f, -d * .3f + .26f), new Vector3(.32f, .32f, .02f), Cream);
                        Spire(lot, new Vector3(0, h * 1.8f, -d * .3f), .36f, 1.1f, Moss);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Part(lot, "Wing", new Vector3(side * w * .37f, h * .4f, d * .02f), new Vector3(w * .2f, h * .8f, d * .56f), Brick);
                            Gable(lot, new Vector3(side * w * .37f, h * .8f, d * .02f), w * .22f, .4f, d * .58f, Slate, true);
                            Windows(lot, new Vector3(side * w * .37f, 0, d * .02f), w * .2f, d * .56f, .1f, h * .8f, 2, Cream);
                            Part(lot, "Gatehouse", new Vector3(side * w * .2f, h * .3f, d * .4f), new Vector3(w * .28f, h * .6f, d * .14f), Stone);
                            Gable(lot, new Vector3(side * w * .2f, h * .6f, d * .4f), w * .3f, .3f, d * .16f, Slate);
                        }
                        for (int i = 0; i < 4; i++)
                        {
                            var at = new Vector3((i % 2 == 0 ? -1 : 1) * w * .15f, .03f, (i < 2 ? -1 : 1) * d * .12f);
                            Part(lot, "Trunk", at + new Vector3(0, .15f, 0), new Vector3(.08f, .3f, .08f), Timber);
                            Spire(lot, at + new Vector3(0, .25f, 0), .3f, .8f, i % 2 == 0 ? Leaf : Moss);
                        }
                        return h * 1.8f + 1.1f;
                    }
                default:
                    return h;
            }
        }
    }
}
