using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ValleyRail.Core;
namespace ValleyRail
{
    public enum ToolMode
    {
        Browse, Track, Station, Trains, Bulldoze, LightRail
    }
    public sealed class GameBootstrap : MonoBehaviour
    {
        public GameSession Game
        {
            get; private set;
        }
        public WorldView World
        {
            get; private set;
        }
        public GameplayPresenter UI
        {
            get; private set;
        }
        public CameraController Camera
        {
            get; private set;
        }
        public ToolMode Mode
        {
            get; private set;
        }
        public SoundEffects Sfx
        {
            get; private set;
        }
        public bool InMenu
        {
            get; private set;
        }
        public bool MenuOpen; public bool SoundEnabled { get; private set; } = true; public bool MusicEnabled { get; private set; } = true; public bool HintsEnabled { get; private set; } = true;
        /// <summary>Current guided-step line for the HUD, or null when the guide is finished or hints are switched off.</summary>
        public string TutorialText => HintsEnabled && Game != null ? Tutorial.Text(Game.World) : null;
        string notice = "Build tracks between the mine and power plant through the southern bridge site.";
        /// <summary>Latest one-line message for the HUD toast. Every assignment bumps <see cref="NoticeVersion"/>, so a repeated message shows again.</summary>
        public string Notice
        {
            get => notice;
            set { notice = value; NoticeVersion++; }
        }
        public int NoticeVersion { get; private set; }
        readonly SimulationClock clock = new SimulationClock(); SaveService saves; Balance balance; Cell? anchor; Cell? lastEnd; BuildPlan preview; int bridgeChoice = -1; float autosave, hud; long savedTick = -1; int savedRevision = -1, shownCityRevision = -1; bool dirty;
        Action tickAction; AudioSource audioSource; AudioClip click; static GameBootstrap active;
        // Development builds log frame and simulation timing every 30 s so Tools/soak.sh can measure the release gates on a device.
        float perfWindow; int perfFrames, perfTicks, perfGcSeen; double simTotalMs, simMaxMs; readonly System.Diagnostics.Stopwatch simWatch = new System.Diagnostics.Stopwatch();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartGame()
        {
            if (Application.isBatchMode)
                return;
            if (!FindFirstObjectByType<GameBootstrap>())
                new GameObject("Game Bootstrap").AddComponent<GameBootstrap>();
        }
        void Awake()
        {
            if (active && active != this)
            {
                Destroy(gameObject);
                return;
            }
            active = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 30;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            var config = Resources.Load<GameBalance>("GameBalance");
            balance = config ? config.values : new Balance();
            saves = new SaveService(Application.isBatchMode ? System.IO.Path.Combine(Application.temporaryCachePath, "ValleyRailTestSaves") : Application.persistentDataPath, new JsonSnapshotCodec(), balance);
            SoundEnabled = PlayerPrefs.GetInt("sound", 1) == 1;
            MusicEnabled = PlayerPrefs.GetInt("music", 1) == 1;
            HintsEnabled = PlayerPrefs.GetInt("hints", 1) == 1;
            audioSource = gameObject.AddComponent<AudioSource>();
            click = AudioClip.Create("Soft UI click", 1600, 1, 16000, false);
            float[] data = new float[1600];
            for (int i = 0; i < data.Length; i++)
                data[i] = Mathf.Sin(i * .18f) * Mathf.Exp(-i / 220f) * .07f;
            click.SetData(data, 0);
            gameObject.AddComponent<ThemeMusic>().app = this;
            Sfx = gameObject.AddComponent<SoundEffects>();
            Sfx.app = this;
            BuildSession(WorldState.New(balance));
            Title();
        }
        public void BuildSession(WorldState state)
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            Game = new GameSession(state, balance);
            tickAction = Game.Step;
            clock.Reset();
            anchor = lastEnd = null;
            bridgeChoice = -1;
            preview = null;
            Mode = ToolMode.Browse;
            autosave = 0;
            dirty = true;
            shownCityRevision = state.cityRevision;
            var world = new GameObject("World");
            world.transform.SetParent(transform);
            World = world.AddComponent<WorldView>();
            World.Initialize(Game);
            var rig = new GameObject("Isometric Camera");
            rig.transform.SetParent(transform);
            var camera = rig.AddComponent<Camera>();
            camera.orthographic = true;
            camera.backgroundColor = new Color(.62f, .75f, .74f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 420;
            camera.tag = "MainCamera";
            rig.AddComponent<AudioListener>();
            Camera = rig.AddComponent<CameraController>();
            Camera.view = camera;
            Camera.app = this;
            Camera.focus = new Vector3(state.cameraX, 0, state.cameraZ);
            Camera.zoom = state.zoom;
            Camera.SetTurn(state.cameraTurn);
            var light = new GameObject("Sun");
            light.transform.SetParent(transform);
            var sun = light.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1, .94f, .82f);
            sun.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -35, 0);
            RenderSettings.ambientLight = new Color(.62f, .7f, .74f);
            var events = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform);
            var ui = new GameObject("UI");
            ui.transform.SetParent(transform);
            UI = ui.AddComponent<GameplayPresenter>();
            UI.Initialize(this);
            UI.UpdateHud();
        }
        void Update()
        {
            if (Game == null)
                return;
            if (!InMenu && !MenuOpen)
            {
                simWatch.Restart();
                int ticks = clock.Advance(Time.unscaledDeltaTime, Game.World.speed, tickAction);
                simWatch.Stop();
                while (Game.Cities.Notifications.Count > 0)
                {
                    var news = Game.Cities.Notifications.Dequeue();
                    Notice = CityNews(news, out var city);
                    if (city != null && (news.kind == Notification.LevelChanged || news.kind == Notification.CityFounded))
                        Sfx.World(SoundCue.TownLevelUp, city.center);
                }
                // Towns change on their own; player actions refresh through Perform, simulation changes refresh here.
                if (Game.World.cityRevision != shownCityRevision)
                {
                    shownCityRevision = Game.World.cityRevision;
                    dirty = true;
                    World.Refresh();
                }
                // Development players log VR_PERF to logcat; the editor (and its test runner) stays quiet.
                if (Debug.isDebugBuild && !Application.isEditor)
                    RecordPerf(ticks);
                World.Animate(Time.unscaledDeltaTime, Game.World.speed == 0);
                autosave += Time.unscaledDeltaTime;
                if (autosave >= 60)
                {
                    autosave = 0;
                    if (dirty || Game.World.tick != savedTick || Game.World.revision != savedRevision)
                        Save(true);
                }
            }
            hud += Time.unscaledDeltaTime;
            if (hud >= .25f)
            {
                hud = 0;
                if (!InMenu)
                    Tutorial.Advance(Game.World);
                UI.UpdateHud();
            }
        }
        void RecordPerf(int ticks)
        {
            double ms = simWatch.Elapsed.TotalMilliseconds;
            simTotalMs += ms;
            if (ms > simMaxMs)
                simMaxMs = ms;
            perfTicks += ticks;
            perfFrames++;
            perfWindow += Time.unscaledDeltaTime;
            if (perfWindow < 30)
                return;
            int gc = GC.CollectionCount(0);
            Debug.Log($"VR_PERF fps={perfFrames / perfWindow:F1} simAvgMs={simTotalMs / perfFrames:F2} simMaxMs={simMaxMs:F2} ticks={perfTicks} speed={Game.World.speed} trains={Game.World.trains.Count} managedMB={GC.GetTotalMemory(false) / 1048576f:F1} gcInWindow={gc - perfGcSeen} totalMB={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F0}");
            perfGcSeen = gc;
            perfWindow = 0;
            perfFrames = perfTicks = 0;
            simTotalMs = simMaxMs = 0;
        }
        /// <summary>"Settlers founded Ashford, a new village north-east of Oakridge." North is +z, east is +x.</summary>
        string FoundingNews(CityState town, int nearestId)
        {
            var near = Game.World.cities.Find(c => c.id == nearestId);
            if (near == null)
                return $"Settlers founded {town.name}, a new village.";
            int dx = town.center.x - near.center.x, dz = town.center.z - near.center.z;
            string ns = 2 * Math.Abs(dz) >= Math.Abs(dx) ? (dz > 0 ? "north" : "south") : "";
            string ew = 2 * Math.Abs(dx) >= Math.Abs(dz) ? (dx > 0 ? "east" : "west") : "";
            return $"Settlers founded {town.name}, a new village {ns}{(ns.Length > 0 && ew.Length > 0 ? "-" : "")}{ew} of {near.name}.";
        }
        string CityNews(Notification n, out CityState city)
        {
            foreach (var c in Game.World.cities)
                if (c.id == n.cityId)
                {
                    city = c;
                    return n.kind == Notification.LevelChanged ? $"{c.name} has become a {CityBalance.LevelNames[n.value]} ({c.population:N0} people)."
                        : n.kind == Notification.CivicBuilt && BuildingCatalog.Valid(n.value) ? $"{c.name} opened a new {BuildingCatalog.Get(n.value).name.ToLowerInvariant()}."
                        : n.kind == Notification.BeachRoad ? $"{c.name} is building a road to the beach."
                        : n.kind == Notification.BeachOpened ? $"{c.name} Beach is open: cars park by the sea and people stroll on the sand."
                        : n.kind == Notification.CityFounded ? FoundingNews(c, n.value)
                        : n.kind == Notification.ServiceOpened ? $"{Roadside.KindName(Game.Cities.RoadBetween(c.producerId, n.value)?.serviceKind ?? 0)} with a tyre shop and a garage opened on the {c.name} – {Game.World.producers.Find(p => p.id == n.value)?.name} highway."
                        : n.kind == Notification.CampOpened ? $"A {Campsites.KindName(n.value)} opened in the country outside {c.name}."
                        : n.kind == Notification.SkiRoad ? $"{c.name} is building a road to {Game.World.producers.Find(p => p.id == n.value)?.name}."
                        : n.kind == Notification.SkiOpened ? $"The road from {c.name} to {Game.World.producers.Find(p => p.id == n.value)?.name} is open: skiers drive up and park by the gondola."
                        : c.name + " is growing.";
                }
            city = null;
            return Notice;
        }
        public Result Perform(Func<Result> action)
        {
            var result = action();
            Notice = result.message;
            if (result.ok)
            {
                dirty = true;
                World.Refresh();
            }
            else
                Sfx.Ui(SoundCue.Error);
            UI.UpdateHud();
            return result;
        }
        public void NewGame(bool demo)
        {
            BuildSession(WorldState.New(balance));
            InMenu = false;
            MenuOpen = false;
            Game.World.speed = 1;
            if (demo)
                Demo();
            Notice = demo ? "Three working lines. Select a station to inspect its train." : "Drag to explore. Tap towns, stations or a train.";
            if (!Application.isBatchMode)
                SceneManager.LoadScene("Game");
        }
        public void Title()
        {
            if (!InMenu && Game != null && Game.World.tracks.Count > 0)
                Save(true);
            InMenu = true;
            Game.World.speed = 0;
            UI.ShowMenu(true);
            if (!Application.isBatchMode)
                SceneManager.LoadScene("MainMenu");
        }
        public void ChooseTool(ToolMode mode)
        {
            if (MenuOpen)
                return;
            Mode = mode;
            CancelPreview();
            World.SetBuildGrid(mode == ToolMode.Track || mode == ToolMode.Station);
            Notice = mode == ToolMode.Track ? "Drag a line, or tap its start and end. Green previews are ready to confirm." : mode == ToolMode.Station ? "Tap open ground or straight track within 3 cells of an industry or town. Choose size and platforms, then build." : mode == ToolMode.Bulldoze ? "Tap track or a station to remove it (50% refund), a town building to demolish it ($500 per level) or a tree to clear it ($50)." : "Drag to explore. Tap towns, stations or a train.";
            if (mode == ToolMode.Trains)
                UI.TrainList();
            if (mode == ToolMode.LightRail)
            {
                Notice = "Pick a stadium, beach or ski resort: the AI lays a tram line from a railway station.";
                UI.LightRailList();
            }
        }
        public void BeginTrack(Cell c)
        {
            if (MenuOpen)
                return;
            if (!anchor.HasValue)
            {
                anchor = c;
                Notice = "Track start: (" + c + "). Choose its end.";
            }
            else
                ExtendTrack(c);
        }
        public void ExtendTrack(Cell c)
        {
            if (MenuOpen || !anchor.HasValue || c.Equals(anchor.Value) || (lastEnd.HasValue && c.Equals(lastEnd.Value)))
                return;
            lastEnd = c;
            preview = Game.Build.Preview(anchor.Value, c, bridgeChoice);
            if (preview.path.Count == 0)
                preview.path.AddRange(new[] { anchor.Value, c });
            World.Preview(preview);
            UI.Preview(preview);
        }
        /// <summary>Tries the next bridge style on the line being laid; the preview, its bridge and its price follow.</summary>
        public void CycleBridge()
        {
            if (MenuOpen || preview == null || !anchor.HasValue || !lastEnd.HasValue || BridgeCatalog.RowOf(preview) == 0)
                return;
            bridgeChoice = (BridgeCatalog.StyleOf(Game.World, preview) + 1) % BridgeCatalog.Count;
            var end = lastEnd.Value;
            lastEnd = null;
            ExtendTrack(end);
        }
        /// <summary>Rebuilds the crossing on a bridge row in another style (free on an empty site) and shows its panel again.</summary>
        public void RestyleBridge(int row, int style)
        {
            if (MenuOpen)
                return;
            Perform(() => Game.Build.RestyleBridge(row, style));
            UI.Bridge(row);
        }
        public void ConfirmBuild()
        {
            if (preview == null || !preview.valid)
                return;
            var result = Perform(() => Game.Build.CommitBuild(preview));
            if (result.ok)
                CancelPreview();
        }
        public void CancelPreview()
        {
            anchor = lastEnd = null;
            bridgeChoice = -1;
            preview = null;
            if (World)
                World.Preview(null);
            if (UI)
                UI.HideContext();
        }
        /// <summary>A tap on the map. The bulldozer takes the building or tree under the finger, which on the tilted view stands in front of the ground cell the ray reaches.</summary>
        public void Tap(Cell ground, Ray ray)
        {
            ProducerState industry = null;
            if (Mode == ToolMode.Bulldoze && Game != null)
                ground = StructurePick.First(Game, ray.origin.x, ray.origin.y, ray.origin.z, ray.direction.x, ray.direction.y, ray.direction.z, ground);
            else if (Game != null)
                industry = IndustryGuide.Hit(Game.World, ray.origin.x, ray.origin.y, ray.origin.z, ray.direction.x, ray.direction.y, ray.direction.z);
            Select(ground, industry);
        }
        /// <param name="industry">The industry whose buildings the finger is on (IndustryGuide.Hit); it wins over a nearby station or town.</param>
        public void Select(Cell c, ProducerState industry = null)
        {
            if (MenuOpen)
                return;
            Notice = MapDefinition.SurfaceNames[MapDefinition.Surface(c)] + " · Map cell " + c + (MapDefinition.Raised(c) ? " · Route tracks around the hills." : "");
            if (Mode == ToolMode.Station)
            {
                UI.StationChoices(c);
                return;
            }
            if (Mode == ToolMode.LightRail)
            {
                UI.LightRailAt(c);
                return;
            }
            if (Mode == ToolMode.Bulldoze)
            {
                Perform(() => Game.Build.Bulldoze(c));
                return;
            }
            foreach (var t in Game.World.trains)
            {
                var p = RailGeometry.TrainPosition(Game, t, 0, out _);
                if (new Cell(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)).Distance(c) <= 1)
                {
                    UI.Train(t);
                    return;
                }
            }
            // A tram, or light rail track and stops, before the station whose forecourt the transfer stop stands on.
            var tramLine = industry == null ? World.LightRailTramNear(c) ?? TramLines.LineAt(Game.World, c) : null;
            if (tramLine != null)
            {
                UI.TramLine(tramLine);
                return;
            }
            foreach (var s in Game.World.stations)
                if ((industry == null && s.cell.Distance(c) <= 2) || StationLayout.PlatformAt(s, c) >= 0 || StationLayout.OnStrip(s, c))
                {
                    UI.Station(s);
                    return;
                }
            if (industry != null)
            {
                UI.Producer(industry);
                return;
            }
            int bridgeRow = BridgeCatalog.SiteNear(c);
            if (bridgeRow != 0)
            {
                UI.Bridge(bridgeRow);
                return;
            }
            var service = ServiceSales.At(Game.World, c);
            if (service != null)
            {
                UI.ServiceArea(service);
                return;
            }
            var camp = Campsites.At(Game.World, c);
            if (camp != null)
            {
                UI.Campsite(camp);
                return;
            }
            var city = Game.Cities.CityAt(c);
            if (city != null)
            {
                UI.Producer(Game.Cargo.Producer(city.producerId));
                return;
            }
            foreach (var p in Game.World.producers)
                if (p.cell.Distance(c) <= 3)
                {
                    UI.Producer(p);
                    return;
                }
            // The tilted view's ray can reach the ground just past a low building: a near miss still opens it.
            service = ServiceSales.At(Game.World, c, 1);
            if (service != null)
            {
                UI.ServiceArea(service);
                return;
            }
            camp = Campsites.At(Game.World, c, 1);
            if (camp != null)
            {
                UI.Campsite(camp);
                return;
            }
            UI.HideContext();
        }
        public void SetSpeed(int speed)
        {
            if (!MenuOpen)
            {
                Game.World.speed = speed;
                dirty = true;
                UI.UpdateHud();
            }
        }
        public void Save(bool auto)
        {
            try
            {
                Game.World.cameraX = Camera.focus.x;
                Game.World.cameraZ = Camera.focus.z;
                Game.World.zoom = Camera.zoom;
                Game.World.cameraTurn = Camera.Turn;
                saves.Save(Game.World, auto);
                savedTick = Game.World.tick;
                savedRevision = Game.World.revision;
                dirty = false;
                // Autosave is silent; a toast every minute would only cover the map.
                if (!auto)
                    Notice = "Game saved.";
            }
            catch (Exception e) { Notice = "Save failed: " + e.Message; Debug.LogWarning(Notice); }
            UI.UpdateHud();
        }
        public bool HasSave(bool auto) => saves.Exists(auto);
        public void Load(bool auto, bool backup = false)
        {
            if (!saves.Exists(auto, backup))
            {
                UI.ShowMenu(InMenu, backup ? "No previous backup exists for this slot." : auto ? "No autosave yet. Start a new game." : "No manual save yet. Use Save Game first.");
                return;
            }
            try
            {
                var state = saves.Load(auto, backup);
                state.speed = 0;
                BuildSession(state);
                InMenu = false;
                UI.CloseMenu();
                if (!Application.isBatchMode)
                    SceneManager.LoadScene("Game");
                Notice = "Game restored and paused. Tap ► to continue.";
            }
            catch (Exception e) { UI.Recovery(auto, e.Message); }
        }
        public void ToggleSound()
        {
            SoundEnabled = !SoundEnabled;
            PlayerPrefs.SetInt("sound", SoundEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }
        public void ToggleMusic()
        {
            MusicEnabled = !MusicEnabled;
            PlayerPrefs.SetInt("music", MusicEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }
        public void ToggleHints()
        {
            HintsEnabled = !HintsEnabled;
            PlayerPrefs.SetInt("hints", HintsEnabled ? 1 : 0);
            PlayerPrefs.Save();
            UI.UpdateHud();
        }
        /// <summary>Button click: like every sound but delivery income, it needs the camera zoomed in (menus always click).</summary>
        public void ClickSound()
        {
            float gain = Sfx.UiGain;
            if (SoundEnabled && gain > 0)
                audioSource.PlayOneShot(click, gain);
        }
        /// <summary>Escape / Android Back: leave the recovery screen, close the pause menu, cancel a preview, close the build tray,
        /// leave a build tool, or open the menu.</summary>
        public void Back()
        {
            if (UI.RecoveryOpen || UI.SettingsOpen)
                UI.ShowMenu(InMenu);
            else if (InMenu)
            {
                // Android convention: Back on the title screen leaves the app. Nothing is lost; the title already autosaved.
                if (Application.platform == RuntimePlatform.Android)
                    Application.Quit();
            }
            else if (MenuOpen)
                UI.CloseMenu();
            else if (preview != null)
                CancelPreview();
            else if (!UI.CloseTray())
            {
                if (Mode == ToolMode.Track || Mode == ToolMode.Station || Mode == ToolMode.Bulldoze || Mode == ToolMode.LightRail)
                    ChooseTool(ToolMode.Browse);
                else
                    UI.ShowMenu(false);
            }
        }
        void OnApplicationPause(bool paused)
        {
            if (paused && Game != null && !InMenu)
            {
                Game.World.speed = 0;
                Save(true);
                clock.Reset();
            }
        }
        void OnApplicationQuit()
        {
            if (Game != null && !InMenu)
                Save(true);
        }
        void OnDestroy()
        {
            if (active == this)
                active = null;
            if (click)
                Destroy(click);
        }
        public void Demo()
        {
            // Optional showcase uses the same validation and purchase commands as normal play.
            Game.World.money = 100000;
            Game.World.tutorialStep = 0;
            DemoLine(new Cell(8, 15), new Cell(50, 15), new Cell(10, 15), new Cell(48, 15), 1, 2, 0, Cargo.Coal);
            var goods = new System.Collections.Generic.List<Cell>();
            for (int z = 31; z <= 40; z++)
                goods.Add(new Cell(12, z));
            for (int x = 13; x <= 18; x++)
                goods.Add(new Cell(x, 40));
            Require(Game.Build.CommitBuild(Game.Build.ValidateBuild(goods)));
            int a = Require(Game.Stations.Place(new Cell(12, 32), 3)), b = Require(Game.Stations.Place(new Cell(16, 40), 4));
            int train = Require(Game.Trains.Buy(a, 1, Cargo.Goods));
            Require(Game.Trains.AssignRoute(train, a, b));
            DemoLine(new Cell(14, 46), new Cell(50, 46), new Cell(16, 46), new Cell(48, 46), 4, 5, 2, Cargo.Passengers);
            World.Refresh();
            Camera.focus = new Vector3(31, 0, 29);
            Camera.zoom = 29;
            Game.World.speed = 1;
            // The showcase is set up, not built by the player: its construction events stay silent.
            Game.Events.Clear();
        }
        void DemoLine(Cell start, Cell end, Cell first, Cell second, int p1, int p2, int model, Cargo cargo)
        {
            Require(Game.Build.CommitBuild(Game.Build.Preview(start, end)));
            int a = Require(Game.Stations.Place(first, p1)), b = Require(Game.Stations.Place(second, p2));
            int train = Require(Game.Trains.Buy(a, model, cargo));
            Require(Game.Trains.AssignRoute(train, a, b));
        }
        static int Require(Result r)
        {
            if (!r.ok)
                throw new InvalidOperationException(r.message);
            return r.id;
        }
    }
}
