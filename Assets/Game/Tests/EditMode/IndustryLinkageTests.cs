using NUnit.Framework;
using UnityEngine;
namespace ValleyRail.Tests
{
    public class IndustryLinkageTests
    {
        [Test]
        public void PumpjackHeadIsLowestWhileTheCrankPinIsHighest()
        {
            Assert.That(IndustryMotion.BeamTilt(0), Is.EqualTo(0).Within(.001f), "crank pin level: beam level");
            Assert.That(IndustryMotion.BeamTilt(180), Is.EqualTo(0).Within(.001f));
            Assert.That(IndustryMotion.BeamTilt(90), Is.LessThan(-15), "pin at the top pulls the tail up and the head down");
            Assert.That(IndustryMotion.BeamTilt(270), Is.EqualTo(-IndustryMotion.BeamTilt(90)).Within(.001f), "the stroke is symmetric");
            for (int crank = 0; crank < 360; crank += 5)
                Assert.That(Mathf.Abs(IndustryMotion.BeamTilt(crank)), Is.LessThanOrEqualTo(20), "a real pumpjack rocks about ±20°");
        }
        [Test]
        public void PitmanArmsKeepTheirLengthRoundTheWholeCrankTurn()
        {
            float rest = Vector3.Distance(IndustryMotion.CrankPin(0), IndustryMotion.BeamTail(IndustryMotion.BeamTilt(0)));
            for (int crank = 0; crank < 360; crank += 5)
            {
                var pin = IndustryMotion.CrankPin(crank);
                var tail = IndustryMotion.BeamTail(IndustryMotion.BeamTilt(crank));
                Assert.That(tail.y - pin.y, Is.EqualTo(IndustryMotion.BeamPivot.y - IndustryMotion.CrankAxle.y).Within(.001f), "the tail rides a fixed height above the pin");
                Assert.That(Vector3.Distance(pin, tail), Is.EqualTo(rest).Within(rest * .05f), "the arm never visibly stretches");
            }
        }
        [Test]
        public void PolishedRodMakesAFullStroke()
        {
            float top = IndustryMotion.RodLift(IndustryMotion.BeamTilt(270)), bottom = IndustryMotion.RodLift(IndustryMotion.BeamTilt(90));
            Assert.That(top, Is.GreaterThan(0));
            Assert.That(bottom, Is.LessThan(0));
            Assert.That(top - bottom, Is.GreaterThan(.5f), "a stroke big enough to see at the default zoom");
        }
        [Test]
        public void SmokePuffsFaceOutwardLikeUnitysOwnCube()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var puff = IndustryMotion.PuffMesh();
            try
            {
                var cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(OutwardFaces(cubeMesh), Is.EqualTo(cubeMesh.triangles.Length / 3), "the winding Unity draws as front faces");
                Assert.That(puff.triangles.Length / 3, Is.EqualTo(20));
                Assert.That(OutwardFaces(puff), Is.EqualTo(20), "every puff face is drawn from outside, not culled");
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(puff);
            }
        }
        // Faces whose winding (and stored normal) points away from the mesh's centre.
        static int OutwardFaces(Mesh mesh)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            int outward = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                var winding = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(winding, a + b + c) > 0 && Vector3.Dot(n[t[i]], a + b + c) > 0)
                    outward++;
            }
            return outward;
        }
        [Test]
        public void SmokePuffsRiseDriftAndThinAway()
        {
            Assert.That(IndustryMotion.PuffOffset(0).magnitude, Is.EqualTo(0).Within(.001f), "a puff leaves from the stack's mouth");
            float height = 0;
            for (int i = 1; i <= 20; i++)
            {
                var offset = IndustryMotion.PuffOffset(IndustryMotion.PuffLife * i / 20);
                Assert.That(offset.y, Is.GreaterThan(height), "puffs keep rising");
                height = offset.y;
            }
            Assert.That(height, Is.GreaterThan(1), "plumes stand well above the stack");
            Assert.That(IndustryMotion.PuffOffset(IndustryMotion.PuffLife).x, Is.GreaterThan(.3f), "and drift downwind");
            float start = IndustryMotion.PuffWidth(0), middle = IndustryMotion.PuffWidth(IndustryMotion.PuffLife / 2);
            Assert.That(start, Is.GreaterThan(0).And.LessThan(middle), "puffs swell as they rise");
            Assert.That(IndustryMotion.PuffWidth(IndustryMotion.PuffLife), Is.EqualTo(0).Within(.001f), "and are gone when recycled");
        }
    }
}
