using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Where each car of a train stands. The simulation moves only the front of a train along its leg, and the cars follow
    /// the rails behind it, back onto the leg it arrived by (RailGeometry.TrainPosition). A train that reverses out of a stop
    /// leaves its cars where they stood: they cover the first stretch of the new leg and stay put until the front has run
    /// past them. The locomotive keeps its end of the consist, so after a reversal the train is pushed, as a push-pull train
    /// is, until the next reversal puts the locomotive in front again.
    /// </summary>
    public sealed class TrainConsist
    {
        /// <summary>Cars sit 560 path units (.56 cells) apart and ride on bogies 165 units either side of their centre.</summary>
        public const int CarSpacing = 560, BogieOffset = 165;

        sealed class Seen
        {
            public List<RailStep> leg;
            public bool pushing, placed;
            public Vector3 locomotive;
        }
        readonly Dictionary<int, Seen> trains = new Dictionary<int, Seen>();

        /// <summary>
        /// Where car <paramref name="car"/> of the <paramref name="cars"/> in <paramref name="train"/> stands, and which way
        /// its front faces. Cars ride on two bogies, so on curves the body follows the chord between them.
        /// </summary>
        public Vector3 CarPose(GameSession game, TrainState train, int car, int cars, out Vector3 forward)
        {
            var seen = Follow(game, train, cars);
            int behind = Behind(game, train, car, cars, seen.pushing);
            var front = RailGeometry.TrainPosition(game, train, behind - BogieOffset, out _);
            var back = RailGeometry.TrainPosition(game, train, behind + BogieOffset, out _);
            Vector3 pose;
            forward = front - back;
            if (forward.sqrMagnitude < 1e-6f)
                pose = RailGeometry.TrainPosition(game, train, behind, out forward);
            else
            {
                forward.Normalize();
                pose = (front + back) * .5f;
            }
            // A pushed train runs backwards: its cars face the way the locomotive does.
            if (seen.pushing)
                forward = -forward;
            if (car == 0)
            {
                seen.locomotive = pose;
                seen.placed = true;
            }
            return pose;
        }

        /// <summary>True while the train runs with its locomotive at the back.</summary>
        public bool Pushing(GameSession game, TrainState train, int cars) => Follow(game, train, cars).pushing;

        /// <summary>Drops what is known about a train that has left the game.</summary>
        public void Forget(int trainId) => trains.Remove(trainId);

        /// <summary>Tracks the train's leg. A new leg keeps the locomotive at the end of the consist where it already stands.</summary>
        Seen Follow(GameSession game, TrainState t, int cars)
        {
            if (!trains.TryGetValue(t.id, out var seen))
            {
                seen = new Seen { leg = t.path };
                trains.Add(t.id, seen);
                return seen;
            }
            if (ReferenceEquals(seen.leg, t.path))
                return seen;
            if (ReferenceEquals(seen.leg, t.returnPath))
                // The train arrived and turned onto the route's other leg; reversing there swaps which end leads.
                seen.pushing ^= RailGeometry.ReversesOut(t);
            else if (seen.placed)
            {
                // A new route: whichever end of the consist is nearer the locomotive's last place keeps it.
                var pulled = RailGeometry.TrainPosition(game, t, Behind(game, t, 0, cars, false), out _);
                var pushed = RailGeometry.TrainPosition(game, t, Behind(game, t, 0, cars, true), out _);
                seen.pushing = (pushed - seen.locomotive).sqrMagnitude < (pulled - seen.locomotive).sqrMagnitude;
            }
            seen.leg = t.path;
            return seen;
        }

        /// <summary>How far behind the train's front the middle of <paramref name="car"/> stands (negative: ahead of it).</summary>
        static int Behind(GameSession game, TrainState t, int car, int cars, bool pushing)
        {
            int rank = pushing ? cars - 1 - car : car;
            return rank * CarSpacing - Ahead(game, t, cars);
        }

        /// <summary>
        /// How far ahead of the simulated front the leading car stands. A train without a route stands in the middle of its
        /// platform; one that reverses out waits until its front has run the length of the consist.
        /// </summary>
        static int Ahead(GameSession game, TrainState t, int cars)
        {
            int span = (cars - 1) * CarSpacing;
            if (t.path.Count == 0)
            {
                var station = game.Trains.Station(t.stationId);
                float middle = station != null ? StationLayout.Middle(StationLayout.Length(station)) : 0;
                return span / 2 + Mathf.RoundToInt(middle * 1000);
            }
            return RailGeometry.ReversesOut(t) ? span - RailGeometry.Front(t, span) : 0;
        }
    }
}
