using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    public class GameplayTests
    {
        [UnityTest]
        public IEnumerator MatureBuildingsCreateVisibleSkyline()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var cityRoot = app.World.transform.Find("Cities");
            float villageHeight = 0;
            foreach (var renderer in cityRoot.GetComponentsInChildren<Renderer>())
                villageHeight = Mathf.Max(villageHeight, renderer.bounds.max.y);
            var city = app.Game.World.cities[0];
            for (int i = 0; i < city.buildings.Count; i++)
            {
                var building = city.buildings[i];
                building.def = BuildingCatalog.Index(i % 3 == 0 ? BuildingCategory.Commercial : BuildingCategory.Residential, 4);
                city.buildings[i] = building;
            }
            CitySimulation.Recount(city, app.Game.Cargo.Producer(city.producerId), app.Game.Balance);
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
            float towerHeight = 0;
            foreach (var renderer in cityRoot.GetComponentsInChildren<Renderer>())
                towerHeight = Mathf.Max(towerHeight, renderer.bounds.max.y);
            Assert.That(towerHeight, Is.GreaterThan(7f));
            Assert.That(towerHeight, Is.GreaterThan(villageHeight * 3), "the town hall's flag tops a day-0 village");
            app.Camera.focus = new Vector3(city.center.x, 2, city.center.z);
            app.Camera.zoom = 9;
            yield return null;
            yield return Capture(app, "Logs/skyscrapers-preview.png");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator BuildingCatalogShowcase()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            // Every single-cell definition along one street, half north of it and half south; the 2×2 landmarks south of a
            // second street three rows down. Everything faces its street.
            var city = app.Game.World.cities[0];
            var c = city.center;
            var small = new System.Collections.Generic.List<int>();
            var large = new System.Collections.Generic.List<int>();
            for (int def = 0; def < BuildingCatalog.Defaults.Length; def++)
                if (!BuildingCatalog.Get(def).retired)
                    (BuildingCatalog.Size(def) == 1 ? small : large).Add(def);
            int half = (small.Count + 1) / 2, west = c.x - half / 2;
            city.buildings.Clear();
            city.roads.Clear();
            city.roads.Add(new RoadState { cell = c });
            for (int x = west - 1; x <= west + half; x++)
            {
                if (x != c.x)
                    city.roads.Add(new RoadState { cell = new Cell(x, c.z) });
                city.roads.Add(new RoadState { cell = new Cell(x, c.z - 3) });
            }
            for (int i = 0; i < small.Count; i++)
                city.buildings.Add(new BuildingState { cell = new Cell(west + i % half, c.z + (i < half ? 1 : -1)), def = small[i] });
            int landmarkX = west;
            foreach (int def in large)
            {
                int side = BuildingCatalog.Size(def);
                city.buildings.Add(new BuildingState { cell = new Cell(landmarkX, c.z - 3 - side), def = def });
                landmarkX += side + 1;
            }
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
            Assert.That(app.World.transform.Find("Cities").GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(10), "every style draws");
            for (int shot = 0; shot < 4; shot++)
            {
                app.Camera.focus = shot < 2 ? new Vector3(west + half * (shot * 2 + 1) / 4f, 0, c.z) : new Vector3(west + (landmarkX - west) * (shot == 2 ? .25f : .7f), 0, c.z - 7);
                app.Camera.zoom = 7;
                yield return null;
                yield return Capture(app, $"Logs/building-catalog-{shot + 1}.png");
            }
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator CrossingSignalsRenderAndChangeWithSimulationTime()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.Game.World.speed = 0;
            Assert.That(app.Game.Build.CommitBuild(app.Game.Build.Preview(new Cell(22, 15), new Cell(28, 15))).ok, Is.True);
            Assert.That(app.Game.Build.CommitBuild(app.Game.Build.Preview(new Cell(25, 13), new Cell(25, 17))).ok, Is.True);
            app.World.Refresh();
            app.Game.World.tick = 0;
            app.World.Animate(0, true);
            var signals = app.World.transform.Find("Crossing signals");
            Assert.That(signals, Is.Not.Null);
            int lights = 0;
            Renderer firstGreen = null;
            foreach (var renderer in signals.GetComponentsInChildren<Renderer>())
                if (renderer.name == "Green light") { lights++; if (!firstGreen) firstGreen = renderer; }
            Assert.That(lights, Is.EqualTo(4));
            var color = firstGreen.sharedMaterial.color;
            app.Game.World.tick = 120;
            app.World.Animate(0, true);
            Assert.That(firstGreen.sharedMaterial.color, Is.Not.EqualTo(color));
            yield return Capture(app, "Logs/crossing-preview.png");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ThreeRoutesRunAndRenderWithoutErrors()
        {
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app)
                app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(true);
            yield return null;
            yield return null;
            Assert.That(app.Game.World.trains.Count, Is.EqualTo(3));
            Assert.That(app.Game.World.stations.Count, Is.EqualTo(6));
            Assert.That(app.Game.World.cities.Count, Is.EqualTo(app.Game.World.producers.FindAll(p => p.kind == ProducerKind.Town).Count), "Every town founds a city");
            app.Game.World.speed = 0;
            for (int i = 0; i < 1200; i++)
                app.Game.Step();
            app.World.Refresh();
            app.World.Animate(1, true);
            yield return null;
            Assert.That(app.Game.World.delivered, Is.GreaterThanOrEqualTo(100));
            Assert.That(app.Game.World.cityRevision, Is.GreaterThan(0), "served towns act within the first minute");
            foreach (var train in app.Game.World.trains)
            {
                var pos = RailGeometry.TrainPosition(app.Game, train, 0, out var forward);
                Assert.That(float.IsNaN(pos.x), Is.False);
                Assert.That(forward.sqrMagnitude, Is.GreaterThan(.9f));
            }
            yield return Capture(app, "Logs/world-preview.png");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator BuildPreviewCancelDoesNotModifyWorld()
        {
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app)
                app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            yield return null;
            app.ChooseTool(ToolMode.Track);
            app.BeginTrack(new Cell(8, 15));
            app.ExtendTrack(new Cell(50, 15));
            yield return Capture(app, "Logs/build-preview.png");
            // Closing the panel must cancel the pending build, not just hide its controls.
            app.UI.transform.Find("Safe area/Context/Scrollable details/CLOSE").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            app.ConfirmBuild();
            Assert.That(app.Game.World.tracks.Count, Is.Zero);
            Assert.That(app.Game.World.money, Is.EqualTo(50000));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator MenuFocusAndContextActionsRemainUsable()
        {
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(true);
            yield return null;
            app.SetSpeed(2);
            app.UI.ShowMenu(false);
            var safe = app.UI.transform.Find("Safe area");
            Assert.That(safe.Find("Menu backdrop").gameObject.activeSelf, Is.True);
            Assert.That(safe.Find("Guidance").gameObject.activeSelf, Is.False);
            Assert.That(app.Game.World.speed, Is.Zero);
            yield return Capture(app, "Logs/menu-preview.png");
            app.UI.CloseMenu();
            Assert.That(app.Game.World.speed, Is.EqualTo(2));
            Assert.That(safe.Find("Menu backdrop").gameObject.activeSelf, Is.False);
            app.UI.Train(app.Game.World.trains[0]);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var body = safe.Find("Context/Scrollable details");
            float previousBottom = 0;
            foreach (var button in body.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                if (button.name == "CLOSE") continue;
                var rect = (RectTransform)button.transform;
                float top = -rect.anchoredPosition.y;
                Assert.That(top, Is.GreaterThanOrEqualTo(previousBottom));
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(96));
                previousBottom = top + rect.rect.height;
            }
            Assert.That(((RectTransform)body).rect.height, Is.GreaterThanOrEqualTo(previousBottom));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator NewTrainModelsRenderAndAppearInShop()
        {
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            foreach (int model in new[] { 3, 4, 5 })
            {
                app.NewGame(true);
                yield return null;
                var old = app.Game.World.trains.Find(t => model == 3 ? t.cargo == Cargo.Coal : t.cargo == Cargo.Passengers);
                int a = old.a, b = old.b;
                var cargo = old.cargo;
                Assert.That(app.Game.Trains.Sell(old.id).ok, Is.True);
                var purchase = app.Game.Trains.Buy(a, model, cargo);
                Assert.That(purchase.ok, Is.True, purchase.message);
                Assert.That(app.Game.Trains.AssignRoute(purchase.id, a, b).ok, Is.True);
                app.World.Refresh();
                app.World.Animate(1, true);
                app.UI.Train(app.Game.Trains.Train(purchase.id));
                yield return Capture(app, "Logs/train-model-" + model + ".png");
            }
            // The shop offers only cargo the station's producer makes or takes. The demo's first station is
            // Pinecrest Mine; a town takes goods and passengers, so every model, old and new, is sold there.
            var mine = ShopCards(app, app.Game.World.stations[0]);
            Assert.That(mine.Length, Is.EqualTo(3), "Three freight models carry coal");
            foreach (var card in mine)
                Assert.That(card, Does.Contain("· Coal"));
            var town = app.Game.World.stations.Find(s => app.Game.Cargo.Producer(s.producerId).kind == ProducerKind.Town);
            var cards = ShopCards(app, town);
            Assert.That(cards.Length, Is.EqualTo(6), "Goods on three freight models and three passenger models");
            for (int model = 0; model < TrainCatalog.Count; model++)
                Assert.That(System.Array.Exists(cards, card => card.StartsWith(TrainCatalog.Name(model))), Is.True, TrainCatalog.Name(model) + " is sold at a town");
            yield return Capture(app, "Logs/train-shop.png");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator StationPassengerCountUpdatesAfterBoarding()
        {
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(true);
            yield return null;
            app.SetSpeed(0);
            var train = app.Game.World.trains.Find(t => t.cargo == Cargo.Passengers);
            var station = app.Game.Trains.Station(train.stationId);
            var destination = app.Game.Trains.Station(train.destination);
            var producer = app.Game.Cargo.Producer(station.producerId);
            producer.inventory = 125;
            train.units = 0;
            app.UI.Station(station);
            app.UI.UpdateHud();
            var label = app.World.transform.Find("Stations/Station status " + station.id).GetComponent<TMPro.TMP_Text>();
            Assert.That(label.text, Does.StartWith("125<").And.Contain(" waiting"));
            Assert.That(Vector3.Dot(label.transform.up, Vector3.up), Is.GreaterThan(.99f), "the letters stand upright");
            Assert.That(Vector3.Dot(label.transform.forward, app.Camera.view.transform.forward), Is.GreaterThan(.8f), "square to the camera");
            Assert.That(label.transform.position.y, Is.InRange(1.1f, 2f), "on the roof, not floating over the town");
            label.ForceMeshUpdate();
            Assert.That(Mathf.Abs(label.textInfo.characterInfo[0].baseLine), Is.LessThan(.05f), "the letters stand on the roof");
            Assert.That(StationLayout.Platforms(station), Is.EqualTo(1));
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(7f), "big enough to read on a short one-platform halt");
            var board = label.transform.Find("Sign board");
            Assert.That(board.localScale.x, Is.GreaterThan(label.textBounds.size.x), "the words stand on a board that frames them");
            var body = app.UI.transform.Find("Safe area/Context/Scrollable details");
            Assert.That(System.Array.Exists(body.GetComponentsInChildren<TMPro.TMP_Text>(), t => t.text.Contains("125 passengers waiting")), Is.True);
            app.Camera.focus = new Vector3(station.cell.x, 0, station.cell.z);
            app.Camera.zoom = 9;
            yield return Capture(app, "Logs/station-passengers.png");
            app.Game.Cargo.Service(train, station, destination);
            app.UI.UpdateHud();
            int remaining = 125 - app.Game.Balance.capacity[train.model];
            Assert.That(producer.inventory, Is.EqualTo(remaining));
            Assert.That(label.text, Does.StartWith(remaining + "<"));
            Assert.That(System.Array.Exists(body.GetComponentsInChildren<TMPro.TMP_Text>(), t => t.text.Contains(remaining + " passengers waiting")), Is.True);
            producer.inventory = 0;
            app.UI.UpdateHud();
            Assert.That(label.text, Does.StartWith("0<"));
            var picture = app.World.TrainPicture(5, Cargo.Passengers);
            Assert.That(picture.IsCreated(), Is.True);
            Assert.That(app.World.TrainPicture(5, Cargo.Passengers), Is.SameAs(picture), "Reuse rendered pictures");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator AutoDestinationButtonStartsService()
        {
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(true);
            app.Game.World.speed = 0;
            var old = app.Game.World.trains[0];
            int origin = old.a, destination = old.b;
            Assert.That(app.Game.Trains.Sell(old.id).ok, Is.True);
            var purchase = app.Game.Trains.Buy(origin, 0, Cargo.Coal);
            Assert.That(purchase.ok, Is.True);
            var train = app.Game.Trains.Train(purchase.id);
            app.UI.Train(train);
            var body = app.UI.transform.Find("Safe area/Context/Scrollable details");
            body.Find("CHOOSE DESTINATION").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return Capture(app, "Logs/auto-destination.png");
            body.Find("AUTO DESTINATION").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(train.destination, Is.EqualTo(destination));
            Assert.That(train.state, Is.EqualTo(ServiceState.Loading));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator AiFixButtonRepairsBlockedJunction()
        {
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var game = app.Game;
            game.World.money = 200000;
            // A branch at (36,46) makes a turnout whose arms do not connect, so the two platforms cannot reach each other.
            Assert.That(game.Build.CommitBuild(game.Build.Preview(new Cell(14, 46), new Cell(50, 46))).ok, Is.True);
            Assert.That(game.Build.CommitBuild(game.Build.Preview(new Cell(36, 46), new Cell(36, 50))).ok, Is.True);
            int a = game.Stations.Place(new Cell(16, 46), 4).id, b = game.Stations.Place(new Cell(48, 46), 5).id;
            var purchase = game.Trains.Buy(a, 2, Cargo.Passengers);
            Assert.That(purchase.ok, Is.True);
            var train = game.Trains.Train(purchase.id);
            Assert.That(game.Trains.AutoDestination(train.id).ok, Is.False);
            app.World.Refresh();
            app.UI.Train(train);
            var body = app.UI.transform.Find("Safe area/Context/Scrollable details");
            body.Find("AI FIX RAILS").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return Capture(app, "Logs/ai-rail-fix.png");
            Assert.That(System.Array.Exists(body.GetComponentsInChildren<TMPro.TMP_Text>(), t => t.text.Contains("junction blocks the way")), Is.True);
            Transform confirm = null;
            foreach (Transform child in body)
                if (child.name.StartsWith("BUILD & START"))
                    confirm = child;
            Assert.That(confirm, Is.Not.Null, "The fix offers to build and start");
            int tracks = game.World.tracks.Count;
            confirm.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(train.a, Is.EqualTo(a));
            Assert.That(train.b, Is.EqualTo(b));
            Assert.That(train.state, Is.EqualTo(ServiceState.Loading));
            Assert.That(game.World.tracks.Count, Is.GreaterThan(tracks));
            Assert.That(body.Find("AI FIX RAILS"), Is.Not.Null, "Back on the train panel after the fix");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator CityCarsDrivePauseAndFollowNewStreets()
        {
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var traffic = app.World.transform.Find("City traffic");
            int cars = traffic.childCount;
            Assert.That(cars, Is.EqualTo(app.Game.World.cities.Count), "One car on each day-0 town square");
            var car = traffic.GetChild(0);
            var initial = car.position;
            app.World.Animate(.3f, true);
            Assert.That(car.position, Is.EqualTo(initial));
            app.Game.World.speed = 1;
            app.World.Animate(.3f, false);
            Assert.That(Vector3.Distance(car.position, initial), Is.GreaterThan(.05f));
            app.World.Refresh();
            Assert.That(traffic.GetChild(0), Is.SameAs(car), "Unchanged streets preserve cars");
            var city = app.Game.World.cities[0];
            // A separate street tests automatic discovery, turns and dead ends.
            for (int i = 0; i < 8; i++)
                city.roads.Add(new RoadState { cell = new Cell(city.center.x + i, city.center.z + 3) });
            app.Game.World.cityRevision++;
            app.World.Refresh();
            app.Game.World.speed = 0;
            yield return null;
            Assert.That(traffic.childCount, Is.GreaterThan(cars));
            app.Game.World.speed = 4;
            for (int frame = 0; frame < 400; frame++)
            {
                app.World.Animate(.05f, false);
                foreach (Transform vehicle in traffic)
                {
                    var cell = new Cell(Mathf.RoundToInt(vehicle.position.x), Mathf.RoundToInt(vehicle.position.z));
                    Assert.That(app.Game.World.cities.Exists(c => c.roads.Exists(r => r.cell.Equals(cell))), Is.True, "Cars stay on roads");
                    Assert.That(float.IsNaN(vehicle.position.x), Is.False);
                }
                AssertNoCollisions(traffic);
            }
            app.SetSpeed(0);
            app.Camera.focus = new Vector3(city.center.x, 0, city.center.z);
            app.Camera.zoom = 6;
            yield return Capture(app, "Logs/city-traffic.png");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator CityCarsDriveFromTownToTownOnOpenHighways()
        {
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var world = app.Game.World;
            CityState from = world.cities[0], to = null;
            foreach (var c in world.cities)
                if (c != from && (to == null || c.center.Distance(from.center) < to.center.Distance(from.center)))
                    to = c;
            // An open highway from the main-street stub facing the other town to that town's facing stub.
            int east = to.center.x > from.center.x ? 1 : -1;
            var start = new Cell(from.center.x + east, from.center.z);
            var end = new Cell(to.center.x - east, to.center.z);
            var road = new IntercityRoadState { a = from.producerId, b = to.producerId };
            for (var cell = start; ; cell = cell.x != end.x ? new Cell(cell.x + east, cell.z) : new Cell(cell.x, cell.z + (end.z > cell.z ? 1 : -1)))
            {
                road.path.Add(cell);
                if (cell.Equals(end)) break;
            }
            road.built = road.path.Count;
            world.intercityRoads.Add(road);
            world.cityRevision++;
            app.World.Refresh();
            yield return null;
            var traffic = app.World.transform.Find("City traffic");
            Assert.That(traffic.childCount, Is.GreaterThan(world.cities.Count), "Open highways get their own traffic");
            bool Street(CityState city, Cell cell) => city.roads.Exists(r => r.cell.Equals(cell));
            var leftTown = new System.Collections.Generic.HashSet<Transform>();
            bool arrived = false;
            world.speed = 4;
            for (int frame = 0; frame < 8000 && !arrived; frame++)
            {
                app.World.Animate(.05f, false);
                foreach (Transform car in traffic)
                {
                    var cell = new Cell(Mathf.RoundToInt(car.position.x), Mathf.RoundToInt(car.position.z));
                    Assert.That(world.cities.Exists(c => Street(c, cell)) || road.path.Contains(cell), Is.True, "Cars stay on streets and highways");
                    if (Street(from, cell)) leftTown.Add(car);
                    else if (leftTown.Contains(car) && Street(to, cell)) arrived = true;
                }
                AssertNoCollisions(traffic);
            }
            Assert.That(arrived, Is.True, "A car leaves its town on the highway and reaches the next town");
            app.SetSpeed(0);
            app.Camera.focus = new Vector3((start.x + end.x) / 2f, 0, (start.z + end.z) / 2f);
            app.Camera.zoom = 12;
            yield return Capture(app, "Logs/highway-traffic.png");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ThemeMusicFollowsTitleGameAndToggle()
        {
            int savedPreference = PlayerPrefs.GetInt("music", 1);
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            try
            {
                if (!app.MusicEnabled)
                    app.ToggleMusic();
                var music = app.GetComponent<ThemeMusic>();
                Assert.That(music, Is.Not.Null);
                app.Title();
                yield return null;
                Assert.That(music.TargetVolume, Is.EqualTo(ThemeMusic.TitleVolume));
                Assert.That(music.Source.loop, Is.False, "Pieces play once, then the shuffle moves on");
                Assert.That(music.Source.isPlaying, Is.True);
                Assert.That(music.CurrentTrack, Is.Not.Null);
                app.NewGame(false);
                yield return null;
                Assert.That(music.TargetVolume, Is.EqualTo(ThemeMusic.GameVolume).And.LessThan(ThemeMusic.TitleVolume));
                app.ToggleMusic();
                Assert.That(app.MusicEnabled, Is.False);
                Assert.That(PlayerPrefs.GetInt("music", 1), Is.EqualTo(0));
                Assert.That(music.TargetVolume, Is.Zero);
                yield return new WaitForSecondsRealtime(2);
                Assert.That(music.Source.volume, Is.Zero);
                Assert.That(music.Source.isPlaying, Is.False, "A muted stream is paused, not decoded");
                app.ToggleMusic();
                yield return null;
                Assert.That(music.Source.isPlaying, Is.True);
            }
            finally
            {
                PlayerPrefs.SetInt("music", savedPreference);
                PlayerPrefs.Save();
            }
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ThemeMusicMovesOnToAnotherPieceWhenOneEnds()
        {
            int savedPreference = PlayerPrefs.GetInt("music", 1);
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            try
            {
                if (!app.MusicEnabled)
                    app.ToggleMusic();
                app.Title();
                var music = app.GetComponent<ThemeMusic>();
                yield return null;
                var first = music.CurrentTrack;
                Assert.That(first, Is.Not.Null);
                music.Source.time = music.Source.clip.length - .2f;
                float waited = 0;
                while (music.CurrentTrack == first && waited < ThemeMusic.GapSeconds + 3)
                {
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                }
                Assert.That(music.CurrentTrack, Is.Not.SameAs(first), "The shuffle never plays a piece twice in a row");
                Assert.That(waited, Is.GreaterThanOrEqualTo(ThemeMusic.GapSeconds), "A short silence separates the pieces");
                Assert.That(music.Source.isPlaying, Is.True);
            }
            finally
            {
                PlayerPrefs.SetInt("music", savedPreference);
                PlayerPrefs.Save();
            }
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator SoundEffectsNeedZoomExceptDeliveryIncome()
        {
            int savedPreference = PlayerPrefs.GetInt("sound", 1);
            var app = Object.FindFirstObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            try
            {
                if (!app.SoundEnabled)
                    app.ToggleSound();
                app.NewGame(false);
                app.SetSpeed(0);
                var sfx = app.Sfx;
                var g = app.Game;
                // The new-game overview zoom: building is silent.
                app.Camera.focus = new Vector3(29, 0, 15);
                app.Camera.zoom = 58;
                yield return null;
                int tracks = sfx.Played(SoundCue.TrackBuilt), stations = sfx.Played(SoundCue.StationBuilt), errors = sfx.Played(SoundCue.Error);
                Assert.That(app.Perform(() => g.Build.CommitBuild(g.Build.Preview(new Cell(8, 15), new Cell(50, 15)))).ok, Is.True);
                int a = app.Perform(() => g.Stations.Place(new Cell(10, 15), 1)).id;
                app.Perform(() => g.Stations.Place(new Cell(10, 15), 1));
                yield return null;
                Assert.That(sfx.Played(SoundCue.TrackBuilt), Is.EqualTo(tracks), "Zoomed out: no construction sound");
                Assert.That(sfx.Played(SoundCue.StationBuilt), Is.EqualTo(stations));
                Assert.That(sfx.Played(SoundCue.Error), Is.EqualTo(errors), "Zoomed out: no error buzz");
                // Zoomed in with the site on screen: the station and a rejected action are heard.
                app.Camera.focus = new Vector3(48, 0, 15);
                app.Camera.zoom = 9;
                yield return null;
                int b = app.Perform(() => g.Stations.Place(new Cell(48, 15), 2)).id;
                app.Perform(() => g.Stations.Place(new Cell(48, 15), 2));
                Assert.That(sfx.Played(SoundCue.Error), Is.EqualTo(errors + 1), "Zoomed in: a rejected action buzzes");
                yield return null;
                Assert.That(sfx.Played(SoundCue.StationBuilt), Is.EqualTo(stations + 1), "Zoomed in on screen: station sound");
                // Zoomed in elsewhere: a purchase at an off-screen station is silent.
                app.Camera.focus = new Vector3(100, 0, 100);
                yield return null;
                int bought = sfx.Played(SoundCue.TrainBought);
                int train = app.Perform(() => g.Trains.Buy(a, 0, Cargo.Coal)).id;
                Assert.That(app.Perform(() => g.Trains.AssignRoute(train, a, b)).ok, Is.True);
                yield return null;
                Assert.That(sfx.Played(SoundCue.TrainBought), Is.EqualTo(bought), "Off-screen: no purchase sound");
                // Delivery income is heard at the overview zoom, far from the station.
                app.Camera.zoom = 58;
                yield return null;
                int coins = sfx.Played(SoundCue.Coins) + sfx.Played(SoundCue.CoinsBig);
                var t = g.Trains.Train(train);
                g.Cargo.Service(t, g.Trains.Station(b), g.Trains.Station(a));
                yield return null;
                Assert.That(sfx.Played(SoundCue.Coins) + sfx.Played(SoundCue.CoinsBig), Is.EqualTo(coins + 1), "Income is audible at any zoom");
                // SOUND off silences income too (wait out the coin cooldown first so only the toggle can explain silence).
                yield return new WaitForSecondsRealtime(.5f);
                app.ToggleSound();
                g.Cargo.Service(t, g.Trains.Station(a), g.Trains.Station(b));
                g.Cargo.Service(t, g.Trains.Station(b), g.Trains.Station(a));
                yield return null;
                Assert.That(sfx.Played(SoundCue.Coins) + sfx.Played(SoundCue.CoinsBig), Is.EqualTo(coins + 1), "SOUND off silences income");
                app.ToggleSound();
                // The rail loop plays while a moving train is on screen at close zoom, and fades out when zoomed out.
                app.Camera.zoom = 9;
                app.SetSpeed(4);
                for (float waited = 0; waited < 5 && !sfx.Loop.isPlaying; waited += Time.unscaledDeltaTime)
                {
                    if (t.state == ServiceState.Travelling && t.step < t.path.Count)
                        app.Camera.focus = RailGeometry.TrainPosition(g, t, 0, out _);
                    yield return null;
                }
                Assert.That(sfx.Loop.isPlaying, Is.True, "A moving train on screen is heard");
                app.Camera.zoom = 58;
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(sfx.Loop.isPlaying, Is.False, "Zoomed out: the rail loop fades out");
                app.SetSpeed(0);
            }
            finally
            {
                PlayerPrefs.SetInt("sound", savedPreference);
                PlayerPrefs.Save();
            }
            LogAssert.NoUnexpectedReceived();
        }
        /// <summary>Opens the station's shop and returns the train card labels.</summary>
        static string[] ShopCards(GameBootstrap app, StationState station)
        {
            app.UI.Station(station);
            var body = app.UI.transform.Find("Safe area/Context/Scrollable details");
            // Cleared panel buttons stay in the hierarchy until the frame ends, so only active ones count.
            System.Array.Find(body.GetComponentsInChildren<UnityEngine.UI.Button>(), b => b.name == "BUY A TRAIN").onClick.Invoke();
            var cards = new System.Collections.Generic.List<string>();
            foreach (var button in body.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (button.name != "CLOSE")
                    cards.Add(button.name);
            return cards.ToArray();
        }
        /// <summary>Vehicles keep their distance: two whose centres come within a car's width are driving through each other.</summary>
        static void AssertNoCollisions(Transform traffic)
        {
            for (int i = 0; i < traffic.childCount; i++)
                for (int j = i + 1; j < traffic.childCount; j++)
                {
                    Vector3 a = traffic.GetChild(i).position, b = traffic.GetChild(j).position;
                    Assert.That(new Vector2(a.x - b.x, a.z - b.z).magnitude, Is.GreaterThan(.2f), "Cars never drive through each other");
                }
        }
        static IEnumerator Capture(GameBootstrap app, string filename)
        {
            var camera = app.Camera.view;
            camera.aspect = 16f / 9;
            var canvas = app.UI.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 3;
            Canvas.ForceUpdateCanvases();
            var texture = new RenderTexture(1600, 900, 24);
            camera.targetTexture = texture;
            for (int frame = 0; frame < 30; frame++)
                yield return null;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(filename, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(texture);
            Object.Destroy(image);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
