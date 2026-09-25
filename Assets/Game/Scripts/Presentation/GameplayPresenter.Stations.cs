using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// The STATIONS list: every station, busiest first, each with a live load bar that turns amber and red as its queue
    /// fills. Tapping one flies the camera there, close enough to see the people waiting, and opens its details.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        const float StationCardHeight = 112, StationZoom = 12;
        static readonly Color BarBack = new Color(.05f, .1f, .13f);
        sealed class StationRow
        {
            public int id;
            public TMP_Text text;
            public RectTransform fill;
        }
        // Rows of the open list, refreshed with the HUD; Context() empties it whenever the panel shows something else.
        readonly List<StationRow> stationRows = new List<StationRow>();

        public void StationList()
        {
            var w = app.Game.World;
            Context("YOUR STATIONS", StationSummary());
            if (w.stations.Count == 0)
                contextText.text = "No stations yet. Open BUILD, choose STATION and tap open ground or straight track near a town or industry.";
            foreach (var s in StationLoad.Busiest(w, app.Game.Balance))
                stationRows.Add(StationCard(s));
        }
        string StationSummary()
        {
            var w = app.Game.World;
            int count = w.stations.Count, waiting = StationLoad.PassengersWaiting(w, app.Game.Balance);
            return $"{count} station{(count == 1 ? "" : "s")} · {waiting:N0} passengers waiting\nBusiest first. Tap one to fly there.";
        }
        StationRow StationCard(StationState s)
        {
            int id = s.id;
            var card = Named(Button(contextBody, s.name, () => FlyToStation(id), new Vector2(22, -205), new Vector2(345, StationCardHeight)), "Station card " + id);
            var text = Label(card);
            Place(text.rectTransform, 0, 0, 1, 1, 16, 30, -16, -8);
            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = 21;
            var bar = Panel("Load bar", card, BarBack);
            bar.GetComponent<Image>().raycastTarget = false;
            Place(bar, 0, 0, 1, 0, 16, 14, -16, 26);
            var fill = Panel("Load", bar, mint);
            fill.GetComponent<Image>().raycastTarget = false;
            var row = new StationRow { id = id, text = text, fill = fill };
            ShowStationRow(row);
            return row;
        }
        void RefreshStationRows()
        {
            if (stationRows.Count == 0 || !context.gameObject.activeSelf)
                return;
            contextText.text = StationSummary();
            foreach (var row in stationRows)
                ShowStationRow(row);
        }
        void ShowStationRow(StationRow row)
        {
            var s = app.Game.Trains.Station(row.id);
            if (s == null)
            {
                row.text.text = $"<color=#{ColorUtility.ToHtmlStringRGB(muted)}>Station removed</color>";
                Place(row.fill, 0, 0, 0, 1, 0, 0, 0, 0);
                return;
            }
            var load = StationLoad.Of(app.Game.World, app.Game.Balance, s);
            var color = StationCrowds.LoadColor(load.Level);
            string queue = load.cargo.HasValue ? $"{load.waiting:N0} {Unit(load.cargo.Value)} waiting · {load.Percent}%" : "Accepts " + IndustryCatalog.Inputs(app.Game.Cargo.Producer(s.producerId).kind).ToLowerInvariant();
            string held = CargoTransfer.Describe(app.Game.Cargo.Producer(s.producerId));
            if (held != null)
                queue += $" · {held} held";
            row.text.text = $"{s.name}\n<color=#{ColorUtility.ToHtmlStringRGB(color)}>{queue}</color>";
            Place(row.fill, 0, 0, load.Fill, 1, 0, 0, 0, 0);
            row.fill.GetComponent<Image>().color = color;
        }
        void FlyToStation(int id)
        {
            var s = app.Game.Trains.Station(id);
            if (s == null)
            {
                StationList();
                return;
            }
            app.Camera.focus = new Vector3(s.cell.x, 0, s.cell.z);
            app.Camera.zoom = Mathf.Min(app.Camera.zoom, StationZoom);
            Station(s);
        }
    }
}
