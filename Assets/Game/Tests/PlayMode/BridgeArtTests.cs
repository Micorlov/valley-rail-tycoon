using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    public class BridgeArtTests
    {
        const int South = 15;

        [UnityTest]
        public IEnumerator EveryStyleKeepsTheDeckClearForTrainsAndCars()
        {
            var app = Boot();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var names = new HashSet<string>();
            for (int style = 0; style < BridgeCatalog.Count; style++)
                foreach (float deck in new[] { WorldView.RailDeck, WorldView.RoadDeck })
                {
                    // A loose root keeps each part as its own renderer, with its name and bounds.
                    var root = new GameObject("Bridge style " + style).transform;
                    root.SetParent(app.World.transform, false);
                    app.World.DrawBridge(South, style, deck, root);
                    var parts = root.GetComponentsInChildren<Renderer>();
                    Assert.That(parts.Length, Is.GreaterThan(20), BridgeCatalog.Name(style) + " is a real structure");
                    var signature = new SortedSet<string>();
                    foreach (var part in parts)
                    {
                        signature.Add(part.name);
                        var b = part.bounds;
                        bool overLane = b.max.x > 29.5f && b.min.x < 32.5f && b.min.z < South + .3f && b.max.z > South - .3f;
                        bool inHeadroom = b.max.y > deck + .02f && b.min.y < deck + WorldView.BridgeHeadroom;
                        Assert.That(overLane && inHeadroom, Is.False, $"{BridgeCatalog.Name(style)}: {part.name} blocks the lane ({b.min} to {b.max})");
                        Assert.That(b.min.x, Is.GreaterThan(28.5f), $"{BridgeCatalog.Name(style)}: {part.name} stays by the river");
                        Assert.That(b.max.x, Is.LessThan(33.5f), $"{BridgeCatalog.Name(style)}: {part.name} stays by the river");
                    }
                    names.Add(string.Join(",", signature));
                    Object.Destroy(root.gameObject);
                }
            Assert.That(names.Count, Is.EqualTo(BridgeCatalog.Count), "each style is built from its own kinds of parts");
        }

        [UnityTest]
        public IEnumerator LayingTrackOffersEveryStyleAndATapRebuildsTheBridge()
        {
            var app = Boot();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            app.Game.World.money = 100000;
            yield return null;
            app.ChooseTool(ToolMode.Track);
            app.BeginTrack(new Cell(8, South));
            app.ExtendTrack(new Cell(50, South));
            yield return null;
            Assert.That(ButtonText("BRIDGE STYLE"), Does.Contain("STONE ARCH"), "the south site starts as a stone arch");
            var ghost = app.World.transform.Find("Bridge preview");
            Assert.That(ghost, Is.Not.Null);
            Assert.That(ghost.childCount, Is.GreaterThan(20), "the planned bridge is drawn before it is bought");
            app.CycleBridge();
            yield return null;
            Assert.That(ButtonText("BRIDGE STYLE"), Does.Contain("TIMBER TRUSS"));
            int money = app.Game.World.money;
            app.ConfirmBuild();
            yield return null;
            Assert.That(BridgeCatalog.StyleAt(app.Game.World, South), Is.EqualTo(BridgeCatalog.Timber), "the chosen style is built");
            Assert.That(app.Game.World.tracks.FindAll(t => t.bridge == South).Count, Is.EqualTo(3));
            Assert.That(money - app.Game.World.money, Is.GreaterThan(BridgeCatalog.Cost(app.Game.Balance, BridgeCatalog.Timber)));
            Assert.That(ghost.childCount, Is.EqualTo(0), "the planned bridge goes once it is built");
            app.World.Refresh();
            int markers = 0;
            foreach (var label in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (label.text == "BRIDGE SITE" && Mathf.Abs(label.transform.position.z - (South + 1.2f)) < .1f)
                {
                    markers++;
                    Assert.That(label.gameObject.activeInHierarchy, Is.False, "the site marker hides under a built bridge");
                }
            Assert.That(markers, Is.EqualTo(1));

            // In browse mode a tap on the crossing opens its panel; picking a style rebuilds it.
            app.ChooseTool(ToolMode.Browse);
            app.Select(new Cell(31, South));
            yield return null;
            Assert.That(GameObject.Find("BRIDGE SUSPENSION"), Is.Not.Null, "the panel lists every style");
            money = app.Game.World.money;
            app.RestyleBridge(South, BridgeCatalog.Suspension);
            yield return null;
            Assert.That(BridgeCatalog.StyleAt(app.Game.World, South), Is.EqualTo(BridgeCatalog.Suspension));
            Assert.That(money - app.Game.World.money, Is.EqualTo(BridgeCatalog.Cost(app.Game.Balance, BridgeCatalog.Suspension)));
            Assert.That(ButtonText("BRIDGE SUSPENSION"), Does.StartWith("●"), "the panel marks the new style");
        }

        /// <summary>Review renders: every style as a railway bridge with a coal train on it, and as a highway bridge.</summary>
        [UnityTest, Explicit]
        public IEnumerator GalleryShowsEveryStyle()
        {
            var app = Boot();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            var game = app.Game;
            game.World.money = 1000000;
            Assert.That(game.Build.CommitBuild(game.Build.Preview(new Cell(8, South), new Cell(50, South))).ok, Is.True);
            var a = game.Stations.Place(new Cell(10, South), 1);
            var b = game.Stations.Place(new Cell(48, South), 2);
            var buy = game.Trains.Buy(a.id, 0, Cargo.Coal);
            Assert.That(game.Trains.AssignRoute(buy.id, a.id, b.id).ok, Is.True);
            var train = game.Trains.Train(buy.id);
            for (int i = 0; i < 6000 && !OnBridge(game, train); i++)
                game.Step();
            Assert.That(OnBridge(game, train), Is.True, "the coal train reaches the bridge");
            var highway = new IntercityRoadState { a = 4, b = 5 };
            for (int x = 22; x <= 40; x++)
                highway.path.Add(new Cell(x, 46));
            highway.built = highway.path.Count;
            game.World.intercityRoads.Add(highway);
            game.World.cityRevision++;
            for (int style = 0; style < BridgeCatalog.Count; style++)
            {
                if (BridgeCatalog.StyleAt(game.World, South) != style)
                    Assert.That(game.Build.RestyleBridge(South, style).ok, Is.True);
                if (BridgeCatalog.StyleAt(game.World, 46) != style)
                    Assert.That(game.Build.RestyleBridge(46, style).ok, Is.True);
                app.World.Refresh();
                string name = BridgeCatalog.Name(style).ToLowerInvariant().Replace(' ', '-');
                app.Camera.focus = new Vector3(31, 0, South);
                app.Camera.zoom = 6;
                yield return Capture(app, $"Logs/bridge-rail-{style}-{name}.png");
                app.Camera.focus = new Vector3(31, 0, 46);
                yield return Capture(app, $"Logs/bridge-road-{style}-{name}.png");
            }
        }

        static GameBootstrap Boot()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }
        static bool OnBridge(GameSession game, TrainState train) =>
            train.path.Count > 0 && train.step < train.path.Count && game.Network.ids[train.path[train.step].trackId].bridge != 0;
        static string ButtonText(string name)
        {
            var button = GameObject.Find(name);
            Assert.That(button, Is.Not.Null, name + " is shown");
            return button.GetComponentInChildren<TMP_Text>().text;
        }
        static IEnumerator Capture(GameBootstrap app, string filename)
        {
            var camera = app.Camera.view;
            camera.aspect = 16f / 9;
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
        }
    }
}
