using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ValleyRail.Core;
namespace ValleyRail
{
    public sealed partial class GameplayPresenter : MonoBehaviour
    {
        GameBootstrap app; RectTransform safe, context, contextBody, menu, menuBody; TMP_Text contextText; TMP_FontAsset font; int selectedTrain, selectedStation, selectedProducer;
        int pausedSpeed;
        RectTransform scrim;
        float nextContextY;
        Action closeContext;
        public bool RecoveryOpen { get; private set; }
        public bool SettingsOpen { get; private set; }
        static string Unit(Cargo c) => c == Cargo.Passengers ? "passengers" : c == Cargo.Goods ? "crates" : c == Cargo.Oil ? "barrels" : "tons";
        static string Describe(ProducerState p, int storage = 0)
        {
            string stock = storage > 0 ? $"{p.inventory} / {storage}" : p.inventory.ToString();
            var output = IndustryCatalog.Output(p.kind);
            string waiting = output.HasValue ? $"{IndustryCatalog.CargoName(output.Value)}: {stock} {Unit(output.Value)} waiting" : "Produces nothing";
            return waiting + "\nAccepts: " + IndustryCatalog.Inputs(p.kind) +
                (IndustryCatalog.Processing(p.kind) ? "\nDeliver input to produce output (1:1)." : "");
        }
        string StationText(StationState s)
        {
            var producer = app.Game.Cargo.Producer(s.producerId);
            if (producer.kind == ProducerKind.Town)
            {
                var load = StationLoad.Of(app.Game.World, app.Game.Balance, s);
                string held = CargoTransfer.Describe(producer);
                string transfer = held == null ? "" : $"\nTransfer: {held} waiting for an onward train";
                return $"<size=30><color=#{ColorUtility.ToHtmlStringRGB(StationCrowds.LoadColor(load.Level))}>{producer.inventory:N0} passengers waiting</color></size>\nTown queue {load.Percent}% full · shared across stations\nAccepts: goods, passengers · other freight as a transfer{transfer}\n{StationKindText(s)}";
            }
            return Describe(producer) + $"\n{StationKindText(s)}";
        }
        string ProducerText(ProducerState p)
        {
            var city = app.Game.Cities.CityFor(p.id);
            if (city == null)
                return IndustryText(p);
            var cities = app.Game.Cities;
            int rate = cities.Rate(city), cost = cities.ActionCost(city), boost = StationCatalog.GrowthPercent(cities.ServedStationLevel(city));
            string pace = city.blockedEvaluations > 0 ? "no space" : rate < cost / 3 ? "slow" : rate < cost ? "steady" : rate < 2 * cost ? "fast" : "booming";
            int minutes = rate <= 0 ? 0 : Math.Max(0, (cost - city.growthPoints + rate - 1) / rate);
            string next = city.blockedEvaluations > 0 ? "blocked" : minutes <= 0 ? "under a minute" : minutes + " min";
            // Five short lines: the details box shows at most five rows at the minimum font size.
            return $"{(city.founded > 0 ? "Settlers' " : "")}{CityBalance.LevelNames[(int)city.level]} · {city.population:N0} people · {CitySimulation.CivicCount(city)}/{BuildingCatalog.CivicOrder.Length} services\nGrowth: {pace}, +{rate} pts/min{(boost > 0 ? $" (station +{boost}%)" : "")}\nNext building: {next}{FundLine(city)}\nLinks {cities.Connections(city)} · {cities.RecentPassengers(city):N0} passengers/4 min\n{cities.RoadStatus(city)}\nWaiting: {p.inventory} / {p.storage} passengers";
        }
        string TrainText(TrainState t)
        {
            var b = app.Game.Balance;
            string route = t.a != 0 ? $"{StopName(t.a)} → {StopName(t.b)}" : "none yet";
            return $"{TrainCatalog.Name(t.model)} · {t.cargo} {t.units}/{b.Capacity(t)} {Unit(t.cargo)}\nStatus: {t.state} · {WagonCount(TrainCatalog.Wagons(t))} · ${b.RunningCost(t)}/min\nRoute: {route}\n{ProfitText(t)}";
        }
        string StopName(int stationId)
        {
            var s = app.Game.Trains.Station(stationId);
            return s == null ? "?" : app.Game.Cargo.Producer(s.producerId).name;
        }
        Color navy = new Color(.035f, .09f, .13f, .96f), muted = new Color(.65f, .76f, .73f), mint = new Color(.55f, .84f, .59f), cream = new Color(.96f, .94f, .85f), violet = new Color(.74f, .66f, 1f), warning = new Color(1f, .72f, .48f);
        // True while a proposal (the AI rail fix route or a new station) is drawn on the map; any other details view clears it.
        bool aiPreview;
        public void Initialize(GameBootstrap app)
        {
            this.app = app;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            font = Resources.Load<TMP_FontAsset>("UIFont");
            if (!font)
                font = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            safe = Panel("Safe area", transform, Color.clear);
            safe.GetComponent<Image>().raycastTarget = false;
            SafeArea();
            BuildHud();
            context = Panel("Context", safe, navy);
            // Details sit under the top row and reach the bottom; the bottom-right tools hide while they are open.
            Place(context, 1, 0, 1, 1, -450, Margin, -Margin, -Margin - TopRow - Gap);
            context.gameObject.AddComponent<RectMask2D>();
            contextBody = Panel("Scrollable details", context, Color.clear);
            contextBody.GetComponent<Image>().raycastTarget = false;
            contextBody.anchorMin = new Vector2(0, 1);
            contextBody.anchorMax = new Vector2(1, 1);
            contextBody.pivot = new Vector2(.5f, 1);
            contextBody.sizeDelta = new Vector2(0, 640);
            contextBody.anchoredPosition = Vector2.zero;
            var scroll = context.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = context;
            scroll.content = contextBody;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            context.gameObject.SetActive(false);
            scrim = Panel("Menu backdrop", safe, new Color(.015f, .035f, .05f, .65f));
            Place(scrim, 0, 0, 1, 1, 0, 0, 0, 0);
            scrim.gameObject.SetActive(false);
            menu = Panel("Menu", safe, navy);
            Place(menu, .5f, 0, .5f, 1, -300, 20, 300, -20);
            menu.gameObject.AddComponent<RectMask2D>();
            menuBody = Panel("Scrollable menu", menu, Color.clear);
            menuBody.GetComponent<Image>().raycastTarget = false;
            menuBody.anchorMin = new Vector2(0, 1);
            menuBody.anchorMax = new Vector2(1, 1);
            menuBody.pivot = new Vector2(.5f, 1);
            menuBody.sizeDelta = new Vector2(0, 950);
            menuBody.anchoredPosition = Vector2.zero;
            var menuScroll = menu.gameObject.AddComponent<ScrollRect>();
            menuScroll.viewport = menu;
            menuScroll.content = menuBody;
            menuScroll.horizontal = false;
            menuScroll.vertical = true;
            menuScroll.movementType = ScrollRect.MovementType.Clamped;
            menuScroll.scrollSensitivity = 30;
            menu.gameObject.SetActive(false);
            BuildFade();
        }
        void SafeArea()
        {
            var area = Screen.safeArea;
            safe.anchorMin = area.position / new Vector2(Screen.width, Screen.height);
            safe.anchorMax = (area.position + area.size) / new Vector2(Screen.width, Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
        RectTransform Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }
        static void Place(RectTransform r, float x0, float y0, float x1, float y1, float left, float bottom, float right, float top)
        {
            r.anchorMin = new Vector2(x0, y0);
            r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(right, top);
        }
        TMP_Text Text(Transform parent, string value, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            fontSize = Mathf.Max(18, fontSize);
            size.y = Mathf.Max(size.y, fontSize * 1.35f);
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = value;
            t.fontSize = fontSize;
            t.color = color;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }
        RectTransform Button(Transform parent, string title, Action action, Vector2 pos, Vector2 size, bool primary = false)
        {
            size.x = Mathf.Max(96, size.x);
            size.y = Mathf.Max(96, size.y);
            if (parent == contextBody && pos.y <= -200)
            {
                pos.y = -nextContextY;
                size.x = context.rect.width - 44;
                nextContextY += size.y + 12;
                contextBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(context.rect.height, nextContextY + 12));
            }
            var r = Panel(title, parent, primary ? mint : new Color(.12f, .20f, .24f));
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            var button = r.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(.72f, .82f, .82f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.5f, .5f, .5f, .55f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            button.onClick.AddListener(() => { app.ClickSound(); action(); });
            var text = Text(r, title, Vector2.zero, size, 18, primary ? navy : cream);
            Place(text.rectTransform, 0, 0, 1, 1, 12, 8, -12, -8);
            text.fontSize = size.x <= 100 ? 18 : 22;
            text.alignment = TextAlignmentOptions.Center;
            return r;
        }
        // A wagon count of 0 pictures the model's standard consist.
        void TrainPreview(RectTransform parent, int model, Cargo cargo, float y, int wagons = 0)
        {
            var go = new GameObject("Train picture", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), 0, 1, 1, 1, 12, -y - 112, -12, -y);
            var picture = go.GetComponent<RawImage>();
            picture.texture = app.World.TrainPicture(model, cargo, wagons);
            picture.raycastTarget = false;
        }
        RectTransform TrainCard(string label, int model, Cargo cargo, Action action, float height = 226, int wagons = 0)
        {
            var card = Button(contextBody, label, action, new Vector2(22, -205), new Vector2(345, height));
            var text = card.GetComponentInChildren<TMP_Text>();
            Place(text.rectTransform, 0, 0, 1, 1, 12, 10, -12, -130);
            TrainPreview(card, model, cargo, 10, wagons);
            return card;
        }
        void Clear(RectTransform panel)
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                var child = panel.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
        void Context(string title, string details)
        {
            ClearAiPreview();
            Clear(contextBody);
            selectedTrain = selectedStation = selectedProducer = selectedTramLine = 0;
            stationRows.Clear();
            contextBody.sizeDelta = new Vector2(0, context.rect.height);
            contextBody.anchoredPosition = Vector2.zero;
            context.gameObject.SetActive(true);
            closeContext = HideContext;
            Text(contextBody, title, new Vector2(22, -24), new Vector2(context.rect.width - 150, 82), 28, cream);
            contextText = Text(contextBody, details, new Vector2(22, -126), new Vector2(context.rect.width - 44, 150), 22, muted);
            float detailHeight = Mathf.Max(96, contextText.GetPreferredValues(details, context.rect.width - 44, 0).y);
            contextText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, detailHeight);
            nextContextY = 126 + detailHeight + 24;
            contextBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(context.rect.height, nextContextY));
            Button(contextBody, "CLOSE", () => closeContext(), new Vector2(context.rect.width - 118, -16), new Vector2(96, 96));
        }

        public void UpdateHud()
        {
            app.World.UpdateStationLabels();
            RefreshHud();
            if (context.gameObject.activeSelf && contextText)
            {
                if (selectedTrain != 0)
                {
                    var t = app.Game.Trains.Train(selectedTrain);
                    if (t != null)
                        contextText.text = TrainText(t);
                }
                else if (selectedStation != 0)
                {
                    var s = app.Game.Trains.Station(selectedStation);
                    if (s != null)
                        contextText.text = StationText(s);
                }
                else if (selectedTramLine != 0)
                    RefreshTramLine();
                else if (selectedProducer != 0)
                    contextText.text = ProducerText(app.Game.Cargo.Producer(selectedProducer));
                else
                    RefreshFleet();
            }
        }
        void Highlight(RectTransform button, bool selected)
        {
            button.GetComponent<Image>().color = selected ? mint : new Color(.12f, .20f, .24f);
            button.GetComponentInChildren<TMP_Text>().color = selected ? navy : cream;
        }
        public void HideContext()
        {
            ClearAiPreview();
            context.gameObject.SetActive(false);
        }
        void ClearAiPreview()
        {
            if (!aiPreview)
                return;
            aiPreview = false;
            if (app.World)
            {
                app.World.Preview(null);
                app.World.MarkDoomed(null);
            }
        }
        public void Preview(BuildPlan plan)
        {
            if (plan == null)
            {
                HideContext();
                return;
            }
            int bridge = BridgeCatalog.RowOf(plan), style = bridge == 0 ? -1 : BridgeCatalog.StyleOf(app.Game.World, plan);
            string crossing = bridge == 0 ? "" : $"\n{BridgeCatalog.Name(style)} bridge · ${BridgeCatalog.Cost(app.Game.Balance, style):N0}";
            Context("LAY THE LINE", $"{plan.path.Count} cells   /   ${plan.cost:N0}\n{(plan.path.Count > 0 ? plan.path[0].ToString() : "-")} → {(plan.path.Count > 0 ? plan.path[plan.path.Count - 1].ToString() : "-")}\n{plan.reason}{crossing}");
            closeContext = app.CancelPreview;
            contextText.color = plan.valid ? mint : new Color(1f, .72f, .48f);
            if (bridge != 0)
                Named(Button(contextBody, $"BRIDGE: {BridgeCatalog.Name(style).ToUpperInvariant()}  ►", app.CycleBridge, new Vector2(22, -210), new Vector2(345, 58)), "BRIDGE STYLE");
            if (plan.valid)
                Button(contextBody, "CONFIRM BUILD", app.ConfirmBuild, new Vector2(22, -210), new Vector2(345, 58), true);
            Button(contextBody, "CANCEL", app.CancelPreview, new Vector2(22, -280), new Vector2(345, 54));
        }
        public void Producer(ProducerState p)
        {
            Context(p.name, ProducerText(p));
            selectedProducer = p.id;
            DonateButton(p);
        }
        public void Station(StationState s)
        {
            Context(s.name, StationText(s));
            selectedStation = s.id;
            Button(contextBody, "BUY A TRAIN", () => Shop(s), new Vector2(22, -210), new Vector2(345, 58), true);
            UpgradeButton(s);
            Button(contextBody, "VIEW TRAINS", TrainList, new Vector2(22, -280), new Vector2(345, 54));
            Button(contextBody, "ALL STATIONS", StationList, new Vector2(22, -350), new Vector2(345, 54));
        }
        void Shop(StationState s)
        {
            Context("CHOOSE YOUR TRAIN", "Freight trains carry industrial cargo.\nPassenger trains connect two towns.\nTap a train to choose its wagons.");
            var b = app.Game.Balance;
            for (int model = 0; model < TrainCatalog.Count; model++)
                for (int type = 0; type < Enum.GetValues(typeof(Cargo)).Length; type++)
                {
                    int m = model;
                    Cargo cargo = (Cargo)type;
                    if (!TrainCatalog.SupportsCargo(m, cargo)) continue;
                    var producer = app.Game.Cargo.Producer(s.producerId);
                    // A city also sends on freight it holds as a transfer station.
                    if (!MapDefinition.Produces(producer.kind, cargo) && !MapDefinition.Accepts(producer.kind, cargo) && !CargoTransfer.Handles(producer, cargo)) continue;
                    // Speed is stored in track units per second, with 1000 units per cell.
                    string details = $"${b.trainPrice[m]:N0} · {b.capacity[m]} {Unit(cargo)}\n{b.speed[m] / 1000f:0.#} cells/s · ${b.runningCost[m]}/min";
                    TrainCard($"{TrainCatalog.Name(m)}{(cargo == Cargo.Passengers ? "" : " · " + IndustryCatalog.CargoName(cargo))}\n{details}", m, cargo,
                        () => BuyWagons(s, m, cargo, TrainCatalog.DefaultWagons(m)));
                }
        }
        public void TrainList()
        {
            Context("YOUR FLEET", FleetText());
            fleetLabels.Clear();
            if (app.Game.World.trains.Count == 0)
                contextText.text = "Your first train starts at a station. Build tracks near an industry, add a station, then tap it to buy a train.";
            foreach (var t in app.Game.World.trains)
            {
                int id = t.id;
                var card = TrainCard(FleetCardText(t), t.model, t.cargo, () => Train(app.Game.Trains.Train(id)), 236, TrainCatalog.Wagons(t));
                fleetLabels.Add((id, card.GetComponentInChildren<TMP_Text>()));
            }
        }
        public void Train(TrainState t)
        {
            Context("TRAIN #" + t.number, TrainText(t));
            selectedTrain = t.id;
            TrainPreview(contextBody, t.model, t.cargo, nextContextY, TrainCatalog.Wagons(t));
            nextContextY += 132;
            Button(contextBody, "CHOOSE DESTINATION", () => Destinations(t), new Vector2(22, -205), new Vector2(345, 54), true);
            var ai = Button(contextBody, "AI FIX RAILS", () => AiFix(t), new Vector2(22, -236), new Vector2(345, 54));
            ai.GetComponent<Image>().color = violet;
            ai.GetComponentInChildren<TMP_Text>().color = navy;
            Button(contextBody, "RETURN TO STATION", () => { app.Perform(() => app.Game.Trains.ReturnToStation(t.id)); Train(t); }, new Vector2(22, -267), new Vector2(345, 50));
            Button(contextBody, "RESUME SERVICE", () => { app.Perform(() => app.Game.Trains.Resume(t.id)); Train(t); }, new Vector2(22, -329), new Vector2(345, 50));
            Button(contextBody, "CLEAR ROUTE", () => { app.Perform(() => app.Game.Trains.ClearRoute(t.id)); Train(t); }, new Vector2(22, -391), new Vector2(345, 50));
            Button(contextBody, $"WAGONS: {TrainCatalog.Wagons(t)} · CHANGE", () => EditWagons(t, TrainCatalog.Wagons(t)), new Vector2(22, -422), new Vector2(345, 50));
            Button(contextBody, "SELL TRAIN", () => ConfirmSale(t), new Vector2(22, -453), new Vector2(345, 50));
        }
        /// <summary>Shows the rail fixer's diagnosis and draws its route on the map; nothing is built until the player confirms.</summary>
        void AiFix(TrainState t)
        {
            var fix = app.Game.Fixer.Plan(t.id);
            Context("AI RAIL FIX", fix.reason);
            contextText.color = fix.valid ? mint : warning;
            if (fix.build != null)
            {
                var preview = AiPreview(fix);
                app.World.Preview(preview);
                aiPreview = true;
                Frame(preview);
            }
            if (fix.valid)
            {
                string label = fix.build.changes.Count == 0 && fix.newStation == null ? "START ROUTE" : $"BUILD & START · ${fix.cost:N0}";
                Button(contextBody, label, () => { app.Perform(() => app.Game.Fixer.Apply(t.id, fix)); Train(t); }, new Vector2(22, -210), new Vector2(345, 58), true);
            }
            else
                app.Sfx.Ui(SoundCue.Error);
            Button(contextBody, "BACK", () => Train(t), new Vector2(22, -280), new Vector2(345, 54));
        }
        /// <summary>The fix's route, plus the platform track of a station it would build first.</summary>
        static BuildPlan AiPreview(RailFix fix)
        {
            if (fix.newStation == null)
                return fix.build;
            var preview = new BuildPlan { valid = fix.build.valid, cost = fix.cost };
            preview.path.AddRange(fix.build.path);
            preview.changes.AddRange(fix.build.changes);
            foreach (var c in fix.newStation.platformCells)
                if (!preview.path.Contains(c))
                    preview.path.Add(c);
            preview.changes.AddRange(fix.newStation.track.changes);
            return preview;
        }
        /// <summary>Centres the camera on the pieces the fix changes (or its whole route) and zooms out until they fit.</summary>
        void Frame(BuildPlan plan)
        {
            var cells = plan.changes.Count > 0 ? plan.changes.ConvertAll(c => c.cell) : plan.path;
            if (cells.Count == 0)
                return;
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var c in cells)
            {
                minX = Math.Min(minX, c.x);
                maxX = Math.Max(maxX, c.x);
                minZ = Math.Min(minZ, c.z);
                maxZ = Math.Max(maxZ, c.z);
            }
            app.Camera.focus = new Vector3((minX + maxX) / 2f, 0, (minZ + maxZ) / 2f);
            app.Camera.zoom = Mathf.Max(app.Camera.zoom, Math.Max(maxX - minX, maxZ - minZ) * .5f + 6);
        }
        void ConfirmSale(TrainState t)
        {
            Context("SELL TRAIN?", $"Refund: ${app.Game.Balance.TrainPrice(t) / 2:N0}\nAny cargo aboard will be discarded.");
            var sell = Button(contextBody, "CONFIRM SALE", () => { app.Perform(() => app.Game.Trains.Sell(t.id)); TrainList(); }, new Vector2(22, -205), new Vector2(345, 54), true);
            sell.GetComponent<Image>().color = new Color(.95f, .59f, .44f);
            Button(contextBody, "KEEP TRAIN", () => Train(t), new Vector2(22, -269), new Vector2(345, 54));
        }
        void Destinations(TrainState t)
        {
            Context("SELECT DESTINATION", "Auto picks the nearest reachable stop for this cargo. The train repeats between the two stations.");
            Button(contextBody, "AUTO DESTINATION", () => { app.Perform(() => app.Game.Trains.AutoDestination(t.id)); Train(t); }, new Vector2(22, -205), new Vector2(345, 54), true);
            int row = 1;
            foreach (var station in app.Game.World.stations)
                if (station.id != t.stationId)
                {
                    int id = station.id;
                    Button(contextBody, station.name, () => { app.Perform(() => app.Game.Trains.AssignRoute(t.id, t.stationId, id)); Train(t); }, new Vector2(22, -205 - row++ * 62), new Vector2(345, 47));
                }
        }
        /// <summary>Shows the title menu (start) or the in-game pause menu. An optional status line replaces the tagline, e.g. after saving.</summary>
        public void ShowMenu(bool start, string status = null)
        {
            // Remember the speed only when the menu first opens; re-rendering it (sound toggle, status) must not record the paused state.
            if (!app.MenuOpen)
                pausedSpeed = app.Game.World.speed;
            app.MenuOpen = true;
            RecoveryOpen = false;
            SettingsOpen = false;
            app.Game.World.speed = 0;
            Clear(menuBody);
            menuBody.anchoredPosition = Vector2.zero;
            menu.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            guidance.gameObject.SetActive(false);
            HideContext();
            // The title screen hides the play HUD so a previous game's figures never show behind it.
            ShowPlayHud(!start);
            Text(menuBody, "VALLEY RAIL", new Vector2(40, -18), new Vector2(520, 56), 44, mint);
            Text(menuBody, status ?? "A small world. A railway of your own.", new Vector2(42, -78), new Vector2(520, 54), 20, status == null ? cream : mint);
            const float step = 108;
            float y = -142;
            if (!start)
                Button(menuBody, "RESUME", CloseMenu, new Vector2(40, y), new Vector2(520, 96), true);
            else
                Button(menuBody, "NEW GAME", () => app.NewGame(false), new Vector2(40, y), new Vector2(520, 96), true);
            y -= step;
            Button(menuBody, start ? "EXPLORE A WORKING RAILWAY" : "SAVE GAME", () => { if (start) app.NewGame(true); else { app.Save(false); ShowMenu(false, app.Notice); } }, new Vector2(40, y), new Vector2(520, 96));
            y -= step;
            var manualSave = Button(menuBody, app.HasSave(false) ? "LOAD MANUAL SAVE" : "NO MANUAL SAVE YET", () => app.Load(false), new Vector2(40, y), new Vector2(520, 96));
            y -= step;
            manualSave.GetComponent<Button>().interactable = app.HasSave(false);
            var autoSave = Button(menuBody, app.HasSave(true) ? "CONTINUE AUTOSAVE" : "NO AUTOSAVE YET", () => app.Load(true), new Vector2(40, y), new Vector2(520, 96));
            y -= step;
            autoSave.GetComponent<Button>().interactable = app.HasSave(true);
            Button(menuBody, "SETTINGS", ShowSettings, new Vector2(40, y), new Vector2(520, 96));
            y -= step;
            if (!start)
            {
                Button(menuBody, "RETURN TO TITLE", app.Title, new Vector2(40, y), new Vector2(520, 96));
                y -= step;
            }
            else if (DesktopControls.IsDesktop)
            {
                // A Mac window needs a way out; phones leave with Back or Home. The title screen has already autosaved.
                Button(menuBody, "QUIT GAME", Application.Quit, new Vector2(40, y), new Vector2(520, 96));
                y -= step;
            }
            string hint = DesktopControls.MenuHint(DesktopControls.IsDesktop);
            float hintHeight = 30 * hint.Split('\n').Length;
            Text(menuBody, hint, new Vector2(42, y - 6), new Vector2(520, hintHeight), 18, muted);
            menuBody.sizeDelta = new Vector2(0, -y + 10 + hintHeight);
        }
        public void CloseMenu()
        {
            // Resume the speed the player had before the menu paused the game. A recovery screen never restores it.
            if (app.MenuOpen && !RecoveryOpen)
                app.Game.World.speed = pausedSpeed;
            app.MenuOpen = false;
            RecoveryOpen = false;
            SettingsOpen = false;
            menu.gameObject.SetActive(false);
            scrim.gameObject.SetActive(false);
            // Guidance and the bottom-right tools come back on the next LateUpdate, driven by notices and state.
            ShowPlayHud(true);
        }
        public void ShowSettings()
        {
            Clear(menuBody);
            menuBody.anchoredPosition = Vector2.zero;
            menu.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            guidance.gameObject.SetActive(false);
            app.MenuOpen = true;
            SettingsOpen = true;
            RecoveryOpen = false;
            Text(menuBody, "SETTINGS", new Vector2(40, -18), new Vector2(520, 56), 44, mint);
            Text(menuBody, "Changes apply immediately and are remembered.", new Vector2(42, -78), new Vector2(520, 54), 20, cream);
            const float step = 108;
            float y = -142;
            Button(menuBody, "SOUND: " + (app.SoundEnabled ? "ON" : "OFF"), () => { app.ToggleSound(); ShowSettings(); }, new Vector2(40, y), new Vector2(520, 96));
            y -= step;
            Button(menuBody, "MUSIC: " + (app.MusicEnabled ? "ON" : "OFF"), () => { app.ToggleMusic(); ShowSettings(); }, new Vector2(40, y), new Vector2(520, 96));
            y -= step;
            Button(menuBody, "TUTORIAL HINTS: " + (app.HintsEnabled ? "ON" : "OFF"), () => { app.ToggleHints(); ShowSettings(); }, new Vector2(40, y), new Vector2(520, 96));
            y -= step;
            Button(menuBody, "BACK", () => ShowMenu(app.InMenu), new Vector2(40, y), new Vector2(520, 96), true);
            y -= step;
            Text(menuBody, $"Autosave runs every changed minute and when the app goes to the background.\nValley Rail {Application.version} · {(Debug.isDebugBuild ? "development build" : "release build")}", new Vector2(42, y - 6), new Vector2(520, 90), 18, muted);
            menuBody.sizeDelta = new Vector2(0, -y + 100);
        }
        public void Recovery(bool auto, string error)
        {
            Clear(menuBody);
            menuBody.anchoredPosition = Vector2.zero;
            menu.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            guidance.gameObject.SetActive(false);
            app.MenuOpen = true;
            RecoveryOpen = true;
            Text(menuBody, "SAVE COULD NOT LOAD", new Vector2(40, -35), new Vector2(520, 70), 30, cream);
            Text(menuBody, error, new Vector2(40, -125), new Vector2(520, 160), 19, muted);
            Button(menuBody, "TRY PREVIOUS BACKUP", () => app.Load(auto, true), new Vector2(40, -300), new Vector2(520, 96), true);
            Button(menuBody, "BACK", () => ShowMenu(app.InMenu), new Vector2(40, -400), new Vector2(520, 96));
            menuBody.sizeDelta = new Vector2(0, 520);
        }
    }
}
