using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Where things stand on a campsite (see <see cref="Campsites"/>), in a frame fixed to its road like
    /// <see cref="RoadsideLayout"/>: u runs with the traffic in the lane beside the site and v points away from the
    /// road, in cells from the middle of the frontage (halfway between path[campAt] and path[campAt + 1]). The site
    /// covers u -2..2, v .5..3.5 whichever way the road's path is listed. The gate is at the upstream end; a
    /// gravel lane runs in from it and along the middle, with three pitches on either side, the reception by the gate
    /// and the shower block and campfire behind the lane. <see cref="WorldView"/> draws from these numbers and
    /// <see cref="CampLife"/> drives and walks by them.
    /// </summary>
    public readonly struct CampLayout
    {
        public const float Lane = RoadsideLayout.Lane, RoadDeck = RoadsideLayout.RoadDeck, Deck = .03f;
        public const float U0 = -2f, U1 = 2f, Near = .5f, Far = 3.5f;
        /// <summary>The gravel lane: in from the gate at GateU, then along LaneV to LaneEnd.</summary>
        public const float GateU = -1.6f, LaneV = 2.05f, LaneEnd = 1.8f, LaneWidth = .3f;
        /// <summary>Pitch rows either side of the lane, and the pitches' centres along u (0-2 on row A by the road, 3-5 behind the lane).</summary>
        public const float RowA = 1.25f, RowB = 2.85f, PitchWidth = .66f, PitchDepth = .92f;
        public static readonly float[] PitchU = { -.15f, .6f, 1.35f, .05f, .8f, 1.55f };
        /// <summary>The pitch arriving campers drive to; it stands empty (tents, caravans) or holds a free cabin until they come.</summary>
        public const int Visitor = 2;
        /// <summary>Reception by the gate, the shower block behind the lane's corner, and the campfire ring beside it.</summary>
        public const float OfficeU = -.92f, OfficeV = 1.05f, ShowerU = -1.5f, ShowerV = 2.92f, FireU = -.68f, FireV = 2.86f, FireRing = .15f;

        public readonly Vector3 origin, flow, away;
        /// <summary>+1 when traffic in the site's lane runs the way the road's path is listed, -1 when against it.</summary>
        public readonly int sign;
        public readonly int at;
        /// <summary>The campsite's style (Campsites.Kinds).</summary>
        public readonly int kind;

        public CampLayout(IntercityRoadState road)
        {
            at = road.campAt;
            kind = Mathf.Clamp(road.campKind, 0, Campsites.Kinds - 1);
            Cell cell = road.path[at], next = road.path[at + 1];
            origin = new Vector3((cell.x + next.x) * .5f, 0, (cell.z + next.z) * .5f);
            away = new Vector3(Directions.Dx[road.campSide], 0, Directions.Dz[road.campSide]);
            // Right-hand traffic: the lane beside the site carries cars whose right hand faces it.
            flow = Vector3.Cross(away, Vector3.up);
            int along = Campsites.Along(road);
            sign = Vector3.Dot(flow, new Vector3(Directions.Dx[along], 0, Directions.Dz[along])) > 0 ? 1 : -1;
        }
        public Vector3 At(float u, float v, float y = 0) => origin + flow * u + away * v + Vector3.up * y;
        /// <summary>A box's world size from its size along u, up and along v (the frame is always square to the grid).</summary>
        public Vector3 Size(float su, float sy, float sv) => new Vector3(
            Mathf.Abs(flow.x) * su + Mathf.Abs(away.x) * sv, sy, Mathf.Abs(flow.z) * su + Mathf.Abs(away.z) * sv);
        public Quaternion Facing(float du, float dv) => Quaternion.LookRotation(flow * du + away * dv);
        public Vector3 Direction(float du, float dv) => (flow * du + away * dv).normalized;
        /// <summary>A world offset of <paramref name="du"/> along u and <paramref name="dv"/> along v (not normalised).</summary>
        public Vector3 Offset(float du, float dv) => flow * du + away * dv;

        public static float PitchV(int pitch) => pitch < 3 ? RowA : RowB;
        /// <summary>+1 when the lane lies further from the road than the pitch (row A), -1 when nearer (row B).</summary>
        public static float ToLane(int pitch) => pitch < 3 ? 1 : -1;
        /// <summary>Where a pitch's car stands: at the pitch's lane edge, beside its tent, caravan or cabin.</summary>
        public Vector3 CarSpot(int pitch) => At(PitchU[pitch] + .17f, PitchV(pitch) + ToLane(pitch) * .27f, Deck);
        /// <summary>The middle of a pitch's tent, caravan or cabin.</summary>
        public Vector3 Unit(int pitch, float y = 0) => At(PitchU[pitch] - .1f, PitchV(pitch) - ToLane(pitch) * .1f, y);

        /// <summary>From a few cells up the road, through the gate and along the lane to the pitch's car spot.</summary>
        public List<Vector3> WayIn(IntercityRoadState road, int pitch)
        {
            var route = RoadLane(road, -5, -3);
            route.Add(At(GateU - .35f, Lane, RoadDeck));
            route.Add(At(GateU, Near + .1f, Deck));
            route.Add(At(GateU, LaneV, Deck));
            route.Add(At(PitchU[pitch] + .17f, LaneV, Deck));
            route.Add(CarSpot(pitch));
            return route;
        }
        /// <summary>From the car spot back to the lane (reversing), out through the gate and a few cells on down the road.</summary>
        public List<Vector3> WayOut(IntercityRoadState road, int pitch)
        {
            var route = new List<Vector3> { CarSpot(pitch), At(PitchU[pitch] + .17f, LaneV, Deck), At(GateU, LaneV, Deck), At(GateU, Near + .1f, Deck), At(GateU + .35f, Lane, RoadDeck) };
            route.AddRange(RoadLane(road, 0, 4));
            return route;
        }
        /// <summary>
        /// Points on the site's lane over road cells <paramref name="from"/> to <paramref name="to"/> counted with the
        /// traffic from path[campAt] (negative is upstream; cell k lies about k - sign / 2 along u), following the road where it bends.
        /// </summary>
        List<Vector3> RoadLane(IntercityRoadState road, int from, int to)
        {
            var lane = new List<Vector3>();
            int n = road.path.Count;
            for (int k = from; k <= to; k++)
            {
                int i = Mathf.Clamp(at + sign * k, 0, n - 1), next = Mathf.Clamp(at + sign * (k + 1), 0, n - 1), back = Mathf.Clamp(at + sign * (k - 1), 0, n - 1);
                Cell cell = road.path[i];
                var ahead = next != i ? Step(cell, road.path[next]) : back != i ? Step(road.path[back], cell) : flow;
                var point = new Vector3(cell.x, RoadDeck, cell.z) + Vector3.Cross(Vector3.up, ahead) * Lane;
                if (lane.Count == 0 || (lane[lane.Count - 1] - point).sqrMagnitude > 1e-4f)
                    lane.Add(point);
            }
            return lane;
        }
        static Vector3 Step(Cell from, Cell to)
        {
            var d = new Vector3(to.x - from.x, 0, to.z - from.z);
            return d.sqrMagnitude > 0 ? d.normalized : Vector3.forward;
        }
    }
}
