using System.Collections.Generic;
using UnityEngine;
using static ValleyRail.TrainParts;
namespace ValleyRail
{
    /// <summary>Locomotives and driving cars, one prototype per train model, drawn facing +z.</summary>
    static class LocomotiveArt
    {
        static readonly Color White = new Color(.95f, .95f, .96f), Cream = new Color(.93f, .88f, .72f);

        /// <param name="tail">A driving car at the back of the train: its lamps show red.</param>
        public static void Draw(TrainMeshBuilder b, int model, Color livery, bool tail)
        {
            switch (model)
            {
                case 0: SteamTankEngine(b, livery); break;
                case 1: StreamlinedDiesel(b, livery); break;
                case 3: HoodUnit(b, livery); break;
                case 4: CommuterCab(b, livery, tail); break;
                case 5: PowerCar(b, livery, tail); break;
                default: ElectricLocomotive(b, livery); break;
            }
        }

        // A small six-coupled tank engine: lined side tanks, brass-banded boiler, smokebox, chimney and a cab at the back.
        static void SteamTankEngine(TrainMeshBuilder b, Color livery)
        {
            var black = new Paint(new Color(.09f, .09f, .1f));
            var brass = new Paint(new Color(.86f, .67f, .3f), Finish.Metal);
            var bufferBeam = new Paint(new Color(.72f, .12f, .08f));
            var wheelCentre = new Paint(new Color(.6f, .1f, .07f));
            b.Box(new Vector3(0, .115f, 0), new Vector3(.3f, .07f, .48f), black);
            b.Box(new Vector3(0, .17f, 0), new Vector3(.44f, .02f, .5f), black);
            foreach (float side in Sides)
                b.Box(new Vector3(side * .216f, .15f, 0), new Vector3(.01f, .026f, .5f), livery);
            foreach (float end in Sides)
                Buffers(b, end, bufferBeam);
            const float wheel = .068f;
            float axle = RailTop + wheel;
            foreach (float z in new[] { .145f, .005f, -.135f })
            {
                b.Cylinder(new Vector3(0, axle, z), .014f, .36f, Iron, AlongX, 6);
                foreach (float side in Sides)
                {
                    b.Cylinder(new Vector3(side * .19f, axle, z), wheel, .028f, Iron, AlongX, 14);
                    b.Cylinder(new Vector3(side * .2055f, axle, z), wheel * .78f, .004f, wheelCentre, AlongX, 14);
                    b.Cylinder(new Vector3(side * .209f, axle, z), .014f, .004f, Steel, AlongX, 8);
                }
            }
            foreach (float side in Sides)
            {
                b.Box(new Vector3(side * .214f, axle, .005f), new Vector3(.008f, .016f, .28f), Steel);
                b.Box(new Vector3(side * .16f, .275f, .075f), new Vector3(.12f, .19f, .28f), livery);
                b.Box(new Vector3(side * .16f, .373f, .075f), new Vector3(.124f, .008f, .284f), black);
                foreach (float y in new[] { .205f, .345f })
                    b.Box(new Vector3(side * .2205f, y, .075f), new Vector3(.002f, .005f, .24f), brass);
                foreach (float z in new[] { -.045f, .195f })
                    b.Box(new Vector3(side * .2205f, .275f, z), new Vector3(.002f, .145f, .005f), brass);
            }
            b.Cylinder(new Vector3(0, .3f, .05f), .115f, .28f, livery, Quaternion.identity, 16);
            foreach (float z in new[] { -.05f, .05f, .15f })
                b.Cylinder(new Vector3(0, .3f, z), .118f, .008f, brass, Quaternion.identity, 16);
            b.Cylinder(new Vector3(0, .3f, .225f), .122f, .07f, black, Quaternion.identity, 16);
            b.Cylinder(new Vector3(0, .3f, .262f), .092f, .006f, Iron, Quaternion.identity, 16);
            b.Cylinder(new Vector3(0, .3f, .266f), .012f, .006f, Steel, Quaternion.identity, 8);
            b.Cylinder(new Vector3(0, .46f, .22f), .033f, .11f, black, AlongY, 12);
            b.Cylinder(new Vector3(0, .515f, .22f), .044f, .018f, black, AlongY, 12);
            b.Cylinder(new Vector3(0, .425f, .07f), .045f, .06f, brass, AlongY, 12);
            b.Cylinder(new Vector3(0, .458f, .07f), .034f, .01f, brass, AlongY, 12);
            b.Cylinder(new Vector3(0, .43f, -.04f), .016f, .05f, brass, AlongY, 8);
            b.Box(new Vector3(.15f, .2f, .245f), new Vector3(.045f, .045f, .03f), black);
            Lamp(b, new Vector3(.15f, .2f, .262f), .015f, HeadLamp);
            // Cab
            b.Box(new Vector3(0, .325f, -.17f), new Vector3(.44f, .29f, .16f), livery);
            b.Box(new Vector3(0, .478f, -.17f), new Vector3(.47f, .016f, .19f), black);
            b.Box(new Vector3(0, .49f, -.17f), new Vector3(.36f, .012f, .17f), black);
            b.Cylinder(new Vector3(0, .5f, -.085f), .008f, .04f, brass, AlongY, 6);
            foreach (float side in Sides)
            {
                Pane(b, new Vector3(side * .2215f, .4f, -.15f), new Vector3(.004f, .07f, .08f));
                Pane(b, new Vector3(side * .13f, .415f, -.0885f), new Vector3(.08f, .065f, .004f));
                b.Box(new Vector3(side * .2215f, .31f, -.17f), new Vector3(.003f, .04f, .07f), brass);
            }
        }

