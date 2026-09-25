using System.Collections.Generic;
using UnityEngine;
namespace ValleyRail
{
    public enum VehicleKind { Car, Truck, Bus, Emergency, Service }

    /// <summary>
    /// One painted box of a road vehicle, in the vehicle frame: nose +z, centred on x and z, road surface at y 0.
    /// A beacon lens is painted dark in the body and lights up in <see cref="flash"/> on its <see cref="beat"/>.
    /// </summary>
    public readonly struct VehiclePart
    {
        public readonly Vector3 center, size;
        public readonly Color color, flash;
        /// <summary>The beat (0 or 1) a beacon lens lights on; -1 for plain paint.</summary>
        public readonly int beat;
        public VehiclePart(Vector3 center, Vector3 size, Color color) : this(center, size, color, color, -1) { }
        public VehiclePart(Vector3 center, Vector3 size, Color color, Color flash, int beat)
        {
            this.center = center;
            this.size = size;
            this.color = color;
            this.flash = flash;
            this.beat = beat;
        }
        public bool IsBeacon => beat >= 0;
    }

    /// <summary>A paint scheme: the main body colour and a second colour for stripes, cargo boxes or roofs.</summary>
    public readonly struct Livery
    {
        public readonly Color main, accent;
        public Livery(Color main, Color accent)
        {
            this.main = main;
            this.accent = accent;
        }
    }

    public sealed class VehicleModel
    {
        public readonly string name;
        public readonly VehicleKind kind;
        /// <summary>Nose to tail in cells.</summary>
        public readonly float length;
        public readonly Livery[] liveries;
        /// <summary>The flashing beacon lenses (the same in every livery); empty for everything but emergency vehicles.</summary>
        public readonly VehiclePart[] beacons;
        readonly System.Action<List<VehiclePart>, Livery> draw;
        public VehicleModel(string name, VehicleKind kind, float length, Livery[] liveries, System.Action<List<VehiclePart>, Livery> draw)
        {
            this.name = name;
            this.kind = kind;
            this.length = length;
            this.liveries = liveries;
            this.draw = draw;
            beacons = Parts(0).FindAll(part => part.IsBeacon).ToArray();
        }
        public List<VehiclePart> Parts(int livery)
        {
            var parts = new List<VehiclePart>(32);
            draw(parts, liveries[livery % liveries.Length]);
            return parts;
        }
    }

    /// <summary>
    /// The thirteen road vehicles of the town traffic: four cars, four trucks, two buses and three emergency vehicles
    /// (police car, ambulance, fire engine) with flashing beacons. Each is drawn from a handful of boxes so a whole vehicle
    /// merges into one small mesh. Cosmetic only; the simulation never sees them.
    /// </summary>
    public static class VehicleCatalog
    {
        // Colours first: the models below read them while this class initializes.
        static readonly Color Glass = new Color(.16f, .24f, .3f), Tyre = new Color(.1f, .1f, .11f), Chassis = new Color(.2f, .2f, .22f),
            HeadLamp = new Color(1f, .95f, .78f), TailLamp = new Color(.72f, .08f, .08f), SignGlow = new Color(1f, .78f, .3f),
            White = new Color(.93f, .93f, .91f), Silver = new Color(.7f, .72f, .75f), Charcoal = new Color(.22f, .24f, .28f),
            Red = new Color(.8f, .18f, .15f), Blue = new Color(.2f, .45f, .78f), Green = new Color(.24f, .42f, .32f),
            Yellow = new Color(.98f, .78f, .12f), Orange = new Color(.93f, .5f, .16f), Cream = new Color(.94f, .9f, .77f),
            Steel = new Color(.78f, .8f, .82f), Timber = new Color(.62f, .44f, .26f), BusRed = new Color(.78f, .1f, .1f),
            PoliceBlue = new Color(.13f, .27f, .62f), Navy = new Color(.1f, .14f, .3f), FireRed = new Color(.84f, .09f, .07f),
            Lime = new Color(.8f, .88f, .22f), MedicGreen = new Color(.14f, .5f, .28f),
            // Beacon lenses: dark in the body, bright while lit.
            LensRed = new Color(.4f, .07f, .07f), LensBlue = new Color(.1f, .16f, .4f), LensAmber = new Color(.42f, .27f, .05f),
            BeaconRed = new Color(1f, .14f, .1f), BeaconBlue = new Color(.28f, .56f, 1f), BeaconAmber = new Color(1f, .7f, .1f);

