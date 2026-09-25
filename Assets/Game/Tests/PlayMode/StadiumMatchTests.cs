using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>A stadium fills with fans and a football match plays on its pitch.</summary>
    public class StadiumMatchTests
    {
        const float Step = .05f;
        GameBootstrap app;
        Cell lot;
        int size;

        /// <summary>A new game with the town cleared down to one 8×8 stadium on a corner lot facing the camera.</summary>
        IEnumerator BuildStadium()
        {
            app = Object.FindAnyObjectByType<GameBootstrap>();
            if (!app) app = new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var city = app.Game.World.cities[0];
            lot = new Cell(city.center.x - 14, city.center.z - 12);
            int def = System.Array.FindIndex(BuildingCatalog.Defaults, d => d.key == "stadium-bowl");
            Assert.That(def, Is.GreaterThanOrEqualTo(0), "stadium-bowl is in the catalog");
            size = BuildingCatalog.Size(def);
            city.buildings.Clear();
            city.roads.Clear();
            city.roads.Add(new RoadState { cell = city.center });
            for (int x = lot.x - 1; x <= lot.x + size; x++)
                city.roads.Add(new RoadState { cell = new Cell(x, lot.z - 1) });
            for (int z = lot.z; z < lot.z + size; z++)
                city.roads.Add(new RoadState { cell = new Cell(lot.x - 1, z) });
            city.buildings.Add(new BuildingState { cell = lot, def = def });
            app.Game.World.cityRevision++;
            app.World.Refresh();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StadiumFillsWithFansAndPlaysFootball()
        {
            yield return BuildStadium();
            var matches = app.World.GetComponentInChildren<StadiumMatch>(true);
            Assert.That(matches.Grounds, Is.EqualTo(1));
            Assert.That(matches.Fans, Is.GreaterThan(800), "the stands are nearly full");
            var match = matches.MatchAt(0);
            Assert.That(match.players.Length, Is.EqualTo(23), "two elevens and a referee");
            Assert.That(matches.GetComponentsInChildren<Collider>(true), Is.Empty, "no physics: the player build strips it");
            var ultras = matches.UltrasAt(0);
            Assert.That(ultras.Flares, Is.EqualTo(16), "eight flares behind each goal");
            Assert.That(ultras.Flags, Is.EqualTo(20));
            Assert.That(matches.GetComponentsInChildren<TMPro.TextMeshPro>(true).Count(t => t.text == StadiumUltras.Slogan), Is.EqualTo(8),
                "two banners on each touchline, one along each end and a big one over each end");
            Assert.That(match.score, Is.EqualTo(FootballMatch.FinalScore), "a new stadium joins its match already at 8-2");
            var ball = matches.BallAt(0);
            var view = app.Camera.view.transform.forward;
            bool wasOver = false;
            int finals = 0;
            float outside = float.MinValue;
            var kickoff = ball.localPosition;
            float travelled = 0;
            // Ten minutes of football, driven directly so the test does not depend on the camera.
            for (int i = 0; i < 12000; i++)
            {
                var before = ball.localPosition;
                matches.Animate(Step, 1, true, view);
                travelled += Vector3.Distance(before, ball.localPosition);
                outside = Mathf.Max(outside, Mathf.Abs(ball.localPosition.x) - size * .5f, Mathf.Abs(ball.localPosition.z) - size * .5f);
                foreach (var p in match.players)
                    Assert.That(float.IsNaN(p.position.x) || float.IsNaN(p.position.y), Is.False);
                bool over = match.Stage == FootballMatch.Phase.FullTime;
                if (over && !wasOver)
                {
                    finals++;
                    Assert.That(match.score, Is.EqualTo(FootballMatch.FinalScore), "every match ends 8-2 to the yellow side");
                }
                wasOver = over;
            }
            Assert.That(finals, Is.GreaterThanOrEqualTo(2));
            Assert.That(travelled, Is.GreaterThan(50), "the ball moves about");
            Assert.That(outside, Is.LessThan(0), "the ball stays inside the ground");
            Assert.That(match.Passes, Is.GreaterThan(50));
            Assert.That(match.Shots, Is.GreaterThan(5));
            Assert.That(match.Goals, Is.GreaterThanOrEqualTo(10), "a whole match of goals: 8 + 2");
            Assert.That(match.Matches, Is.GreaterThanOrEqualTo(2), "a new match kicks off after full time");
            // Any town change redraws the buildings; the ground and its match carry on.
            app.Game.World.cityRevision++;
            app.World.Refresh();
            Assert.That(matches.MatchAt(0), Is.SameAs(match));
            Assert.That(ball.localPosition, Is.Not.EqualTo(kickoff));
        }

        [UnityTest]
        public IEnumerator StandsFacingTheCameraAreCutAwayWhenZoomedIn()
        {
            yield return BuildStadium();
            var matches = app.World.GetComponentInChildren<StadiumMatch>(true);
            var view = app.Camera.view.transform.forward;
            matches.Animate(Step, 0, true, view);
            Assert.That(Active("Stand cut away"), Is.EqualTo(2), "one touchline stand and one end are lowered");
            Assert.That(Active("Stand"), Is.EqualTo(2));
            Assert.That(Active("Floodlight mast"), Is.EqualTo(3), "the mast between the lowered stands goes");
            Assert.That(matches.RootAt(0).Cast<Transform>().Count(t => t.name == "Curva" && t.gameObject.activeSelf), Is.EqualTo(1), "the lowered end loses its big banner");
            // Looking from the opposite corner lowers the other two.
            matches.Animate(Step, 0, true, new Vector3(-view.x, view.y, -view.z));
            Assert.That(Active("Stand cut away"), Is.EqualTo(2));
            // Zoomed out, the full stadium stands and its life is hidden.
            matches.Animate(Step, 0, false, view);
            Assert.That(Active("Stand cut away"), Is.EqualTo(0));
            Assert.That(Active("Stand"), Is.EqualTo(4));
            Assert.That(matches.RootAt(0).gameObject.activeSelf, Is.False);
        }
        int Active(string name)
        {
            int count = 0;
            foreach (Transform child in app.World.transform.Find("Cities"))
                if (child.name == name && child.gameObject.activeSelf)
                    count++;
            return count;
        }

        /// <summary>Renders the stadium for review: Logs/stadium-wide.png, stadium-pitch.png, stadium-goal.png, stadium-far-side.png.</summary>
        [UnityTest, Explicit("Art capture tool; run on demand.")]
        public IEnumerator ShowStadiumMatch()
        {
            yield return BuildStadium();
            var matches = app.World.GetComponentInChildren<StadiumMatch>(true);
            foreach (var label in app.World.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                if (!label.GetComponentInParent<StadiumMatch>(true))
                    label.gameObject.SetActive(false);
            var camera = app.Camera.view;
            app.Camera.enabled = false;
            var centre = new Vector3(lot.x + (size - 1) / 2f, .4f, lot.z + (size - 1) / 2f);
            for (int i = 0; i < 500; i++)
                matches.Animate(Step, 1, true, camera.transform.forward);
            yield return Shoot(camera, centre, 5.2f, "Logs/stadium-wide.png");
            yield return Shoot(camera, centre, 2.4f, "Logs/stadium-pitch.png");
            var ahead = new Vector3(camera.transform.forward.x, 0, camera.transform.forward.z).normalized;
            yield return Shoot(camera, centre + ahead * 2.6f + Vector3.up * .6f, 2f, "Logs/stadium-ultras.png");
            var match = matches.MatchAt(0);
            for (int i = 0; i < 40000 && match.CheeringTeam < 0; i++)
                matches.Animate(Step, 1, true, camera.transform.forward);
            for (int i = 0; i < 12; i++)
                matches.Animate(Step, 1, true, camera.transform.forward);
            yield return Shoot(camera, centre, 4f, "Logs/stadium-goal.png");
            camera.transform.rotation = Quaternion.Euler(35.264f, 225, 0);
            matches.Animate(Step, 0, true, camera.transform.forward);
            yield return Shoot(camera, centre, 4f, "Logs/stadium-far-side.png");
            app.Camera.enabled = true;
            Assert.That(File.Exists("Logs/stadium-goal.png"));
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