        static Section BulldogNose(float z)
        {
            float top = .49f, halfWidth = HalfWidth;
            if (z > .09f)
                top = Mathf.Lerp(.49f, .425f, Mathf.InverseLerp(.09f, .15f, z));
            if (z > .15f)
            {
                float t = Mathf.Clamp01((z - .15f) / .12f);
                top = .3f + .125f * Mathf.Sqrt(1 - t * t);
            }
            if (z > .12f)
            {
                float t = Mathf.Clamp01((z - .12f) / .15f);
                halfWidth = HalfWidth * (.35f + .65f * Mathf.Sqrt(1 - t * t));
            }
            return Section.Span(Floor, top, halfWidth);
        }

        // A streamlined cab unit: rounded "bulldog" nose, raked windscreen, portholes, grilles and a nose band that wraps round the front.
        static void StreamlinedDiesel(TrainMeshBuilder b, Color livery)
        {
            var grille = new Paint(new Color(.2f, .21f, .23f), Finish.Matte);
            Underframe(b, Frame);
            Bogies(b, 2, .05f);
            Couplers(b);
            b.Box(new Vector3(0, .115f, 0), new Vector3(.32f, .06f, .12f), Frame);
            var stations = Stations(-.26f, .27f, 8, -.25f, -.21f, -.19f, -.17f, -.15f, -.11f, -.09f, -.07f, -.05f, -.01f, .03f, .085f, .09f, .11f, .13f, .15f, .17f, .19f, .21f, .225f, .24f, .25f, .258f, .265f);
            b.Loft(BodyProfile, stations, BulldogNose, (z, at) =>
            {
                var band = BandAt(at.y);
                if (band == Band.Skirt) return Frame;
                if (z > .09f && z < .15f && band >= Band.Windows) return Glass;
                if (band == Band.Windows && In(z, .03f, .085f)) return Glass;
                if (band == Band.Windows && (In(z, -.25f, -.21f) || In(z, -.15f, -.11f) || In(z, -.05f, -.01f))) return grille;
                if (band == Band.Windows && (In(z, -.19f, -.17f) || In(z, -.09f, -.07f))) return Glass;
                if (band == Band.Lower || (z > .17f && band <= Band.Belt)) return Warning;
                return livery;
            }, livery, Warning);
            b.Box(new Vector3(0, .12f, .255f), new Vector3(.38f, .1f, .02f), Frame, Quaternion.Euler(-25, 0, 0));
            Lamp(b, new Vector3(0, .31f, .27f), .02f, HeadLamp);
            b.Cylinder(new Vector3(0, .5f, .05f), .01f, .05f, Steel, Quaternion.identity, 6);
            foreach (float z in new[] { -.2f, -.1f })
                b.Cylinder(new Vector3(0, .49f, z), .045f, .012f, grille, AlongY, 12);
            b.Box(new Vector3(0, .492f, -.02f), new Vector3(.14f, .012f, .06f), grille);
            b.Box(new Vector3(0, .33f, -.262f), new Vector3(.1f, .22f, .004f), Shade(livery, .8f));
        }

