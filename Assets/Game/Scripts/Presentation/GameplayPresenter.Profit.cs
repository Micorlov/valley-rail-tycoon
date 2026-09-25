using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>Profit lines for the fleet list and the train panel: delivery income minus running costs, over the last month and year.</summary>
    public sealed partial class GameplayPresenter
    {
        static readonly Color loss = new Color(1f, .55f, .47f);
        // Labels of the fleet cards on screen, rewritten by UpdateHud so the figures stay live while the list is open.
        readonly List<(int train, TMP_Text label)> fleetLabels = new List<(int, TMP_Text)>();
        string Signed(long amount)
        {
            var color = amount > 0 ? mint : amount < 0 ? loss : muted;
            string value = (amount > 0 ? "+$" : amount < 0 ? "-$" : "$") + Math.Abs(amount).ToString("N0");
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{value}</color>";
        }
        // The verdict follows the last year, which for a young train is everything since it was bought.
        string Verdict(TrainProfit year)
        {
            if (year.Profit == 0)
                return $"<color=#{ColorUtility.ToHtmlStringRGB(muted)}>NO EARNINGS YET</color>";
            return $"<b><color=#{ColorUtility.ToHtmlStringRGB(year.Profit > 0 ? mint : loss)}>{(year.Profit > 0 ? "PROFIT" : "LOSS")}</color></b>";
        }
        string FleetCardText(TrainState t)
        {
            long tick = app.Game.World.tick;
            var year = t.accounts.LastYear(tick);
            return $"#{t.number}  {TrainCatalog.Name(t.model)}\n{t.cargo} · {t.state}\n<size=85%>{Verdict(year)}  Month {Signed(t.accounts.LastMonth(tick).Profit)} · Year {Signed(year.Profit)}</size>";
        }
        string ProfitText(TrainState t)
        {
            long tick = app.Game.World.tick;
            var month = t.accounts.LastMonth(tick);
            var year = t.accounts.LastYear(tick);
            return $"{Verdict(year)} · income - running costs\nMonth: {Signed(month.Profit)}  (${month.income:N0} - ${month.cost:N0})\nYear: {Signed(year.Profit)}  (${year.income:N0} - ${year.cost:N0})";
        }
        string FleetText()
        {
            long tick = app.Game.World.tick, month = 0, year = 0;
            foreach (var t in app.Game.World.trains)
            {
                month += t.accounts.LastMonth(tick).Profit;
                year += t.accounts.LastYear(tick).Profit;
            }
            return $"Fleet · last month: {Signed(month)}\nFleet · last year: {Signed(year)}\nProfit = income - running costs\n1 month = 1 game minute\nTap a train to manage its route.\nTo buy a train, tap a station.";
        }
        /// <summary>Rewrites the fleet header and cards while the fleet list is the open view; any other view has destroyed them.</summary>
        void RefreshFleet()
        {
            if (fleetLabels.Count == 0)
                return;
            if (!fleetLabels[0].label || !fleetLabels[0].label.gameObject.activeInHierarchy)
            {
                fleetLabels.Clear();
                return;
            }
            contextText.text = FleetText();
            foreach (var (id, label) in fleetLabels)
            {
                var t = app.Game.Trains.Train(id);
                if (t != null && label)
                    label.text = FleetCardText(t);
            }
        }
    }
}
