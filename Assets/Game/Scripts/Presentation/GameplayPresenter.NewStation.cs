using System;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// The NEW STATION panel. A tap in Station mode proposes a station there: on straight track it follows the track, on open
    /// ground it lays its own platform track (ROTATE turns it). SIZE and PLATFORMS cycle the platform length and the number of
    /// parallel tracks. The map shows the footprint in green or red, and one button per town or industry in reach builds it.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        // The last choices stay for the next station, so a player building several alike taps once each.
        int newStationLength = StationLayout.MinLength, newStationPlatforms = 1, newStationAxis = 1;

        public void StationChoices(Cell c)
        {
            var track = app.Game.Network.At(c);
            bool onTrack = track != null;
            if (onTrack && !Directions.Straight(track.mask))
            {
                app.Notice = "Tap open ground or the middle of straight track.";
                app.Sfx.Ui(SoundCue.Error);
                return;
            }
            int axis = onTrack ? (track.mask == 10 ? 1 : 0) : newStationAxis;
            var plan = app.Game.Stations.Plan(c, axis, newStationLength, newStationPlatforms);
            Context("NEW STATION", NewStationText(plan, onTrack));
            contextText.color = plan.valid ? mint : warning;
            ShowStationPlan(plan);
            ButtonRow(("SIZE " + newStationLength, () => { newStationLength = newStationLength >= StationLayout.MaxLength ? StationLayout.MinLength : newStationLength + 1; StationChoices(c); }),
                ("PLATFORMS " + newStationPlatforms, () => { newStationPlatforms = newStationPlatforms % StationLayout.MaxPlatforms + 1; StationChoices(c); }));
            if (!onTrack)
                Button(contextBody, "ROTATE", () => { newStationAxis = 1 - newStationAxis; StationChoices(c); }, new Vector2(22, -205), new Vector2(345, 56));
            if (!plan.valid)
            {
                app.Sfx.Ui(SoundCue.Error);
                return;
            }
            foreach (var p in plan.nearby)
            {
                int id = p.id;
                Button(contextBody, "BUILD FOR " + p.name, () => { if (app.Perform(() => app.Game.Stations.Place(plan, id)).ok) HideContext(); }, new Vector2(22, -205), new Vector2(345, 56), true);
            }
        }
        string NewStationText(StationPlan plan, bool onTrack)
        {
            int station = StationLayout.Cost(app.Game.Balance, plan.length, plan.platforms), laid = plan.track.changes.Count;
            string size = StationLayout.Describe(plan.length, plan.platforms) + (onTrack ? " · on your track" : " · lays its own track");
            // A plan refused before pricing has no cost yet; an unaffordable one is priced like a valid one.
            string cost = plan.cost == 0 ? $"Station ${station:N0} + ${app.Game.Balance.straightCost} per new track cell"
                : laid > 0 ? $"${plan.cost:N0}: station ${station:N0} + {laid} track ${plan.track.cost:N0}" : $"${plan.cost:N0} for the station";
            string next = plan.valid ? "Build it for:" : plan.reason;
            return size + "\n" + cost + (plan.platforms > 1 ? "\nEach platform can take its own railway and train." : "") + "\n" + next;
        }
        /// <summary>Platform cells and the building strip on the map: new track as rails, open cells as tiles, green when buildable.</summary>
        void ShowStationPlan(StationPlan plan)
        {
            var shape = new BuildPlan { valid = plan.valid };
            shape.path.AddRange(plan.platformCells);
            shape.path.AddRange(plan.strip);
            shape.changes.AddRange(plan.track.changes);
            app.World.Preview(shape);
            aiPreview = true;
        }
        /// <summary>Buttons side by side across the details panel, below everything added so far.</summary>
        RectTransform[] ButtonRow(params (string title, Action action)[] buttons)
        {
            const float gap = 12, height = 96;
            float width = (context.rect.width - 44 - gap * (buttons.Length - 1)) / buttons.Length;
            var row = new RectTransform[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                // Created above y -200 so Button() does not stack it, then moved onto this row.
                var r = Button(contextBody, buttons[i].title, buttons[i].action, new Vector2(22, -100), new Vector2(width, height));
                r.anchoredPosition = new Vector2(22 + i * (width + gap), -nextContextY);
                row[i] = r;
            }
            nextContextY += height + gap;
            contextBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(context.rect.height, nextContextY + 12));
            return row;
        }
    }
}
