using System.Collections.Generic;
using UnityEngine;
namespace ValleyRail
{
    /// <summary>
    /// Measurements of a drive-in cinema, shared by its art and its life. The screen stands at the back (-z) facing the
    /// entrance (+z). Parking bays sit on arcs round a point behind the screen, so every car looks straight at it, each
    /// bay on a low ramp. The snack bar, with the projection booth on its roof, stands in the middle of the back rows.
    /// Cars come in past the ticket booth on the right lane and leave by the left one.
    /// Frame: origin at the centre of the lot on the ground, one unit per cell; a 5×5 lot is the design size. Plain numbers
    /// only (no engine calls), so the car logic built on it runs headless.
    /// </summary>
    public sealed class DriveInLayout
    {
        public readonly struct Bay
        {
            public readonly int row;
            /// <summary>Heading of a car parked here, in degrees round y: straight at the screen.</summary>
            public readonly float angle, radius, yaw;
            public readonly Vector3 at;
            public Bay(int row, float angle, float radius, Vector3 at, float yaw)
            {
                this.row = row;
                this.angle = angle;
                this.radius = radius;
                this.at = at;
                this.yaw = yaw;
            }
            public Quaternion Facing => Quaternion.Euler(0, yaw, 0);
        }
        /// <summary>Top of the gravel, where the cars stand.</summary>
        public const float Deck = .026f;
        /// <summary>Along each arc, one bay per this much.</summary>
        public const float BayPitch = .3f;
        /// <summary>How much a parked car's nose is lifted by its ramp, in degrees.</summary>
        public const float RampTilt = 4;
        const float MaxAngle = 35 * Mathf.Deg2Rad, MaxTurnIn = 50 * Mathf.Deg2Rad, AisleOffset = .31f;
        public readonly int size;
        public readonly float half, scale;
        /// <summary>The centre of the row arcs, behind the screen.</summary>
        public readonly Vector3 focus;
        public readonly float screenWidth, screenHeight, screenBottom, screenZ;
        public readonly float[] rows;
        public readonly List<Bay> bays = new List<Bay>();
        /// <summary>The snack bar's footprint centre and size; the projection booth on top looks out through <see cref="projector"/>.</summary>
        public readonly Vector3 snackBar, snackBarSize, projector;
        public readonly float laneX;
        /// <summary>Street ends of the ways in and out, where arriving cars stop to pay, the ticket booth and the sign.</summary>
        public readonly Vector3 gateIn, gateOut, ticketStop, ticketBooth, sign;
        // Leaving cars keep this far from the screen before they turn off the front aisle.
        readonly float frontAisleLimit;

