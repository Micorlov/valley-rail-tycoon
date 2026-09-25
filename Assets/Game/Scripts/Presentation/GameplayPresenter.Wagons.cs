using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// The wagon picker: choose a new train's length before buying it, or add and remove wagons of a train standing at a
    /// station. Both show the chosen consist with its capacity and running cost, and redraw on every - / + tap.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        static string WagonCount(int wagons) => wagons == 1 ? "1 wagon" : wagons + " wagons";

        string WagonTerms(int model, Cargo cargo, int wagons)
        {
            var b = app.Game.Balance;
            return $"{WagonCount(wagons)} · {b.Capacity(model, wagons)} {Unit(cargo)} · ${b.RunningCost(model, wagons)}/min";
        }

        string EachWagon(int model, Cargo cargo)
        {
            var b = app.Game.Balance;
            return $"Each wagon: ${b.WagonPrice(model):N0} · +{b.WagonCapacity(model)} {Unit(cargo)} · +${b.WagonRunningCost(model)}/min";
        }

        /// <summary>Choose how many wagons a new train gets, then buy it at this station.</summary>
        void BuyWagons(StationState s, int model, Cargo cargo, int wagons)
        {
            var b = app.Game.Balance;
            string load = cargo == Cargo.Passengers ? "Passengers" : IndustryCatalog.CargoName(cargo);
            // Speed is stored in track units per second, with 1000 units per cell.
            Context(TrainCatalog.Name(model).ToUpperInvariant(), $"{load} · {WagonTerms(model, cargo, wagons)}\nSpeed: {b.speed[model] / 1000f:0.#} cells/s\n{EachWagon(model, cargo)}");
            WagonStepper(model, cargo, wagons, n => BuyWagons(s, model, cargo, n));
            Button(contextBody, $"BUY · ${b.TrainPrice(model, wagons):N0}", () =>
            {
                var result = app.Perform(() => app.Game.Trains.Buy(s.id, model, cargo, wagons));
                if (result.ok)
                    Train(app.Game.Trains.Train(result.id));
            }, new Vector2(22, -210), new Vector2(345, 58), true);
            Button(contextBody, "BACK", () => Shop(s), new Vector2(22, -280), new Vector2(345, 54));
        }

        /// <summary>Add or remove wagons of a train. The price or refund shows before anything is paid.</summary>
        void EditWagons(TrainState t, int wagons)
        {
            var b = app.Game.Balance;
            int current = TrainCatalog.Wagons(t), price = Math.Abs(wagons - current) * b.WagonPrice(t.model);
            string change = wagons == current ? "Tap - or + to change the length." : wagons > current ? $"Cost: ${price:N0}" : $"Refund: ${price / 2:N0}";
            bool atStation = t.state == ServiceState.Parked || t.state == ServiceState.Loading;
            string where = atStation ? "" : $"\n<color=#{ColorUtility.ToHtmlStringRGB(warning)}>Wagons change at a station: tap RETURN TO STATION first.</color>";
            Context($"TRAIN #{t.number} WAGONS", $"Now: {WagonTerms(t.model, t.cargo, current)}\nNew: {WagonTerms(t.model, t.cargo, wagons)}\n{change}{where}");
            WagonStepper(t.model, t.cargo, wagons, n => EditWagons(t, n));
            if (wagons != current)
            {
                string label = wagons > current ? $"ADD {WagonCount(wagons - current).ToUpperInvariant()} · ${price:N0}" : $"REMOVE {WagonCount(current - wagons).ToUpperInvariant()} · +${price / 2:N0}";
                Button(contextBody, label, () =>
                {
                    if (app.Perform(() => app.Game.Trains.SetWagons(t.id, wagons)).ok)
                        Train(t);
                }, new Vector2(22, -210), new Vector2(345, 58), true);
            }
            Button(contextBody, "BACK", () => Train(t), new Vector2(22, -280), new Vector2(345, 54));
        }

        /// <summary>A picture of the chosen consist above a [-] n wagons [+] row. Each tap calls <paramref name="choose"/> with the new count.</summary>
        void WagonStepper(int model, Cargo cargo, int wagons, Action<int> choose)
        {
            TrainPreview(contextBody, model, cargo, nextContextY, wagons);
            nextContextY += 132;
            float width = context.rect.width - 44;
            var row = Panel("Wagon count", contextBody, Color.clear);
            row.GetComponent<Image>().raycastTarget = false;
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
            row.anchoredPosition = new Vector2(22, -nextContextY);
            row.sizeDelta = new Vector2(width, 96);
            StepButton(row, "-", Vector2.zero, wagons > TrainCatalog.MinWagons, () => choose(wagons - 1));
            StepButton(row, "+", new Vector2(width - 96, 0), wagons < TrainCatalog.MaxWagons, () => choose(wagons + 1));
            var label = Text(row, WagonCount(wagons).ToUpperInvariant(), new Vector2(96, 0), new Vector2(width - 192, 96), 28, cream);
            label.alignment = TextAlignmentOptions.Center;
            nextContextY += 96 + 12;
            contextBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(context.rect.height, nextContextY + 12));
        }

        void StepButton(RectTransform row, string sign, Vector2 position, bool enabled, Action action)
        {
            var button = Button(row, sign, action, position, new Vector2(96, 96));
            button.name = sign == "+" ? "More wagons" : "Fewer wagons";
            button.GetComponent<Button>().interactable = enabled;
            button.GetComponentInChildren<TMP_Text>().fontSize = 44;
        }
    }
}
