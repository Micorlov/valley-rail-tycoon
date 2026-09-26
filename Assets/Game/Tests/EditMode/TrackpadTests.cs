using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
namespace ValleyRail.Tests
{
    public sealed class TrackpadTests
    {
        [Test]
        public void ASmallTwistDuringAPinchDoesNotTurnTheView()
        {
            var twist = new TwistGesture();
            Assert.That(twist.Step(8, false), Is.EqualTo(0));
            Assert.That(twist.Step(-5, false), Is.EqualTo(0));
            Assert.That(twist.Step(12, true), Is.EqualTo(0));
        }

        [Test]
        public void TwistingFarEnoughTurnsOnceEachWay()
        {
            var twist = new TwistGesture();
            Assert.That(twist.Step(20, false), Is.EqualTo(0));
            Assert.That(twist.Step(15, false), Is.EqualTo(1), "anticlockwise past the threshold");
            twist.Step(0, true);
            Assert.That(twist.Step(-TwistGesture.TurnDegrees, false), Is.EqualTo(-1), "clockwise in a new gesture");
        }

        [Test]
        public void OneGestureTurnsTheViewAtMostOnce()
        {
            var twist = new TwistGesture();
            int turns = 0;
            for (int i = 0; i < 20; i++)
                turns += twist.Step(10, false);
            Assert.That(turns, Is.EqualTo(1));
            twist.Step(0, true);
            Assert.That(twist.Step(40, false), Is.EqualTo(1), "the next gesture turns again");
        }

        [Test]
        public void SpreadingFingersZoomsInAndPinchingZoomsOut()
        {
            Assert.That(DesktopControls.PinchZoom(29, .5f), Is.LessThan(29));
            Assert.That(DesktopControls.PinchZoom(29, -.5f), Is.GreaterThan(29));
            Assert.That(DesktopControls.PinchZoom(DesktopControls.PinchZoom(29, .3f), -.3f), Is.EqualTo(29).Within(1e-4f));
            Assert.That(DesktopControls.PinchZoom(29, 0), Is.EqualTo(29));
        }

        [Test]
        public void ASwipeDragsTheMapWithTheContent()
        {
            // AppKit: positive y means the content moves down the screen; Unity's screen y points up.
            Assert.That(DesktopControls.SwipeDrag(12, 30), Is.EqualTo(new Vector2(12, -30)));
            Assert.That(DesktopControls.SwipeDrag(0, 0), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void CommandSwipeUpZoomsIn()
        {
            Assert.That(DesktopControls.CommandSwipeZoom(29, -100), Is.LessThan(29), "content moving up zooms in");
            Assert.That(DesktopControls.CommandSwipeZoom(29, 100), Is.GreaterThan(29));
        }

        [Test]
        public void FrameMatchesThePluginLayout()
        {
            // VRTrackpadFrame in Native/macOS/ValleyTrackpad.m: five floats, then two ints.
            Assert.That(Marshal.SizeOf<TrackpadFrame>(), Is.EqualTo(28));
            Assert.That((int)Marshal.OffsetOf<TrackpadFrame>("wheel"), Is.EqualTo(16));
            Assert.That((int)Marshal.OffsetOf<TrackpadFrame>("rotateEnded"), Is.EqualTo(20));
            Assert.That((int)Marshal.OffsetOf<TrackpadFrame>("precise"), Is.EqualTo(24));
        }

        [Test]
        public void TheEditorNeverLoadsThePlugin()
        {
            Assert.That(MacTrackpad.Read(out var frame), Is.False);
            Assert.That(frame.magnify, Is.EqualTo(0));
        }
    }
}
