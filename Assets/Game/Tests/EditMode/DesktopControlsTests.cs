using NUnit.Framework;
using UnityEngine;
namespace ValleyRail.Tests
{
    public sealed class DesktopControlsTests
    {
        [Test]
        public void OneWheelNotchZoomsByTheSameFractionCloseUpAndFarOut()
        {
            float near = DesktopControls.WheelZoom(2, 1), far = DesktopControls.WheelZoom(60, 1);
            Assert.That(near / 2, Is.EqualTo(far / 60).Within(1e-5f));
            Assert.That(far / 60, Is.EqualTo(Mathf.Exp(-DesktopControls.WheelZoomPerNotch)).Within(1e-5f));
            Assert.That(far, Is.LessThan(60 * .9f), "one notch must visibly zoom in");
        }

        [Test]
        public void ScrollingBackOutUndoesScrollingIn()
        {
            float zoom = DesktopControls.WheelZoom(DesktopControls.WheelZoom(29, 1.5f), -1.5f);
            Assert.That(zoom, Is.EqualTo(29).Within(1e-4f));
            Assert.That(DesktopControls.WheelZoom(29, 0), Is.EqualTo(29));
        }

        [Test]
        public void AFlungScrollIsCappedPerFrame()
        {
            float cap = DesktopControls.WheelZoom(29, DesktopControls.MaxNotchesPerFrame);
            Assert.That(DesktopControls.WheelZoom(29, 60), Is.EqualTo(cap));
            Assert.That(DesktopControls.WheelZoom(29, -60), Is.EqualTo(DesktopControls.WheelZoom(29, -DesktopControls.MaxNotchesPerFrame)));
        }

        [Test]
        public void PlusZoomsInAndMinusZoomsOutSymmetrically()
        {
            float zoomedIn = DesktopControls.KeyZoom(29, 1, .5f);
            Assert.That(zoomedIn, Is.LessThan(29));
            Assert.That(DesktopControls.KeyZoom(zoomedIn, -1, .5f), Is.EqualTo(29).Within(1e-4f));
        }

        [Test]
        public void NoPanKeysMeansNoDrag()
        {
            Assert.That(DesktopControls.KeyPanDrag(false, false, false, false, false, 900, .1f), Is.EqualTo(Vector2.zero));
            Assert.That(DesktopControls.KeyPanDrag(true, true, true, true, true, 900, .1f), Is.EqualTo(Vector2.zero), "opposite keys cancel");
        }

        [Test]
        public void PanKeysDragAgainstTheirDirectionAtOneScreenPerSecond()
        {
            var up = DesktopControls.KeyPanDrag(false, false, true, false, false, 900, .5f);
            Assert.That(up.x, Is.EqualTo(0));
            Assert.That(up.y, Is.EqualTo(-450).Within(1e-3f), "Up moves the view up the map, like dragging the map down");
            var right = DesktopControls.KeyPanDrag(false, true, false, false, false, 900, .5f);
            Assert.That(right.x, Is.EqualTo(-450).Within(1e-3f));
        }

        [Test]
        public void DiagonalPanIsNoFasterAndShiftSpeedsItUp()
        {
            var straight = DesktopControls.KeyPanDrag(false, false, true, false, false, 900, .1f);
            var diagonal = DesktopControls.KeyPanDrag(true, false, true, false, false, 900, .1f);
            Assert.That(diagonal.magnitude, Is.EqualTo(straight.magnitude).Within(1e-3f));
            var fast = DesktopControls.KeyPanDrag(false, false, true, false, true, 900, .1f);
            Assert.That(fast.magnitude, Is.EqualTo(straight.magnitude * DesktopControls.FastPanFactor).Within(1e-3f));
        }

        [Test]
        public void OnlyBuiltDesktopPlayersCountAsDesktop()
        {
            Assert.That(DesktopControls.IsDesktopPlayer(RuntimePlatform.OSXPlayer), Is.True);
            Assert.That(DesktopControls.IsDesktopPlayer(RuntimePlatform.WindowsPlayer), Is.True);
            Assert.That(DesktopControls.IsDesktopPlayer(RuntimePlatform.Android), Is.False);
            Assert.That(DesktopControls.IsDesktopPlayer(RuntimePlatform.OSXEditor), Is.False, "the editor keeps the phone layout the tests expect");
            Assert.That(DesktopControls.IsDesktop, Is.False);
        }

        [Test]
        public void MenuHintNamesTheControlsOfEachPlatform()
        {
            string desktop = DesktopControls.MenuHint(true), phone = DesktopControls.MenuHint(false);
            StringAssert.Contains("WASD", desktop);
            StringAssert.Contains("Q / E", desktop);
            StringAssert.Contains("two-finger swipe", desktop);
            StringAssert.Contains("Twist", desktop);
            StringAssert.DoesNotContain("Pinch / scroll", desktop, "on a Mac trackpad a two-finger scroll pans, it does not zoom");
            StringAssert.Contains("Pinch / scroll", phone);
            StringAssert.Contains("Build tracks near an industry", desktop);
            StringAssert.Contains("Build tracks near an industry", phone);
        }

        [Test]
        public void LeavingFullScreenOpensAWindowSmallerThanTheDisplay()
        {
            var window = DesktopControls.WindowSize(3024, 1964);
            Assert.That(window, Is.EqualTo(new Vector2Int(2419, 1571)));
        }
    }
}
