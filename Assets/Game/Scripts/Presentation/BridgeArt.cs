using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// River crossings in the styles of <see cref="BridgeCatalog"/>. A span runs bank to bank over the three river cells of a
    /// marked row. The river is shallow, so each style is told apart by what stands on and above its deck: stone parapets,
    /// cutwaters and lamps; low timber trusses; a tall green lattice; a white arch; or orange towers hung with cables.
    /// </summary>
    public sealed partial class WorldView
    {
        // Rails ride on sleepers just above a rail deck; a highway's asphalt (drawn by CityView) lies on a road deck.
        public const float RailDeck = .09f, RoadDeck = .12f;
        /// <summary>Clear height over the deck (trains with raised pantographs, trucks). Overhead steel stays above it.</summary>
        public const float BridgeHeadroom = .85f;
        const float BridgeWest = 29.5f, BridgeEast = 32.5f, BridgeMiddle = 31f, Waterline = -.04f;
        static readonly float[] BridgeSides = { -1f, 1f };
        static readonly Color Sandstone = new Color(.8f, .72f, .58f), Coping = new Color(.9f, .86f, .75f), PierStone = new Color(.6f, .54f, .45f),
            BridgeLamp = new Color(1f, .87f, .52f), BridgeLampPost = new Color(.18f, .2f, .22f),
            TimberBrown = new Color(.52f, .35f, .2f), DarkTimber = new Color(.34f, .22f, .13f), Planks = new Color(.64f, .49f, .32f),
            TrussGreen = new Color(.18f, .46f, .3f), DeckGrey = new Color(.42f, .43f, .45f), ArchWhite = new Color(.94f, .94f, .91f),
            TowerOrange = new Color(.88f, .38f, .16f), CableOrange = new Color(.7f, .27f, .11f), Anchorage = new Color(.62f, .62f, .6f);
        Transform bridgePreview;
        string bridgePreviewKey;
        // The "BRIDGE SITE" marker of each row, shown only while nothing crosses the river there.
        readonly Dictionary<int, GameObject> siteLabels = new Dictionary<int, GameObject>();
        int bridgeSitesRevision = -1, bridgeSitesCityRevision = -1;

        /// <summary>Draws the whole crossing on <paramref name="row"/> in <paramref name="style"/>; a tint paints every part one colour.</summary>
        public void DrawBridge(int row, int style, float deck, Transform parent, Color? tint = null)
        {
            var span = new BridgePainter(this, row, deck, parent, tint);
            switch (style)
            {
                case BridgeCatalog.Timber: span.TimberTruss(); break;
                case BridgeCatalog.SteelTruss: span.SteelTruss(); break;
                case BridgeCatalog.SteelArch: span.SteelArch(); break;
                case BridgeCatalog.Suspension: span.Suspension(); break;
                default: span.StoneArch(); break;
            }
        }
        void RefreshBridgeSites()
        {
            if (bridgeSitesRevision == game.World.revision && bridgeSitesCityRevision == game.World.cityRevision)
                return;
            bridgeSitesRevision = game.World.revision;
            bridgeSitesCityRevision = game.World.cityRevision;
            foreach (var site in siteLabels)
                if (site.Value)
                    site.Value.SetActive(!BridgeCatalog.HasRailway(game.World, site.Key) && !BridgeCatalog.HasHighway(game.World, site.Key));
        }
        /// <summary>Shows the bridge a planned line would build, in its real colours (red while the plan is refused).</summary>
        void PreviewBridge(BuildPlan plan)
        {
            int row = plan == null ? 0 : BridgeCatalog.RowOf(plan);
            int style = row == 0 ? -1 : BridgeCatalog.StyleOf(game.World, plan);
            string key = row == 0 ? null : row + "/" + style + "/" + plan.valid;
            if (key == bridgePreviewKey)
                return;
            bridgePreviewKey = key;
            if (!bridgePreview)
                bridgePreview = Root("Bridge preview");
            Clear(bridgePreview);
            if (row != 0)
                DrawBridge(row, style, RailDeck, bridgePreview, plan.valid ? (Color?)null : new Color(.96f, .25f, .21f));
        }

        sealed class BridgePainter
        {
            readonly WorldView view; readonly Transform parent; readonly Color? tint; readonly float z, deck;
            public BridgePainter(WorldView view, int row, float deck, Transform parent, Color? tint)
            {
                this.view = view;
                this.parent = parent;
                this.tint = tint;
                z = row;
                this.deck = deck;
            }
            void Part(string name, Vector3 at, Vector3 size, Color color, Quaternion? rotation = null) =>
                view.Box(name, at, size, tint ?? color, parent, rotation);
            /// <summary>A straight member from a to b with the given cross-section.</summary>
            void Beam(string name, Vector3 a, Vector3 b, float width, float depth, Color color)
            {
                var along = b - a;
                float length = along.magnitude;
                if (length < 1e-4f)
                    return;
                var up = Mathf.Abs(along.y) > .99f * length ? Vector3.right : Vector3.up;
                Part(name, (a + b) * .5f, new Vector3(width, depth, length + .01f), color, Quaternion.LookRotation(along / length, up));
            }
            void Deck(Color color, float width, float thickness) =>
                Part("Bridge deck", new Vector3(BridgeMiddle, deck - thickness / 2, z), new Vector3(BridgeEast - BridgeWest + .02f, thickness, width), color);

            /// <summary>Sandstone deck between parapets, pointed cutwaters on both faces of its two piers and a lamp at each corner.</summary>
            public void StoneArch()
            {
                Deck(Sandstone, 1.1f, .14f);
                foreach (float x in new[] { 30.5f, 31.5f })
                    Part("Pier", new Vector3(x, deck - .22f, z), new Vector3(.22f, .3f, 1.18f), PierStone);
                foreach (float side in BridgeSides)
                {
                    float edge = z + side * .52f;
                    Part("Parapet", new Vector3(BridgeMiddle, deck + .07f, edge), new Vector3(3f, .14f, .07f), Sandstone);
                    Part("Parapet coping", new Vector3(BridgeMiddle, deck + .155f, edge), new Vector3(3.04f, .03f, .1f), Coping);
                    foreach (float x in new[] { 30.5f, 31.5f })
                    {
                        Part("Refuge", new Vector3(x, deck + .085f, z + side * .57f), new Vector3(.26f, .17f, .14f), Sandstone);
                        float height = deck - Waterline + .1f;
                        Part("Cutwater", new Vector3(x, deck - .02f - height / 2, z + side * .6f), new Vector3(.19f, height, .19f), PierStone, Quaternion.Euler(0, 45, 0));
                    }
                    foreach (float x in new[] { BridgeWest + .06f, BridgeEast - .06f })
                    {
                        var foot = new Vector3(x, deck, z + side * .57f);
                        Part("End pillar", foot + Vector3.up * .17f, new Vector3(.16f, .34f, .16f), Coping);
                        Part("Lamp post", foot + Vector3.up * .43f, new Vector3(.035f, .18f, .035f), BridgeLampPost);
                        Part("Lamp", foot + Vector3.up * .54f, new Vector3(.09f, .09f, .09f), BridgeLamp);
                    }
                }
            }

            /// <summary>A plank deck on pile bents, with a low X-braced wooden truss along each side and no bracing overhead.</summary>
            public void TimberTruss()
            {
                Deck(Planks, 1f, .08f);
                const int panels = 6;
                float step = (BridgeEast - BridgeWest) / panels, top = deck + .34f, foot = deck + .02f;
                foreach (float side in BridgeSides)
                {
                    float edge = z + side * .5f;
                    Beam("Timber chord", new Vector3(BridgeWest, foot, edge), new Vector3(BridgeEast, foot, edge), .06f, .06f, DarkTimber);
                    Beam("Timber chord", new Vector3(BridgeWest + step * .5f, top, edge), new Vector3(BridgeEast - step * .5f, top, edge), .06f, .06f, TimberBrown);
                    Beam("Timber end post", new Vector3(BridgeWest, foot, edge), new Vector3(BridgeWest + step * .5f, top, edge), .06f, .06f, TimberBrown);
                    Beam("Timber end post", new Vector3(BridgeEast, foot, edge), new Vector3(BridgeEast - step * .5f, top, edge), .06f, .06f, TimberBrown);
                    for (int i = 1; i < panels; i++)
                    {
                        float x = BridgeWest + i * step;
                        Beam("Timber post", new Vector3(x, foot, edge), new Vector3(x, top, edge), .05f, .05f, TimberBrown);
                        if (i + 1 < panels)
                        {
                            // Both diagonals of each inner panel: the X that makes it read as a wooden truss.
                            Beam("Timber brace", new Vector3(x, foot, edge), new Vector3(x + step, top, edge), .035f, .035f, DarkTimber);
                            Beam("Timber brace", new Vector3(x, top, edge), new Vector3(x + step, foot, edge), .035f, .035f, DarkTimber);
                        }
                    }
                    // Pile clusters stand in the river beside the deck, one pair per bent.
                    foreach (float x in new[] { 30.5f, 31.5f })
                        foreach (float dx in new[] { -.07f, .07f })
                            Part("Pile", new Vector3(x + dx, (deck + Waterline) / 2 - .15f, z + side * .6f), new Vector3(.07f, deck - Waterline + .28f, .07f), DarkTimber);
                }
                foreach (float x in new[] { 30.5f, 31.5f })
                    Part("Cap beam", new Vector3(x, deck - .1f, z), new Vector3(.1f, .06f, 1.3f), DarkTimber);
            }

            /// <summary>A through Warren truss in railway green: trains run inside the lattice under a braced top.</summary>
            public void SteelTruss()
            {
                Deck(DeckGrey, 1f, .1f);
                const int panels = 6;
                float step = (BridgeEast - BridgeWest) / panels, top = deck + BridgeHeadroom + .1f, foot = deck + .03f;
                foreach (float side in BridgeSides)
                {
                    float edge = z + side * .5f;
                    Beam("Bottom chord", new Vector3(BridgeWest, foot, edge), new Vector3(BridgeEast, foot, edge), .07f, .09f, TrussGreen);
                    Beam("Top chord", new Vector3(BridgeWest + step * .5f, top, edge), new Vector3(BridgeEast - step * .5f, top, edge), .08f, .08f, TrussGreen);
                    for (int i = 0; i < panels; i++)
                    {
                        float x0 = BridgeWest + i * step, xm = x0 + step * .5f;
                        Beam("Truss diagonal", new Vector3(x0, foot, edge), new Vector3(xm, top, edge), .06f, .06f, TrussGreen);
                        Beam("Truss diagonal", new Vector3(xm, top, edge), new Vector3(x0 + step, foot, edge), .06f, .06f, TrussGreen);
                        Beam("Truss vertical", new Vector3(xm, foot, edge), new Vector3(xm, top, edge), .035f, .035f, TrussGreen);
                    }
                }
                for (int i = 0; i < panels; i++)
                {
                    float xm = BridgeWest + (i + .5f) * step;
                    Beam("Top strut", new Vector3(xm, top, z - .5f), new Vector3(xm, top, z + .5f), .05f, .05f, TrussGreen);
                    if (i + 1 < panels)
                    {
                        Beam("Top bracing", new Vector3(xm, top, z - .5f), new Vector3(xm + step, top, z + .5f), .035f, .035f, TrussGreen);
                        Beam("Top bracing", new Vector3(xm, top, z + .5f), new Vector3(xm + step, top, z - .5f), .035f, .035f, TrussGreen);
                    }
                }
                foreach (float x in new[] { BridgeWest + step * .5f, BridgeEast - step * .5f })
                    Part("Portal", new Vector3(x, top - .02f, z), new Vector3(.06f, .07f, 1f), TrussGreen);
            }

            /// <summary>A white tied arch over each side of the deck, hangers down to the tie and bracing across the crown.</summary>
            public void SteelArch()
            {
                Deck(DeckGrey, 1f, .1f);
                const int segments = 16;
                const float rise = 1.25f;
                foreach (float side in BridgeSides)
                {
                    float edge = z + side * .52f;
                    Beam("Arch tie", new Vector3(BridgeWest, deck + .03f, edge), new Vector3(BridgeEast, deck + .03f, edge), .07f, .1f, ArchWhite);
                    for (int i = 0; i < segments; i++)
                        Beam("Arch rib", Rib((float)i / segments, side, rise), Rib((float)(i + 1) / segments, side, rise), .09f, .11f, ArchWhite);
                    for (int k = 1; k < 12; k++)
                    {
                        var p = Rib(k / 12f, side, rise);
                        Beam("Hanger", p, new Vector3(p.x, deck + .03f, p.z), .025f, .025f, ArchWhite);
                    }
                    foreach (float x in new[] { BridgeWest + .06f, BridgeEast - .06f })
                        Part("Arch footing", new Vector3(x, deck - .05f, edge), new Vector3(.24f, .16f, .22f), Anchorage);
                }
                // Cross bracing only where the crown is high enough for trains to pass beneath.
                Vector3? last = null;
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    if (Rib(t, 0, rise).y - .05f < deck + BridgeHeadroom)
                    {
                        last = null;
                        continue;
                    }
                    Beam("Arch brace", Rib(t, -1, rise), Rib(t, 1, rise), .05f, .05f, ArchWhite);
                    if (last.HasValue)
                        Beam("Arch brace", new Vector3(last.Value.x, last.Value.y, z - .52f), Rib(t, 1, rise), .035f, .035f, ArchWhite);
                    last = Rib(t, -1, rise);
                }
            }
            Vector3 Rib(float t, float side, float rise) =>
                new Vector3(Mathf.Lerp(BridgeWest + .06f, BridgeEast - .06f, t), deck + .03f + rise * 4 * t * (1 - t), z + side * .52f);

            /// <summary>Two orange towers at the banks carry sagging main cables, back-stayed to anchor blocks on land.</summary>
            public void Suspension()
            {
                Deck(DeckGrey, 1f, .1f);
                const float towerWest = BridgeWest + .3f, towerEast = BridgeEast - .3f, spread = .6f;
                float crown = deck + 1.6f, saddle = crown - .05f, low = deck + .2f;
                foreach (float x in new[] { towerWest, towerEast })
                {
                    foreach (float side in BridgeSides)
                    {
                        var legFoot = new Vector3(x, Waterline - .1f, z + side * spread);
                        Beam("Tower leg", legFoot, new Vector3(x, crown, z + side * spread), .11f, .11f, TowerOrange);
                        Part("Tower cap", new Vector3(x, crown + .03f, z + side * spread), new Vector3(.15f, .06f, .15f), TowerOrange);
                    }
                    Beam("Tower beam", new Vector3(x, deck + BridgeHeadroom + .08f, z - spread), new Vector3(x, deck + BridgeHeadroom + .08f, z + spread), .09f, .09f, TowerOrange);
                    Beam("Tower beam", new Vector3(x, crown - .12f, z - spread), new Vector3(x, crown - .12f, z + spread), .09f, .09f, TowerOrange);
                    Beam("Tower beam", new Vector3(x, deck - .07f, z - spread), new Vector3(x, deck - .07f, z + spread), .08f, .06f, TowerOrange);
                }
                foreach (float side in BridgeSides)
                {
                    float edge = z + side * spread;
                    Beam("Deck girder", new Vector3(BridgeWest, deck + .03f, z + side * .56f), new Vector3(BridgeEast, deck + .03f, z + side * .56f), .06f, .09f, TowerOrange);
                    const int segments = 14;
                    for (int i = 0; i < segments; i++)
                        Beam("Main cable", Cable(towerWest, towerEast, (float)i / segments, saddle, low, edge), Cable(towerWest, towerEast, (float)(i + 1) / segments, saddle, low, edge), .04f, .04f, CableOrange);
                    for (float x = towerWest + .2f; x < towerEast - .1f; x += .2f)
                    {
                        var hook = Cable(towerWest, towerEast, (x - towerWest) / (towerEast - towerWest), saddle, low, edge);
                        Beam("Suspender", hook, new Vector3(x, deck + .05f, z + side * .56f), .02f, .02f, CableOrange);
                    }
                    // Back-stays run from each tower top down to an anchor block on the bank behind it.
                    foreach (var (tower, anchor) in new[] { (towerWest, BridgeWest - .55f), (towerEast, BridgeEast + .55f) })
                    {
                        var from = new Vector3(tower, saddle, edge);
                        var to = new Vector3(anchor, .1f, edge);
                        for (int i = 0; i < 5; i++)
                            Beam("Back-stay", Sag(from, to, i / 5f), Sag(from, to, (i + 1) / 5f), .04f, .04f, CableOrange);
                        Part("Anchorage", new Vector3(anchor, .08f, edge), new Vector3(.3f, .16f, .24f), Anchorage);
                    }
                }
            }
            static Vector3 Cable(float west, float east, float t, float saddle, float low, float edge)
            {
                float u = 2 * t - 1;
                return new Vector3(Mathf.Lerp(west, east, t), low + (saddle - low) * u * u, edge);
            }
            static Vector3 Sag(Vector3 from, Vector3 to, float t) => Vector3.Lerp(from, to, t) - Vector3.up * (.08f * 4 * t * (1 - t));
        }
    }
}
