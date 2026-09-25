using System;
using System.Collections.Generic;
using System.IO;
namespace ValleyRail.Core
{
    /// <summary>What a light rail line runs to. Stored in saves: append new kinds, never renumber.</summary>
    public enum TramVenueKind
    {
        None = 0, Stadium = 1, Beach = 2, Ski = 3
    }
    /// <summary>One tram on a line. Its position runs round the line's loop (<see cref="TramRoute"/>): out on one track, back on the other.</summary>
    [Serializable]
    public class TramState
    {
        public int id;
        /// <summary>Front of the tram along the loop, 0 up to the loop length; -1 while a newly bought tram waits to enter at the transfer stop.</summary>
        public int position = -1;
        public int dwell, units, moveRemainder, costRemainder;
    }
    /// <summary>
    /// A light rail line from a railway station's forecourt (the transfer stop, <c>cells[0]</c>) to a venue (the last cell):
    /// a stadium, a beach car park or a ski resort. Double track in town streets and on its own grassy bed elsewhere, run by
    /// up to <see cref="TramCatalog.MaxTrams"/> trams. Passengers change between trains and trams at the transfer stop.
    /// </summary>
    [Serializable]
    public class TramLineState
    {
        public int id, stationId, venueKind, venueId, paid;
        /// <summary>Identifies the venue with its kind and id: a stadium's anchor cell, a beach road's entrance, a resort's cell.</summary>
        public Cell venueCell;
        public List<Cell> cells = new List<Cell>();
        /// <summary>Riders waiting at the transfer stop for the venue, and at the venue for the station.</summary>
        public int waitingOut, waitingBack;
        /// <summary>Riders the trams brought who are still at the venue; they head back a few at a time.</summary>
        public int visitors;
        public int localRemainder, drawRemainder, transferRemainder;
        public TrainAccounts accounts = new TrainAccounts();
        public List<TramState> trams = new List<TramState>();
    }
    /// <summary>Light rail prices and running figures. They live in code, not the balance asset.</summary>
    public static class TramCatalog
    {
        public const int Price = 6500, Capacity = 36, Speed = 1600, RunningCost = 10, Dwell = 40;
        /// <summary>Simulation units: the least distance between one tram's front and the next tram's front, and a tram's length.</summary>
        public const int Gap = 1500, Length = 1100;
        public const int StreetCell = 150, OpenCell = 250, BridgeCell = 400, Crossing = 400, TransferStop = 1500, VenueStop = 1000;
        public const int MinCells = 5, MaxCells = 64, MaxLines = 12, MaxTrams = 4;
        public const int TransferShare = 25, QueueCap = 240, VisitorsCap = 2000, HomePercent = 5, HomeEvery = 60;
        public const int MinFareCells = 8, MaxFareCells = 32;
        /// <summary>How many trams a line of <paramref name="cells"/> cells can run: one per four cells, up to MaxTrams.</summary>
        public static int TramsFor(int cells) => Math.Max(1, Math.Min(MaxTrams, cells / 4));
        public static int Fare(Balance b, int cells) => b.CargoRate(Cargo.Passengers) * Math.Max(MinFareCells, Math.Min(MaxFareCells, cells - 1));
        /// <summary>Riders a minute who come from the town's station queue to go to the venue.</summary>
        public static int LocalRate(int kind) => kind == (int)TramVenueKind.Beach ? 16 : 12;
        /// <summary>Riders a minute the venue itself sends to the station (fans, bathers and skiers heading for a train).</summary>
        public static int DrawRate(int kind) => kind == (int)TramVenueKind.Beach ? 12 : kind == (int)TramVenueKind.Ski ? 10 : 8;
        public static string KindName(int kind) => kind == (int)TramVenueKind.Stadium ? "Stadium" : kind == (int)TramVenueKind.Beach ? "Beach" : kind == (int)TramVenueKind.Ski ? "Ski resort" : "Venue";
    }
    /// <summary>
    /// Where heavy rail and light rail may meet. A tram cell's mask holds the tram's ports (bits 0-3) and <see cref="Stop"/>
    /// at either end of a line. Heavy track may only cross a tram line straight over at right angles, away from its stops.
    /// </summary>
    public static class TramRules
    {
        public const int Stop = 16;
        static int Ports(int tramMask) => tramMask & 15;
        /// <summary>True when heavy track heading <paramref name="d"/> may run through a cell with this tram mask.</summary>
        public static bool TrackMayEnter(int tramMask, int d) =>
            tramMask == 0 || ((tramMask & Stop) == 0 && Directions.Straight(Ports(tramMask)) && (Ports(tramMask) & (1 << d)) == 0);
        /// <summary>True when a heavy track piece may lie on a cell with this tram mask: a straight, level crossing at right angles.</summary>
        public static bool TrackMayCross(int tramMask, int trackMask, int bridge) =>
            tramMask == 0 || ((tramMask & Stop) == 0 && Directions.Straight(Ports(tramMask)) && Directions.Straight(trackMask) && bridge == 0 && (Ports(tramMask) & trackMask) == 0);
    }
    /// <summary>Rules and hooks over the saved light rail lines that other services call.</summary>
    public static class TramLines
    {
        /// <summary>Passengers a train unloads at a station change to its light rail lines: each line takes its share.</summary>
        public static void Arrive(WorldState w, StationState at, Cargo cargo, int units)
        {
            if (cargo != Cargo.Passengers || units <= 0 || w.tramLines == null)
                return;
            foreach (var line in w.tramLines)
            {
                if (line.stationId != at.id)
                    continue;
                int share = units * TramCatalog.TransferShare + line.transferRemainder;
                line.transferRemainder = share % 100;
                line.waitingOut = Math.Min(TramCatalog.QueueCap, line.waitingOut + share / 100);
            }
        }
        public static bool UsesStation(WorldState w, int stationId) => w.tramLines != null && w.tramLines.Exists(l => l.stationId == stationId);
        public static TramLineState Line(WorldState w, int id) => w.tramLines?.Find(l => l.id == id);
        /// <summary>The line with a track or stop on the cell, or null.</summary>
        public static TramLineState LineAt(WorldState w, Cell c)
        {
            if (w.tramLines == null)
                return null;
            foreach (var line in w.tramLines)
                foreach (var cell in line.cells)
                    if (cell.Equals(c))
                        return line;
            return null;
        }
        /// <summary>The line serving the stadium whose footprint covers the cell, or null.</summary>
        public static TramLineState ServingBuilding(WorldState w, Cell c)
        {
            if (w.tramLines == null)
                return null;
            foreach (var line in w.tramLines)
            {
                if (line.venueKind != (int)TramVenueKind.Stadium)
                    continue;
                foreach (var city in w.cities)
                    if (city.producerId == line.venueId)
                        foreach (var bs in city.buildings)
                            if (bs.cell.Equals(line.venueCell) && CityLayout.Covers(bs, c))
                                return line;
            }
            return null;
        }
        /// <summary>
        /// Save checks: only the structure, never the land or whether the station and venue still exist. A line whose station
        /// or venue is gone idles (TramService.Status), so a later map change can never make a save unloadable.
        /// </summary>
        public static void Validate(WorldState w, Balance b, Action<int> check)
        {
            if (w.tramLines == null)
                return;
            if (w.tramLines.Count > TramCatalog.MaxLines)
                throw new InvalidDataException("Too many light rail lines.");
            var used = new HashSet<int>();
            var venues = new HashSet<string>();
            foreach (var line in w.tramLines)
            {
                if (line == null || line.cells == null || line.trams == null)
                    throw new InvalidDataException("Invalid light rail line.");
                check(line.id);
                if (line.stationId <= 0 || !Enum.IsDefined(typeof(TramVenueKind), line.venueKind) || line.venueKind == 0 || !venues.Add(line.venueKind + ":" + line.venueId + ":" + line.venueCell.Key) ||
                    line.paid < 0 || line.cells.Count < TramCatalog.MinCells || line.cells.Count > TramCatalog.MaxCells)
                    throw new InvalidDataException("Invalid light rail line.");
                for (int i = 0; i < line.cells.Count; i++)
                {
                    var c = line.cells[i];
                    if (!MapDefinition.InBounds(c) || !used.Add(c.Key) || (i > 0 && c.Distance(line.cells[i - 1]) != 1))
                        throw new InvalidDataException("Invalid light rail track.");
                }
                int n = line.cells.Count;
                if (Directions.Between(line.cells[0], line.cells[1]) != Directions.Between(line.cells[1], line.cells[2]) ||
                    Directions.Between(line.cells[n - 3], line.cells[n - 2]) != Directions.Between(line.cells[n - 2], line.cells[n - 1]))
                    throw new InvalidDataException("Light rail stops must be straight.");
                if (line.waitingOut < 0 || line.waitingOut > TramCatalog.QueueCap || line.waitingBack < 0 || line.waitingBack > TramCatalog.QueueCap ||
                    line.visitors < 0 || line.visitors > TramCatalog.VisitorsCap || line.localRemainder < 0 || line.localRemainder >= 1200 ||
                    line.drawRemainder < 0 || line.drawRemainder >= 1200 || line.transferRemainder < 0 || line.transferRemainder >= 100)
                    throw new InvalidDataException("Invalid light rail riders.");
                if (!TrainAccounts.Valid(line.accounts, w.tick))
                    throw new InvalidDataException("Invalid light rail accounts.");
                if (line.trams.Count < 1 || line.trams.Count > TramCatalog.TramsFor(n))
                    throw new InvalidDataException("Invalid tram count.");
                int loop = TramRoute.LoopLength(line.cells);
                foreach (var t in line.trams)
                {
                    if (t == null)
                        throw new InvalidDataException("Invalid tram.");
                    check(t.id);
                    if (t.position < -1 || t.position >= loop || t.dwell < 0 || t.dwell > TramCatalog.Dwell || t.units < 0 || t.units > TramCatalog.Capacity ||
                        t.moveRemainder < 0 || t.moveRemainder >= 20 || t.costRemainder < 0 || t.costRemainder >= 1200)
                        throw new InvalidDataException("Invalid tram.");
                }
            }
        }
    }
}