        static readonly Livery[] CarPaints =
        {
            new Livery(White, Charcoal), new Livery(Silver, Charcoal), new Livery(Charcoal, Silver), new Livery(Red, Charcoal),
            new Livery(Blue, Charcoal), new Livery(Green, Cream),
        };

        public static readonly VehicleModel[] Models =
        {
            new VehicleModel("Hatchback", VehicleKind.Car, .30f, CarPaints, Hatchback),
            new VehicleModel("Saloon", VehicleKind.Car, .36f, CarPaints, Saloon),
            new VehicleModel("SUV", VehicleKind.Car, .39f, CarPaints, Suv),
            new VehicleModel("Taxi", VehicleKind.Car, .36f, new[] { new Livery(Yellow, Charcoal), new Livery(Charcoal, Yellow) }, Taxi),
            new VehicleModel("Delivery van", VehicleKind.Truck, .40f,
                new[] { new Livery(White, Blue), new Livery(Red, Yellow), new Livery(Silver, Orange), new Livery(White, Green) }, Van),
            new VehicleModel("Pickup truck", VehicleKind.Truck, .40f, CarPaints, Pickup),
            new VehicleModel("Box truck", VehicleKind.Truck, .56f,
                new[] { new Livery(Blue, White), new Livery(Red, Cream), new Livery(Green, White), new Livery(Orange, White) }, BoxTruck),
            new VehicleModel("Tanker truck", VehicleKind.Truck, .58f,
                new[] { new Livery(Red, Steel), new Livery(White, Steel), new Livery(Blue, Steel) }, Tanker),
            new VehicleModel("City bus", VehicleKind.Bus, .66f,
                new[] { new Livery(Blue, Cream), new Livery(Green, Cream), new Livery(Orange, White) }, CityBus),
            new VehicleModel("Double-decker bus", VehicleKind.Bus, .62f, new[] { new Livery(BusRed, Cream), new Livery(Blue, Cream) }, DoubleDecker),
            new VehicleModel("Police car", VehicleKind.Emergency, .36f, new[] { new Livery(White, PoliceBlue), new Livery(Navy, White) }, PoliceCar),
            new VehicleModel("Ambulance", VehicleKind.Emergency, .42f, new[] { new Livery(White, Red), new Livery(Lime, MedicGreen) }, Ambulance),
            new VehicleModel("Fire engine", VehicleKind.Emergency, .6f, new[] { new Livery(FireRed, White), new Livery(FireRed, Silver) }, FireEngine),
        };
        public static int Count => Models.Length;

        /// <summary>
        /// The flatbed tow truck that clears broken-down cars off the highways (CityTraffic.Incidents). Not a traffic model:
        /// it never spawns in the everyday mix, and its amber beacons are the breakdown's to flash, with no siren.
        /// </summary>
        public static readonly VehicleModel TowTruck = new VehicleModel("Tow truck", VehicleKind.Service, .62f,
            new[] { new Livery(Orange, Steel), new Livery(Yellow, Charcoal), new Livery(White, Steel), new Livery(Red, Silver) }, Tow);
        /// <summary>The top of the tow truck's flatbed, and the middle of the car it carries, in the vehicle frame.</summary>
        public const float TowBedTop = .145f, TowBedMiddle = -.085f;

        /// <summary>
        /// The order new traffic is spawned in, repeated: mostly cars, a steady share of trucks and buses, and a police car,
        /// ambulance or fire engine now and then. Every model shows up within the first thirteen, so the first town already
        /// has nearly one of each, and a police car is the second vehicle, so even the smallest town has one.
        /// </summary>
        public static readonly int[] Mix = { 0, 10, 1, 8, 2, 11, 4, 3, 12, 6, 7, 5, 9, 0, 1, 2, 0, 4, 10, 3, 8, 1, 5, 0, 2, 3, 0, 1 };

