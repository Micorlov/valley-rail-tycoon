using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>The NEW STATION panel: a station on open ground with the size and platforms chosen by its buttons.</summary>
    public class NewStationPanelTests
    {
        static GameBootstrap App()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }
        static Transform Details(GameBootstrap app) => app.UI.transform.Find("Safe area/Context/Scrollable details");
        static Button Find(GameBootstrap app, string prefix)
        {
            foreach (var button in Details(app).GetComponentsInChildren<Button>())
                if (button.name.StartsWith(prefix))
                    return button;
            return null;
        }
        /// <summary>Presses a cycling button until its label reads <paramref name="wanted"/> (the panel keeps the last choice).</summary>
        static void Cycle(GameBootstrap app, string prefix, string wanted)
        {
            for (int i = 0; i < 8 && Find(app, prefix).name != wanted; i++)
                Find(app, prefix).onClick.Invoke();
            Assert.That(Find(app, prefix).name, Is.EqualTo(wanted));
        }

        [UnityTest]
        public IEnumerator StationPanelBuildsOnOpenGroundWithChosenSize()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            app.ChooseTool(ToolMode.Station);
            // Water: the panel explains and offers no build button.
            app.Select(new Cell(31, 20));
            Assert.That(Find(app, "SIZE"), Is.Not.Null, "size stays adjustable on a refused spot");
            Assert.That(Find(app, "BUILD FOR"), Is.Null, "nothing to build on water");
            // Open ground below Pinecrest Mine.
            app.Select(new Cell(10, 15));
            Assert.That(Find(app, "ROTATE"), Is.Not.Null, "open ground can be turned");
            Cycle(app, "SIZE", "SIZE 4");
            Cycle(app, "PLATFORMS", "PLATFORMS 2");
            Assert.That(Details(app).GetComponentsInChildren<TMPro.TMP_Text>()[1].text, Does.Contain("4 cells × 2 platforms"));
            var build = Find(app, "BUILD FOR Pinecrest Mine");
            Assert.That(build, Is.Not.Null, "the mine is in reach");
            int tracks = app.Game.World.tracks.Count;
            build.onClick.Invoke();
            yield return null;
            Assert.That(app.Game.World.stations.Count, Is.EqualTo(1));
            var s = app.Game.World.stations[0];
            Assert.That(StationLayout.Length(s), Is.EqualTo(4));
            Assert.That(StationLayout.Platforms(s), Is.EqualTo(2));
            Assert.That(app.Game.World.tracks.Count, Is.EqualTo(tracks + 8), "the station laid both platform tracks");
            app.World.Refresh();
            yield return null;
            Assert.That(app.World.transform.Find("Stations/Island platform"), Is.Not.Null, "a second platform draws an island");
            // Tapping the far end of the second platform opens the station, though it is three cells from the station cell.
            app.ChooseTool(ToolMode.Browse);
            var cells = StationLayout.Cells(s);
            app.Select(cells[cells.Count - 1]);
            Assert.That(Details(app).GetComponentInChildren<TMPro.TMP_Text>().text, Does.Contain("Pinecrest Mine Station"));
            Assert.That(Find(app, "BUY A TRAIN"), Is.Not.Null);
        }
    }
}