        public DriveInLayout(int size)
        {
            this.size = size;
            half = size / 2f;
            scale = size / 5f;
            focus = new Vector3(0, 0, -3.6f * scale);
            screenZ = -2.05f * scale;
            screenWidth = 3f * scale;
            screenHeight = screenWidth * 9 / 16;
            screenBottom = .5f * scale;
            rows = new[] { 2.4f * scale, 3.02f * scale, 3.64f * scale, 4.26f * scale };
            snackBar = new Vector3(0, 0, .35f * scale);
            snackBarSize = new Vector3(.9f, .3f, .42f);
            projector = new Vector3(0, snackBarSize.y + .12f, snackBar.z - snackBarSize.z / 2 + .06f);
            laneX = 2.12f * scale;
            gateIn = new Vector3(1.62f * scale, Deck, half + .12f);
            gateOut = new Vector3(-1.62f * scale, Deck, half + .12f);
            ticketStop = new Vector3(gateIn.x, Deck, 1.85f * scale);
            ticketBooth = new Vector3(gateIn.x - .36f, 0, ticketStop.z);
            sign = new Vector3(-.1f * scale, 0, half - .3f);
            frontAisleLimit = -1.75f * scale;
            float frontLimit = -1.45f * scale, sideLimit = 1.75f * scale, gap = snackBarSize.x / 2 + .17f;
            for (int row = 0; row < rows.Length; row++)
            {
                float r = rows[row];
                // Keep the ends of each arc in front of the screen's line and inside the side lanes.
                float limit = Mathf.Min(MaxAngle, LimitFor(r, frontLimit));
                if (sideLimit < r)
                    limit = Mathf.Min(limit, Mathf.Asin(sideLimit / r));
                int count = Mathf.FloorToInt(2 * limit * r / BayPitch);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i - (count - 1) / 2f) * BayPitch / r;
                    var at = Point(r, angle);
                    if (row >= 2 && Mathf.Abs(at.x) < gap)
                        continue; // the snack bar stands here
                    bays.Add(new Bay(row, angle, r, at, Mathf.Atan2(focus.x - at.x, focus.z - at.z) * Mathf.Rad2Deg));
                }
            }
        }
        /// <summary>The widest angle at which the arc of radius <paramref name="r"/> still lies at or in front of <paramref name="z"/>.</summary>
        float LimitFor(float r, float z)
        {
            float cos = (z - focus.z) / r;
            return cos >= 1 ? 0 : cos <= -1 ? Mathf.PI : Mathf.Acos(cos);
        }
        /// <summary>The point on the arc of radius <paramref name="r"/> at <paramref name="angle"/> (0 straight out from the screen, positive to +x), on the deck.</summary>
        public Vector3 Point(float r, float angle) => new Vector3(focus.x + r * Mathf.Sin(angle), Deck, focus.z + r * Mathf.Cos(angle));

        /// <summary>Street → ticket booth → right lane → the aisle behind the bay's row → forward into the bay. <paramref name="stop"/> is the pay stop's index.</summary>
        public List<Vector3> ArrivalPath(Bay bay, out int stop)
        {
            var path = new List<Vector3> { gateIn, new Vector3(gateIn.x, Deck, half - .25f), ticketStop };
            stop = path.Count - 1;
            float aisle = bay.radius + AisleOffset;
            float entry = Mathf.Asin(Mathf.Min(laneX / aisle, Mathf.Sin(MaxTurnIn)));
            var join = Point(aisle, entry);
            path.Add(new Vector3(gateIn.x, Deck, Mathf.Max(join.z + .45f, ticketStop.z - .5f)));
            path.Add(new Vector3(laneX, Deck, Mathf.Max(join.z + .2f, ticketStop.z - .8f)));
            Arc(path, aisle, entry, bay.angle + .12f / aisle);
            path.Add(Point(bay.radius + .12f, bay.angle));
            path.Add(bay.at);
            return path;
        }
        /// <summary>A departure path ends with this many points: joining the exit lane, up it, across to the gate, the street.</summary>
        public const int ExitLanePoints = 4;
        /// <summary>Bay → forward into the aisle in front → along it to the left lane → up the lane → street.</summary>
        public List<Vector3> DeparturePath(Bay bay)
        {
            float aisle = bay.radius - AisleOffset;
            var path = new List<Vector3> { bay.at };
            float exit = -Mathf.Min(Mathf.Asin(Mathf.Min(laneX / aisle, Mathf.Sin(MaxTurnIn))), LimitFor(aisle, frontAisleLimit));
            Arc(path, aisle, bay.angle - .06f, Mathf.Min(exit, bay.angle - .1f));
            var leave = path[path.Count - 1];
            path.Add(new Vector3(-laneX, Deck, leave.z + .25f));
            path.Add(new Vector3(-laneX, Deck, ticketStop.z - .8f));
            path.Add(new Vector3(gateOut.x, Deck, half - .25f));
            path.Add(gateOut);
            return path;
        }
        /// <summary>Points about every .12 along an arc from one angle to another (either way round), both ends included.</summary>
        void Arc(List<Vector3> path, float r, float from, float to)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) * r / .12f));
            for (int i = 0; i <= steps; i++)
                path.Add(Point(r, Mathf.Lerp(from, to, i / (float)steps)));
        }
    }
}
