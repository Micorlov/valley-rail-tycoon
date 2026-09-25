using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    /// <summary>Towns are named by big Hollywood-style signs standing on clear ground at the edge nearest the camera.</summary>
    public class TownSignTests
    {
        static GameBootstrap App()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }
        static TextMeshPro Face(Transform sign, CityState city)
        {
            foreach (var text in sign.GetComponentsInChildren<TextMeshPro>(true))
                if (text.name == city.name.ToUpperInvariant())
                    return text;
            return null;
        }
        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
        static HashSet<int> Occupied(WorldState w)
        {
            var cells = new HashSet<int>();
            foreach (var t in w.tracks)
                cells.Add(t.cell.Key);
            foreach (var road in w.intercityRoads)
                for (int i = 0; i < road.built; i++)
                    cells.Add(road.path[i].Key);
            foreach (var city in w.cities)
            {
                foreach (var r in city.roads)
                    cells.Add(r.cell.Key);
                foreach (var bs in city.buildings)
                    for (int i = 0; i < CityLayout.FootprintCells(bs); i++)
                        cells.Add(CityLayout.FootprintCell(bs, i).Key);
            }
            return cells;
        }
        static void AssertSignsStandClear(GameBootstrap app)
        {
            var w = app.Game.World;
            var occupied = Occupied(w);
            var forward = Flat(app.Camera.view.transform.forward).normalized;
            var pines = new HashSet<int>();
            foreach (var t in app.World.GetComponentsInChildren<Transform>(false))
                if (t.name == "Pine")
                    pines.Add(new Cell(Mathf.RoundToInt(t.position.x), Mathf.RoundToInt(t.position.z)).Key);
            Assert.That(w.cities.Count, Is.GreaterThan(0));
            foreach (var city in w.cities)
            {
                var sign = app.World.TownSignFor(city);
                Assert.That(sign, Is.Not.Null, city.name + " has a sign");
                var face = Face(sign, city);
                Assert.That(face, Is.Not.Null, city.name + " sign spells the town name");
                Assert.That(face.text, Is.EqualTo(city.name.ToUpperInvariant()));
                Assert.That(face.text, Does.Not.Match("[0-9]"), "no population on the sign");
                var b = face.textBounds;
                Vector3 left = face.transform.TransformPoint(new Vector3(b.min.x, b.min.y, 0)), right = face.transform.TransformPoint(new Vector3(b.max.x, b.min.y, 0));
                Assert.That(left.y, Is.EqualTo(0).Within(.05f), city.name + " letters stand on the ground");
                Assert.That(face.transform.TransformVector(new Vector3(0, b.size.y, 0)).y, Is.GreaterThan(1.5f), city.name + " letters are big");
                Assert.That(Vector3.Dot(face.transform.forward, app.Camera.view.transform.forward), Is.GreaterThan(.8f), city.name + " sign faces the camera");
                var center = new Vector2(city.center.x, city.center.z);
                float near = 0;
                foreach (var r in city.roads)
                    near = Mathf.Min(near, Vector2.Dot(new Vector2(r.cell.x, r.cell.z) - center, forward));
                Assert.That(Vector2.Dot(Flat(sign.position) - center, forward), Is.LessThan(near), city.name + " sign stands in front of the town");
                for (float t = 0; t <= 1; t += .05f)
                {
                    var p = Vector3.Lerp(left, right, t);
                    var cell = new Cell(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
                    Assert.That(occupied.Contains(cell.Key) || MapDefinition.Water(cell), Is.False, $"{city.name} sign stands clear of rails, roads and buildings at {cell.x},{cell.z}");
                    var before = new Vector2(p.x, p.z) - forward;
                    Assert.That(pines.Contains(cell.Key) || pines.Contains(new Cell(Mathf.RoundToInt(before.x), Mathf.RoundToInt(before.y)).Key), Is.False,
                        $"no tree hides {city.name}'s letters at {cell.x},{cell.z}");
                }
            }
        }
        [UnityTest]
        public IEnumerator EveryTownHasABigGroundSignWithOnlyItsName()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            AssertSignsStandClear(app);
            foreach (var label in app.World.GetComponentsInChildren<TextMeshPro>(true))
                Assert.That(label.text, Does.Not.Contain("POP"), "no floating population labels");
        }
        [UnityTest]
        public IEnumerator SignsMoveToTheNearSideWhenTheViewTurns()
        {
            var app = App();
            yield return null;
            app.NewGame(false);
            app.SetSpeed(0);
            yield return null;
            var city = app.Game.World.cities[0];
            var before = app.World.TownSignFor(city).position;
            app.Camera.RotateView();
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(app.World.TownSignFor(city).position, Is.Not.EqualTo(before), "the sign moved to the new near side");
            AssertSignsStandClear(app);
        }
    }
}
