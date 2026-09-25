using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// AI FIX in the track tool: one tap scans the whole railway for gaps, loose ends and cut junctions, draws the repair
    /// on the map with red pins on track it takes away, and quotes the price. Nothing is built until FIX ALL.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        RectTransform aiTrackFix;
        /// <summary>The violet AI FIX button, shown beside the active tool while laying track (see RefreshTools).</summary>
        void BuildTrackRepairButton()
        {
            aiTrackFix = Named(Button(tools, "AI FIX", TrackRepair, Vector2.zero, new Vector2(180, ToolHeight)), "AI FIX");
            aiTrackFix.GetComponent<UnityEngine.UI.Image>().color = violet;
            Label(aiTrackFix).color = navy;
        }
        void TrackRepair()
        {
            app.CancelPreview();
            var repair = app.Game.Repairs.Plan();
            Context("AI TRACK FIX", repair.reason);
            contextText.color = repair.valid ? mint : repair.items.Count == 0 ? cream : warning;
            if (repair.build != null)
            {
                app.World.Preview(repair.build);
                var pins = new List<Vector3>();
                foreach (var c in repair.doomed)
                    pins.Add(new Vector3(c.x, MapDefinition.Height(c.x, c.z) + .3f, c.z));
                app.World.MarkDoomed(pins);
                aiPreview = true;
                Frame(repair.build);
            }
            if (repair.valid)
                Button(contextBody, $"FIX ALL · ${repair.build.cost:N0}", () => { if (app.Perform(() => app.Game.Repairs.Apply(repair)).ok) HideContext(); else TrackRepair(); }, new Vector2(22, -210), new Vector2(345, 58), true);
            else if (repair.items.Count > 0)
                app.Sfx.Ui(SoundCue.Error);
            Button(contextBody, "BACK", HideContext, new Vector2(22, -280), new Vector2(345, 54));
        }
    }
}
