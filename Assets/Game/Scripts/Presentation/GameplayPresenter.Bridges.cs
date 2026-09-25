using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>The bridge panel: a tap on a river crossing shows its style, and each other style rebuilds it for that style's price.</summary>
    public sealed partial class GameplayPresenter
    {
        public void Bridge(int row)
        {
            var w = app.Game.World;
            int current = BridgeCatalog.StyleAt(w, row);
            bool railway = BridgeCatalog.HasRailway(w, row), highway = BridgeCatalog.HasHighway(w, row), built = railway || highway;
            string crossing = railway ? "Railway bridge over the river" : highway ? "Highway bridge over the river" : "Bridge site: lay track across to build";
            string action = built ? "Tap a style to rebuild it. Traffic keeps running." : "Tap a style to plan it here (free).";
            Context(BridgeCatalog.Name(current).ToUpperInvariant() + " BRIDGE", $"{crossing}\n{BridgeCatalog.Blurb(current)}\n{action}");
            for (int style = 0; style < BridgeCatalog.Count; style++)
            {
                int pick = style;
                string name = BridgeCatalog.Name(style).ToUpperInvariant();
                string label = style == current ? "● " + name : built ? $"{name}  ·  ${BridgeCatalog.Cost(app.Game.Balance, style):N0}" : name;
                Named(Button(contextBody, label, () => app.RestyleBridge(row, pick), new Vector2(22, -210), new Vector2(345, 58), style == current), "BRIDGE " + name);
            }
        }
    }
}
