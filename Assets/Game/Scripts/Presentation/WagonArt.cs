using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
using static ValleyRail.TrainParts;
namespace ValleyRail
{
    /// <summary>Coaches and freight wagons, one design per cargo, drawn facing +z. Mixed variants make freight trains look like real consists.</summary>
    static class WagonArt
    {
        public const int Variants = 4;
        static readonly Color Cream = new Color(.93f, .88f, .72f);
        static readonly Color[] CoalBodies = { new Color(.19f, .19f, .2f), new Color(.36f, .23f, .17f), new Color(.42f, .43f, .44f), new Color(.24f, .27f, .31f) };
        static readonly Color[] OreBodies = { new Color(.5f, .24f, .16f), new Color(.4f, .2f, .14f), new Color(.34f, .21f, .17f), new Color(.46f, .3f, .2f) };
        static readonly Color[] BoxcarColors = { new Color(.52f, .21f, .15f), new Color(.3f, .37f, .44f) };
        static readonly Color[] ContainerColors = { new Color(.15f, .33f, .56f), new Color(.85f, .45f, .14f) };
        static readonly Color[] TankColors = { new Color(.1f, .1f, .11f), new Color(.86f, .87f, .88f), new Color(.13f, .13f, .14f), new Color(.6f, .62f, .65f) };
        static readonly Color[] BarkColors = { new Color(.36f, .26f, .17f), new Color(.42f, .31f, .2f), new Color(.3f, .22f, .15f) };
        static readonly float[] ClassicWindows = { -.185f, -.127f, -.107f, -.049f, -.029f, .029f, .049f, .107f, .127f, .185f };

        /// <summary>Whether a design shows its load, so loaded and empty wagons need different meshes.</summary>
        public static bool ShowsLoad(Cargo cargo, int variant) =>
            cargo == Cargo.Coal || cargo == Cargo.IronOre || cargo == Cargo.Wood || cargo == Cargo.Steel || (cargo == Cargo.Goods && variant % 2 == 1);

        public static void Draw(TrainMeshBuilder b, int model, Color livery, Cargo cargo, int variant, bool loaded)
        {
            switch (cargo)
            {
                case Cargo.Passengers:
                    if (model == 4 || model == 5)
                        ModernCoach(b, livery, model == 5 ? .155f : Floor);
                    else
                        ClassicCoach(b, livery);
                    break;
                case Cargo.Coal: Hopper(b, CoalBodies[variant], .4f, new Paint(new Color(.13f, .13f, .14f)), loaded); break;
                case Cargo.IronOre: Hopper(b, OreBodies[variant], .34f, new Paint(new Color(.5f, .29f, .2f), Finish.Matte), loaded); break;
                case Cargo.Goods:
                    if (variant % 2 == 0)
                        Boxcar(b, BoxcarColors[variant / 2]);
                    else
                        ContainerFlat(b, ContainerColors[variant / 2], loaded);
                    break;
                case Cargo.Wood: LogCar(b, variant, loaded); break;
                case Cargo.Oil: TankCar(b, TankColors[variant]); break;
                default: CoilCar(b, loaded); break;
            }
        }

        static void RunningGear(TrainMeshBuilder b)
        {
            Underframe(b, Frame);
            Bogies(b, 2, .05f);
            Couplers(b);
        }

        // A traditional coach: two-tone livery, separate windows, end doors, roof ventilators.
        static void ClassicCoach(TrainMeshBuilder b, Color livery)
        {
            var cream = new Paint(Cream);
            var roof = new Paint(new Color(.4f, .41f, .43f));
            var door = new Paint(Shade(livery, .88f));
            Underframe(b, Frame);
            Bogies(b, 2, .05f);
            Gangways(b, .32f, .26f);
            b.Box(new Vector3(0, .12f, 0), new Vector3(.26f, .05f, .16f), Frame);
            var edges = new List<float>(ClassicWindows) { -.245f, -.2f, .2f, .245f };
            b.Loft(BodyProfile, Stations(-.26f, .26f, 2, edges.ToArray()), z => Section.Span(Floor, .48f, HalfWidth), (z, at) =>
            {
                var band = BandAt(at.y);
                if (band == Band.Roof) return roof;
                if (band == Band.Skirt) return Frame;
                if (In(Mathf.Abs(z), .2f, .245f))
                    return band == Band.Windows ? Glass : band >= Band.Cornice ? cream : door;
                if (band == Band.Windows) return IsClassicWindow(z) ? Glass : cream;
                return band >= Band.Belt ? cream : new Paint(livery);
            }, livery, livery);
            for (int i = -2; i <= 2; i++)
                b.Box(new Vector3(0, .481f, i * .09f), new Vector3(.05f, .016f, .04f), roof);
        }

