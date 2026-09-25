using System.Collections.Generic;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// The window a tap on an industry opens: what it takes in, what it ships out and where each load comes from or
    /// goes to. It replaces the caption that used to float over every industry.
    /// </summary>
    public sealed partial class GameplayPresenter
    {
        const string TakesColor = "8FD697", ShipsColor = "F2C14E", ValueColor = "F5F0D9";
        string IndustryText(ProducerState p)
        {
            if (p.kind == ProducerKind.SkiResort)
                return ResortText(p);
            var w = app.Game.World;
            var takes = IndustryGuide.Takes(p.kind);
            var output = IndustryCatalog.Output(p.kind);
            var lines = new List<string> { IndustryGuide.KindName(p.kind).ToUpperInvariant(), Heading("TAKES IN", TakesColor) };
            if (takes.Count == 0)
                lines.Add(Value("Nothing") + "\nIt produces on its own.");
            foreach (var cargo in takes)
                lines.Add(Value(IndustryCatalog.CargoName(cargo)) + "\n" + Where("From", IndustryGuide.NearestSource(w, p, cargo), p));
            lines.Add(Heading("SHIPS OUT", ShipsColor));
            if (output.HasValue)
            {
                var cargo = output.Value;
                lines.Add(Value(IndustryCatalog.CargoName(cargo)) + $"\nReady: {p.inventory} / {app.Game.Balance.storage} {Unit(cargo)}\n" + Where("To", IndustryGuide.NearestBuyer(w, p, cargo), p));
            }
            else
                lines.Add(Value("Nothing") + "\nPower only.");
            lines.Add("<size=20>" + IndustryGuide.HowItWorks(p.kind) + "\nBuild a station within 3 cells, on open ground or straight track.</size>");
            return string.Join("\n", lines);
        }
        static string Heading(string text, string color) => $"<size=12> </size>\n<color=#{color}><b>{text}</b></color>";
        static string Value(string text) => $"<size=30><color=#{ValueColor}>{text}</color></size>";
        static string Where(string direction, ProducerState other, ProducerState from) => other == null
            ? $"{direction}: none on the map yet"
            : $"{direction}: {other.name} ({other.cell.Distance(from.cell)} cells)";
    }
}
