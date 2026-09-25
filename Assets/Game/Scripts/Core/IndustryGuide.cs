using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>
    /// What a tapped industry takes in and ships out, where on the map those loads come from and go to, and which
    /// industry a finger on the tilted view is really pointing at. Industries carry no floating caption; this feeds the
    /// details window instead.
    /// </summary>
    public static class IndustryGuide
    {
        static readonly Cargo[] Freight = { Cargo.Coal, Cargo.Wood, Cargo.Oil, Cargo.IronOre, Cargo.Steel, Cargo.Goods };
        // The 3×3 yard reaches 1.45 cells from the centre; the ray walk starts above the tallest furnace column.
        const float Reach = 1.45f, Top = 3f, Step = .05f;
        public static string KindName(ProducerKind kind)
        {
            switch (kind)
            {
                case ProducerKind.Mine: return "Coal mine";
                case ProducerKind.Plant: return "Power station";
                case ProducerKind.OilWells: return "Oil wells";
                case ProducerKind.IronMine: return "Iron mine";
                case ProducerKind.SteelMill: return "Steel mill";
                case ProducerKind.SkiResort: return "Ski resort";
                default: return kind.ToString();
            }
        }
        public static List<Cargo> Takes(ProducerKind kind)
        {
            var list = new List<Cargo>();
            foreach (var c in Freight)
                if (MapDefinition.Accepts(kind, c))
                    list.Add(c);
            return list;
        }
        /// <summary>One line on how input turns into output, matching CargoFlow.Step and CargoFlow.Receive.</summary>
        public static string HowItWorks(ProducerKind kind)
        {
            var output = IndustryCatalog.Output(kind);
            var input = Takes(kind);
            if (!output.HasValue)
                return input.Count > 0 ? $"Burns {Name(input[0])} to make power. Every delivery pays." : "";
            if (IndustryCatalog.Processing(kind))
                return $"Makes nothing on its own: each unit of {Name(input[0])} delivered becomes 1 unit of {Name(output.Value)}.";
            if (input.Count > 0)
                return $"Makes {Name(output.Value)} on its own, and each unit of {Name(input[0])} delivered adds 1 more.";
            return $"Makes {Name(output.Value)} on its own. Pick it up by train.";
        }
        /// <summary>The nearest other producer that makes <paramref name="cargo"/>, or null.</summary>
        public static ProducerState NearestSource(WorldState w, ProducerState p, Cargo cargo) =>
            Nearest(w, p, other => MapDefinition.Produces(other.kind, cargo));
        /// <summary>The nearest other producer that pays for <paramref name="cargo"/>, or null.</summary>
        public static ProducerState NearestBuyer(WorldState w, ProducerState p, Cargo cargo) =>
            Nearest(w, p, other => MapDefinition.Accepts(other.kind, cargo));
        static ProducerState Nearest(WorldState w, ProducerState p, System.Func<ProducerState, bool> match)
        {
            ProducerState best = null;
            int bestDistance = int.MaxValue;
            foreach (var other in w.producers)
            {
                if (other.id == p.id || !match(other))
                    continue;
                int d = other.cell.Distance(p.cell);
                if (d < bestDistance)
                {
                    best = other;
                    bestDistance = d;
                }
            }
            return best;
        }
        static string Name(Cargo c) => IndustryCatalog.CargoName(c).ToLowerInvariant();
        /// <summary>
        /// The industry whose buildings the finger's ray passes through first, or null. The ray meets the ground well
        /// behind a building on the tilted view, so a ground-cell lookup alone lands on a town or station behind it.
        /// </summary>
        /// <param name="ox">Ray origin and (normalised) direction in world units, one unit per cell.</param>
        public static ProducerState Hit(WorldState w, float ox, float oy, float oz, float dx, float dy, float dz)
        {
            if (dy > -.01f)
                return null;
            float start = System.Math.Max(0, (oy - Top) / -dy), end = oy / -dy;
            for (float t = start; t <= end; t += Step)
            {
                float x = ox + dx * t, y = oy + dy * t, z = oz + dz * t;
                foreach (var p in w.producers)
                    if (p.kind != ProducerKind.Town && System.Math.Abs(x - p.cell.x) <= Reach + MapDefinition.Reach(p.kind) - 1 && System.Math.Abs(z - p.cell.z) <= Reach + MapDefinition.Reach(p.kind) - 1 && y <= BulkHeight(p.kind))
                        return p;
            }
            return null;
        }
        /// <summary>Height of the solid part of each industry's art (WorldView.DrawIndustry); thin chimneys above it do not count.</summary>
        public static float BulkHeight(ProducerKind kind)
        {
            switch (kind)
            {
                case ProducerKind.Refinery: case ProducerKind.SteelMill: return 2.3f;
                case ProducerKind.Forest: return 2.1f;
                case ProducerKind.IronMine: return 2f;
                case ProducerKind.Sawmill: return 1.7f;
                case ProducerKind.Plant: return 1.5f;
                case ProducerKind.SkiResort: return 1.6f; // the lodge's eaves (SkiArt)
                default: return 1.3f;
            }
        }
    }
}
