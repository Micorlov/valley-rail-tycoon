using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>
    /// Campsites outside the towns (built in stages, then a fire, campers and visitors driving in) and buying a
    /// roadside filling station from its tap window.
    /// </summary>
    public class CampsiteTests
    {
        const float Step = .05f;
        GameBootstrap app;
        IntercityRoadState road;

        /// <summary>A new game with an open highway running straight east from Oakridge, as RoadsideTests lays it.</summary>
        IEnumerator BuildHighway()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var game = app.Game;
            var town = game.Cities.CityFor(5);
            int east = town.center.x;
            foreach (var r in town.roads)
                if (r.cell.z == town.center.z)
                    east = Mathf.Max(east, r.cell.x);
            road = new IntercityRoadState { a = town.producerId, b = 14 };
            for (int x = east; x <= east + 24; x++)
                road.path.Add(new Cell(x, town.center.z));
            road.built = road.path.Count;
            game.World.intercityRoads.Add(road);
            game.Cities.Rebuild();
            game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
        }
        void RunUntilCamp(int stage)
        {
            for (int i = 0; i < 4000 && road.camp < stage; i++)
                app.Game.Step();
            Assert.That(road.camp, Is.EqualTo(stage), "the campsite goes up a stage every 200 ticks");
            app.World.Refresh();
        }
        CampLife Life => app.World.GetComponentInChildren<CampLife>(true);

        [UnityTest]
        public IEnumerator ACampsiteOpensOutsideTownWithAFireCampersAndVisitors()
        {
            yield return BuildHighway();
            RunUntilCamp(1);
            Assert.That(Campsites.Shaped(road) && Campsites.Town(road) == 5, "planned in Oakridge's half of the highway");
            for (int i = 0; i < Campsites.Cells; i++)
                Assert.That(app.Game.Build.Placeable(Campsites.SiteCell(road, i)), Is.False, "track may not cross the campsite");
            RunUntilCamp(2);
            Assert.That(Life.Sites, Is.EqualTo(0), "no campers while it is being built");
            RunUntilCamp(Campsites.Steps);
            var life = Life;
            Assert.That(life.Sites, Is.EqualTo(1));
            Assert.That(life.Campers(0), Is.GreaterThanOrEqualTo(5), "campers round the fire and about the site");
            yield return null;
            Assert.That(life.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
            float low = 1, high = 0;
            bool parked = false, pitched = false;
            for (int i = 0; i < 3600; i++)
            {
                life.Animate(Step, 1, true);
                low = Mathf.Min(low, life.FlameHeight(0));
                high = Mathf.Max(high, life.FlameHeight(0));
                parked |= life.VisitorParked(0);
                pitched |= life.TentUp(0) > .99f;
            }
            Assert.That(high - low, Is.GreaterThan(.02f), "the campfire flickers");
            Assert.That(life.Arrivals, Is.GreaterThanOrEqualTo(2), "visitors drive in from the highway");
            Assert.That(life.Departures, Is.GreaterThanOrEqualTo(1), "and drive off again");
            Assert.That(parked, "a visitor parks on the free pitch");
            Assert.That(pitched, "and pitches a tent at a tent campsite");
            foreach (var t in life.GetComponentsInChildren<Transform>(true))
                Assert.That(float.IsNaN(t.position.x) || float.IsNaN(t.position.z), Is.False, t.name);
            // Any town change redraws the towns; the campsite carries on with the same visitors.
            int arrivals = life.Arrivals;
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(life.Sites, Is.EqualTo(1));
            Assert.That(life.Arrivals, Is.EqualTo(arrivals));
            for (int kind = 1; kind < Campsites.Kinds; kind++)
            {
                road.campKind = kind;
                app.Game.World.cityRevision++;
                app.World.Refresh();
                Assert.That(life.Sites, Is.EqualTo(1));
                Assert.That(life.Kind(0), Is.EqualTo(kind));
                int arrived = life.Arrivals;
                for (int i = 0; i < 1200; i++)
                    life.Animate(Step, 1, true);
                Assert.That(life.Arrivals, Is.GreaterThan(arrived), Campsites.KindName(kind) + " gets visitors");
                Assert.That(life.TentUp(0), Is.EqualTo(0), "only a tent campsite pitches tents");
            }
            // Zoomed out, the people hide; paused, nothing moves.
            life.Animate(Step, 1, false);
            // Earlier styles' roots are switched off and destroyed at the end of the frame: look at the live one.
            Transform people = null;
            foreach (Transform child in life.transform)
                if (child.gameObject.activeSelf)
                    people = child.Find("Campsite people");
            Assert.That(people.gameObject.activeSelf, Is.False);
            float flame = life.FlameHeight(0);
            life.Animate(Step, 0, false);
            Assert.That(life.FlameHeight(0), Is.EqualTo(flame));
            // A tap on the campsite opens its window.
            app.Select(Campsites.SiteCell(road, 5));
            Assert.That(ContextText(), Does.Contain("PITCHES"));
        }

        [UnityTest]
        public IEnumerator AFillingStationIsBoughtFromItsWindowForOverAMillion()
        {
            yield return BuildHighway();
            for (int i = 0; i < 4000 && !Roadside.Open(road); i++)
                app.Game.Step();
            Assert.That(Roadside.Open(road));
            app.World.Refresh();
            var w = app.Game.World;
            w.money = 3_000_000;
            app.Select(Roadside.SiteCell(road, 1));
            Assert.That(ContextText(), Does.Contain("FOR SALE"));
            var buy = FindButton("BUY FOR $");
            Assert.That(buy, Is.Not.Null, "an open station has a BUY button");
            int price = app.Game.Cities.ServicePrice(road);
            Assert.That(price, Is.GreaterThan(1_000_000));
            buy.onClick.Invoke();
            yield return null;
            Assert.That(road.serviceOwned, "bought");
            Assert.That(w.money, Is.EqualTo(3_000_000 - price));
            Assert.That(ContextText(), Does.Contain("YOUR STATION"));
            Assert.That(FindButton("BUY FOR $"), Is.Null, "no BUY button once it is yours");
            int before = w.money;
            for (int i = 0; i < ServiceSales.PayPeriod; i++)
                app.Game.Step();
            Assert.That(w.money, Is.GreaterThan(before), "it pays every minute");
            // Too poor: the window stays and nothing is spent.
            var second = w.intercityRoads.Find(r => Roadside.Open(r) && !r.serviceOwned);
            if (second != null)
            {
                w.money = 10_000;
                app.UI.ServiceArea(second);
                FindButton("BUY FOR $").onClick.Invoke();
                Assert.That(second.serviceOwned, Is.False);
                Assert.That(w.money, Is.EqualTo(10_000));
            }
        }
        string ContextText()
        {
            var text = "";
            foreach (var t in app.UI.GetComponentsInChildren<TMPro.TMP_Text>())
                text += t.text + "\n";
            return text;
        }
        UnityEngine.UI.Button FindButton(string prefix)
        {
            foreach (var b in app.UI.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (b.name.StartsWith(prefix))
                    return b;
            return null;
        }

        /// <summary>
        /// Renders campsites for review: Logs/camp-kind-0..2.png (each style), camp-stage-1..2.png (under construction),
        /// camp-wide.png (with its highway and town), camp-back.png (the other way round) and camp-owned-station.png (a bought filling station's flag).
        /// </summary>
        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowCampsites()
        {
            yield return BuildHighway();
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            app.Camera.enabled = false;
            var f = default(CampLayout);
            for (int stage = 1; stage < Campsites.Steps; stage++)
            {
                RunUntilCamp(stage);
                f = new CampLayout(road);
                yield return Shoot(camera, f.At(0, 2f, .2f), 2.6f, $"Logs/camp-stage-{stage}.png");
            }
            RunUntilCamp(Campsites.Steps);
            f = new CampLayout(road);
            var life = Life;
            var centre = f.At(0, 2f, .2f);
            for (int kind = 0; kind < Campsites.Kinds; kind++)
            {
                road.campKind = kind;
                app.Game.World.cityRevision++;
                app.World.Refresh();
                for (int i = 0; i < 900; i++)
                    life.Animate(Step, 1, true);
                yield return Shoot(camera, centre, 2.4f, $"Logs/camp-kind-{kind}.png");
            }
            road.campKind = 0;
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return Shoot(camera, centre, 9f, "Logs/camp-wide.png");
            var turn = camera.transform.rotation;
            camera.transform.rotation = Quaternion.Euler(0, 180, 0) * turn;
            yield return Shoot(camera, centre, 2.4f, "Logs/camp-back.png");
            camera.transform.rotation = turn;
            for (int i = 0; i < 4000 && !Roadside.Open(road); i++)
                app.Game.Step();
            app.Game.World.money = 5_000_000;
            app.Game.Cities.BuyService(road);
            app.World.Refresh();
            yield return Shoot(camera, new RoadsideLayout(road).At(.6f, 1f, .3f), 1.8f, "Logs/camp-owned-station.png");
            app.Camera.enabled = true;
            Assert.That(File.Exists("Logs/camp-kind-0.png"));
        }
        static IEnumerator Shoot(Camera camera, Vector3 target, float zoom, string file)
        {
            camera.orthographicSize = zoom;
            camera.transform.position = target - camera.transform.forward * 180;
            camera.aspect = 16f / 9;
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            for (int frame = 0; frame < 3; frame++)
                yield return null;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            File.WriteAllBytes(file, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(texture);
            Object.Destroy(image);
        }
    }
}
