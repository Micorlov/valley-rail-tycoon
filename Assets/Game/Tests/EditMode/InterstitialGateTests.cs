using NUnit.Framework;
namespace ValleyRail.Tests
{
    /// <summary>When an interstitial may follow a finished session: the first-ever one is spared, then 90 s apart.</summary>
    public class InterstitialGateTests
    {
        [Test]
        public void FirstLaunchFirstSessionNeverShowsSecondMay()
        {
            var gate = new InterstitialGate(launchedBefore: false);
            Assert.IsFalse(gate.GameFinished(500));
            Assert.IsTrue(gate.GameFinished(600));
        }
        [Test]
        public void LaterLaunchFirstSessionMayShow()
        {
            Assert.IsTrue(new InterstitialGate(launchedBefore: true).GameFinished(10));
        }
        [Test]
        public void NinetySecondCooldownAfterAShownAd()
        {
            var gate = new InterstitialGate(launchedBefore: true);
            Assert.IsTrue(gate.GameFinished(100));
            gate.Shown(100);
            Assert.IsFalse(gate.GameFinished(189));
            Assert.IsTrue(gate.GameFinished(190));
        }
        [Test]
        public void MissingAdDoesNotStartTheCooldown()
        {
            var gate = new InterstitialGate(launchedBefore: true);
            Assert.IsTrue(gate.GameFinished(100));
            Assert.IsTrue(gate.GameFinished(110));
        }
    }
}
