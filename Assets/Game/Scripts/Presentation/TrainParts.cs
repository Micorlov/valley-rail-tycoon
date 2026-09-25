using System.Collections.Generic;
using UnityEngine;
namespace ValleyRail
{
    /// <summary>
    /// Running gear, fittings and the body profile shared by every train car.
    /// Car-local units: +z is forward, y = 0 is the rail centre line, and cars sit .56 apart.
    /// </summary>
    static class TrainParts
    {
        public const float RailTop = .025f, Floor = .17f, HalfWidth = .22f, HalfLength = .26f;
        public static readonly float[] Sides = { -1, 1 };
        public static readonly Quaternion AlongX = Quaternion.FromToRotation(Vector3.forward, Vector3.right);
        public static readonly Quaternion AlongY = Quaternion.FromToRotation(Vector3.forward, Vector3.up);
        public static readonly Paint Iron = new Paint(new Color(.15f, .15f, .16f), Finish.Metal);
        public static readonly Paint Steel = new Paint(new Color(.66f, .68f, .7f), Finish.Metal);
        public static readonly Paint Frame = new Paint(new Color(.2f, .2f, .21f), Finish.Matte);
        public static readonly Paint Rubber = new Paint(new Color(.11f, .11f, .12f), Finish.Matte);
        public static readonly Paint Glass = new Paint(new Color(.12f, .18f, .23f), Finish.Glass);
        public static readonly Paint HeadLamp = new Paint(new Color(1f, .96f, .78f), Finish.Glass);
        public static readonly Paint TailLamp = new Paint(new Color(.9f, .1f, .07f), Finish.Glass);
        public static readonly Paint Warning = new Paint(new Color(.97f, .78f, .1f));
        public static readonly Paint Insulator = new Paint(new Color(.86f, .85f, .8f));

        // Declared before BodyProfile: static fields initialise in source order.
        static readonly float[] WallBands = { -1, -.7f, -.5f, -.2f, -.02f, .48f, .55f };
        /// <summary>Unit cross-section of a car body: walls split at the livery bands, an arched roof and a flat floor.</summary>
        public static readonly Vector2[][] BodyProfile = MakeBodyProfile();
        /// <summary>A heap of bulk load, open at the bottom.</summary>
        public static readonly Vector2[][] MoundProfile =
        {
            new[] { new Vector2(-1, -1), new Vector2(-.82f, -.4f), new Vector2(-.55f, .3f), new Vector2(-.25f, .8f), new Vector2(0, .95f), new Vector2(.25f, .8f), new Vector2(.55f, .3f), new Vector2(.82f, -.4f), new Vector2(1, -1) }
        };

        static Vector2[][] MakeBodyProfile()
        {
            var shell = new List<Vector2>();
            foreach (float y in WallBands)
                shell.Add(new Vector2(-1, y));
            for (int degrees = 160; degrees >= 20; degrees -= 20)
            {
                float a = degrees * Mathf.Deg2Rad;
                shell.Add(new Vector2(Mathf.Cos(a), .55f + .45f * Mathf.Sin(a)));
            }
            for (int i = WallBands.Length - 1; i >= 0; i--)
                shell.Add(new Vector2(1, WallBands[i]));
            return new[] { shell.ToArray(), new[] { new Vector2(1, -1), new Vector2(-1, -1) } };
        }

        /// <summary>Which livery band of <see cref="BodyProfile"/> a unit height falls in.</summary>
        public enum Band { Skirt, Stripe, Lower, Belt, Windows, Cornice, Roof }
        public static Band BandAt(float y) =>
            y < -.7f ? Band.Skirt : y < -.5f ? Band.Stripe : y < -.2f ? Band.Lower : y < -.02f ? Band.Belt : y < .48f ? Band.Windows : y < .55f ? Band.Cornice : Band.Roof;
        public static bool In(float z, float from, float to) => z > from && z < to;

        /// <summary>Evenly spaced stations plus extra ones, sorted, for lofts whose bands need exact edges.</summary>
        public static float[] Stations(float from, float to, int steps, params float[] extra)
        {
            var list = new List<float>(extra);
            for (int i = 0; i <= steps; i++)
                list.Add(Mathf.Lerp(from, to, i / (float)steps));
            list.Sort();
            for (int i = list.Count - 1; i > 0; i--)
                if (list[i] - list[i - 1] < .001f)
                    list.RemoveAt(i);
            return list.ToArray();
        }

        public static Color Shade(Color color, float factor) => new Color(color.r * factor, color.g * factor, color.b * factor, 1);

        public static void Wheelset(TrainMeshBuilder b, float z, float radius)
        {
            float y = RailTop + radius;
            b.Cylinder(new Vector3(0, y, z), .014f, .38f, Iron, AlongX, 6);
            foreach (float side in Sides)
                b.Cylinder(new Vector3(side * .195f, y, z), radius, .03f, Iron, AlongX, 12);
        }