        // A road switcher: narrow long hood with radiator fans, full-width cab, short nose, walkways with handrails and six-wheel bogies.
        static void HoodUnit(TrainMeshBuilder b, Color livery)
        {
            var grille = new Paint(new Color(.2f, .2f, .22f), Finish.Matte);
            var cabRoof = new Paint(new Color(.9f, .9f, .88f));
            Bogies(b, 3, .045f, .15f);
            Couplers(b);
            b.Box(new Vector3(0, .12f, 0), new Vector3(.32f, .065f, .16f), Frame);
            b.Box(new Vector3(0, .175f, 0), new Vector3(.44f, .035f, .52f), Frame);
            foreach (float side in Sides)
                b.Box(new Vector3(side * .2205f, .172f, 0), new Vector3(.002f, .012f, .5f), Warning);
            var hood = Stations(-.25f, .045f, 6, -.24f, -.2f, -.17f, -.13f, -.1f, -.06f);
            b.Loft(BodyProfile, hood, z => Section.Span(.1925f, .43f, .15f), (z, at) =>
                BandAt(at.y) == Band.Windows && (In(z, -.24f, -.2f) || In(z, -.17f, -.13f) || In(z, -.1f, -.06f)) ? grille : new Paint(livery),
                livery, livery);
            var cab = Stations(.045f, .165f, 4, .07f, .15f);
            b.Loft(BodyProfile, cab, z => Section.Span(.1925f, .5f, .205f), (z, at) =>
            {
                var band = BandAt(at.y);
                if (band == Band.Roof) return cabRoof;
                return band == Band.Windows && In(z, .07f, .15f) ? Glass : new Paint(livery);
            }, livery, livery);
            var nose = Stations(.165f, .25f, 3);
            b.Loft(BodyProfile, nose, z => Section.Span(.1925f, .375f - .03f * Mathf.InverseLerp(.2f, .25f, z), .14f), (z, at) => livery, livery, livery);
            foreach (float side in Sides)
            {
                Pane(b, new Vector3(side * .085f, .43f, .1665f), new Vector3(.13f, .075f, .004f));
                Pane(b, new Vector3(side * .1f, .465f, .0435f), new Vector3(.06f, .04f, .004f));
                Lamp(b, new Vector3(side * .05f, .31f, .256f), .016f, HeadLamp);
                Lamp(b, new Vector3(side * .17f, .215f, .266f), .012f, HeadLamp);
                // Walkway handrails along the long hood and round the short nose.
                foreach (float z in new[] { -.245f, -.17f, -.095f, -.02f, .04f, .2f, .25f })
                    b.Box(new Vector3(side * .212f, .247f, z), new Vector3(.007f, .11f, .007f), Warning);
                b.Box(new Vector3(side * .212f, .3f, -.1025f), new Vector3(.007f, .007f, .29f), Warning);
                b.Box(new Vector3(side * .212f, .3f, .2075f), new Vector3(.007f, .007f, .085f), Warning);
                foreach (float end in Sides)
                    b.Box(new Vector3(side * .205f, .135f, end * .235f), new Vector3(.04f, .05f, .04f), Warning);
            }
            b.Box(new Vector3(0, .22f, .2515f), new Vector3(.26f, .02f, .004f), Warning);
            b.Box(new Vector3(0, .125f, .258f), new Vector3(.42f, .07f, .02f), Frame, Quaternion.Euler(-20, 0, 0));
            b.Box(new Vector3(0, .13f, -.258f), new Vector3(.42f, .06f, .02f), Frame);
            foreach (float z in new[] { -.22f, -.15f })
                b.Cylinder(new Vector3(0, .432f, z), .05f, .012f, grille, AlongY, 12);
            b.Box(new Vector3(0, .44f, -.03f), new Vector3(.05f, .025f, .08f), Rubber);
            b.Cylinder(new Vector3(0, .51f, .1f), .008f, .05f, Steel, Quaternion.identity, 6);
        }