        static void Box(List<VehiclePart> parts, float x, float y, float z, float width, float height, float length, Color color) =>
            parts.Add(new VehiclePart(new Vector3(x, y, z), new Vector3(width, height, length), color));
        /// <summary>A left and right wheel standing on the road at <paramref name="z"/>.</summary>
        static void Axle(List<VehiclePart> parts, float z, float track, float radius, float width)
        {
            for (int side = -1; side <= 1; side += 2)
                Box(parts, side * track, radius, z, width, radius * 2, radius * 2, Tyre);
        }
        /// <summary>A beacon lens, painted dark, that flashes red or blue on beat 0 or 1.</summary>
        static void Beacon(List<VehiclePart> parts, float x, float y, float z, float width, float height, float length, bool red, int beat) =>
            parts.Add(new VehiclePart(new Vector3(x, y, z), new Vector3(width, height, length), red ? LensRed : LensBlue, red ? BeaconRed : BeaconBlue, beat));
        /// <summary>An amber beacon lens, painted dark, that flashes on beat 0 or 1.</summary>
        static void AmberBeacon(List<VehiclePart> parts, float x, float y, float z, float width, float height, float length, int beat) =>
            parts.Add(new VehiclePart(new Vector3(x, y, z), new Vector3(width, height, length), LensAmber, BeaconAmber, beat));
        /// <summary>Head lamps on the nose and tail lamps on the back, <paramref name="spread"/> either side of the middle.</summary>
        static void Lamps(List<VehiclePart> parts, float nose, float tail, float spread, float height)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Box(parts, side * spread, height, nose + .003f, .04f, .022f, .006f, HeadLamp);
                Box(parts, side * spread, height, tail - .003f, .04f, .022f, .006f, TailLamp);
            }
        }

        // Cars: a painted body, a glass cabin under a painted roof. They differ in stance and where the cabin sits.
        static void Hatchback(List<VehiclePart> p, Livery l)
        {
            Box(p, 0, .085f, 0, .18f, .08f, .30f, l.main);
            Box(p, 0, .16f, -.025f, .16f, .07f, .19f, Glass);
            Box(p, 0, .2f, -.03f, .165f, .016f, .17f, l.main);
            Axle(p, .095f, .085f, .042f, .036f);
            Axle(p, -.095f, .085f, .042f, .036f);
            Lamps(p, .15f, -.15f, .055f, .1f);
        }
        static void Saloon(List<VehiclePart> p, Livery l)
        {
            Box(p, 0, .085f, 0, .19f, .075f, .36f, l.main);
            Box(p, 0, .158f, -.01f, .16f, .07f, .17f, Glass);
            Box(p, 0, .198f, -.015f, .165f, .016f, .13f, l.main);
            Axle(p, .115f, .09f, .042f, .036f);
            Axle(p, -.115f, .09f, .042f, .036f);
            Lamps(p, .18f, -.18f, .058f, .1f);
        }
        static void Suv(List<VehiclePart> p, Livery l)
        {
            Box(p, 0, .105f, 0, .2f, .11f, .37f, l.main);
            Box(p, 0, .2f, -.03f, .18f, .08f, .26f, Glass);
            Box(p, 0, .25f, -.03f, .185f, .02f, .25f, l.main);
            for (int side = -1; side <= 1; side += 2)
                Box(p, side * .07f, .265f, -.03f, .012f, .01f, .2f, Chassis);
            Box(p, 0, .13f, -.192f, .09f, .09f, .02f, Tyre);
            Axle(p, .12f, .095f, .05f, .04f);
            Axle(p, -.12f, .095f, .05f, .04f);
            Lamps(p, .185f, -.185f, .062f, .13f);
        }
        static void Taxi(List<VehiclePart> p, Livery l)
        {
            Saloon(p, l);
            Box(p, 0, .1f, 0, .194f, .018f, .3f, l.accent);
            Box(p, 0, .218f, -.015f, .075f, .024f, .03f, SignGlow);
        }

        // Trucks: a cab up front and a working back.
        static void Van(List<VehiclePart> p, Livery l)
        {
            Box(p, 0, .145f, -.03f, .2f, .2f, .34f, l.main);
            Box(p, 0, .1f, .17f, .2f, .11f, .06f, l.main);
            Box(p, 0, .195f, .142f, .18f, .08f, .006f, Glass);
            Box(p, 0, .195f, .11f, .204f, .06f, .05f, Glass);
            Box(p, 0, .13f, -.05f, .204f, .03f, .28f, l.accent);
            Axle(p, .12f, .092f, .045f, .038f);
            Axle(p, -.13f, .092f, .045f, .038f);
            Lamps(p, .2f, -.2f, .065f, .11f);
        }
        static void Pickup(List<VehiclePart> p, Livery l)
        {
            Box(p, 0, .095f, 0, .2f, .09f, .4f, l.main);
            Box(p, 0, .18f, .05f, .17f, .08f, .13f, Glass);
            Box(p, 0, .226f, .05f, .18f, .018f, .14f, l.main);
            for (int side = -1; side <= 1; side += 2)
                Box(p, side * .093f, .165f, -.105f, .014f, .05f, .19f, l.main);
            Box(p, 0, .165f, -.193f, .2f, .05f, .014f, l.main);
            Box(p, 0, .17f, -.11f, .1f, .06f, .09f, Timber);
            Axle(p, .13f, .095f, .05f, .04f);
            Axle(p, -.13f, .095f, .05f, .04f);
            Lamps(p, .2f, -.2f, .065f, .11f);
        }
        /// <summary>The cab, chassis and wheels shared by both lorries, the cab's front face at <paramref name="nose"/>.</summary>
        static void LorryCab(List<VehiclePart> p, Livery l, float nose, float tail)
        {
            Box(p, 0, .075f, (nose + tail) / 2 - .01f, .16f, .04f, nose - tail - .04f, Chassis);
            Box(p, 0, .17f, nose - .075f, .22f, .2f, .15f, l.main);
            Box(p, 0, .215f, nose + .001f, .19f, .07f, .006f, Glass);
            Box(p, 0, .215f, nose - .055f, .224f, .06f, .08f, Glass);
            Box(p, 0, .085f, nose + .002f, .22f, .03f, .01f, Chassis);
            Axle(p, nose - .08f, .1f, .055f, .05f);
            Axle(p, tail + .1f, .1f, .055f, .05f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(p, side * .07f, .11f, nose + .003f, .04f, .022f, .006f, HeadLamp);
                Box(p, side * .08f, .12f, tail - .003f, .04f, .022f, .006f, TailLamp);
            }
        }
        static void BoxTruck(List<VehiclePart> p, Livery l)
        {
            LorryCab(p, l, .28f, -.28f);
            Box(p, 0, .215f, -.075f, .23f, .27f, .41f, l.accent);
            Box(p, 0, .3f, -.075f, .234f, .04f, .39f, l.main);
        }
        static void Tanker(List<VehiclePart> p, Livery l)
        {
            LorryCab(p, l, .29f, -.29f);
            Box(p, 0, .19f, -.085f, .2f, .17f, .41f, l.accent);
            Box(p, 0, .285f, -.085f, .14f, .025f, .39f, l.accent);
            Box(p, 0, .19f, -.085f, .204f, .035f, .39f, l.main);
            Box(p, 0, .302f, -.005f, .05f, .012f, .05f, Chassis);
            Box(p, 0, .302f, -.165f, .05f, .012f, .05f, Chassis);
        }

        /// <summary>
        /// A lorry cab with a flat steel bed behind it (its top at TowBedTop) long enough for a car, side rails, the winch at
        /// the bed's head and a bar of two amber beacons on the cab roof.
        /// </summary>
        static void Tow(List<VehiclePart> p, Livery l)
        {
            LorryCab(p, l, .31f, -.31f);
            Box(p, 0, TowBedTop - .02f, TowBedMiddle, .23f, .04f, .44f, l.accent);
            for (int side = -1; side <= 1; side += 2)
                Box(p, side * .11f, TowBedTop + .007f, TowBedMiddle, .012f, .014f, .44f, l.main);
            Box(p, 0, TowBedTop + .045f, .145f, .16f, .09f, .03f, Chassis);
            Box(p, 0, .09f, -.3f, .2f, .03f, .02f, Chassis);
            Box(p, 0, .28f, .235f, .17f, .02f, .04f, Chassis);
            AmberBeacon(p, -.055f, .302f, .235f, .05f, .026f, .04f, 0);
            AmberBeacon(p, .055f, .302f, .235f, .05f, .026f, .04f, 1);
        }

        // Buses: long bodies with a window band. Traffic drives on the right, so the doors face the kerb on +x.
        static void CityBus(List<VehiclePart> p, Livery l)
        {
            Box(p, 0, .18f, 0, .23f, .26f, .66f, l.main);
            Box(p, 0, .215f, -.01f, .234f, .085f, .56f, Glass);
            Box(p, 0, .2f, .331f, .2f, .14f, .006f, Glass);
            Box(p, 0, .288f, .332f, .15f, .03f, .006f, SignGlow);
            Box(p, 0, .316f, 0, .21f, .014f, .6f, l.accent);
            Box(p, 0, .08f, 0, .234f, .03f, .66f, l.accent);
            Box(p, .116f, .155f, .25f, .004f, .18f, .06f, Glass);
            Box(p, .116f, .155f, -.02f, .004f, .18f, .06f, Glass);
            Axle(p, .21f, .1f, .055f, .05f);
            Axle(p, -.2f, .1f, .055f, .05f);
            Lamps(p, .33f, -.33f, .08f, .1f);
        }
        static void DoubleDecker(List<VehiclePart> p, Livery l)
        {
            Box(p, 0, .28f, 0, .23f, .46f, .62f, l.main);
            Box(p, 0, .2f, -.01f, .234f, .08f, .52f, Glass);
            Box(p, 0, .39f, 0, .234f, .08f, .58f, Glass);
            Box(p, 0, .19f, .311f, .19f, .11f, .006f, Glass);
            Box(p, 0, .39f, .311f, .2f, .08f, .006f, Glass);
            Box(p, 0, .3f, 0, .234f, .03f, .624f, l.accent);
            Box(p, 0, .465f, .312f, .14f, .03f, .006f, SignGlow);
            Box(p, 0, .516f, 0, .21f, .014f, .58f, l.accent);
            Box(p, .116f, .15f, .24f, .004f, .17f, .06f, Glass);
            Axle(p, .2f, .1f, .055f, .05f);
            Axle(p, -.19f, .1f, .055f, .05f);
            Lamps(p, .31f, -.31f, .08f, .1f);
        }

        // Emergency vehicles: beacons on the roof flash left and right in turn (see EmergencyLights).
        static void PoliceCar(List<VehiclePart> p, Livery l)
        {
            Saloon(p, l);
            Box(p, 0, .1f, 0, .194f, .02f, .3f, l.accent);
            Box(p, 0, .128f, .148f, .186f, .008f, .06f, l.accent);
            Beacon(p, -.042f, .222f, -.015f, .075f, .032f, .045f, true, 0);
            Beacon(p, .042f, .222f, -.015f, .075f, .032f, .045f, false, 1);
        }
        static void Ambulance(List<VehiclePart> p, Livery l)
        {
            Box(p, 0, .1f, .18f, .2f, .11f, .06f, l.main);
            Box(p, 0, .145f, .095f, .2f, .2f, .11f, l.main);
            Box(p, 0, .2f, .152f, .18f, .07f, .006f, Glass);
            Box(p, 0, .2f, .105f, .204f, .06f, .07f, Glass);
            Box(p, 0, .12f, .12f, .204f, .03f, .17f, l.accent);
            Box(p, 0, .17f, -.08f, .22f, .25f, .26f, l.main);
            Box(p, 0, .2f, -.08f, .224f, .04f, .26f, l.accent);
            Box(p, 0, .22f, -.211f, .12f, .06f, .006f, Glass);
            Axle(p, .13f, .095f, .045f, .038f);
            Axle(p, -.14f, .1f, .045f, .038f);
            Lamps(p, .21f, -.21f, .065f, .11f);
            Beacon(p, -.075f, .31f, .03f, .05f, .03f, .04f, false, 0);
            Beacon(p, .075f, .31f, .03f, .05f, .03f, .04f, false, 1);
            Beacon(p, -.075f, .31f, -.19f, .05f, .03f, .04f, false, 1);
            Beacon(p, .075f, .31f, -.19f, .05f, .03f, .04f, false, 0);
        }
        static void FireEngine(List<VehiclePart> p, Livery l)
        {
            LorryCab(p, l, .3f, -.3f);
            Box(p, 0, .165f, -.075f, .23f, .21f, .43f, l.main);
            Box(p, 0, .12f, -.075f, .234f, .03f, .41f, l.accent);
            // The ladder on the roof, on a turntable at the back.
            Box(p, 0, .276f, -.22f, .09f, .012f, .07f, Chassis);
            for (int side = -1; side <= 1; side += 2)
                Box(p, side * .045f, .289f, -.05f, .014f, .016f, .48f, l.accent);
            for (int rung = 0; rung < 5; rung++)
                Box(p, 0, .289f, -.25f + rung * .1f, .08f, .01f, .012f, l.accent);
            Beacon(p, -.06f, .285f, .24f, .05f, .03f, .04f, false, 0);
            Beacon(p, .06f, .285f, .24f, .05f, .03f, .04f, false, 1);
            Beacon(p, -.085f, .285f, -.27f, .04f, .03f, .04f, false, 1);
            Beacon(p, .085f, .285f, -.27f, .04f, .03f, .04f, false, 0);
        }
    }
}
