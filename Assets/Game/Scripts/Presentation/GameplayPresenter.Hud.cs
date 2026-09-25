using UnityEngine;
using TMPro;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// The play HUD keeps to the corners so the map stays visible: money top-left, pause/speed/menu top-right and
    /// a bottom-right build button whose tray opens only on demand. Notices are toasts that fade, and the whole HUD
    /// dims while a finger moves the map.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        public const float NoticeSeconds = 4f;
        const float NoticeFade = .5f, GestureAlpha = .12f, FadeRate = 6f;
        const float Margin = 20, Gap = 10, MoneyWidth = 440, TopRow = 104, IconSize = 96, ToolHeight = 110, MinToastWidth = 480;
        static readonly string[] trayNames = { "TRACK", "STATION", "BULLDOZE", "LIGHT RAIL" };
        RectTransform hud, controls, tools, view, guidance, pauseButton, speedButton, stationsButton, trainsButton, buildButton, activeTool, doneButton, closeTray;
        readonly RectTransform[] trayButtons = new RectTransform[4];
        TMP_Text money, stats, hint;
        CanvasGroup[] fadeGroups;
        CanvasGroup guidanceGroup;
        bool trayOpen, shownLive;
        int runSpeed = 1, shownNotice = -1, toolsLayout = -1;
        float noticeShownAt = float.NegativeInfinity, hudAlpha = 1, toastWidth;
        string shownTutorial;
        byte shownLineAlpha = 255;

        void BuildHud()
        {
            hud = Panel("HUD", safe, new Color(navy.r, navy.g, navy.b, .85f));
            Place(hud, 0, 1, 0, 1, Margin, -Margin - TopRow, Margin + MoneyWidth, -Margin);
            money = Text(hud, "$0", new Vector2(18, -8), new Vector2(MoneyWidth - 36, 43), 30, cream);
            stats = Text(hud, "", new Vector2(18, -60), new Vector2(MoneyWidth - 36, 30), 18, muted);

            controls = Panel("Controls", safe, Color.clear);
            controls.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Place(controls, 1, 1, 1, 1, -Margin - 3 * IconSize - 2 * Gap, -Margin - IconSize, -Margin, -Margin);
            pauseButton = IconButton(controls, "PAUSE", "II", TogglePause, 0);
            speedButton = IconButton(controls, "SPEED", "1x", CycleSpeed, 1);
            IconButton(controls, "MENU", "≡", () => ShowMenu(false), 2);

            tools = Panel("Tools", safe, Color.clear);
            tools.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Place(tools, 0, 0, 1, 0, Margin, Margin, -Margin, Margin + ToolHeight);
            stationsButton = Button(tools, "STATIONS", StationList, Vector2.zero, new Vector2(200, ToolHeight));
            trainsButton = Button(tools, "TRAINS", () => app.ChooseTool(ToolMode.Trains), Vector2.zero, new Vector2(170, ToolHeight));
            buildButton = Button(tools, "BUILD", () => trayOpen = true, Vector2.zero, new Vector2(200, ToolHeight), true);
            for (int i = 0; i < trayButtons.Length; i++)
            {
                var mode = TrayMode(i);
                trayButtons[i] = Button(tools, trayNames[i], () => { trayOpen = false; app.ChooseTool(mode); }, Vector2.zero, new Vector2(180, ToolHeight));
            }
            closeTray = Named(Button(tools, "×", () => trayOpen = false, Vector2.zero, new Vector2(ToolHeight, ToolHeight)), "CLOSE TRAY");
            Label(closeTray).fontSize = 40;
            activeTool = Named(Button(tools, "● TRACK", () => trayOpen = true, Vector2.zero, new Vector2(220, ToolHeight), true), "ACTIVE TOOL");
            doneButton = Named(Button(tools, "× DONE", () => app.ChooseTool(ToolMode.Browse), Vector2.zero, new Vector2(160, ToolHeight)), "DONE");
            BuildTrackRepairButton();

            // Bottom-left: turn the map a quarter turn. The UI font has no rotate arrow, so the icon is drawn.
            view = Panel("View", safe, Color.clear);
            view.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Place(view, 0, 0, 0, 0, Margin, Margin, Margin + ToolHeight, Margin + ToolHeight);
            var rotate = Named(Button(view, "", () => app.Camera.RotateView(), Vector2.zero, new Vector2(ToolHeight, ToolHeight)), "ROTATE");
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            icon.transform.SetParent(rotate, false);
            Place(icon.GetComponent<RectTransform>(), 0, 0, 1, 1, 18, 18, -18, -18);
            var picture = icon.GetComponent<UnityEngine.UI.RawImage>();
            picture.texture = RotateIcon();
            picture.color = cream;
            picture.raycastTarget = false;

            // Toast: never a touch target, so a notice can't swallow a tap meant for the map.
            guidance = Panel("Guidance", safe, new Color(navy.r, navy.g, navy.b, .88f));
            guidance.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            guidance.anchorMin = guidance.anchorMax = guidance.pivot = new Vector2(.5f, 1);
            hint = Text(guidance, "", new Vector2(18, -10), new Vector2(400, 30), 20, cream);
            guidance.gameObject.SetActive(false);
        }
        /// <summary>Called once the details panel exists, so it dims with the rest of the HUD.</summary>
        void BuildFade()
        {
            fadeGroups = new[] { Group(hud), Group(controls), Group(tools), Group(view), Group(context) };
            guidanceGroup = Group(guidance);
            RefreshTools();
        }
        static CanvasGroup Group(RectTransform r) => r.gameObject.AddComponent<CanvasGroup>();
        static RectTransform Named(RectTransform r, string name)
        {
            r.name = name;
            return r;
        }
        static TMP_Text Label(RectTransform button) => button.GetComponentInChildren<TMP_Text>();
        static ToolMode TrayMode(int index) => index == 0 ? ToolMode.Track : index == 1 ? ToolMode.Station : index == 2 ? ToolMode.Bulldoze : ToolMode.LightRail;
        static int TrayIndex(ToolMode mode) => mode == ToolMode.Track ? 0 : mode == ToolMode.Station ? 1 : mode == ToolMode.Bulldoze ? 2 : mode == ToolMode.LightRail ? 3 : -1;
        RectTransform IconButton(RectTransform parent, string name, string icon, System.Action action, int slot)
        {
            var b = Named(Button(parent, icon, action, new Vector2(slot * (IconSize + Gap), 0), new Vector2(IconSize, IconSize)), name);
            Label(b).fontSize = 34;
            return b;
        }

        void LateUpdate()
        {
            SafeArea();
            if (!hint)
                return;
            bool playing = !app.InMenu && !app.MenuOpen;
            bool gesturing = playing && app.Camera && app.Camera.Gesturing;
            if (gesturing)
                trayOpen = false;
            hudAlpha = Mathf.MoveTowards(hudAlpha, gesturing ? GestureAlpha : 1, Time.unscaledDeltaTime * FadeRate);
            foreach (var group in fadeGroups)
                group.alpha = hudAlpha;
            RefreshTools();
            RefreshGuidance(playing);
        }
        void RefreshHud()
        {
            var w = app.Game.World;
            money.text = "$" + w.money.ToString("N0");
            EconomyService.Recent(w, out int income, out int expense);
            stats.text = $"+${income:N0}  -${expense:N0}/min  ·  {w.trains.Count} train{(w.trains.Count == 1 ? "" : "s")}";
            if (w.speed > 0)
                runSpeed = w.speed;
            bool paused = w.speed == 0;
            Label(pauseButton).text = paused ? "►" : "II";
            Highlight(pauseButton, paused);
            Label(speedButton).text = runSpeed + "x";
            RefreshStationRows();
        }
        static int NextSpeed(int speed) => speed == 1 ? 2 : speed == 2 ? 4 : 1;
        void TogglePause()
        {
            int speed = app.Game.World.speed;
            if (speed > 0)
                runSpeed = speed;
            app.SetSpeed(speed == 0 ? runSpeed : 0);
        }
        void CycleSpeed()
        {
            int speed = app.Game.World.speed;
            runSpeed = NextSpeed(speed == 0 ? runSpeed : speed);
            app.SetSpeed(runSpeed);
        }
        /// <summary>Closes the build tray if it is open; returns whether it was.</summary>
        public bool CloseTray()
        {
            bool wasOpen = trayOpen;
            trayOpen = false;
            return wasOpen;
        }
        void ShowPlayHud(bool visible)
        {
            trayOpen = false;
            hud.gameObject.SetActive(visible);
            controls.gameObject.SetActive(visible);
            view.gameObject.SetActive(visible);
            RefreshTools();
        }
        static Texture2D rotateIcon;
        /// <summary>A white clockwise arrow (↻) on transparent pixels, drawn once and tinted by the RawImage.</summary>
        static Texture2D RotateIcon()
        {
            if (rotateIcon)
                return rotateIcon;
            const int size = 96;
            const float radius = 30, halfWidth = 5.5f;
            var centre = new Vector2(size / 2f, size / 2f - 4);
            // Arrowhead at the top of the ring, pointing right (clockwise) into the gap between 30° and 90°.
            Vector2 tip = centre + new Vector2(17, radius), upper = centre + new Vector2(-3, radius + 14), lower = centre + new Vector2(-3, radius - 14);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    var offset = p - centre;
                    float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                    float ring = angle > 30 && angle < 90 ? 0 : Mathf.Clamp01(halfWidth + .5f - Mathf.Abs(offset.magnitude - radius));
                    float head = Mathf.Clamp01(Mathf.Min(Edge(tip, lower, p), Mathf.Min(Edge(lower, upper, p), Edge(upper, tip, p))) + .5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Max(ring, head)));
                }
            rotateIcon = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            rotateIcon.SetPixels32(pixels);
            rotateIcon.Apply();
            return rotateIcon;
        }
        // Signed distance from p to the line a→b; positive on the right-hand side (inside a clockwise triangle).
        static float Edge(Vector2 a, Vector2 b, Vector2 p)
        {
            var d = (b - a).normalized;
            return d.y * (p.x - a.x) - d.x * (p.y - a.y);
        }
        /// <summary>Shows exactly one bottom-right row: idle (Stations, Trains, Build), the open tray, or the active tool with Done.</summary>
        void RefreshTools()
        {
            int tool = TrayIndex(app.Mode);
            bool show = !app.InMenu && !app.MenuOpen && hud.gameObject.activeSelf && !context.gameObject.activeSelf;
            int layout = !show ? 0 : trayOpen ? 10 + tool : tool >= 0 ? 20 + tool : 1;
            if (layout == toolsLayout)
                return;
            toolsLayout = layout;
            tools.gameObject.SetActive(show);
            if (!show)
                return;
            if (trayOpen)
            {
                for (int i = 0; i < trayButtons.Length; i++)
                    Highlight(trayButtons[i], i == tool);
                ShowRow(trayButtons[0], trayButtons[1], trayButtons[2], trayButtons[3], closeTray);
            }
            else if (tool >= 0)
            {
                Label(activeTool).text = "● " + trayNames[tool];
                if (app.Mode == ToolMode.Track)
                    ShowRow(aiTrackFix, activeTool, doneButton);
                else
                    ShowRow(activeTool, doneButton);
            }
            else
                ShowRow(stationsButton, trainsButton, buildButton);
        }
        void ShowRow(params RectTransform[] row)
        {
            foreach (Transform child in tools)
                child.gameObject.SetActive(System.Array.IndexOf(row, child as RectTransform) >= 0);
            float x = 0;
            for (int i = row.Length - 1; i >= 0; i--)
            {
                var r = row[i];
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(1, 0);
                r.anchoredPosition = new Vector2(-x, 0);
                x += r.sizeDelta.x + Gap;
            }
        }
        /// <summary>Tutorial step stays up while hints are on; each new notice shows for a few seconds, then fades.</summary>
        void RefreshGuidance(bool playing)
        {
            bool fresh = app.NoticeVersion != shownNotice;
            if (fresh)
            {
                shownNotice = app.NoticeVersion;
                noticeShownAt = Time.unscaledTime;
            }
            float age = Time.unscaledTime - noticeShownAt;
            bool live = age < NoticeSeconds && !string.IsNullOrEmpty(app.Notice);
            string tutorial = app.TutorialText;
            bool show = playing && (tutorial != null || live);
            if (guidance.gameObject.activeSelf != show)
                guidance.gameObject.SetActive(show);
            if (!show)
                return;
            float fade = Mathf.Clamp01((NoticeSeconds - age) / NoticeFade);
            // Under a tutorial line the box stays up, so only the notice line fades (a rich-text alpha tag).
            byte lineAlpha = tutorial != null && live ? (byte)(255 * fade) : (byte)255;
            if (fresh || live != shownLive || lineAlpha != shownLineAlpha || !ReferenceEquals(tutorial, shownTutorial) || !Mathf.Approximately(toastWidth, safe.rect.width))
            {
                shownLive = live;
                shownTutorial = tutorial;
                shownLineAlpha = lineAlpha;
                LayoutGuidance(tutorial == null ? app.Notice : live ? $"{tutorial}\n<alpha=#{lineAlpha:X2}>{app.Notice}" : tutorial);
            }
            guidanceGroup.alpha = hudAlpha * (tutorial != null ? 1 : fade);
        }
        void LayoutGuidance(string text)
        {
            toastWidth = safe.rect.width;
            float left = Margin + MoneyWidth + Gap, right = toastWidth - Margin - 3 * IconSize - 3 * Gap;
            bool below = right - left < MinToastWidth;
            float maxWidth = below ? Mathf.Min(720, toastWidth - 2 * Margin) : Mathf.Min(900, right - left);
            hint.text = text;
            // Hug the text so a short notice covers as little map as possible.
            float boxWidth = Mathf.Min(maxWidth, hint.GetPreferredValues(text).x + 36);
            float textWidth = boxWidth - 36;
            float textHeight = hint.GetPreferredValues(text, textWidth, 0).y;
            hint.rectTransform.sizeDelta = new Vector2(textWidth, textHeight);
            guidance.sizeDelta = new Vector2(boxWidth, textHeight + 20);
            if (below)
            {
                guidance.anchorMin = guidance.anchorMax = guidance.pivot = new Vector2(0, 1);
                guidance.anchoredPosition = new Vector2(Margin, -Margin - TopRow - Gap);
            }
            else
            {
                guidance.anchorMin = guidance.anchorMax = guidance.pivot = new Vector2(.5f, 1);
                guidance.anchoredPosition = new Vector2((left + right) / 2 - toastWidth / 2, -Margin);
            }
        }
    }
}