        static bool IsClassicWindow(float z)
        {
            for (int i = 0; i < ClassicWindows.Length; i += 2)
                if (In(z, ClassicWindows[i], ClassicWindows[i + 1]))
                    return true;
            return false;
        }

        // A modern coach: white body, continuous tinted window band, coloured stripe and doors, skirts.
        static void ModernCoach(TrainMeshBuilder b, Color livery, float floor)
        {
            var white = new Paint(new Color(.95f, .95f, .96f));
            var roof = new Paint(new Color(.84f, .85f, .87f));
            var skirt = new Paint(new Color(.55f, .57f, .6f));
            Bogies(b, 2, .05f);
            b.Box(new Vector3(0, .135f, 0), new Vector3(.38f, .04f, .5f), Frame);
            b.Box(new Vector3(0, .115f, 0), new Vector3(.28f, .04f, .16f), Frame);
            Gangways(b, .31f, .25f);
            b.Loft(BodyProfile, Stations(-.26f, .26f, 4, -.245f, -.2f, .2f, .245f), z => Section.Span(floor, .47f, HalfWidth), (z, at) =>
            {
                var band = BandAt(at.y);
                if (band == Band.Roof) return roof;
                if (band == Band.Skirt) return skirt;
                if (band == Band.Windows) return Glass;
                if (In(Mathf.Abs(z), .2f, .245f) && band >= Band.Stripe) return new Paint(livery);
                return band == Band.Belt ? new Paint(livery) : white;
            }, white, white);
        }

        // An open hopper with ribbed sides and discharge bays; loaded ones carry a load heaped above the rim.
        static void Hopper(TrainMeshBuilder b, Color body, float top, Paint heap, bool loaded)
        {
            var rib = Shade(body, .75f);
            RunningGear(b);
            float height = top - .17f, y = (top + .17f) * .5f;
            foreach (float side in Sides)
            {
                b.Box(new Vector3(side * .205f, y, 0), new Vector3(.02f, height, .5f), body);
                b.Box(new Vector3(side * .209f, top, 0), new Vector3(.03f, .014f, .5f), rib);
                for (int i = 0; i < 6; i++)
                    b.Box(new Vector3(side * .2165f, y, -.2f + i * .08f), new Vector3(.007f, height, .014f), rib);
            }
            foreach (float end in Sides)
                b.Box(new Vector3(0, y, end * .24f), new Vector3(.39f, height, .02f), body);
            b.Box(new Vector3(0, .185f, 0), new Vector3(.39f, .02f, .46f), rib);
            foreach (float s in Sides)
                b.Box(new Vector3(0, .14f, s * .055f), new Vector3(.3f, .06f, .09f), rib, Quaternion.Euler(s * 25, 0, 0));
            if (!loaded)
                return;
            float fill = top - .03f;
            b.Box(new Vector3(0, fill, 0), new Vector3(.39f, .012f, .46f), heap);
            b.Loft(MoundProfile, Stations(-.22f, .22f, 8), z =>
            {
                float taper = Mathf.Clamp01((.22f - Mathf.Abs(z)) / .06f);
                float rise = .05f * (.3f + .7f * taper) * (1 + .12f * Mathf.Sin(z * 37));
                return new Section(fill + rise, .185f, rise);
            }, (z, at) => heap);
        }

