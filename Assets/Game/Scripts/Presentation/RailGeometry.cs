using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    public static class RailGeometry
    {
        static Vector3 Port(int d) => new Vector3(Directions.Dx[d] * .5f, 0, Directions.Dz[d] * .5f);
        public static Vector3 Sample(Cell c, int entry, int exit, float t)
        {
            Vector3 a = Port(entry), b = Port(exit), p;
            if (Directions.Opp(entry) == exit)
                p = Vector3.LerpUnclamped(a, b, t);
            else
            {
                Vector3 center = a + b;
                p = center + Vector3.Slerp(a - center, b - center, Mathf.Clamp01(t));
            }
            return new Vector3(c.x, .14f, c.z) + p;
        }
        public static Vector3 Sample(TrackPieceState track, RailStep step, float t)
        {
            float u = step.fromCenter ? .5f + t * .5f : step.toCenter ? t * .5f : t;
            return Sample(track.cell, step.entry, step.exit, u);
        }
        /// <summary>
        /// The point <paramref name="offset"/> path units behind the train's front (negative: ahead of it). Behind the start of
        /// its leg lies the leg it arrived by, when the train leaves its stop straight ahead.
        /// </summary>
        public static Vector3 TrainPosition(GameSession g, TrainState t, int offset, out Vector3 forward)
        {
            if (t.path.Count == 0)
            {
                var s = g.Trains.Station(t.stationId);
                var stop = StationLayout.Center(s, t.platform);
                forward = s.axis == 1 ? Vector3.right : Vector3.forward;
                return new Vector3(stop.x, .14f, stop.z) - forward * (offset / 1000f);
            }
            int index = t.step;
            int distance = t.distance - offset;
            while (distance < 0 && index > 0)
            {
                index--;
                distance += t.path[index].length;
            }
            if (distance < 0 && LeavesAhead(t))
            {
                int last = t.returnPath.Count - 1;
                return Along(g, t.returnPath, last, t.returnPath[last].length + distance, out forward);
            }
            return Along(g, t.path, index, distance, out forward);
        }
        /// <summary>
        /// The point <paramref name="distance"/> units into step <paramref name="index"/> of <paramref name="steps"/>, walking on
        /// to the neighbouring steps. Past either end of the steps the last piece's line carries on.
        /// </summary>
        static Vector3 Along(GameSession g, List<RailStep> steps, int index, int distance, out Vector3 forward)
        {
            while (distance < 0 && index > 0)
            {
                index--;
                distance += steps[index].length;
            }
            // Negative offsets (a leading bogie) may run past the current step into the next one.
            while (distance > steps[index].length && index < steps.Count - 1)
            {
                distance -= steps[index].length;
                index++;
            }
            var step = steps[index];
            var track = g.Network.ids[step.trackId];
            float u = distance / (float)step.length;
            Vector3 p = Sample(track, step, u);
            forward = (Sample(track, step, u + .01f) - Sample(track, step, u - .01f)).normalized;
            return p;
        }
        /// <summary>Path units from the start of the train's leg to its front, counted up to <paramref name="limit"/> at most.</summary>
        public static int Front(TrainState t, int limit)
        {
            int front = t.distance;
            for (int i = 0; i < t.step && i < t.path.Count && front < limit; i++)
                front += t.path[i].length;
            return Mathf.Min(front, limit);
        }
        /// <summary>True when the train's leg leaves its first stop straight on, the way the leg it arrived by was heading.</summary>
        public static bool LeavesAhead(TrainState t) => Arrived(t, out var arrival) && t.path[0].exit == arrival.exit;
        /// <summary>True when the train's leg leaves its first stop back the way it came in: the train reverses there.</summary>
        public static bool ReversesOut(TrainState t) => Arrived(t, out var arrival) && t.path[0].exit == arrival.entry;
        /// <summary>The last step of the leg that ended where the current leg starts (the other leg of the route).</summary>
        static bool Arrived(TrainState t, out RailStep arrival)
        {
            arrival = t.returnPath.Count > 0 ? t.returnPath[t.returnPath.Count - 1] : null;
            return arrival != null && t.path.Count > 0 && arrival.toCenter && t.path[0].fromCenter && arrival.trackId == t.path[0].trackId;
        }
    }
}
