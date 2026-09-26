using System.Runtime.InteropServices;
using UnityEngine;
namespace ValleyRail
{
    /// <summary>Trackpad input summed by the macOS plugin since the previous frame. Layout matches VRTrackpadFrame in Native/macOS/ValleyTrackpad.m.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TrackpadFrame
    {
        /// <summary>Pinch magnification, positive when the fingers spread.</summary>
        public float magnify;
        /// <summary>Two-finger twist in degrees, positive anticlockwise.</summary>
        public float rotate;
        /// <summary>Two-finger swipe (or Magic Mouse) scroll in screen pixels, as AppKit reports it.</summary>
        public float panX, panY;
        /// <summary>Line-based mouse-wheel scroll; positive rolls away from the player.</summary>
        public float wheel;
        /// <summary>Non-zero when a twist gesture ended.</summary>
        public int rotateEnded;
        /// <summary>Non-zero when any precise (swipe) scroll arrived.</summary>
        public int precise;
    }

    /// <summary>
    /// Reads the macOS trackpad through Assets/Plugins/macOS/ValleyTrackpad.bundle (built by Tools/build_trackpad_plugin.sh).
    /// Unity's Input System sees a two-finger swipe only as a mouse wheel and never sees pinch or rotate.
    /// </summary>
    public static class MacTrackpad
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("ValleyTrackpad")]
        static extern int VRTrackpadRead(out TrackpadFrame frame);
        static bool unavailable;
#endif
        /// <summary>Hands over this frame's gestures. False outside the macOS player or if the plugin could not load.</summary>
        public static bool Read(out TrackpadFrame frame)
        {
            frame = default;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (unavailable)
                return false;
            try
            {
                return VRTrackpadRead(out frame) != 0;
            }
            catch (System.Exception e) when (e is System.DllNotFoundException || e is System.EntryPointNotFoundException)
            {
                unavailable = true;
                Debug.LogWarning("Trackpad plugin unavailable, so pinch and twist are off: " + e.Message);
                return false;
            }
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Turns a two-finger twist into at most one quarter turn of the view per gesture, once the fingers have turned
    /// far enough that a pinch's small wobble never counts.
    /// </summary>
    public sealed class TwistGesture
    {
        /// <summary>Degrees the fingers must turn before the view follows.</summary>
        public const float TurnDegrees = 30;
        float twist;
        bool turned;
        /// <summary>Feeds this frame's twist. Returns 1 for an anticlockwise quarter turn, -1 for clockwise, else 0.</summary>
        public int Step(float degrees, bool ended)
        {
            int turn = 0;
            if (!turned)
            {
                twist += degrees;
                if (Mathf.Abs(twist) >= TurnDegrees)
                {
                    turn = twist > 0 ? 1 : -1;
                    turned = true;
                }
            }
            if (ended)
            {
                twist = 0;
                turned = false;
            }
            return turn;
        }
    }
}