        // A ribbed boxcar with a sliding door, reporting marks and corner grab irons.
        static void Boxcar(TrainMeshBuilder b, Color body)
        {
            var trim = new Paint(Shade(body, .78f));
            var marks = new Paint(Cream);
            RunningGear(b);
            b.Box(new Vector3(0, .31f, 0), new Vector3(.42f, .28f, .5f), body);
            b.Box(new Vector3(0, .457f, 0), new Vector3(.44f, .016f, .51f), new Paint(new Color(.5f, .5f, .5f)));
            foreach (float side in Sides)
            {
                float x = side * .21f;
                for (int i = 0; i < 8; i++)
                {
                    float z = -.23f + i * .0657f;
                    if (Mathf.Abs(z) > .09f)
                        b.Box(new Vector3(x + side * .0025f, .31f, z), new Vector3(.005f, .27f, .012f), trim);
                }
                b.Box(new Vector3(x + side * .003f, .305f, 0), new Vector3(.006f, .25f, .15f), trim);
                foreach (float y in new[] { .18f, .44f })
                    b.Box(new Vector3(x + side * .004f, y, -.04f), new Vector3(.006f, .01f, .3f), Iron);
                b.Box(new Vector3(x + side * .0065f, .32f, .06f), new Vector3(.004f, .12f, .008f), Iron);
                b.Box(new Vector3(x + side * .003f, .4f, -.17f), new Vector3(.004f, .03f, .09f), marks);
                b.Box(new Vector3(x + side * .003f, .365f, -.17f), new Vector3(.004f, .016f, .06f), marks);
                foreach (float end in Sides)
                    for (int k = 0; k < 3; k++)
                        b.Box(new Vector3(x + side * .006f, .22f + k * .07f, end * .235f), new Vector3(.004f, .006f, .03f), Iron);
            }
        }

        // A flat wagon for one shipping container; empty ones show only the locating pins.
        static void ContainerFlat(TrainMeshBuilder b, Color box, bool loaded)
        {
            var rib = new Paint(Shade(box, .8f));
            Bogies(b, 2, .05f);
            Couplers(b);
            b.Box(new Vector3(0, .155f, 0), new Vector3(.12f, .05f, .52f), Frame);
            b.Box(new Vector3(0, .185f, 0), new Vector3(.4f, .02f, .52f), new Paint(new Color(.36f, .24f, .18f)));
            if (!loaded)
            {
                foreach (float side in Sides)
                    foreach (float end in Sides)
                        b.Box(new Vector3(side * .18f, .205f, end * .23f), new Vector3(.03f, .02f, .03f), Iron);
                return;
            }
            b.Box(new Vector3(0, .325f, 0), new Vector3(.4f, .26f, .48f), box);
            foreach (float side in Sides)
            {
                for (int i = 0; i < 10; i++)
                    b.Box(new Vector3(side * .2025f, .325f, -.216f + i * .048f), new Vector3(.005f, .245f, .012f), rib);
                foreach (float y in new[] { .2f, .45f })
                    b.Box(new Vector3(side * .2f, y, 0), new Vector3(.012f, .012f, .48f), rib);
                b.Box(new Vector3(side * .2045f, .38f, .03f), new Vector3(.004f, .05f, .17f), new Paint(new Color(.95f, .95f, .93f)));
            }
            b.Box(new Vector3(0, .325f, -.2415f), new Vector3(.38f, .24f, .004f), rib);
            foreach (float x in new[] { -.12f, -.04f, .04f, .12f })
                b.Box(new Vector3(x, .325f, -.2445f), new Vector3(.006f, .22f, .004f), Iron);
        }

