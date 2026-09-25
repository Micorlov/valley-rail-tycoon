using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Shared measurements of a beach car park and its stretch of sand, in world units. <c>CoastView</c> draws from them and
    /// <see cref="BeachLife"/> moves cars and people on them, so parked cars sit in the painted bays and sunbathers lie on
    /// the towels. Everything follows from the car park's entrance cell (the beach road's last cell), with the sea to the east.
    /// </summary>
    public static class BeachLayout
    {
        public struct Bay
        {
            public Vector3 at, facing;
        }
        /// <summary>The car park surface's top, a touch above the street deck so the painted lines never flicker.</summary>
        public const float Deck = .03f;
        /// <summary>The car park's inland and seaward edges, the middle of its aisle, and the waterline at the map edge.</summary>
        public const float West = Coast.ParkX - .5f, East = Coast.SandFrom - .5f, Aisle = (West + East) / 2, Waterline = MapDefinition.Size - .5f;
        /// <summary>Bays along the coast: width, depth from the edge, and how far the painted rows keep from the car park's ends.</summary>
        public const float BayPitch = .46f, BayDepth = .56f, EndMargin = .16f;
        public const int BaysPerRow = 8;
        /// <summary>The boardwalk from the car park onto the sand, and how far along the coast people stroll either side of it.</summary>
        public const float WalkwayEnd = East + .9f, StrollSouth = 8, StrollNorth = 9, SandWest = East + .35f, SandEast = Waterline - .18f;
        /// <summary>Umbrellas with towels further along the sand, away from the loungers.</summary>
        public const int Umbrellas = 4;
        /// <summary>The lounger rows beside the kiosk: two columns of umbrellas, each with a lounger either side.</summary>
        public const int ClubRows = 4;
        public const float ClubStart = 2.7f, ClubPitch = 1f, LoungerGap = .2f, LoungerTop = .07f;
        static readonly float[] ClubColumns = { East + 1f, East + 1.85f };
        /// <summary>The walkway between the two lounger columns.</summary>
        public const float ClubAisle = East + 1.425f;
        /// <summary>How far the shallow water with its waves reaches past the waterline. There is no open sea beyond it.</summary>
        public const float WaterWidth = 2.4f;
        /// <summary>The strip along the water's edge that strollers keep to, clear of the loungers.</summary>
        public const float ShoreWest = East + 2.3f;

        public static float South(Cell entrance) => entrance.z - Coast.ParkBehind - .5f;
        public static float North(Cell entrance) => South(entrance) + Coast.ParkLength;
        public static float Middle(Cell entrance) => South(entrance) + Coast.ParkLength / 2f;
        /// <summary>The row the boardwalk runs along: between two east bays, straight out from the aisle.</summary>
        public static float WalkRow(Cell entrance) => Middle(entrance);
        /// <summary>The stretch of beach visitors wander over, kept inside the map.</summary>
        public static float StrollFrom(Cell entrance) => Mathf.Max(.8f, entrance.z - StrollSouth);
        public static float StrollTo(Cell entrance) => Mathf.Min(MapDefinition.Size - 1.8f, entrance.z + StrollNorth);

        /// <summary>
        /// Two facing rows of bays either side of the aisle, noses out towards the car park's edges. The inland row leaves a
        /// gap where the road comes in.
        /// </summary>
        public static List<Bay> Bays(Cell entrance)
        {
            var bays = new List<Bay>(2 * BaysPerRow);
            float south = South(entrance) + EndMargin;
            for (int k = 0; k < BaysPerRow; k++)
            {
                float z = south + BayPitch * (k + .5f);
                if (Mathf.Abs(z - entrance.z) > .45f)
                    bays.Add(new Bay { at = new Vector3(West + BayDepth / 2, Deck, z), facing = Vector3.left });
                bays.Add(new Bay { at = new Vector3(East - BayDepth / 2, Deck, z), facing = Vector3.right });
            }
            return bays;
        }
        /// <summary>The painted line across the lot between bays k-1 and k (k = 0 .. BaysPerRow).</summary>
        public static float BayLine(Cell entrance, int k) => South(entrance) + EndMargin + BayPitch * k;

        public static Vector3 Kiosk(Cell entrance) => new Vector3(East + .6f, 0, WalkRow(entrance) + 1.3f);
        public static Vector3 Lifeguard(Cell entrance) => new Vector3(Waterline - 1.05f, 0, Middle(entrance) - 2.6f);

        /// <summary>Umbrellas in neat rows north of the kiosk, each shading two sun loungers. Rows that would leave the map are dropped.</summary>
        public static List<Vector3> ClubUmbrellas(Cell entrance)
        {
            var spots = new List<Vector3>(ClubRows * ClubColumns.Length);
            for (int row = 0; row < ClubRows; row++)
            {
                float z = WalkRow(entrance) + ClubStart + row * ClubPitch;
                if (z + LoungerGap + .2f > MapDefinition.Size - .5f)
                    break;
                foreach (float x in ClubColumns)
                    spots.Add(new Vector3(x, 0, z));
            }
            return spots;
        }
        /// <summary>Every sun lounger: the middle of its bed, one either side of each club umbrella, lying along the beach's width.</summary>
        public static List<Vector3> Loungers(Cell entrance)
        {
            var loungers = new List<Vector3>();
            foreach (var umbrella in ClubUmbrellas(entrance))
                for (int side = -1; side <= 1; side += 2)
                    loungers.Add(umbrella + new Vector3(0, 0, side * LoungerGap));
            return loungers;
        }
        /// <summary>Where someone lies on a lounger: on the cushion, head on the raised back rest (the landward end).</summary>
        public static Vector3 OnLounger(Vector3 lounger) => lounger + new Vector3(.13f, LoungerTop + .03f, 0);
        public static readonly Quaternion LyingOnLounger = Quaternion.Euler(90, -90, 0);
        /// <summary>Where someone stands to get onto a lounger: at its foot, on the sea side.</summary>
        public static Vector3 BesideLounger(Vector3 lounger) => lounger + new Vector3(.27f, 0, 0);
        /// <summary>
        /// Umbrellas with towels on the sand south of the boardwalk, seeded by the car park's row so a beach keeps its layout
        /// between redraws. They keep clear of the boardwalk, the lifeguard tower, the shore strip and each other.
        /// </summary>
        public static List<Vector3> UmbrellaSpots(Cell entrance)
        {
            var spots = new List<Vector3>(Umbrellas);
            var random = new System.Random(entrance.z * 7919 + 131);
            float from = StrollFrom(entrance) + .6f, to = WalkRow(entrance) - .9f;
            for (int tries = 0; tries < 400 && spots.Count < Umbrellas; tries++)
            {
                var at = new Vector3(Mathf.Lerp(East + .75f, ShoreWest - .3f, (float)random.NextDouble()), 0, Mathf.Lerp(from, to, (float)random.NextDouble()));
                if (Mathf.Abs(at.z - WalkRow(entrance)) < .7f || Flat(at - Kiosk(entrance)) < .8f || Flat(at - Lifeguard(entrance)) < .8f)
                    continue;
                bool crowded = false;
                foreach (var other in spots)
                    crowded |= Flat(at - other) < 1.1f;
                if (!crowded)
                    spots.Add(at);
            }
            return spots;
        }
        /// <summary>Where the towel lies beside an umbrella, and the sunbather on it (lying along the coast).</summary>
        public static Vector3 Towel(Vector3 umbrella) => umbrella + new Vector3(.2f, 0, -.06f);
        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