        static Section BoxCab(float z)
        {
            float end = Mathf.InverseLerp(.2f, .26f, Mathf.Abs(z));
            return Section.Span(Floor, .47f, HalfWidth - .016f * end);
        }

        // A European electric locomotive: cabs at both ends, a machine room with grilles, and one pantograph raised.
        static void ElectricLocomotive(TrainMeshBuilder b, Color livery)
        {
            var cream = new Paint(Cream);
            var roof = new Paint(new Color(.44f, .45f, .47f));
            var grille = new Paint(Shade(livery, .55f), Finish.Matte);
            Underframe(b, Frame);
            Bogies(b, 2, .055f);
            b.Box(new Vector3(0, .12f, 0), new Vector3(.28f, .06f, .14f), Frame);
            var stations = Stations(-.26f, .26f, 4, -.245f, -.235f, -.17f, -.11f, -.07f, -.05f, -.01f, .01f, .05f, .07f, .11f, .17f, .235f, .245f);
            b.Loft(BodyProfile, stations, BoxCab, (z, at) =>
            {
                var band = BandAt(at.y);
                float a = Mathf.Abs(z);
                if (band == Band.Roof) return roof;
                if (band == Band.Skirt) return Frame;
                if (band == Band.Stripe) return cream;
                if (band == Band.Windows && In(a, .17f, .235f)) return Glass;
                if (band == Band.Windows && (In(a, .01f, .05f) || In(a, .07f, .11f))) return grille;
                return livery;
            }, livery, livery);
            foreach (float end in Sides)
            {
                float face = end * (HalfLength + .002f);
                foreach (float side in Sides)
                {
                    Pane(b, new Vector3(side * .075f, .405f, face), new Vector3(.13f, .075f, .004f));
                    Lamp(b, new Vector3(side * .14f, .225f, face + end * .004f), .016f, end > 0 ? HeadLamp : Glass);
                }
                Lamp(b, new Vector3(0, .445f, face + end * .004f), .013f, end > 0 ? HeadLamp : Glass);
                b.Box(new Vector3(0, .28f, face), new Vector3(.3f, .06f, .004f), Warning);
                Buffers(b, end, Frame);
            }
            Pantograph(b, -.12f, .47f, true);
            Pantograph(b, .12f, .47f, false);
            b.Box(new Vector3(0, .476f, 0), new Vector3(.08f, .012f, .1f), Iron);
        }

        static Section RakedCab(float z)
        {
            float t = Mathf.InverseLerp(.19f, .262f, z);
            return Section.Span(Floor, .47f - .06f * t, HalfWidth - .02f * t * t);
        }

