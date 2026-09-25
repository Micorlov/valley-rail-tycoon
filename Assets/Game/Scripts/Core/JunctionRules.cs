namespace ValleyRail.Core
{
    /// <summary>How the cosmetic road traffic takes turns at a junction.</summary>
    public enum JunctionKind { GiveWay, Lights, Roundabout }

    /// <summary>What a traffic light shows to one road through its junction.</summary>
    public enum SignalLight { Red, Amber, Green }

    /// <summary>
    /// One junction's traffic lights: the road whose turn it is (0 north-south, 1 east-west), what that road sees, and
    /// for how many traffic seconds. While the road's light is Red it is the all-red moment before the other road's green.
    /// </summary>
    public struct SignalPhase
    {
        public int road;
        public SignalLight light;
        public float time;
    }

    /// <summary>
    /// Which road junctions get traffic lights or a roundabout, and how the lights change. Every choice follows from the
    /// cell alone, so the traffic, the street art and the tests agree and it survives saving and loading. The lights are
    /// vehicle-actuated, like real ones: they rest on green for the road that last had it, and change only for a car
    /// waiting on the other road, after a shortest green, or once a busy road has had its longest green.
    /// </summary>
    public static class JunctionRules
    {
        /// <summary>Traffic seconds: the shortest and longest green, the amber, and the all-red moment before the other road goes.</summary>
        public const float MinGreen = 4f, MaxGreen = 10f, Amber = 1.6f, AllRed = 1.2f;

        /// <summary>
        /// The kind of a junction with <paramref name="exits"/> (bit d: a road leaves in direction d). Only junctions of three
        /// or four roads are controlled. In town, two in five get lights and one in five a roundabout; where highways meet
        /// out of town, half get a roundabout and half lights. A town's plaza keeps its fountain.
        /// </summary>
        public static JunctionKind Kind(Cell c, int exits, bool town, bool plaza)
        {
            if (plaza || Directions.Count(exits) < 3)
                return JunctionKind.GiveWay;
            int pick = Hash(c) % 10;
            if (!town)
                return pick < 5 ? JunctionKind.Roundabout : JunctionKind.Lights;
            return pick < 4 ? JunctionKind.Lights : pick < 6 ? JunctionKind.Roundabout : JunctionKind.GiveWay;
        }

        /// <summary>A stable hash of a cell, so neighbouring junctions differ.</summary>
        public static int Hash(Cell c) => (int)(((uint)(c.x * 92837111) ^ (uint)(c.z * 689287499)) >> 3 & 0x7fffffff);

        /// <summary>The lights of a junction when the traffic starts: green for one of its roads, picked by the cell.</summary>
        public static SignalPhase Start(Cell c) => new SignalPhase { road = Hash(c) & 1, light = SignalLight.Green };

        /// <summary>
        /// The lights <paramref name="seconds"/> later, given whether a car waits to drive in along the road that has its turn
        /// (<paramref name="waitingHere"/>) and along the other road (<paramref name="waitingThere"/>).
        /// </summary>
        public static SignalPhase Advance(SignalPhase phase, float seconds, bool waitingHere, bool waitingThere)
        {
            phase.time += seconds;
            switch (phase.light)
            {
                case SignalLight.Green:
                    if (waitingThere && phase.time >= MinGreen && (!waitingHere || phase.time >= MaxGreen))
                        return new SignalPhase { road = phase.road, light = SignalLight.Amber };
                    break;
                case SignalLight.Amber:
                    if (phase.time >= Amber)
                        return new SignalPhase { road = phase.road, light = SignalLight.Red };
                    break;
                default:
                    if (phase.time >= AllRed)
                        return new SignalPhase { road = 1 - phase.road, light = SignalLight.Green };
                    break;
            }
            return phase;
        }

        /// <summary>What traffic coming along <paramref name="road"/> sees: its road's light while it has the turn, else red.</summary>
        public static SignalLight Shows(SignalPhase phase, int road) => (road & 1) == phase.road ? phase.light : SignalLight.Red;

        /// <summary>A car coming along <paramref name="road"/> may drive into the junction: its light is green.</summary>
        public static bool Go(SignalPhase phase, int road) => Shows(phase, road) == SignalLight.Green;
    }
}
