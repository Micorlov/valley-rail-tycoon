using UnityEngine;
namespace ValleyRail
{
    /// <summary>
    /// Mouse, trackpad and keyboard play for the desktop (macOS) build. The phone plays by touch; on a Mac the left button
    /// (or a trackpad tap) taps and drags the map, the right or middle button pans in every mode (so the left can draw
    /// track), the wheel zooms, and on a trackpad a two-finger swipe pans, a pinch zooms, Cmd + swipe zooms and a twist
    /// turns the view (see MacTrackpad). The keyboard pans (WASD / arrows, Shift for faster), zooms (+ / -), turns the
    /// view (Q / E), pauses (Space), picks a speed (1 / 2 / 3) and toggles full screen (Ctrl+Cmd+F or F11).
    /// The maths lives here as pure functions so EditMode tests can check it without input devices.
    /// </summary>
    public static class DesktopControls
    {
        /// <summary>Zoom change per wheel notch. The Input System reports about ±1 per notch on every platform.</summary>
        public const float WheelZoomPerNotch = .12f;
        /// <summary>Most scroll honoured in one frame, so a flung trackpad or a burst of wheel events stays controllable.</summary>
        public const float MaxNotchesPerFrame = 4;
        /// <summary>Keyboard pan speed in screen heights per second; holding Shift multiplies it.</summary>
        public const float KeyPanScreensPerSecond = 1, FastPanFactor = 2.5f;
        /// <summary>Held + / - keys scale the zoom by e^rate per second.</summary>
        public const float KeyZoomRate = 1.8f;
        /// <summary>Leaving full screen opens a window this fraction of the display in each direction.</summary>
        public const float WindowFraction = .8f;
        /// <summary>Desktop players redraw at this rate; phones stay at 30 to save battery.</summary>
        public const int FrameRate = 60;

        public static bool IsDesktopPlayer(RuntimePlatform platform) =>
            platform == RuntimePlatform.OSXPlayer || platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.LinuxPlayer;
        /// <summary>True in a built Mac, Windows or Linux player. False on phones and in the editor, where tests expect the phone layout.</summary>
        public static bool IsDesktop => IsDesktopPlayer(Application.platform);

        /// <summary>Wheel zoom is multiplicative, so one notch feels the same close up and far out. Positive scroll zooms in.</summary>
        public static float WheelZoom(float zoom, float scroll) =>
            zoom * Mathf.Exp(-Mathf.Clamp(scroll, -MaxNotchesPerFrame, MaxNotchesPerFrame) * WheelZoomPerNotch);
        /// <summary>Pinch zoom follows the fingers: spreading them by a magnification of m zooms in by e^m.</summary>
        public static float PinchZoom(float zoom, float magnification) => zoom * Mathf.Exp(-magnification);
        /// <summary>
        /// A two-finger swipe drags the map. AppKit's scroll delta points the way the content should move, already
        /// following the player's natural-scrolling setting, with y pointing down; Unity's screen y points up.
        /// </summary>
        public static Vector2 SwipeDrag(float scrollX, float scrollY) => new Vector2(scrollX, -scrollY);
        /// <summary>Cmd + two-finger swipe zooms per swiped pixel, for a Magic Mouse, which cannot pinch.</summary>
        public const float CommandSwipeZoomPerPixel = .004f;
        /// <summary>Cmd + swipe: content moving up (fingers up with natural scrolling) zooms in.</summary>
        public static float CommandSwipeZoom(float zoom, float scrollY) => zoom * Mathf.Exp(scrollY * CommandSwipeZoomPerPixel);
        /// <summary>Zoom after holding + (direction 1) or - (direction -1) for the given seconds.</summary>
        public static float KeyZoom(float zoom, int direction, float seconds) => zoom * Mathf.Exp(-direction * KeyZoomRate * seconds);
        /// <summary>
        /// The screen drag, in pixels, that the held pan keys stand for. Up moves the view up the map, which is the same as
        /// dragging the map down, so the drag points against the key. Diagonals are no faster than straight lines.
        /// </summary>
        public static Vector2 KeyPanDrag(bool left, bool right, bool up, bool down, bool fast, float screenHeight, float seconds)
        {
            var direction = new Vector2((right ? 1 : 0) - (left ? 1 : 0), (up ? 1 : 0) - (down ? 1 : 0));
            if (direction == Vector2.zero)
                return Vector2.zero;
            return -direction.normalized * (KeyPanScreensPerSecond * (fast ? FastPanFactor : 1) * screenHeight * seconds);
        }
        /// <summary>The control line under the title and pause menus.</summary>
        public static string MenuHint(bool desktop) => desktop
            ? "Drag, WASD or two-finger swipe to move · Pinch to zoom\nTwist or Q / E to turn · Wheel zooms · Space pauses\nBuild tracks near an industry, then add a station."
            : "Drag to explore · Pinch / scroll to zoom\nBuild tracks near an industry, then add a station.";
        /// <summary>Window size when leaving full screen on a display of the given size.</summary>
        public static Vector2Int WindowSize(int displayWidth, int displayHeight) =>
            new Vector2Int(Mathf.RoundToInt(displayWidth * WindowFraction), Mathf.RoundToInt(displayHeight * WindowFraction));
        /// <summary>Switches between borderless full screen at the display's resolution and a centred resizable window.</summary>
        public static void ToggleFullScreen()
        {
            var display = Screen.currentResolution;
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
                return;
            }
            var window = WindowSize(display.width, display.height);
            Screen.SetResolution(window.x, window.y, FullScreenMode.Windowed);
        }
    }
}
