using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>The window a tap on a ski resort opens: its tourists, its railway station, its road and how to serve it.</summary>
    public sealed partial class GameplayPresenter
    {
        string ResortText(ProducerState p)
        {
            var game = app.Game;
            var lines = new System.Collections.Generic.List<string> { "SKI RESORT", Heading("TOURISTS", ShipsColor) };
            lines.Add(Value($"{p.inventory} / {game.Balance.storage}") + "\nwaiting to go home by train");
            ProducerState town = null;
            foreach (var other in game.World.producers)
                if (other.kind == ProducerKind.Town && (town == null || other.cell.Distance(p.cell) < town.cell.Distance(p.cell)))
                    town = other;
            lines.Add(Where("Nearest town", town, p));
            var station = game.World.stations.Find(s => s.producerId == p.id);
            lines.Add(Heading("STATION", TakesColor));
            lines.Add(station != null ? Value(station.name) : "None yet: build one within 3 cells of the resort.");
            lines.Add(Heading("ROAD & CAR PARK", ShipsColor));
            var road = game.Cities.SkiRoadOf(p.id);
            var from = road == null ? null : game.Cargo.Producer(road.a);
            lines.Add(road == null ? "No road yet: the nearest town builds one once the towns are linked."
                : road.Complete ? $"Open from {from?.name}: skiers drive up and park by the gondola."
                : $"{from?.name} is building it: {road.built * 100 / road.path.Count}%");
            lines.Add("<size=20>A gondola runs to the summit, with blue, red and black pistes. Trains between the resort and the towns carry tourists both ways, paid by distance.</size>");
            return string.Join("\n", lines);
        }
    }
}
