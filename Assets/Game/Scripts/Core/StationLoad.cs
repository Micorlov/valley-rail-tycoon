using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    public enum LoadLevel { Empty, Calm, Busy, Crowded }
    /// <summary>
    /// How full the queue behind a station is. A town's passenger queue is shared by every station of that town, so they
    /// all report the same load; industries measure their stock against the balance storage.
    /// </summary>
    public readonly struct StationLoad
    {
        /// <summary>Passengers drawn on a full platform.</summary>
        public const int MaxFigures = 24;
        public const float BusyFill = .5f, CrowdedFill = .85f;
        public readonly int waiting, capacity;
        /// <summary>What waits here, or null for a stop that only accepts deliveries.</summary>
        public readonly Cargo? cargo;
        public StationLoad(int waiting, int capacity, Cargo? cargo)
        {
            this.waiting = Math.Max(0, waiting);
            this.capacity = Math.Max(0, capacity);
            this.cargo = cargo;
        }
        public bool Passengers => cargo == Cargo.Passengers;
        public float Fill => capacity > 0 ? Math.Min(1f, waiting / (float)capacity) : waiting > 0 ? 1f : 0f;
        public int Percent => (int)Math.Round(Fill * 100);
        public LoadLevel Level => waiting == 0 ? LoadLevel.Empty : Fill >= CrowdedFill ? LoadLevel.Crowded : Fill >= BusyFill ? LoadLevel.Busy : LoadLevel.Calm;
        /// <summary>Waiting people to draw on the platform: none for freight, at least one while anyone waits.</summary>
        public int Figures => !Passengers || waiting == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(Fill * MaxFigures));

        public static StationLoad Of(WorldState w, Balance b, StationState s)
        {
            var p = Producer(w, s.producerId);
            var output = p == null ? null : IndustryCatalog.Output(p.kind);
            if (!output.HasValue)
                return new StationLoad(0, 0, null);
            return new StationLoad(p.inventory, p.kind == ProducerKind.Town ? p.storage : b.storage, output);
        }
        /// <summary>All stations, busiest first: fuller queues, then longer ones, then by name.</summary>
        public static List<StationState> Busiest(WorldState w, Balance b)
        {
            var ranked = new List<(StationState station, StationLoad load)>(w.stations.Count);
            foreach (var s in w.stations)
                ranked.Add((s, Of(w, b, s)));
            ranked.Sort((x, y) =>
            {
                int byFill = y.load.Fill.CompareTo(x.load.Fill);
                if (byFill != 0)
                    return byFill;
                int byWaiting = y.load.waiting.CompareTo(x.load.waiting);
                return byWaiting != 0 ? byWaiting : string.CompareOrdinal(x.station.name, y.station.name);
            });
            return ranked.ConvertAll(r => r.station);
        }
        /// <summary>Passengers waiting at towns that have a station, counting a shared town queue once.</summary>
        public static int PassengersWaiting(WorldState w, Balance b)
        {
            var towns = new HashSet<int>();
            int total = 0;
            foreach (var s in w.stations)
            {
                var load = Of(w, b, s);
                if (load.Passengers && towns.Add(s.producerId))
                    total += load.waiting;
            }
            return total;
        }
        static ProducerState Producer(WorldState w, int id)
        {
            foreach (var p in w.producers)
                if (p.id == id)
                    return p;
            return null;
        }
    }
}
