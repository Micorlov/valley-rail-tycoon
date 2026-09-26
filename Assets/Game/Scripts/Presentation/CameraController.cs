using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using ValleyRail.Core;
namespace ValleyRail
{
    public sealed class CameraController : MonoBehaviour
    {
        public Camera view; public GameBootstrap app; public Vector3 focus = new Vector3(31, 0, 29); public float zoom = 29;
        bool down, blocked, multi; Vector2 begin, last; float pinch; Vector2 midpoint;
        // Right- or middle-button drag (desktop): pans in every mode, so the left button stays free to draw track.
        bool panning, panBlocked; Vector2 panBegin, panLast;
        readonly TwistGesture twist = new TwistGesture();
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        // Movement beyond this many pixels turns a tap into a drag.
        const float TapSlop = 16;
        /// <summary>Orthographic half-heights the view spans: close enough for a single car to fill a good part of the screen, far enough for the whole map.</summary>
        public const float MinZoom = 1.2f, MaxZoom = 82;
        /// <summary>True while a finger (or mouse) that started on the map is dragging, drawing track or pinching; the HUD fades meanwhile.</summary>
        public bool Gesturing { get; private set; }
        // The isometric view looks along one of four diagonals; each turn swings it 90° clockwise around the focus point.
        const float BaseYaw = 45, Pitch = 35.264f, TurnDegreesPerSecond = 90 / .3f;
        float yaw = BaseYaw;
        /// <summary>View direction in clockwise quarter turns, 0-3.</summary>
        public int Turn { get; private set; }
        /// <summary>Starts an animated quarter turn clockwise.</summary>
        public void RotateView() => Turn = (Turn + 1) & 3;
        /// <summary>Starts an animated quarter turn anticlockwise (desktop Q key).</summary>
        public void RotateViewBack() => Turn = (Turn + 3) & 3;
        /// <summary>Faces a direction at once, e.g. when a saved game loads.</summary>
        public void SetTurn(int turn)
        {
            Turn = turn & 3;
            yaw = BaseYaw + 90 * Turn;
        }
        public bool OverUI(Vector2 pos)
        {
            if (!EventSystem.current)
                return false;
            var data = new PointerEventData(EventSystem.current) { position = pos };
            hits.Clear();
            EventSystem.current.RaycastAll(data, hits);
            return hits.Count > 0;
        }
        bool legacyInputUnavailable;
        void LateUpdate()
        {
            // Back must work while the menu is open, so it is read before the world-input guard.
            if (BackPressed())
                app.Back();
            if (FullScreenPressed())
                DesktopControls.ToggleFullScreen();
            if (!app.InMenu && !app.MenuOpen)
            {
                ReadInput();
                ReadKeys();
            }
            else
            {
                // A press interrupted by a menu must not complete as a map tap after the menu closes.
                down = multi = blocked = panning = false;
                Gesturing = false;
            }
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            focus.x = Mathf.Clamp(focus.x, 0, MapDefinition.Size - 1);
            focus.z = Mathf.Clamp(focus.z, 0, MapDefinition.Size - 1);
            view.orthographicSize = zoom;
            yaw = Mathf.MoveTowardsAngle(yaw, BaseYaw + 90 * Turn, TurnDegreesPerSecond * Time.unscaledDeltaTime);
            view.transform.rotation = Quaternion.Euler(Pitch, yaw, 0);
            if (app.World)
                app.World.FaceCamera(yaw);
            view.transform.position = focus - view.transform.forward * 180;
        }
        public bool Ground(Vector2 screen, out Cell cell)
        {
            var ray = view.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out var hit, 400f))
            {
                cell = new Cell(Mathf.RoundToInt(hit.point.x), Mathf.RoundToInt(hit.point.z));
                return MapDefinition.InBounds(cell);
            }
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 p = ray.GetPoint(enter);
                cell = new Cell(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
                return MapDefinition.InBounds(cell);
            }
            cell = default;
            return false;
        }
        void Pan(Vector2 delta)
        {
            Vector3 right = view.transform.right;
            Vector3 forward = Vector3.Cross(right, Vector3.up);
            focus -= (right * delta.x + forward * delta.y) * (zoom * 2 / Screen.height);
        }
        void ReadInput()
        {
            var touch = Touchscreen.current;
            int count = 0;
            Vector2 p0 = default, p1 = default;
            if (touch != null)
                foreach (var t in touch.touches)
                    if (t.isInProgress)
                    {
                        if (count == 0)
                            p0 = t.position.ReadValue();
                        else if (count == 1)
                            p1 = t.position.ReadValue();
                        count++;
                    }
            if (count >= 2)
            {
                float distance = Vector2.Distance(p0, p1);
                Vector2 mid = (p0 + p1) * .5f;
                if (!multi)
                {
                    blocked = OverUI(p0) || OverUI(p1);
                    app.CancelPreview();
                }
                else if (!blocked)
                {
                    zoom *= pinch / Mathf.Max(1, distance);
                    Pan(mid - midpoint);
                }
                multi = true;
                down = false;
                pinch = distance;
                midpoint = mid;
                Gesturing = !blocked;
                return;
            }
            if (multi)
            {
                if (count == 0)
                {
                    multi = false;
                    blocked = false;
                    Gesturing = false;
                }
                return;
            }
            bool pressed = count == 1;
            Vector2 position = p0;
            if (count == 0 && Mouse.current != null)
            {
                var mouse = Mouse.current;
                pressed = mouse.leftButton.isPressed;
                position = mouse.position.ReadValue();
                // On a Mac the trackpad plugin tells a two-finger swipe (pan) from a mouse wheel (zoom) and adds pinch and twist.
                if (MacTrackpad.Read(out var pad))
                    ReadTrackpad(pad, position);
                else if (!OverUI(position))
                    zoom = DesktopControls.WheelZoom(zoom, mouse.scroll.ReadValue().y);
                // A trackpad tap can press and release between two frames, where isPressed never sees it. Count it as held
                // for this frame so it completes as a tap on the next. (Read every frame: the first read starts the tracking.)
                if (mouse.leftButton.wasPressedThisFrame && !pressed && !down)
                    pressed = true;
                DragPan(mouse, position);
            }
            if (pressed && !down)
            {
                down = true;
                blocked = OverUI(position);
                begin = last = position;
                // Touching the map puts the build tray away, like any popup.
                if (!blocked)
                    app.UI.CloseTray();
                if (!blocked && app.Mode == ToolMode.Track && Ground(position, out var start))
                    app.BeginTrack(start);
            }
            else if (pressed && down && !blocked)
            {
                Vector2 delta = position - last;
                if (Vector2.Distance(begin, position) >= TapSlop)
                    Gesturing = true;
                // One finger drags the camera in every mode except Track, where a drag draws the line.
                if (app.Mode != ToolMode.Track)
                    Pan(delta);
                else if (app.Mode == ToolMode.Track && Vector2.Distance(begin, position) > 12 && Ground(position, out var end))
                    app.ExtendTrack(end);
                last = position;
            }
            else if (!pressed && down)
            {
                down = false;
                Gesturing = false;
                if (!blocked && Ground(last, out var c))
                {
                    if (app.Mode == ToolMode.Track)
                        app.ExtendTrack(c);
                    else if (Vector2.Distance(begin, last) < TapSlop)
                        app.Tap(c, view.ScreenPointToRay(last));
                }
                blocked = false;
            }
        }
        // Mac trackpad: pinch zooms and a twist turns the view anywhere. Over the map a two-finger swipe pans (Cmd + swipe
        // zooms) and the wheel zooms; over a panel both belong to the UI, which scrolls its lists from the same events.
        void ReadTrackpad(TrackpadFrame pad, Vector2 position)
        {
            zoom = DesktopControls.PinchZoom(zoom, pad.magnify);
            // An anticlockwise twist turns the camera clockwise, which spins the map anticlockwise under the fingers.
            int turn = twist.Step(pad.rotate, pad.rotateEnded != 0);
            if (turn > 0)
                RotateView();
            else if (turn < 0)
                RotateViewBack();
            if (OverUI(position))
                return;
            zoom = DesktopControls.WheelZoom(zoom, pad.wheel);
            if (pad.precise == 0)
                return;
            var keys = Keyboard.current;
            if (keys != null && (keys.leftCommandKey.isPressed || keys.rightCommandKey.isPressed))
                zoom = DesktopControls.CommandSwipeZoom(zoom, pad.panY);
            else
                Pan(DesktopControls.SwipeDrag(pad.panX, pad.panY));
        }
        void DragPan(Mouse mouse, Vector2 position)
        {
            bool held = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            if (!held)
            {
                if (panning && !down)
                    Gesturing = false;
                panning = false;
                return;
            }
            if (!panning)
            {
                panning = true;
                panBlocked = OverUI(position);
                panBegin = panLast = position;
                if (!panBlocked)
                    app.UI.CloseTray();
                return;
            }
            if (panBlocked)
                return;
            Pan(position - panLast);
            panLast = position;
            if (Vector2.Distance(panBegin, position) >= TapSlop)
                Gesturing = true;
        }
        // Desktop keys: WASD / arrows pan (Shift faster), + / - zoom, Q / E turn the view, Space pauses, 1 / 2 / 3 set the speed.
        // Keys held with Cmd or Ctrl belong to the system (Cmd+Q quits), so they are left alone.
        void ReadKeys()
        {
            var keys = Keyboard.current;
            if (keys == null || keys.ctrlKey.isPressed || keys.leftCommandKey.isPressed || keys.rightCommandKey.isPressed)
                return;
            float dt = Time.unscaledDeltaTime;
            Pan(DesktopControls.KeyPanDrag(keys.aKey.isPressed || keys.leftArrowKey.isPressed, keys.dKey.isPressed || keys.rightArrowKey.isPressed,
                keys.wKey.isPressed || keys.upArrowKey.isPressed, keys.sKey.isPressed || keys.downArrowKey.isPressed, keys.shiftKey.isPressed, Screen.height, dt));
            int zoomIn = (keys.equalsKey.isPressed || keys.numpadPlusKey.isPressed ? 1 : 0) - (keys.minusKey.isPressed || keys.numpadMinusKey.isPressed ? 1 : 0);
            if (zoomIn != 0)
                zoom = DesktopControls.KeyZoom(zoom, zoomIn, dt);
            if (keys.eKey.wasPressedThisFrame)
                RotateView();
            if (keys.qKey.wasPressedThisFrame)
                RotateViewBack();
            if (keys.spaceKey.wasPressedThisFrame)
                app.UI.TogglePause();
            if (keys.digit1Key.wasPressedThisFrame)
                app.SetSpeed(1);
            if (keys.digit2Key.wasPressedThisFrame)
                app.SetSpeed(2);
            if (keys.digit3Key.wasPressedThisFrame)
                app.SetSpeed(4);
        }
        // Ctrl+Cmd+F is the macOS full-screen shortcut; F11 is the Windows and Linux one. Works in menus too.
        static bool FullScreenPressed()
        {
            var keys = Keyboard.current;
            if (keys == null)
                return false;
            bool command = keys.leftCommandKey.isPressed || keys.rightCommandKey.isPressed;
            return keys.f11Key.wasPressedThisFrame || (command && keys.ctrlKey.isPressed && keys.fKey.wasPressedThisFrame);
        }
        // Android's Back button arrives as Escape. The Input System keyboard device may not exist until the first key
        // event on some phones, so the legacy manager is consulted as a fallback (project input handling is set to Both).
        bool BackPressed()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
            if (legacyInputUnavailable)
                return false;
            try
            {
                return Input.GetKeyDown(KeyCode.Escape);
            }
            catch (System.InvalidOperationException)
            {
                legacyInputUnavailable = true;
                return false;
            }
        }
    }
}