        // A bulkhead flat with stakes; loaded ones carry three tiers of logs with pale cut ends.
        static void LogCar(TrainMeshBuilder b, int variant, bool loaded)
        {
            RunningGear(b);
            b.Box(new Vector3(0, .18f, 0), new Vector3(.4f, .02f, .52f), new Paint(new Color(.3f, .24f, .19f)));
            foreach (float end in Sides)
                b.Box(new Vector3(0, .29f, end * .245f), new Vector3(.4f, .2f, .015f), Iron);
            foreach (float side in Sides)
                foreach (float z in new[] { -.16f, -.05f, .06f, .17f })
                    b.Box(new Vector3(side * .2f, .28f, z), new Vector3(.014f, .18f, .014f), Iron);
            if (!loaded)
                return;
            var rng = new System.Random(variant * 7919 + 17);
            var cut = new Paint(new Color(.82f, .66f, .45f), Finish.Matte);
            (int count, float y)[] tiers = { (4, .237f), (3, .315f), (2, .393f) };
            foreach (var (count, y) in tiers)
                for (int i = 0; i < count; i++)
                {
                    float x = (i - (count - 1) * .5f) * .09f + (float)(rng.NextDouble() - .5) * .01f;
                    float radius = .043f + (float)rng.NextDouble() * .006f;
                    float length = .42f + (float)rng.NextDouble() * .03f;
                    var bark = new Paint(BarkColors[rng.Next(BarkColors.Length)], Finish.Matte);
                    b.Cylinder(new Vector3(x, y, (float)(rng.NextDouble() - .5) * .02f), radius, length, bark, Quaternion.identity, 8, cut);
                }
        }

        // A tank wagon with dished heads, dome, walkway, bands and hazard placards.
        static void TankCar(TrainMeshBuilder b, Color shell)
        {
            var paint = new Paint(shell);
            var band = new Paint(Shade(shell, shell.r > .5f ? .8f : 1.8f));
            Bogies(b, 2, .05f);
            Couplers(b);
            b.Box(new Vector3(0, .155f, 0), new Vector3(.1f, .05f, .52f), Frame);
            foreach (float end in Sides)
            {
                b.Box(new Vector3(0, .175f, end * .24f), new Vector3(.4f, .014f, .05f), Frame);
                b.Box(new Vector3(0, .2f, end * .16f), new Vector3(.24f, .05f, .04f), Frame);
                b.Cylinder(new Vector3(0, .345f, end * .226f), .128f, .012f, paint, Quaternion.identity, 16);
                b.Cylinder(new Vector3(0, .345f, end * .234f), .085f, .006f, paint, Quaternion.identity, 16);
            }
            b.Cylinder(new Vector3(0, .345f, 0), .155f, .44f, paint, Quaternion.identity, 16);
            foreach (float z in new[] { -.13f, .13f })
                b.Cylinder(new Vector3(0, .345f, z), .158f, .01f, band, Quaternion.identity, 16);
            b.Cylinder(new Vector3(0, .51f, 0), .05f, .05f, paint, AlongY, 12);
            b.Cylinder(new Vector3(0, .54f, 0), .03f, .012f, Iron, AlongY, 10);
            b.Box(new Vector3(0, .501f, 0), new Vector3(.16f, .006f, .14f), Iron);
            foreach (float side in Sides)
                b.Box(new Vector3(side * .158f, .345f, .185f), new Vector3(.004f, .035f, .035f), new Paint(new Color(.95f, .45f, .1f)), Quaternion.Euler(45, 0, 0));
        }

        // A coil wagon with cradle troughs; loaded ones carry three strapped steel coils.
        static void CoilCar(TrainMeshBuilder b, bool loaded)
        {
            var body = new Paint(new Color(.27f, .33f, .42f));
            RunningGear(b);
            b.Box(new Vector3(0, .185f, 0), new Vector3(.4f, .03f, .52f), body);
            foreach (float side in Sides)
                b.Box(new Vector3(side * .13f, .225f, 0), new Vector3(.05f, .05f, .48f), body);
            foreach (float end in Sides)
                b.Box(new Vector3(0, .235f, end * .245f), new Vector3(.4f, .07f, .02f), body);
            if (!loaded)
                return;
            var coil = new Paint(new Color(.62f, .64f, .67f), Finish.Metal);
            foreach (float z in new[] { -.15f, 0, .15f })
            {
                b.Cylinder(new Vector3(0, .305f, z), .1f, .11f, coil, Quaternion.identity, 14);
                b.Cylinder(new Vector3(0, .305f, z), .04f, .114f, Rubber, Quaternion.identity, 10);
                b.Cylinder(new Vector3(0, .305f, z), .102f, .01f, Iron, Quaternion.identity, 14);
            }
        }
    }
}