        /// <summary>A two- or three-axle bogie centred at <paramref name="z"/>: wheels, side frames, axle boxes and springs.</summary>
        public static void Bogie(TrainMeshBuilder b, float z, int axles, float radius)
        {
            float pitch = radius * 2 + .015f, span = pitch * (axles - 1), y = RailTop + radius;
            for (int i = 0; i < axles; i++)
                Wheelset(b, z - span * .5f + i * pitch, radius);
            foreach (float side in Sides)
            {
                b.Box(new Vector3(side * .222f, y + .005f, z), new Vector3(.022f, radius * 1.1f, span + radius * 1.3f), Frame);
                b.Box(new Vector3(side * .224f, y + radius * .7f + .008f, z), new Vector3(.028f, .022f, .05f), Steel);
                for (int i = 0; i < axles; i++)
                    b.Box(new Vector3(side * .235f, y, z - span * .5f + i * pitch), new Vector3(.012f, .03f, .034f), Iron);
            }
            b.Box(new Vector3(0, y + radius * .75f, z), new Vector3(.42f, .03f, .06f), Frame);
        }

        public static void Bogies(TrainMeshBuilder b, int axles, float radius, float centre = .165f)
        {
            Bogie(b, -centre, axles, radius);
            Bogie(b, centre, axles, radius);
        }

        public static void Underframe(TrainMeshBuilder b, Paint paint) =>
            b.Box(new Vector3(0, .1525f, 0), new Vector3(.40f, .035f, HalfLength * 2), paint);

        public static void Couplers(TrainMeshBuilder b)
        {
            foreach (float end in Sides)
                b.Box(new Vector3(0, .15f, end * (HalfLength + .012f)), new Vector3(.05f, .035f, .03f), Iron);
        }

        /// <summary>Rubber bellows that close the gap to the next coach.</summary>
        public static void Gangways(TrainMeshBuilder b, float y, float height, bool front = true, bool back = true)
        {
            foreach (float end in Sides)
                if (end > 0 ? front : back)
                    b.Box(new Vector3(0, y, end * (HalfLength + .01f)), new Vector3(.24f, height, .025f), Rubber);
        }

        /// <summary>A buffer beam with two sprung buffers, European style.</summary>
        public static void Buffers(TrainMeshBuilder b, float end, Paint beam)
        {
            float z = end * HalfLength;
            b.Box(new Vector3(0, .15f, z), new Vector3(.42f, .05f, .02f), beam);
            foreach (float side in Sides)
            {
                b.Cylinder(new Vector3(side * .13f, .15f, z + end * .012f), .011f, .018f, Iron, Quaternion.identity, 6);
                b.Cylinder(new Vector3(side * .13f, .15f, z + end * .023f), .022f, .005f, Steel, Quaternion.identity, 10);
            }
        }

        /// <summary>A thin bar between two points (not vertical).</summary>
        public static void Strut(TrainMeshBuilder b, Vector3 from, Vector3 to, float thickness, Paint paint)
        {
            var d = to - from;
            b.Box((from + to) * .5f, new Vector3(thickness, thickness, d.magnitude), paint, Quaternion.LookRotation(d));
        }

        /// <summary>A single-arm pantograph on insulators; lowered ones fold flat on the roof.</summary>
        public static void Pantograph(TrainMeshBuilder b, float z, float roof, bool raised)
        {
            b.Box(new Vector3(0, roof + .006f, z), new Vector3(.17f, .012f, .13f), Frame);
            foreach (float x in Sides)
                foreach (float dz in Sides)
                    b.Cylinder(new Vector3(x * .065f, roof + .02f, z + dz * .045f), .01f, .022f, Insulator, AlongY, 6);
            float lift = raised ? .1f : .022f;
            var hinge = new Vector3(0, roof + .034f, z - .05f);
            var knee = new Vector3(0, roof + .034f + lift * .55f, z + (raised ? .06f : .1f));
            var head = new Vector3(0, roof + .042f + lift, z - .02f);
            foreach (float x in Sides)
                Strut(b, hinge + new Vector3(x * .025f, 0, 0), knee, .01f, Steel);
            Strut(b, knee, head, .009f, Steel);
            b.Box(head + Vector3.up * .006f, new Vector3(.24f, .01f, .022f), Steel);
            foreach (float x in Sides)
                Strut(b, head + new Vector3(x * .12f, .006f, 0), head + new Vector3(x * .15f, -.012f, 0), .008f, Steel);
        }

        public static void Lamp(TrainMeshBuilder b, Vector3 at, float radius, Paint paint) =>
            b.Cylinder(at, radius, .012f, paint, Quaternion.identity, 8);

        /// <summary>A window pane standing proud of a flat face; <paramref name="size"/> is its extent in x, y and z.</summary>
        public static void Pane(TrainMeshBuilder b, Vector3 at, Vector3 size) => b.Box(at, size, Glass);
    }
}
