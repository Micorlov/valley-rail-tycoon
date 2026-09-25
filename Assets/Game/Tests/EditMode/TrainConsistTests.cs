using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>
    /// Train cars stay on the rails while trains stand at platforms and reverse out of them. The railway is one straight line
    /// along z 15 with a terminus near each end, so every stop is a reversal, and a car off z 15 or past a rail end has left
    /// the track (before this, cars of a train about to reverse were drawn past the buffers, over the town).
    /// </summary>
    public class TrainConsistTests
    {
        const int RailStart = 8, RailEnd = 50, RailZ = 15, Ticks = 3000;
        GameSession game;
        TrainConsist consist;
        TrainState train;
        int a, b;

        [SetUp]
        public void Setup()
        {
            game = new GameSession(WorldState.New(new Balance()), new Balance());
            consist = new TrainConsist();
            var plan = game.Build.Preview(new Cell(RailStart, RailZ), new Cell(RailEnd, RailZ));
            Assert.That(plan.valid, Is.True, plan.reason);
            OK(game.Build.CommitBuild(plan));
            a = OK(game.Stations.Place(new Cell(10, RailZ), 1));
            b = OK(game.Stations.Place(new Cell(48, RailZ), 2));
            // The longest consist, nearly five cells, at three-cell platforms.
            train = game.Trains.Train(OK(game.Trains.Buy(a, 0, Cargo.Coal, TrainCatalog.MaxWagons)));
        }

        static int OK(Result r)
        {
            Assert.That(r.ok, Is.True, r.message);
            return r.id;
        }

        int Cars => WorldView.TrainCarCount(train);
        Vector3 Pose(int car) => consist.CarPose(game, train, car, Cars, out _);
        Vector3[] Poses()
        {
            var poses = new Vector3[Cars];
            for (int i = 0; i < poses.Length; i++)
                poses[i] = Pose(i);
            return poses;
        }
        static bool OnRails(Vector3 p) => Mathf.Abs(p.z - RailZ) < .01f && p.x >= RailStart - .5f && p.x <= RailEnd + .5f;

        [Test]
        public void TrainWithoutARouteStandsCentredOnItsPlatform()
        {
            var poses = Poses();
            foreach (var p in poses)
                Assert.That(OnRails(p), Is.True, $"car at {p} is off the rails");
            Assert.That((poses[0].x + poses[poses.Length - 1].x) / 2, Is.EqualTo(10).Within(.01f));
        }

        [Test]
        public void CarsStayOnTheRailsAndNeverJumpThroughEveryStop()
        {
            OK(game.Trains.AssignRoute(train.id, a, b));
            // The front covers speed / 20 path units a tick; no car may move further than that.
            float stride = game.Balance.speed[train.model] / 20 / 1000f + .01f;
            var last = Poses();
            var state = train.state;
            var near = new HashSet<int>();
            int arrivals = 0, standing = 0;
            for (int tick = 0; tick < Ticks; tick++)
            {
                game.Step();
                if (state == ServiceState.Travelling && train.state == ServiceState.Loading)
                    arrivals++;
                bool stood = state == ServiceState.Loading && train.state == ServiceState.Loading;
                var now = Poses();
                RoadLanes.TracksNearTrains(game.World, near);
                for (int car = 0; car < now.Length; car++)
                {
                    Assert.That(OnRails(now[car]), Is.True, $"car {car} is off the rails at {now[car]} on tick {tick} ({train.state})");
                    // Level crossings stay closed under every car, standing or moving.
                    var under = game.Network.At(new Cell(Mathf.RoundToInt(now[car].x), Mathf.RoundToInt(now[car].z)));
                    Assert.That(near, Does.Contain(under.id), $"car {car} stands on track {under.id} that counts as clear on tick {tick} ({train.state})");
                    float moved = Vector3.Distance(now[car], last[car]);
                    Assert.That(moved, Is.LessThan(stood ? 1e-4f : stride), $"car {car} jumped {moved} on tick {tick} ({state} to {train.state})");
                }
                if (stood)
                    standing++;
                state = train.state;
                last = now;
            }
            Assert.That(arrivals, Is.GreaterThanOrEqualTo(4));
            Assert.That(standing, Is.GreaterThan(0));
        }

        [Test]
        public void ReversingAtATerminusPushesTheTrainUntilTheNextReversal()
        {
            OK(game.Trains.AssignRoute(train.id, a, b));
            var state = train.state;
            int arrivals = 0, checkedEnds = 0;
            for (int tick = 0; tick < Ticks; tick++)
            {
                game.Step();
                if (state == ServiceState.Travelling && train.state == ServiceState.Loading)
                    arrivals++;
                state = train.state;
                Assert.That(RailGeometry.ReversesOut(train), Is.True, "Every stop on this line is a terminus");
                var poses = Poses();
                bool pushing = consist.Pushing(game, train, Cars);
                Assert.That(pushing, Is.EqualTo(arrivals % 2 == 1), $"after {arrivals} arrivals");
                if (train.state == ServiceState.Travelling && train.step > 10 && train.step < train.path.Count - 10)
                {
                    // Pushed, the locomotive runs last, further from the destination than the last car.
                    float destination = game.Trains.Station(train.destination).cell.x;
                    bool locomotiveLast = Mathf.Abs(poses[0].x - destination) > Mathf.Abs(poses[poses.Length - 1].x - destination);
                    Assert.That(locomotiveLast, Is.EqualTo(pushing), $"tick {tick}");
                    checkedEnds++;
                }
            }
            Assert.That(arrivals, Is.GreaterThanOrEqualTo(4));
            Assert.That(checkedEnds, Is.GreaterThan(0));
        }
    }
}
