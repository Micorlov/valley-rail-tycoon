using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>The style chosen for one marked bridge site (a row of <see cref="MapDefinition.BridgeRows"/>).</summary>
    [Serializable]
    public class BridgeStyleState
    {
        public int row, style;
    }
    /// <summary>
    /// The styles a river crossing can be built in. Each marked site has one style, shared by the railway or highway that
    /// crosses it. A site nobody chose a style for shows its own default, so the valley's crossings differ from the start.
    /// </summary>
    public static class BridgeCatalog
    {
        public const int StoneArch = 0, Timber = 1, SteelTruss = 2, SteelArch = 3, Suspension = 4, Count = 5;
        static readonly string[] names = { "Stone arch", "Timber truss", "Steel truss", "Steel arch", "Suspension" };
        static readonly string[] blurbs =
        {
            "Sandstone piers, parapets and lamps",
            "Cheapest: low wooden trusses on piles",
            "Green steel lattice to run through",
            "A white steel arch over the deck",
            "Tall orange towers and cables",
        };
        // Percent of Balance.bridgeCost. The stone arch keeps the original bridge price.
        static readonly int[] price = { 100, 50, 160, 240, 400 };
        // One per MapDefinition.BridgeRows entry. The south site keeps the original stone bridge, and its price, for the tutorial.
        static readonly int[] siteDefaults = { StoneArch, SteelTruss, Suspension, SteelArch };

        public static bool Valid(int style) => style >= 0 && style < Count;
        public static string Name(int style) => names[style];
        public static string Blurb(int style) => blurbs[style];
        public static int Cost(Balance b, int style) => b.bridgeCost * price[style] / 100;
        public static bool Site(int row) => Array.IndexOf(MapDefinition.BridgeRows, row) >= 0;
        public static int DefaultStyle(int row)
        {
            int site = Array.IndexOf(MapDefinition.BridgeRows, row);
            return site < 0 ? StoneArch : siteDefaults[site];
        }
        public static int StyleAt(WorldState w, int row)
        {
            if (w.bridgeStyles != null)
                foreach (var s in w.bridgeStyles)
                    if (s.row == row)
                        return s.style;
            return DefaultStyle(row);
        }
        /// <summary>Records the style of a site. Callers charge for it; this only remembers the choice.</summary>
        public static void Choose(WorldState w, int row, int style)
        {
            if (!Site(row) || !Valid(style))
                throw new ArgumentOutOfRangeException(nameof(style));
            if (w.bridgeStyles == null)
                w.bridgeStyles = new List<BridgeStyleState>();
            var chosen = w.bridgeStyles.Find(s => s.row == row);
            if (chosen == null)
                w.bridgeStyles.Add(new BridgeStyleState { row = row, style = style });
            else
                chosen.style = style;
        }
        /// <summary>The first bridge row a plan builds on, or 0 when it crosses no new bridge.</summary>
        public static int RowOf(BuildPlan plan)
        {
            foreach (var t in plan.changes)
                if (t.bridge != 0)
                    return t.bridge;
            return 0;
        }
        /// <summary>The style a plan builds its bridge in: its own choice, else the site's current style.</summary>
        public static int StyleOf(WorldState w, BuildPlan plan) => Valid(plan.bridgeStyle) ? plan.bridgeStyle : StyleAt(w, RowOf(plan));
        public static bool HasRailway(WorldState w, int row) => w.tracks.Exists(t => t.bridge == row);
        /// <summary>True once a highway has been built across the site (roads cross water only on marked sites).</summary>
        public static bool HasHighway(WorldState w, int row)
        {
            foreach (var road in w.intercityRoads)
                for (int i = 0; i < road.built && i < road.path.Count; i++)
                    if (MapDefinition.Bridge(road.path[i]) == row)
                        return true;
            return false;
        }
        /// <summary>The site a tap lands on: the river cells of a bridge row, a cell to either side, or the rows just beside it. 0 when none.</summary>
        public static int SiteNear(Cell c)
        {
            if (c.x < 29 || c.x > 33)
                return 0;
            foreach (int row in MapDefinition.BridgeRows)
                if (Math.Abs(c.z - row) <= 1)
                    return row;
            return 0;
        }
    }
}
