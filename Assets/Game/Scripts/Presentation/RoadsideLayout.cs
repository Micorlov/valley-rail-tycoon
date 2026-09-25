using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Where things stand at a roadside service area, in a frame fixed to its road (see <see cref="Roadside"/>): u runs
    /// with the traffic in the lane beside the site and v points away from the road, both in cells from the middle road
    /// cell. The site covers u -1.5..1.5, v .5..2.5. The garage stands upstream, the filling station in the middle with
    /// its shop behind, the tyre shop downstream. <see cref="WorldView"/> draws from these numbers and
    /// <see cref="RoadsideLife"/> drives by them, so cars stop beside the pumps and the lift stands in the garage door.
    /// </summary>
    public readonly struct RoadsideLayout
    {
        /// <summary>Lane offset from the road's centre line (as CityTraffic and BeachLife drive), and the car heights on road and forecourt.</summary>
        public const float Lane = .2f, RoadDeck = .028f, Deck = .03f;
        public const float Near = .5f, Far = 2.5f, Half = 1.5f;
        /// <summary>Driveways cross the verge at these u; cars leave the road lane at Turn and cross the verge at DriveV.</summary>
        public const float EntryU = -1.1f, ExitU = 1.1f, Turn = 1.4f, DriveV = .72f, DriveWidth = .6f;
        /// <summary>The pump island runs along u at IslandV; cars fuel in the lanes either side, at -PumpU (rear) and +PumpU (front).</summary>
        public const float IslandV = 1.1f, IslandLength = .84f, PumpU = .24f, LaneStart = -.66f, LaneEnd = .66f;
        public static readonly float[] PumpLanes = { .88f, 1.32f };
        /// <summary>The canopy over the island and both lanes.</summary>
        public const float CanopyU = .64f, CanopyNear = .7f, CanopyFar = 1.52f, CanopyTop = .52f;
        /// <summary>The shop behind the forecourt.</summary>
        public const float ShopU = .5f, ShopNear = 1.82f, ShopFar = 2.38f, ShopHeight = .3f;
        /// <summary>The garage upstream: two bays facing the road, a lift in front of the open one.</summary>
        public const float GarageFrom = -1.46f, GarageTo = -.76f, GarageNear = 1.8f, GarageFar = 2.44f, GarageHeight = .36f, LiftU = -1.28f, LiftV = 1.5f, ClosedBayU = -.94f;
        /// <summary>The tyre shop downstream: an open-fronted shed, tyre stacks beside it and a car on a jack in front.</summary>
        public const float TyreFrom = .8f, TyreTo = 1.46f, TyreNear = 1.86f, TyreFar = 2.44f, TyreHeight = .3f, TyreCarU = 1.1f, TyreCarV = 1.46f, StackU = 1.36f;
        public static readonly float[] StackV = { 1.28f, 1.46f, 1.64f };
        /// <summary>The truck stop's canopy stands higher, so lorries fit under it.</summary>
        public const float TruckCanopyTop = .72f;
        /// <summary>The eco station's wind turbine, between the café and the tyre shop.</summary>
        public const float TurbineU = .65f, TurbineV = 2.28f, HubHeight = 1.25f;

        public readonly Vector3 origin, flow, away;
        /// <summary>+1 when traffic in the site's lane runs the way the road's path is listed, -1 when against it.</summary>
        public readonly int sign;
        public readonly int at;
        /// <summary>The station's style (Roadside.Kinds).</summary>
        public readonly int kind;

        public RoadsideLayout(IntercityRoadState road)
        {
            at = road.serviceAt;
            kind = Mathf.Clamp(road.serviceKind, 0, Roadside.Kinds - 1);
            var cell = road.path[at];
            origin = new Vector3(cell.x, 0, cell.z);
            away = new Vector3(Directions.Dx[road.serviceSide], 0, Directions.Dz[road.serviceSide]);
            // Right-hand traffic: the lane beside the site carries cars whose right hand faces it.
            flow = Vector3.Cross(away, Vector3.up);
            int along = Roadside.Along(road);
            sign = Vector3.Dot(flow, new Vector3(Directions.Dx[along], 0, Directions.Dz[along])) > 0 ? 1 : -1;
        }
        public Vector3 At(float u, float v, float y = 0) => origin + flow * u + away * v + Vector3.up * y;
        /// <summary>A box's world size from its size along u, up and along v (the frame is always square to the grid).</summary>
        public Vector3 Size(float su, float sy, float sv) => new Vector3(
            Mathf.Abs(flow.x) * su + Mathf.Abs(away.x) * sv, sy, Mathf.Abs(flow.z) * su + Mathf.Abs(away.z) * sv);
        public Quaternion Facing(float du, float dv) => Quaternion.LookRotation(flow * du + away * dv);
        public Vector3 Direction(float du, float dv) => (flow * du + away * dv).normalized;

        /// <summary>A pump slot: the car's centre when it stands at the pump.</summary>
        public Vector3 Slot(int lane, bool front) => At(front ? PumpU : -PumpU, PumpLanes[lane], Deck);
        /// <summary>From a few cells up the road, along its near lane, through the entrance to the start of a pump lane.</summary>
        public List<Vector3> WayIn(IntercityRoadState road, int lane)
        {
            var route = RoadLane(road, -4, -2);
            route.Add(At(-Turn, Lane, RoadDeck));
            route.Add(At(EntryU, DriveV, Deck));
            route.Add(At(LaneStart, PumpLanes[lane], Deck));
            return route;
        }
        /// <summary>From the end of a pump lane through the exit and a few cells on down the road.</summary>
        public List<Vector3> WayOut(IntercityRoadState road, int lane)
        {
            var route = new List<Vector3> { At(LaneEnd, PumpLanes[lane], Deck), At(ExitU, DriveV, Deck), At(Turn, Lane, RoadDeck) };
            route.AddRange(RoadLane(road, 2, 4));
            return route;
        }
        /// <summary>
        /// Points on the site's lane over road cells <paramref name="from"/> to <paramref name="to"/> counted with the
        /// traffic from the middle cell (negative is upstream), following the road where it bends.
        /// </summary>
        List<Vector3> RoadLane(IntercityRoadState road, int from, int to)
        {
            var lane = new List<Vector3>();
            int n = road.path.Count;
            for (int k = from; k <= to; k++)
            {
                int i = Mathf.Clamp(at + sign * k, 0, n - 1), next = Mathf.Clamp(at + sign * (k + 1), 0, n - 1), back = Mathf.Clamp(at + sign * (k - 1), 0, n - 1);
                Cell cell = road.path[i];
                // Travel direction at this cell: towards the next cell with the traffic, or away from the previous one at a road end.
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
