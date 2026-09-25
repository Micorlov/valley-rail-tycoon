using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>One pine of the valley: its cell, the height of its foliage cone in cells and one of four shades of green.</summary>
    public struct TreeSpot
    {
        // The foliage cone sits on a short trunk; Radius is the cone's base radius in cells.
        public const float Crown = .35f, Radius = .72f;
        public Cell cell; public float height; public int shade;
        public float Top => Crown + height;
    }
    /// <summary>
    /// The valley's pines. The map fixes where they grow (a seeded scatter clear of industries, water, hills and dry
    /// plains); a save records only the trees that are gone. A tree stands until the bulldozer fells it or track, a
    /// station, a town building, a street or a built highway covers its cell. <see cref="Sync"/> makes those losses
    /// permanent before anything is removed, so a cleared cell stays bare instead of sprouting its tree again.
    /// </summary>
    public sealed class Scenery
    {
        const int Seed = 1729, Attempts = 1050, Border = 2, IndustryClearance = 6;
        readonly WorldState w;
        readonly TreeSpot[] trees;
        // Per map cell: 1 + the index of the tree growing there, or 0.
        readonly int[] slot = new int[MapDefinition.Size * MapDefinition.Size];
        readonly bool[] felled, covered = new bool[MapDefinition.Size * MapDefinition.Size];
        int coveredRevision = -1, coveredCityRevision = -1;
        public Scenery(WorldState w)
        {
            this.w = w;
            w.felledTrees = w.felledTrees ?? new List<Cell>();
            trees = Layout(w.producers);
            for (int i = 0; i < trees.Length; i++)
                slot[trees[i].cell.Key] = i + 1;
            felled = new bool[trees.Length];
            // Cells that no longer hold a tree (a changed scatter) are ignored rather than rejected.
            foreach (var c in w.felledTrees)
                if (MapDefinition.InBounds(c) && slot[c.Key] != 0)
                    felled[slot[c.Key] - 1] = true;
        }
        public IReadOnlyList<TreeSpot> Trees => trees;
        /// <summary>The scatter the map has always drawn: the same random sequence, so existing forests keep every tree in place.</summary>
        public static TreeSpot[] Layout(List<ProducerState> producers)
        {
            var spots = new List<TreeSpot>(Attempts);
            var taken = new HashSet<int>();
            var rng = new Random(Seed);
            for (int i = 0; i < Attempts; i++)
            {
                var c = new Cell(rng.Next(Border, MapDefinition.Size - Border), rng.Next(Border, MapDefinition.Size - Border));
                bool near = false;
                // Towns settlers found later never reshuffle the scatter; their streets and homes cover the pines instead.
                foreach (var p in producers)
                    if (!CitySimulation.Founded(p) && p.kind != ProducerKind.SkiResort && c.Distance(p.cell) < IndustryClearance)
                        near = true;
                if (near || MapDefinition.Water(c) || MapDefinition.Raised(c) || MapDefinition.Terrain(c) == 2 || !taken.Add(c.Key))
                    continue;
                float height = .9f + (float)rng.NextDouble() * .65f;
                int shade = rng.Next(4);
                // No pines on the beach sand. Its draws are still taken, so every other tree keeps its place and shape.
                if (!Coast.Beach(c))
                    spots.Add(new TreeSpot { cell = c, height = height, shade = shade });
            }
            return spots.ToArray();
        }
        /// <summary>Index of the tree growing on the cell, standing or not, or -1.</summary>
        public int TreeIndex(Cell c) => MapDefinition.InBounds(c) ? slot[c.Key] - 1 : -1;
        public bool Standing(int index)
        {
            Refresh();
            return !felled[index] && !covered[trees[index].cell.Key];
        }
        public bool TreeAt(Cell c)
        {
            int index = TreeIndex(c);
            return index >= 0 && Standing(index);
        }
        /// <summary>Removes a standing tree for good; false when none stands on the cell. Costs nothing: callers charge.</summary>
        public bool Fell(Cell c)
        {
            if (!TreeAt(c))
                return false;
            Mark(slot[c.Key] - 1);
            return true;
        }
        /// <summary>Fells every tree that something now covers, so removing that thing later leaves bare ground.</summary>
        public void Sync()
        {
            Refresh();
            for (int i = 0; i < trees.Length; i++)
                if (!felled[i] && covered[trees[i].cell.Key])
                    Mark(i);
        }
        void Mark(int index)
        {
            felled[index] = true;
            w.felledTrees.Add(trees[index].cell);
        }
        /// <summary>Recomputes the covered cells when track, stations or towns changed since the last look.</summary>
        void Refresh()
        {
            if (coveredRevision == w.revision && coveredCityRevision == w.cityRevision)
                return;
            coveredRevision = w.revision;
            coveredCityRevision = w.cityRevision;
            Array.Clear(covered, 0, covered.Length);
            foreach (var t in w.tracks)
                Cover(t.cell);
            // A ski resort's base clears its ground (SkiResorts).
            foreach (var p in w.producers)
                if (p.kind == ProducerKind.SkiResort)
                    for (int i = 0; i < SkiResorts.FootprintCells; i++)
                        Cover(SkiResorts.FootprintCell(p.cell, i));
            foreach (var s in w.stations)
                foreach (var c in StationLayout.Strip(s))
                    Cover(c);
            foreach (var city in w.cities)
            {
                foreach (var r in city.roads)
                    Cover(r.cell);
                foreach (var bs in city.buildings)
                    for (int k = 0; k < CityLayout.FootprintCells(bs); k++)
                        Cover(CityLayout.FootprintCell(bs, k));
            }
            foreach (var road in w.intercityRoads)
            {
                for (int i = 0; i < road.built; i++)
                    Cover(road.path[i]);
                // A beach car park clears its ground when its construction starts.
                if (road.ToBeach && road.park > 0)
                    for (int i = 0; i < Coast.ParkCells; i++)
                        Cover(Coast.ParkCell(Coast.Entrance(road), i));
                // So does a roadside service area.
                if (Roadside.Planned(road) && Roadside.Shaped(road))
                    for (int i = 0; i < Roadside.Cells; i++)
                        Cover(Roadside.SiteCell(road, i));
                // And a campsite.
                if (Campsites.Planned(road) && Campsites.Shaped(road))
                    for (int i = 0; i < Campsites.Cells; i++)
                        Cover(Campsites.SiteCell(road, i));
            }
        }
        void Cover(Cell c)
        {
            if (MapDefinition.InBounds(c))
                covered[c.Key] = true;
        }
    }
}
