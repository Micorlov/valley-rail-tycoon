using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// AUTO ROUTE: a window that asks where a line starts and where it goes, shows the AI's route and price on the map, and
    /// builds it all in one tap (AutoRoute). It opens by itself at the start of a new game.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        public void AutoRouteFrom()
        {
            Context("AUTO ROUTE", "Where should your railway start?\nPick a place, then where it goes. The AI builds the stations, the track and a train for you.");
            // The line the tutorial or current goal asks for goes first, one tap from its price.
            if (Tutorial.Suggested(app.Game.World, out string start, out string end))
            {
                ProducerState a = app.Game.World.producers.Find(p => p.name == start), z = app.Game.World.producers.Find(p => p.name == end);
                if (a != null && z != null)
                    Button(contextBody, $"SUGGESTED\n<size=75%>{start.ToUpperInvariant()} → {end.ToUpperInvariant()}</size>", () => AutoRouteQuote(a, z), new Vector2(22, -205), new Vector2(345, 84), true);
            }
            foreach (var p in AutoRoute.Origins(app.Game.World))
            {
                var from = p;
                Button(contextBody, $"{p.name.ToUpperInvariant()}\n<size=75%>SENDS {Sends(p)}</size>", () => AutoRouteTo(from), new Vector2(22, -205), new Vector2(345, 84));
            }
        }
        void AutoRouteTo(ProducerState from)
        {
            Context("FROM " + from.name.ToUpperInvariant(), $"Where should the {Sends(from).ToLowerInvariant()} go? Nearest first; shorter lines are cheaper.");
            foreach (var p in AutoRoute.Targets(app.Game.World, from))
            {
                var to = p;
                Button(contextBody, $"{p.name.ToUpperInvariant()}\n<size=75%>{p.cell.Distance(from.cell)} CELLS AWAY</size>", () => AutoRouteQuote(from, to), new Vector2(22, -205), new Vector2(345, 84));
            }
            Button(contextBody, "BACK", AutoRouteFrom, new Vector2(22, -205), new Vector2(345, 56));
        }
        void AutoRouteQuote(ProducerState from, ProducerState to)
        {
            var plan = AutoRoute.Plan(app.Game, from.id, to.id);
            string train = TrainCatalog.Name(plan.model);
            string details = plan.fix == null ? plan.reason
                : $"Stations at {from.name} and {to.name}, the track between them and a {train} train · ${plan.cost:N0}\n" +
                  (plan.valid ? "Green marks the route. The train starts running as soon as it is built." : plan.reason);
            Context(from.name.ToUpperInvariant() + " → " + to.name.ToUpperInvariant(), details);
            contextText.color = plan.valid ? mint : warning;
            if (plan.fix != null)
            {
                var preview = AiPreview(plan.fix);
                foreach (var c in plan.site.platformCells)
                    if (!preview.path.Contains(c))
                        preview.path.Add(c);
                preview.changes.AddRange(plan.site.track.changes);
                app.World.Preview(preview);
                aiPreview = true;
                // Zoom in on the line (Frame only ever zooms out), so the player sees what they are buying and then the train.
                FrameCells(preview.path, new List<Cell>());
            }
            var build = Button(contextBody, plan.valid ? $"BUILD IT · ${plan.cost:N0}" : "CAN'T BUILD THIS", () => ConfirmAutoRoute(from, to), new Vector2(22, -205), new Vector2(345, 64), true);
            build.GetComponent<Button>().interactable = plan.valid;
            Button(contextBody, "BACK", () => AutoRouteTo(from), new Vector2(22, -205), new Vector2(345, 56));
            if (!plan.valid)
                app.Sfx.Ui(SoundCue.Error);
        }
        void ConfirmAutoRoute(ProducerState from, ProducerState to)
        {
            if (!app.Perform(() => AutoRoute.Build(app.Game, from.id, to.id)).ok)
            {
                AutoRouteQuote(from, to);
                return;
            }
            app.Sfx.Ui(SoundCue.StationBuilt);
            HideContext();
        }
        static string Sends(ProducerState p) => IndustryCatalog.CargoName(IndustryCatalog.Output(p.kind).Value).ToUpperInvariant();
    }
}
