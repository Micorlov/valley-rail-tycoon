using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// LIGHT RAIL in the build tray. The list shows every stadium, beach and ski resort with its line or why it cannot have one
    /// yet. Picking a venue asks the AI (TramPlanner) to lay a line from the nearest town railway station: the route shows
    /// on the map with its price, FROM cycles through the nearest stations, and one tap builds it. A line's panel shows its
    /// riders and profit and adds, sells or removes trams.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        const float LightRailZoom = 8f;
        int selectedTramLine;

        public void LightRailList()
        {
            var w = app.Game.World;
            var venues = TramVenues.All(w);
            // Venues with a line first, then those ready for one, then the rest; nearest name order within each.
            venues.Sort((a, z) => Rank(a) != Rank(z) ? Rank(a).CompareTo(Rank(z)) : string.CompareOrdinal(a.name, z.name));
            Context("LIGHT RAIL", LightRailSummary());
            foreach (var v in venues)
            {
                var venue = v;
                string state = v.lineId != 0 ? "LINE RUNNING" : v.open ? "TAP TO PLAN A LINE" : v.status.ToUpperInvariant();
                var card = Button(contextBody, $"{v.name.ToUpperInvariant()}\n<size=75%>{TramCatalog.KindName((int)v.kind)} · {state}</size>", () => OpenVenue(venue), new Vector2(22, -205), new Vector2(345, 84), v.lineId != 0);
                if (!v.open && v.lineId == 0)
                    Label(card).color = muted;
            }
        }
        static int Rank(TramVenue v) => v.lineId != 0 ? 0 : v.open ? 1 : 2;
        string LightRailSummary()
        {
            var w = app.Game.World;
            long tick = w.tick;
            var year = new TrainProfit();
            int trams = 0;
            foreach (var line in w.tramLines)
            {
                var y = line.accounts.LastYear(tick);
                year.income += y.income;
                year.cost += y.cost;
                trams += line.trams.Count;
            }
            string lines = w.tramLines.Count == 0 ? "No lines yet." : $"{w.tramLines.Count} line{(w.tramLines.Count == 1 ? "" : "s")} · {trams} tram{(trams == 1 ? "" : "s")} · year {Signed(year.Profit)}";
            return $"Trams run from a town's railway station to its stadium, beach or ski resort. A quarter of the passengers arriving by train change to the tram, and visitors ride back to catch trains.\n{lines}";
        }
        void OpenVenue(TramVenue v)
        {
            if (v.lineId != 0)
            {
                var line = app.Game.Trams.Line(v.lineId);
                if (line != null)
                {
                    TramLine(line);
                    return;
                }
            }
            LightRailVenue(v, 0);
        }
        void LightRailVenue(TramVenue v, int pick)
        {
            string title = "LIGHT RAIL TO " + v.name.ToUpperInvariant();
            if (!v.open)
            {
                Context(title, $"{v.name} cannot take a line yet: {v.status}.\nA beach opens when its car park is finished.");
                contextText.color = warning;
                Button(contextBody, "BACK", LightRailList, new Vector2(22, -205), new Vector2(345, 56));
                return;
            }
            var stations = app.Game.Trams.Planner.Stations(v);
            if (stations.Count == 0)
            {
                Context(title, $"No town railway station is within {TramPlanner.Reach} cells of {v.name}.\nBuild a station in a nearby town first; the tram's transfer stop stands in front of it.");
                contextText.color = warning;
                FrameCells(new List<Cell> { v.cell }, new List<Cell>());
                Button(contextBody, "BACK", LightRailList, new Vector2(22, -205), new Vector2(345, 56));
                return;
            }
            pick = ((pick % stations.Count) + stations.Count) % stations.Count;
            var s = stations[pick];
            var plan = app.Game.Trams.Planner.Plan(v, s.id);
            Context(title, LightRailPlanText(v, s, plan));
            contextText.color = plan.valid ? mint : warning;
            ShowTramPlan(plan);
            var build = Button(contextBody, plan.valid ? $"BUILD LIGHT RAIL · ${plan.cost:N0}" : "CAN'T BUILD THIS YET", () => ConfirmLightRail(v, pick, plan), new Vector2(22, -205), new Vector2(345, 64), true);
            build.GetComponent<Image>().color = violet;
            build.GetComponent<Button>().interactable = plan.valid;
            if (stations.Count > 1)
                Button(contextBody, $"FROM: {s.name.ToUpperInvariant()}  ►", () => LightRailVenue(v, pick + 1), new Vector2(22, -205), new Vector2(345, 56));
            Button(contextBody, "BACK", LightRailList, new Vector2(22, -205), new Vector2(345, 56));
            if (!plan.valid)
                app.Sfx.Ui(SoundCue.Error);
        }
        string LightRailPlanText(TramVenue v, StationState s, TramPlan plan)
        {
            if (plan.cells.Count == 0)
                return $"From {s.name}\n{plan.reason}";
            string trees = plan.clearingCost > 0 ? $" + trees ${plan.clearingCost:N0}" : "";
            string crossings = plan.crossings > 0 ? $" · {plan.crossings} railway crossing{(plan.crossings == 1 ? "" : "s")}" : "";
            string price = $"${plan.cost:N0}: track ${plan.trackCost:N0} + stops ${plan.stopCost:N0} + first tram ${plan.tramCost:N0}{trees}";
            string outcome = plan.valid ? $"Fare ${TramCatalog.Fare(app.Game.Balance, plan.cells.Count)} a rider · up to {TramCatalog.TramsFor(plan.cells.Count)} trams on this line." : plan.reason;
            string ai = $"<color=#{ColorUtility.ToHtmlStringRGB(violet)}>AI · transfer stop in front of {s.name}; {plan.streetCells} of {plan.cells.Count} cells run in the street{crossings}.</color>";
            return $"{price}\n{outcome}\n{ai}";
        }
        /// <summary>The planned line on the map: its double track as one ribbon, with a red pin over each tree it clears.</summary>
        void ShowTramPlan(TramPlan plan)
        {
            if (plan.cells.Count == 0)
            {
                app.World.Preview(null);
                app.World.MarkDoomed(null);
                return;
            }
            var shape = new BuildPlan { valid = plan.valid };
            int n = plan.cells.Count;
            for (int i = 0; i < n; i++)
            {
                var c = plan.cells[i];
                int mask = 0;
                if (i > 0) mask |= 1 << Directions.Between(c, plan.cells[i - 1]);
                if (i + 1 < n) mask |= 1 << Directions.Between(c, plan.cells[i + 1]);
                // The stops draw as straight pieces, so the ribbon reaches the very end of the line.
                if (Directions.Count(mask) == 1)
                    mask |= 1 << Directions.Opp(i == 0 ? Directions.Between(c, plan.cells[1]) : Directions.Between(c, plan.cells[n - 2]));
                shape.path.Add(c);
                shape.changes.Add(new TrackPieceState { cell = c, mask = mask });
            }
            app.World.Preview(shape);
            var doomed = new List<Vector3>();
            foreach (var c in plan.trees)
                doomed.Add(new Vector3(c.x, 2f, c.z));
            app.World.MarkDoomed(doomed);
            aiPreview = true;
            FrameCells(plan.cells, new List<Cell>());
            app.Camera.zoom = Mathf.Max(app.Camera.zoom, LightRailZoom);
        }
        void ConfirmLightRail(TramVenue v, int pick, TramPlan plan)
        {
            var result = app.Perform(() => app.Game.Trams.Build(plan));
            if (!result.ok)
            {
                LightRailVenue(v, pick);
                return;
            }
            app.Sfx.Ui(SoundCue.StationBuilt);
            var line = app.Game.Trams.Line(result.id);
            if (line != null)
                TramLine(line);
        }
        public void TramLine(TramLineState line)
        {
            Context(LineTitle(line), TramLineText(line));
            selectedTramLine = line.id;
            int max = TramCatalog.TramsFor(line.cells.Count);
            var add = Button(contextBody, line.trams.Count < max ? $"ADD TRAM · ${TramCatalog.Price:N0}" : $"FULL: {max} TRAM{(max == 1 ? "" : "S")}", () => { app.Perform(() => app.Game.Trams.AddTram(line.id)); TramLine(line); }, new Vector2(22, -205), new Vector2(345, 58), true);
            add.GetComponent<Button>().interactable = line.trams.Count < max;
            if (line.trams.Count > 1)
                Button(contextBody, $"SELL A TRAM · +${TramCatalog.Price / 2:N0}", () => { app.Perform(() => app.Game.Trams.SellTram(line.id)); TramLine(line); }, new Vector2(22, -205), new Vector2(345, 54));
            Button(contextBody, "SHOW ON MAP", () => FrameCells(line.cells, new List<Cell>()), new Vector2(22, -205), new Vector2(345, 54));
            Button(contextBody, "REMOVE LINE", () => ConfirmRemoveLine(line), new Vector2(22, -205), new Vector2(345, 54));
            Button(contextBody, "ALL LIGHT RAIL", LightRailList, new Vector2(22, -205), new Vector2(345, 54));
        }
        string LineTitle(TramLineState line)
        {
            foreach (var v in TramVenues.All(app.Game.World))
                if (v.Matches(line))
                    return v.name.ToUpperInvariant() + " LINE";
            return TramCatalog.KindName(line.venueKind).ToUpperInvariant() + " LINE";
        }
        string TramLineText(TramLineState line)
        {
            var station = app.Game.Trams.Station(line);
            long tick = app.Game.World.tick;
            var month = line.accounts.LastMonth(tick);
            var year = line.accounts.LastYear(tick);
            var status = app.Game.Trams.Status(line);
            string where = TramCatalog.KindName(line.venueKind).ToLowerInvariant();
            return $"{(station != null ? station.name : "Station gone")} ⇄ {where} · {line.cells.Count} cells\n" +
                $"{TramService.Describe(status)} · {line.trams.Count}/{TramCatalog.TramsFor(line.cells.Count)} trams · ${TramCatalog.Fare(app.Game.Balance, line.cells.Count)} a ride\n" +
                $"Waiting: {line.waitingOut} at the station · {line.waitingBack} at the {where} · {line.visitors:N0} visiting\n" +
                $"{Verdict(year)} · Month {Signed(month.Profit)} · Year {Signed(year.Profit)}";
        }
        void RefreshTramLine()
        {
            var line = app.Game.Trams.Line(selectedTramLine);
            if (line != null)
                contextText.text = TramLineText(line);
        }
        void ConfirmRemoveLine(TramLineState line)
        {
            int refund = line.paid / 2 + line.trams.Count * (TramCatalog.Price / 2);
            Context("REMOVE THIS LINE?", $"The track, both stops and {line.trams.Count} tram{(line.trams.Count == 1 ? "" : "s")} go. You get ${refund:N0} back, half of what they cost.");
            contextText.color = warning;
            var remove = Button(contextBody, $"REMOVE LINE · +${refund:N0}", () => { if (app.Perform(() => app.Game.Trams.Remove(line.id)).ok) LightRailList(); }, new Vector2(22, -205), new Vector2(345, 58), true);
            remove.GetComponent<Image>().color = warning;
            Button(contextBody, "KEEP IT", () => TramLine(line), new Vector2(22, -205), new Vector2(345, 54));
        }
        /// <summary>A tap with the LIGHT RAIL tool: a tram or its track opens the line, a venue opens its plan, anything else the list.</summary>
        public void LightRailAt(Cell c)
        {
            var line = app.World.LightRailTramNear(c) ?? TramLines.LineAt(app.Game.World, c);
            if (line != null)
            {
                TramLine(line);
                return;
            }
            foreach (var v in TramVenues.All(app.Game.World))
                foreach (var t in TramVenues.Targets(app.Game.World, v))
                    if (t.Distance(c) <= 2)
                    {
                        OpenVenue(v);
                        return;
                    }
            LightRailList();
        }
    }
}
