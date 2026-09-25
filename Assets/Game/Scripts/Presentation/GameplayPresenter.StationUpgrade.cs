using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// AI STATION UPGRADE. The violet button on a station asks the AI (StationUpgradeService.Recommend) for the next type, size
    /// and platform count, and opens with that pick. TYPE, SIZE and PLATFORMS change it; every change re-plans and shows the
    /// price, what the bulldozer clears (red pins over each building, street cell and tree) and the new footprint. BULLDOZE & BUILD applies
    /// it and plays the show: a bulldozer clears the site, then the new station rises.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        // The panel frames the whole site with its demolition pins; the show then moves in close to watch the bulldozer and crane.
        const float PanelZoom = 6.5f, ShowZoom = 4.2f;
        int upgradeLevel, upgradeLength, upgradePlatforms;
        string upgradeAdvice = "";

        void UpgradeButton(StationState s)
        {
            bool busy = app.World.StationUpgrading(s.id), full = app.Game.Upgrades.FullyUpgraded(s);
            string label = busy ? "UPGRADE UNDER WAY…" : full ? "FULLY UPGRADED" : "AI UPGRADE STATION";
            var button = Button(contextBody, label, () => UpgradeStation(s.id), new Vector2(22, -245), new Vector2(345, 58));
            button.GetComponent<Image>().color = violet;
            button.GetComponentInChildren<TMP_Text>().color = navy;
            button.GetComponent<Button>().interactable = !busy && !full;
        }
        /// <summary>"Central station · 5 cells × 2 platforms · loads 30% faster" for the station details.</summary>
        string StationKindText(StationState s)
        {
            var kind = app.Game.Cargo.Producer(s.producerId).kind;
            string faster = s.level > 0 ? $" · loads {StationCatalog.FasterLoadingPercent(s.level)}% faster" : "";
            return $"{StationCatalog.Name(kind, s.level)} · {StationLayout.Describe(s)}{faster}";
        }
        /// <summary>Opens the upgrade panel on the AI's pick.</summary>
        void UpgradeStation(int stationId)
        {
            var advice = app.Game.Upgrades.Recommend(stationId);
            upgradeAdvice = advice.reason;
            upgradeLevel = advice.level;
            upgradeLength = advice.length;
            upgradePlatforms = advice.platforms;
            ShowUpgrade(stationId);
        }
        void ShowUpgrade(int stationId)
        {
            var s = app.Game.Trains.Station(stationId);
            if (s == null)
            {
                StationList();
                return;
            }
            var kind = app.Game.Cargo.Producer(s.producerId).kind;
            var plan = app.Game.Upgrades.Plan(stationId, upgradeLevel, upgradeLength, upgradePlatforms);
            Context("AI STATION UPGRADE", UpgradeText(s, kind, plan));
            contextText.color = plan.valid ? mint : warning;
            ShowUpgradePlan(plan);
            TypeRows(s, kind);
            ButtonRow(("SIZE " + upgradeLength, () => { upgradeLength = Cycle(upgradeLength, Mathf.Max(StationLayout.Length(s), StationCatalog.MinLength(upgradeLevel)), StationLayout.MaxLength); ShowUpgrade(stationId); }),
                ("PLATFORMS " + upgradePlatforms, () => { upgradePlatforms = Cycle(upgradePlatforms, Mathf.Max(StationLayout.Platforms(s), StationCatalog.MinPlatformCount(upgradeLevel)), StationLayout.MaxPlatforms); ShowUpgrade(stationId); }));
            var build = Button(contextBody, plan.valid ? $"BULLDOZE & BUILD · ${plan.cost:N0}" : "CAN'T BUILD THIS YET", () => ConfirmUpgrade(stationId), new Vector2(22, -205), new Vector2(345, 64), true);
            build.GetComponent<Image>().color = violet;
            build.GetComponent<Button>().interactable = plan.valid;
            Button(contextBody, "USE AI PICK", () => UpgradeStation(stationId), new Vector2(22, -205), new Vector2(345, 56));
            Button(contextBody, "BACK", () => Station(s), new Vector2(22, -205), new Vector2(345, 56));
            if (!plan.valid)
                app.Sfx.Ui(SoundCue.Error);
        }
        static int Cycle(int value, int min, int max) => value >= max ? min : Mathf.Max(min, value + 1);
        /// <summary>One button per rung, two to a row; rungs below the station's own are done and disabled.</summary>
        void TypeRows(StationState s, ProducerKind kind)
        {
            int max = StationCatalog.MaxLevel(kind);
            var titles = new List<(string, System.Action)>();
            for (int level = 0; level <= max; level++)
            {
                int picked = level;
                titles.Add((StationCatalog.Name(kind, level).ToUpperInvariant().Replace(' ', '\n'), () => PickType(s.id, picked)));
            }
            for (int first = 0; first <= max; first += 2)
            {
                var row = ButtonRow(titles.GetRange(first, Mathf.Min(2, max + 1 - first)).ToArray());
                for (int i = 0; i < row.Length; i++)
                {
                    int level = first + i;
                    Highlight(row[i], level == upgradeLevel);
                    row[i].GetComponent<Button>().interactable = level >= s.level;
                }
            }
        }
        void PickType(int stationId, int level)
        {
            upgradeLevel = level;
            upgradeLength = Mathf.Max(upgradeLength, StationCatalog.MinLength(level));
            upgradePlatforms = Mathf.Max(upgradePlatforms, StationCatalog.MinPlatformCount(level));
            ShowUpgrade(stationId);
        }
        string UpgradeText(StationState s, ProducerKind kind, UpgradePlan plan)
        {
            string change = $"{StationCatalog.Name(kind, s.level)} → {StationCatalog.Name(kind, upgradeLevel)} · {StationLayout.Describe(upgradeLength, upgradePlatforms)}";
            string price = plan.cost == 0 ? "" : $"\n${plan.cost:N0}: station ${plan.stationPrice:N0}" +
                (plan.clearingCost > 0 ? $" + clearing ${plan.clearingCost:N0}" : "") + (plan.trackCost > 0 ? $" + track ${plan.trackCost:N0}" : "") +
                (plan.skyscrapers > 0 ? $"\nSkyscrapers: ${plan.skyscraperCost:N0} for {plan.skyscrapers} tower{(plan.skyscrapers > 1 ? "s" : "")}" : "");
            string bonus = $"Loads {StationCatalog.FasterLoadingPercent(upgradeLevel)}% faster" + (kind == ProducerKind.Town ? $", town grows {StationCatalog.GrowthPercent(upgradeLevel)}% faster" + (StationCatalog.ExtraBuildings(upgradeLevel) > 0 ? $", +{StationCatalog.ExtraBuildings(upgradeLevel)} building{(StationCatalog.ExtraBuildings(upgradeLevel) > 1 ? "s" : "")}/min" : "") : "") + ".";
            string outcome = plan.valid ? $"{StationUpgradeService.Clearing(plan)} {bonus}" : plan.reason;
            string ai = $"<color=#{ColorUtility.ToHtmlStringRGB(violet)}>AI · {upgradeAdvice}</color>";
            return $"{change}{price}\n{outcome}\n{ai}";
        }
        /// <summary>The new footprint on the map, the track it lays, and a red pin over everything the bulldozer will clear.</summary>
        void ShowUpgradePlan(UpgradePlan plan)
        {
            var shape = new BuildPlan { valid = plan.valid };
            shape.path.AddRange(plan.platformCells);
            shape.path.AddRange(plan.strip);
            shape.changes.AddRange(plan.track.changes);
            app.World.Preview(shape);
            var doomed = new List<Vector3>();
            foreach (var bs in plan.buildings)
                doomed.Add(app.World.BuildingSpot(bs));
            foreach (var c in plan.trees)
                doomed.Add(new Vector3(c.x, 2f, c.z));
            foreach (var c in plan.streets)
                doomed.Add(new Vector3(c.x, .2f, c.z));
            app.World.MarkDoomed(doomed);
            aiPreview = true;
            FrameCells(plan.platformCells, plan.strip);
        }
        void FrameCells(List<Cell> a, List<Cell> b)
        {
            var cells = new List<Cell>(a);
            cells.AddRange(b);
            if (cells.Count == 0)
                return;
            float minX = cells[0].x, maxX = minX, minZ = cells[0].z, maxZ = minZ;
            foreach (var c in cells)
            {
                minX = Mathf.Min(minX, c.x);
                maxX = Mathf.Max(maxX, c.x);
                minZ = Mathf.Min(minZ, c.z);
                maxZ = Mathf.Max(maxZ, c.z);
            }
            app.Camera.focus = new Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2);
            app.Camera.zoom = Mathf.Max(PanelZoom, Mathf.Max(maxX - minX, maxZ - minZ) * .5f + 4);
        }
        void ConfirmUpgrade(int stationId)
        {
            var s = app.Game.Trains.Station(stationId);
            if (s == null || app.World.StationUpgrading(stationId))
                return;
            var kind = app.Game.Cargo.Producer(s.producerId).kind;
            // Hide the finished station before the upgrade redraws the map, so the show can raise it.
            app.World.BeginStationUpgrade(stationId);
            var result = app.Perform(() => app.Game.Upgrades.Apply(stationId, upgradeLevel, upgradeLength, upgradePlatforms));
            if (!result.ok)
            {
                app.World.CancelStationUpgrade(stationId);
                ShowUpgrade(stationId);
                return;
            }
            HideContext();
            app.Camera.zoom = ShowZoom;
            string opened = $"{s.name} reopens as a {StationCatalog.Name(kind, s.level)}!";
            app.Notice = "The bulldozer is clearing the site…";
            app.World.PlayStationUpgrade(app.Game.Upgrades.Last,
                at => app.Sfx.World(SoundCue.Bulldoze, new Cell(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.z))),
                () => { app.Sfx.Ui(SoundCue.StationBuilt); app.Notice = opened; });
        }
    }
}
