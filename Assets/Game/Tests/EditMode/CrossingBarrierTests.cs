using NUnit.Framework;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    public class CrossingBarrierTests
    {
        [Test]
        public void SettledBarrierIsUpWithoutATrainAndDownUnderOne()
        {
            Assert.That(CrossingBarrier.Settled(false).Open, Is.True);
            Assert.That(CrossingBarrier.Settled(false).lowered, Is.EqualTo(0));
            Assert.That(CrossingBarrier.Settled(true).Open, Is.False);
            Assert.That(CrossingBarrier.Settled(true).lowered, Is.EqualTo(1));
        }
        [Test]
        public void LampsWarnBeforeTheBoomsComeDown()
        {
            var warning = CrossingBarrier.Settled(false).Advance(true, CrossingBarrier.Warning / 2);
            Assert.That(warning.Flashing && !warning.Open, Is.True, "the lamps flash and cars stop at once");
            Assert.That(warning.lowered, Is.EqualTo(0), "the booms stay up during the warning");
            var lowering = warning.Advance(true, CrossingBarrier.Warning / 2 + CrossingBarrier.LowerTime / 2);
            Assert.That(lowering.lowered, Is.EqualTo(.5f).Within(.001f));
            Assert.That(lowering.Advance(true, CrossingBarrier.LowerTime).lowered, Is.EqualTo(1), "a boom stops once it lies across the road");
        }
        [Test]
        public void BoomsCloseBeforeTheFastestTrainArrives()
        {
            // The fastest locomotive covers 4.5 cells a second and a crossing closes RoadLanes.Approach ahead of it.
            float arrival = RoadLanes.Approach / 1000f / 4.5f;
            Assert.That(CrossingBarrier.Warning + CrossingBarrier.LowerTime, Is.LessThan(arrival));
            Assert.That(CrossingBarrier.Settled(false).Advance(true, arrival).lowered, Is.EqualTo(1));
        }
        [Test]
        public void CarsWaitUntilTheBoomsAreFullyUp()
        {
            var rising = CrossingBarrier.Settled(true).Advance(false, CrossingBarrier.RaiseTime / 2);
            Assert.That(rising.trainNear, Is.False);
            Assert.That(rising.lowered, Is.EqualTo(.5f).Within(.001f));
            Assert.That(rising.Open || !rising.Flashing, Is.False, "half-raised booms still hold the cars and flash");
            var up = rising.Advance(false, CrossingBarrier.RaiseTime);
            Assert.That(up.Open && !up.Flashing && up.lowered == 0, Is.True);
        }
        [Test]
        public void ARisingBoomTurnsStraightBackDownForTheNextTrain()
        {
            var rising = CrossingBarrier.Settled(true).Advance(false, CrossingBarrier.RaiseTime / 2);
            var back = rising.Advance(true, CrossingBarrier.LowerTime / 4);
            Assert.That(back.lowered, Is.EqualTo(.75f).Within(.001f), "no second warning while the booms are still down");
        }
        [Test]
        public void PausedBarrierHoldsStill()
        {
            var lowering = CrossingBarrier.Settled(false).Advance(true, CrossingBarrier.Warning + CrossingBarrier.LowerTime / 3);
            var paused = lowering.Advance(true, 0);
            Assert.That(paused.lowered, Is.EqualTo(lowering.lowered));
            Assert.That(lowering.Advance(true, -1).lowered, Is.EqualTo(lowering.lowered), "negative time never moves a boom");
        }
    }
}
