using NUnit.Framework;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    public class JunctionRulesTests
    {
        const int Crossroads = 15, TeeJunction = 7, Straight = 5;

        [Test]
        public void OnlyJunctionsOfThreeOrFourRoadsAwayFromThePlazaAreControlled()
        {
            var cell = new Cell(20, 20);
            Assert.That(JunctionRules.Kind(cell, Straight, true, false), Is.EqualTo(JunctionKind.GiveWay), "a straight street");
            Assert.That(JunctionRules.Kind(cell, 1, false, false), Is.EqualTo(JunctionKind.GiveWay), "a dead end");
            Assert.That(JunctionRules.Kind(cell, Crossroads, true, true), Is.EqualTo(JunctionKind.GiveWay), "the plaza keeps its fountain");
        }

        [Test]
        public void SomeTownJunctionsGetLightsFewerARoundaboutAndTheRestGiveWay()
        {
            int lights = 0, rings = 0, plain = 0, total = 0;
            for (int x = 0; x < 64; x++)
                for (int z = 0; z < 64; z++)
                {
                    var kind = JunctionRules.Kind(new Cell(x, z), (x + z) % 2 == 0 ? Crossroads : TeeJunction, true, false);
                    Assert.That(JunctionRules.Kind(new Cell(x, z), (x + z) % 2 == 0 ? Crossroads : TeeJunction, true, false), Is.EqualTo(kind), "the same cell always gets the same");
                    if (kind == JunctionKind.Lights) lights++;
                    else if (kind == JunctionKind.Roundabout) rings++;
                    else plain++;
                    total++;
                }
            Assert.That(lights / (float)total, Is.EqualTo(.4f).Within(.05f), "two in five get traffic lights");
            Assert.That(rings / (float)total, Is.EqualTo(.2f).Within(.05f), "one in five gets a roundabout");
            Assert.That(plain / (float)total, Is.EqualTo(.4f).Within(.05f), "the rest give way");
        }

        [Test]
        public void JunctionsOutOfTownGetARoundaboutOrLights()
        {
            int rings = 0, total = 0;
            for (int x = 0; x < 64; x++)
                for (int z = 0; z < 64; z++)
                {
                    var kind = JunctionRules.Kind(new Cell(x, z), TeeJunction, false, false);
                    Assert.That(kind, Is.Not.EqualTo(JunctionKind.GiveWay));
                    if (kind == JunctionKind.Roundabout) rings++;
                    total++;
                }
            Assert.That(rings / (float)total, Is.EqualTo(.5f).Within(.05f));
        }

        [Test]
        public void LightsRestOnGreenWhileNobodyWaitsOnTheOtherRoad()
        {
            var phase = JunctionRules.Start(new Cell(10, 10));
            Assert.That(phase.light, Is.EqualTo(SignalLight.Green));
            int road = phase.road;
            for (int step = 0; step < 1000; step++)
                phase = JunctionRules.Advance(phase, .1f, step % 3 == 0, false);
            Assert.That(JunctionRules.Shows(phase, road), Is.EqualTo(SignalLight.Green), "no call from the other road: no change");
            Assert.That(JunctionRules.Shows(phase, 1 - road), Is.EqualTo(SignalLight.Red));
        }

        [Test]
        public void ACarWaitingOnTheOtherRoadGetsGreenAfterAmberAndAllRed()
        {
            var phase = new SignalPhase { road = 0, light = SignalLight.Green };
            float t = 0;
            while (phase.light == SignalLight.Green)
            {
                phase = JunctionRules.Advance(phase, .05f, false, true);
                t += .05f;
            }
            Assert.That(t, Is.EqualTo(JunctionRules.MinGreen).Within(.06f), "the green lasts its shortest when its road is empty");
            Assert.That(JunctionRules.Shows(phase, 0), Is.EqualTo(SignalLight.Amber));
            Assert.That(JunctionRules.Go(phase, 1), Is.False, "the waiting road is still red during the amber");
            phase = JunctionRules.Advance(phase, JunctionRules.Amber + .01f, false, true);
            Assert.That(JunctionRules.Shows(phase, 0), Is.EqualTo(SignalLight.Red));
            Assert.That(JunctionRules.Shows(phase, 1), Is.EqualTo(SignalLight.Red), "a moment of red both ways clears the junction");
            phase = JunctionRules.Advance(phase, JunctionRules.AllRed + .01f, false, true);
            Assert.That(JunctionRules.Go(phase, 1), Is.True, "then the waiting road goes");
            Assert.That(JunctionRules.Go(phase, 3), Is.True, "west and east share the east-west road");
            Assert.That(JunctionRules.Shows(phase, 2), Is.EqualTo(SignalLight.Red));
        }

        [Test]
        public void ABusyRoadKeepsItsGreenUpToTheLongest()
        {
            var phase = new SignalPhase { road = 1, light = SignalLight.Green };
            float t = 0;
            while (phase.light == SignalLight.Green && t < 60)
            {
                phase = JunctionRules.Advance(phase, .05f, true, true);
                t += .05f;
            }
            Assert.That(t, Is.EqualTo(JunctionRules.MaxGreen).Within(.06f));
        }

        [Test]
        public void LightsNeverLetBothRoadsGoAtOnce()
        {
            var random = new System.Random(5);
            var phase = JunctionRules.Start(new Cell(3, 4));
            int changes = 0;
            for (int step = 0; step < 20000; step++)
            {
                int before = phase.road;
                phase = JunctionRules.Advance(phase, (float)random.NextDouble() * .3f, random.Next(3) == 0, random.Next(2) == 0);
                if (phase.road != before)
                    changes++;
                Assert.That(JunctionRules.Shows(phase, 0) == SignalLight.Red || JunctionRules.Shows(phase, 1) == SignalLight.Red, Is.True, $"step {step}");
            }
            Assert.That(changes, Is.GreaterThan(100), "the lights keep changing under traffic");
        }

        [Test]
        public void NeighbouringJunctionsStartOnDifferentRoads()
        {
            int northSouth = 0;
            for (int x = 0; x < 20; x++)
                for (int z = 0; z < 20; z++)
                    if (JunctionRules.Start(new Cell(x, z)).road == 0)
                        northSouth++;
            Assert.That(northSouth, Is.InRange(120, 280));
        }
    }
}