        // An electric multiple unit driving car: raked windscreen, sliding doors and a continuous window band.
        static void CommuterCab(TrainMeshBuilder b, Color livery, bool tail)
        {
            var white = new Paint(new Color(.93f, .94f, .94f));
            var roof = new Paint(new Color(.72f, .74f, .76f));
            Underframe(b, Frame);
            Bogies(b, 2, .045f);
            b.Box(new Vector3(0, .12f, 0), new Vector3(.3f, .05f, .18f), Frame);
            Gangways(b, .31f, .25f, front: false);
            var stations = Stations(-.26f, .262f, 4, -.13f, -.07f, .05f, .11f, .19f, .205f, .22f, .235f, .25f);
            b.Loft(BodyProfile, stations, RakedCab, (z, at) =>
            {
                var band = BandAt(at.y);
                bool door = In(z, -.13f, -.07f) || In(z, .05f, .11f);
                if (z > .19f && band >= Band.Windows) return Glass;
                if (band == Band.Roof) return roof;
                if (band == Band.Skirt) return Frame;
                if (door) return band == Band.Windows ? Glass : new Paint(livery);
                if (band == Band.Windows) return Glass;
                return band == Band.Stripe ? new Paint(livery) : white;
            }, white, livery);
            foreach (float side in Sides)
            {
                Pane(b, new Vector3(side * .075f, .335f, .2635f), new Vector3(.14f, .08f, .004f));
                Lamp(b, new Vector3(side * .13f, .225f, .265f), .016f, tail ? TailLamp : HeadLamp);
            }
            b.Box(new Vector3(0, .15f, .272f), new Vector3(.08f, .04f, .03f), Iron);
            b.Box(new Vector3(0, .125f, .255f), new Vector3(.4f, .05f, .02f), Frame);
            if (tail)
                Pantograph(b, -.1f, .47f, true);
            else
                b.Box(new Vector3(0, .482f, -.1f), new Vector3(.22f, .03f, .12f), roof);
            b.Box(new Vector3(0, .478f, .08f), new Vector3(.18f, .02f, .08f), roof);
        }

        static float NoseT(float z) => Mathf.Clamp01((z - .02f) / .29f);
        static Section BulletNose(float z)
        {
            float t = NoseT(z);
            float top = .215f + .255f * Mathf.Pow(1 - Mathf.Pow(t, 1.6f), .625f);
            float halfWidth = Mathf.Max(.03f, HalfWidth * Mathf.Pow(1 - t * t * t, 1 / 3f));
            return Section.Span(.155f + .03f * t * t, top, halfWidth);
        }

        // A high-speed power car: long aerodynamic nose with a wrap-round windscreen, coloured chin and stripe, and body skirts.
        static void PowerCar(TrainMeshBuilder b, Color livery, bool tail)
        {
            var white = new Paint(White);
            var roof = new Paint(new Color(.84f, .85f, .87f));
            var skirt = new Paint(new Color(.55f, .57f, .6f));
            var grille = new Paint(new Color(.32f, .34f, .37f), Finish.Matte);
            var lamp = tail ? TailLamp : HeadLamp;
            Bogies(b, 2, .05f);
            b.Box(new Vector3(0, .135f, -.02f), new Vector3(.38f, .04f, .44f), Frame);
            b.Box(new Vector3(0, .15f, -.272f), new Vector3(.05f, .035f, .03f), Iron);
            Gangways(b, .31f, .25f, front: false);
            var stations = new List<float> { -.26f, -.24f, -.2f, -.16f, -.12f, -.08f, -.04f, 0 };
            foreach (float t in new[] { 0, .08f, .16f, .24f, .28f, .34f, .4f, .46f, .52f, .6f, .66f, .72f, .78f, .85f, .9f, .94f, .97f, .99f, 1 })
                stations.Add(.02f + .29f * t);
            b.Loft(BodyProfile, stations.ToArray(), BulletNose, (z, at) =>
            {
                float t = NoseT(z);
                var band = BandAt(at.y);
                if (In(t, .28f, .52f) && band >= Band.Windows) return Glass;
                if (In(t, .85f, .9f) && band == Band.Lower) return lamp;
                if (t > .72f && band <= Band.Belt) return livery;
                if (band == Band.Belt) return livery;
                if (band == Band.Skirt) return skirt;
                if (band == Band.Roof) return roof;
                if (band == Band.Windows && (In(z, -.2f, -.12f) || In(z, -.08f, 0))) return grille;
                return white;
            }, white, livery);
            Pantograph(b, -.15f, .47f, true);
            b.Box(new Vector3(0, .474f, -.02f), new Vector3(.16f, .016f, .1f), roof);
        }
    }
}
