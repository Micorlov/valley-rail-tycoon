using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Hollywood-style town signs: big white letters standing on open ground where the road enters town, on the side nearest
    /// the camera so no building hides them. A sign turns with the view and moves to the new near side after a quarter turn.
    /// </summary>
    public sealed partial class WorldView
    {
        const float SignFontSize = 26, SignDepth = .35f, SignOutlineWidth = .16f;
        const int SignDepthLayers = 7;
        static readonly Color SignWhite = new Color(.98f, .97f, .94f), SignSide = new Color(.6f, .61f, .63f), SignInk = new Color(.08f, .09f, .1f);
        // Spots tried for each sign: cells beyond the town's near edge, then shifts along it. Cheaper spots come first.
        static readonly float[] SignGaps = { 1.5f, 2.5f, 3.5f, 5f, 7f }, SignShifts = { 0, 2, -2, 4, -4, 6, -6, 9, -9, 12, -12 };
        // Trees closer than this in front of the letters would hide them: seen from the camera's 35° pitch, the tallest pine
        // (top 1.9) covers nothing of the sign only from 1.9 / tan 35° ≈ 2.7 cells away. They are cleared with those under the sign.
        const float SignTreeClearance = 2.7f;
        sealed class TownSign
        {
            public Transform root;
            public float width;
            public Vector2 at, forward, right;
        }
        readonly Dictionary<int, TownSign> townSigns = new Dictionary<int, TownSign>();
        // Trees the signs hid; they grow back when a sign moves away.
        HashSet<int> signClearedTrees = new HashSet<int>();
        Transform signRoot;
        int signTurn = -1, signRevision = -1, signCityRevision = -1;
        /// <summary>The sign standing at <paramref name="city"/>'s entrance, or null before the first refresh.</summary>
        public Transform TownSignFor(CityState city) => townSigns.TryGetValue(city.id, out var sign) ? sign.root : null;
        /// <summary>Quarter turns from the starting view, once the current turn settles; the view rests at 45° plus whole quarter turns.</summary>
        int SettledTurn => (int)Mathf.Repeat(Mathf.Round((labelYaw - 45) / 90), 4);
        /// <summary>Called after every refresh: moves signs when tracks, towns or the settled view direction changed.</summary>
        void UpdateTownSigns()
        {
            int turn = SettledTurn;
            if (signRevision == revision && signCityRevision == cityRevision && signTurn == turn)
                return;
            signRevision = revision;
            signCityRevision = cityRevision;
            signTurn = turn;
            if (!signRoot)
                signRoot = Root("Town signs");
            var occupied = OccupiedCells();
            var live = new HashSet<int>();
            foreach (var city in game.World.cities)
            {
                live.Add(city.id);
                if (!townSigns.TryGetValue(city.id, out var sign))
                    townSigns[city.id] = sign = CreateTownSign(city);
                PlaceTownSign(city, sign, occupied);
            }
            var gone = new List<int>();
            foreach (var kv in townSigns)
                if (!live.Contains(kv.Key))
                    gone.Add(kv.Key);
            foreach (int id in gone)
            {
                Destroy(townSigns[id].root.gameObject);
                townSigns.Remove(id);
            }
            ClearTreesAtSigns(occupied);
            TurnTownSigns();
        }
        void ClearTreesAtSigns(HashSet<int> occupied)
        {
            var cleared = new HashSet<int>();
            foreach (var sign in townSigns.Values)
                foreach (var c in SignCells(sign.at, sign.forward, sign.right, sign.width, SignTreeClearance, .4f))
                    // Only trees still standing, or already hidden by a sign: rails and bulldozers clear land for good.
                    if (trees.TryGetValue(c.Key, out var tree) && (tree.activeSelf || signClearedTrees.Contains(c.Key)))
                    {
                        tree.SetActive(false);
                        cleared.Add(c.Key);
                    }
            // Regrow only pines the world still has: not felled by the bulldozer, not under rails, roads or buildings.
            foreach (int key in signClearedTrees)
                if (!cleared.Contains(key) && !occupied.Contains(key) && game.Scenery.TreeAt(Cell.FromKey(key)) && trees.TryGetValue(key, out var tree))
                    tree.SetActive(true);
            signClearedTrees = cleared;
        }
        /// <summary>Keeps every sign square to the camera while the view turns.</summary>
        void TurnTownSigns()
        {
            var facing = Quaternion.Euler(0, labelYaw, 0);
            foreach (var sign in townSigns.Values)
                sign.root.rotation = facing;
        }
        TownSign CreateTownSign(CityState city)
        {
            string text = city.name.ToUpperInvariant();
            var root = new GameObject("Town sign " + city.name).transform;
            root.SetParent(signRoot, false);
            // Identical copies stacked behind the white face read as solid letters: seen from above, the grey copies show
            // as the letters' tops.
            var layers = new TextMeshPro[SignDepthLayers + 1];
            for (int i = SignDepthLayers; i >= 0; i--)
            {
                var go = new GameObject(i == 0 ? text : "Letter depth " + i, typeof(TextMeshPro));
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(0, 0, i * SignDepth / SignDepthLayers);
                var layer = go.GetComponent<TextMeshPro>();
                layer.text = text;
                layer.fontSize = SignFontSize;
                layer.fontStyle = FontStyles.Bold;
                layer.alignment = TextAlignmentOptions.Center;
                layer.enableWordWrapping = false;
                layer.color = i == 0 ? SignWhite : SignSide;
                layers[i] = layer;
            }
            var face = layers[0];
            face.outlineWidth = SignOutlineWidth;
            face.outlineColor = SignInk;
            face.ForceMeshUpdate();
            var bounds = face.textBounds;
            // Stand the letters on the ground: shift every copy so the lowest glyph touches the sign's base.
            foreach (var layer in layers)
                layer.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, layer.transform.localPosition.z);
            return new TownSign { root = root, width = bounds.size.x };
        }
        /// <summary>Cells a sign must not stand on: rails, platforms' building strips, roads and every town's buildings.</summary>
        HashSet<int> OccupiedCells()
        {
            var occupied = new HashSet<int>(townStreets);
            foreach (var t in game.World.tracks)
                occupied.Add(t.cell.Key);
            foreach (var s in game.World.stations)
                for (int i = -1; i <= 1; i++)
                    occupied.Add(new Cell(s.cell.x + (s.axis == 1 ? i : 0), s.cell.z + (s.axis == 0 ? i : 0)).Move(s.side).Key);
            foreach (var road in game.World.intercityRoads)
                for (int i = 0; i < road.built; i++)
                    occupied.Add(road.path[i].Key);
            foreach (var city in game.World.cities)
                foreach (var bs in city.buildings)
                    for (int i = 0; i < CityLayout.FootprintCells(bs); i++)
                        occupied.Add(CityLayout.FootprintCell(bs, i).Key);
            return occupied;
        }
        void PlaceTownSign(CityState city, TownSign sign, HashSet<int> occupied)
        {
            float yaw = (45 + 90 * signTurn) * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(forward.y, -forward.x);
            var center = new Vector2(city.center.x, city.center.z);
            // The town's edge nearest the camera, measured along the view direction from the plaza.
            float near = 0;
            foreach (var cell in TownCells(city))
                near = Mathf.Min(near, Vector2.Dot(new Vector2(cell.x, cell.z) - center, forward));
            var entrance = NearEntrance(city, center, forward, near);
            var best = center + forward * (near - SignGaps[0]);
            float bestCost = float.MaxValue;
            foreach (float gap in SignGaps)
                foreach (float shift in SignShifts)
                {
                    var at = center + forward * (near - gap) + right * shift;
                    // Clear ground matters most; then stay close to the town, beside the road into it where there is one.
                    float pull = entrance.HasValue ? Vector2.Distance(at, entrance.Value) * .4f : Mathf.Abs(shift) * .5f;
                    float cost = SignFootprintCost(at, forward, right, sign.width, occupied) + gap + pull;
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = at;
                    }
                }
            sign.at = best;
            sign.forward = forward;
            sign.right = right;
            sign.root.position = new Vector3(best.x, 0, best.y);
        }
        /// <summary>Map cells under a sign's letters, from <paramref name="front"/> cells before them to <paramref name="back"/> behind.</summary>
        static IEnumerable<Cell> SignCells(Vector2 at, Vector2 forward, Vector2 right, float width, float front, float back)
        {
            var seen = new HashSet<(int, int)>();
            for (float u = -width / 2 - .3f; u <= width / 2 + .3f; u += .5f)
                for (float v = -front; v <= back; v += .5f)
                {
                    var p = at + right * u + forward * v;
                    var c = new Cell(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                    if (seen.Add((c.x, c.z)))
                        yield return c;
                }
        }
        static IEnumerable<Cell> TownCells(CityState city)
        {
            foreach (var r in city.roads)
                yield return r.cell;
            foreach (var bs in city.buildings)
                for (int i = 0; i < CityLayout.FootprintCells(bs); i++)
                    yield return CityLayout.FootprintCell(bs, i);
        }
        /// <summary>The town street where a highway leaves on the camera's side of town, if any.</summary>
        Vector2? NearEntrance(CityState city, Vector2 center, Vector2 forward, float near)
        {
            Vector2? entrance = null;
            float nearest = near + 2;
            foreach (var r in city.roads)
            {
                if (!highwayJoints.ContainsKey(r.cell.Key))
                    continue;
                var at = new Vector2(r.cell.x, r.cell.z);
                float depth = Vector2.Dot(at - center, forward);
                if (depth <= nearest)
                {
                    nearest = depth;
                    entrance = at;
                }
            }
            return entrance;
        }
        /// <summary>Scores the ground under a sign: 100 per blocked cell, a little per tree that would have to go.</summary>
        float SignFootprintCost(Vector2 at, Vector2 forward, Vector2 right, float width, HashSet<int> occupied)
        {
            float cost = 0;
            foreach (var c in SignCells(at, forward, right, width, .3f, .3f))
                if (!MapDefinition.InBounds(c) || occupied.Contains(c.Key) || MapDefinition.Water(c) || MapDefinition.Blocked(c, game.World))
                    cost += 100;
                else if (trees.TryGetValue(c.Key, out var tree) && tree.activeSelf)
                    cost += .2f;
            return cost;
        }
    }
}
