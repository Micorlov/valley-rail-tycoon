using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>A place a light rail line can run to. <c>kind</c>, <c>id</c> and <c>cell</c> identify it, as a line stores them.</summary>
    public struct TramVenue
    {
        public TramVenueKind kind; public int id; public Cell cell;
        public string name;
        /// <summary>False while the venue cannot take a line yet (a beach whose car park is still going up); <c>status</c> says why.</summary>
        public bool open;
        public string status;
        /// <summary>The id of the line already serving it, or 0.</summary>
        public int lineId;
        public bool Matches(TramLineState line) => line.venueKind == (int)kind && line.venueId == id && line.venueCell.Equals(cell);
    }
    /// <summary>
    /// The places light rail serves: every town stadium, every beach (open once its car park is finished) and every ski resort.
    /// Each venue lists the cells its stop may take: next to the stadium, the beach car park or the resort's base.
    /// </summary>
    public static class TramVenues
    {
        public static List<TramVenue> All(WorldState w)
        {
            var list = new List<TramVenue>();
            foreach (var city in w.cities)
                foreach (var bs in city.buildings)
                    if (Stadium(bs))
                        list.Add(Finish(w, new TramVenue { kind = TramVenueKind.Stadium, id = city.producerId, cell = bs.cell, name = city.name + " Stadium", open = true, status = "Open" }));
            foreach (var road in w.intercityRoads)
            {
                if (!road.ToBeach || road.path == null || road.path.Count == 0)
                    continue;
                var town = w.cities.Find(c => c.producerId == road.a);
                string status = Coast.Open(road) ? "Open" : !road.Complete ? "Beach road under construction" : $"Car park stage {road.park} of {Coast.ParkSteps}";
                list.Add(Finish(w, new TramVenue { kind = TramVenueKind.Beach, id = road.a, cell = Coast.Entrance(road), name = (town != null ? town.name : "Coast") + " Beach", open = Coast.Open(road), status = status }));
            }
            foreach (var p in w.producers)
                if (p.kind == ProducerKind.SkiResort)
                    list.Add(Finish(w, new TramVenue { kind = TramVenueKind.Ski, id = p.id, cell = p.cell, name = p.name, open = true, status = "Open" }));
            return list;
        }
        static TramVenue Finish(WorldState w, TramVenue v)
        {
            var line = w.tramLines?.Find(l => v.Matches(l));
            v.lineId = line != null ? line.id : 0;
            return v;
        }
        /// <summary>True for a stadium: the live 8×8 bowl and the retired stadium styles older saves may still hold.</summary>
        public static bool Stadium(BuildingState bs) =>
            BuildingCatalog.Valid(bs.def) && (BuildingCatalog.Get(bs.def).style == BuildingStyle.Arena || BuildingCatalog.Get(bs.def).style == BuildingStyle.Stadium);
        /// <summary>The venue a line runs to as it stands now, or false when it is gone or not open (the line then idles).</summary>
        public static bool Resolve(WorldState w, TramLineState line, out TramVenue venue)
        {
            foreach (var v in All(w))
                if (v.Matches(line))
                {
                    venue = v;
                    return v.open;
                }
            venue = default;
            return false;
        }
        /// <summary>The cells the venue's stop may take, each beside the venue, nearest first to the venue's own cell.</summary>
        public static List<Cell> Targets(WorldState w, TramVenue v)
        {
            var cells = new List<Cell>();
            var seen = new HashSet<int>();
            switch (v.kind)
            {
                case TramVenueKind.Stadium:
                    foreach (var city in w.cities)
                        if (city.producerId == v.id)
                            foreach (var bs in city.buildings)
                                if (bs.cell.Equals(v.cell) && Stadium(bs))
                                    for (int i = 0; i < CityLayout.FootprintCells(bs); i++)
                                        Around(CityLayout.FootprintCell(bs, i), c => CityLayout.Covers(bs, c), cells, seen);
                    break;
                case TramVenueKind.Beach:
                    for (int i = 0; i < Coast.ParkCells; i++)
                        Around(Coast.ParkCell(v.cell, i), c => IsPark(v.cell, c) || c.Equals(v.cell), cells, seen);
                    break;
                case TramVenueKind.Ski:
                    for (int i = 0; i < SkiResorts.FootprintCells; i++)
                        Around(SkiResorts.FootprintCell(v.cell, i), c => SkiResorts.Covers(v.cell, c), cells, seen);
                    break;
            }
            var anchor = v.cell;
            cells.Sort((a, b) => a.Distance(anchor) != b.Distance(anchor) ? a.Distance(anchor).CompareTo(b.Distance(anchor)) : a.Key.CompareTo(b.Key));
            return cells;
        }
        static bool IsPark(Cell entrance, Cell c)
        {
            for (int i = 0; i < Coast.ParkCells; i++)
                if (Coast.ParkCell(entrance, i).Equals(c))
                    return true;
            return false;
        }
        static void Around(Cell c, Func<Cell, bool> inside, List<Cell> cells, HashSet<int> seen)
        {
            for (int d = 0; d < 4; d++)
            {
                var n = c.Move(d);
                if (MapDefinition.InBounds(n) && !inside(n) && seen.Add(n.Key))
                    cells.Add(n);
            }
        }
    }
}
