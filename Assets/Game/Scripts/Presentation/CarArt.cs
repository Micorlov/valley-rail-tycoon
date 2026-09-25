using UnityEngine;
namespace ValleyRail
{
    /// <summary>
    /// Parked cars for car parks, police yards and driveways, drawn like the moving traffic cars: a painted body on four
    /// wheels, a glass cabin under a painted roof, and head and tail lamps.
    /// </summary>
    public sealed partial class WorldView
    {
        const float TrafficCarLength = .34f;
        static readonly Color CarGlass = new Color(.16f, .24f, .3f), Tyre = new Color(.1f, .1f, .11f), HeadLamp = new Color(1f, .95f, .78f),
            TailLamp = new Color(.72f, .08f, .08f), BeaconBlue = new Color(.3f, .6f, 1f);
        // An instance field: it reads colours declared in other partial files, which static initializers may not see yet.
        // Mostly white, silver and dark cars, like a real car park, with a few bright ones.
        readonly Color[] CarPaint =
        {
            new Color(.93f, .93f, .91f), new Color(.7f, .72f, .75f), new Color(.22f, .24f, .28f), Red,
            new Color(.2f, .45f, .78f), new Color(.24f, .42f, .32f), Gold,
        };
        /// <summary>
        /// A parked car standing at <paramref name="ground"/> in the lot frame, <paramref name="length"/> long. The heading turns it
        /// in 90° steps from nose-to-street (+z) so every box stays axis-aligned. Body, cabin and roof join the always-drawn city
        /// mesh unless <paramref name="closeUpOnly"/>; wheels and lamps only show zoomed in. Returns the height of the roof top.
        /// </summary>
        float ParkedCar(Lot lot, Vector3 ground, float length, Color paint, int heading = 0, bool patrol = false, bool closeUpOnly = false)
        {
            // Parts are given for a traffic-sized car (nose +z, .34 long, standing at y 0), then turned and scaled into place.
            var turn = Quaternion.Euler(0, heading, 0);
            bool across = heading / 90 % 2 != 0;
            float scale = length / TrafficCarLength;
            void Piece(string name, Vector3 local, Vector3 size, Color color, bool closeUp)
            {
                var box = across ? new Vector3(size.z, size.y, size.x) : size;
                Box(name, lot.at + lot.turn * (ground + turn * local * scale), box * scale, color, closeUp || closeUpOnly ? cityDetailRoot : cityRoot, lot.turn);
            }
            Piece("Car body", new Vector3(0, .07f, 0), new Vector3(.18f, .075f, .34f), paint, false);
            Piece("Car windows", new Vector3(0, .135f, -.015f), new Vector3(.15f, .055f, .17f), CarGlass, false);
            Piece("Car roof", new Vector3(0, .17f, -.02f), new Vector3(.158f, .016f, .12f), paint, false);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int end = -1; end <= 1; end += 2)
                    Piece("Wheel", new Vector3(side * .08f, .036f, end * .1f), new Vector3(.034f, .072f, .072f), Tyre, true);
                Piece("Headlamp", new Vector3(side * .056f, .085f, .171f), new Vector3(.04f, .022f, .006f), HeadLamp, true);
                Piece("Tail lamp", new Vector3(side * .056f, .085f, -.171f), new Vector3(.04f, .022f, .006f), TailLamp, true);
            }
            float top = .178f;
            if (patrol)
            {
                Piece("Patrol stripe", new Vector3(0, .075f, 0), new Vector3(.184f, .026f, .3f), PoliceBlue, false);
                Piece("Light bar", new Vector3(-.028f, .187f, -.02f), new Vector3(.052f, .018f, .034f), Red, false);
                Piece("Light bar", new Vector3(.028f, .187f, -.02f), new Vector3(.052f, .018f, .034f), BeaconBlue, false);
                top = .196f;
            }
            return ground.y + top * scale;
        }
    }
}
