using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>Freight a city holds for a later train (see <see cref="CargoTransfer"/>): one stock per cargo and source.</summary>
    [Serializable]
    public class TransferStock
    {
        public Cargo cargo; public int origin, units;
        /// <summary>Money already paid for these units when they were dropped off. The final delivery pays the rest of the fare.</summary>
        public int credit;
    }
    /// <summary>
    /// City transfer stations. A town station takes freight the town itself does not use (oil, coal, wood…) and holds it, shared
    /// by every station of that town, until a second train carries it on to an industry that uses it. The first train earns
    /// <see cref="Share"/>% of its own leg's fare on drop-off; the final delivery pays the fare from the original source minus that
    /// credit. Town to town is never a route, so freight cannot bounce between cities for pay.
    /// </summary>
    public static class CargoTransfer
    {
        public const int Share = 50;
        public static bool Holds(ProducerKind kind, Cargo cargo) => kind == ProducerKind.Town && cargo != Cargo.Passengers && !MapDefinition.Accepts(kind, cargo);
        /// <summary>A leg that ends the cargo's journey: from its source, or a city holding it, to a stop that uses it.</summary>
        /// <remarks>Tourists travel between towns and ski resorts, never from one resort to another.</remarks>
        public static bool Delivers(ProducerKind a, ProducerKind b, Cargo cargo) =>
            (Final(a, b, cargo) || Final(b, a, cargo)) && !(a == ProducerKind.SkiResort && b == ProducerKind.SkiResort);
        /// <summary>A first leg that leaves the cargo at a city for a later train.</summary>
        public static bool Feeds(ProducerKind a, ProducerKind b, Cargo cargo) =>
            (MapDefinition.Produces(a, cargo) && Holds(b, cargo)) || (MapDefinition.Produces(b, cargo) && Holds(a, cargo));
        static bool Final(ProducerKind from, ProducerKind to, Cargo cargo) =>
            (MapDefinition.Produces(from, cargo) || Holds(from, cargo)) && MapDefinition.Accepts(to, cargo);
        public static int Held(ProducerState p, Cargo cargo)
        {
            int units = 0;
            foreach (var s in p.transfers)
                if (s.cargo == cargo)
                    units += s.units;
            return units;
        }
        /// <summary>True once the city has taken this cargo, even when a train has just emptied its stock.</summary>
        public static bool Handles(ProducerState p, Cargo cargo) => p.transfers.Exists(s => s.cargo == cargo);
        /// <summary>Adds units to the stock from <paramref name="origin"/>, up to <paramref name="storage"/> of this cargo in the city. Returns the units kept.</summary>
        public static int Store(ProducerState p, Cargo cargo, int origin, int units, int storage, out TransferStock stock)
        {
            stock = p.transfers.Find(s => s.cargo == cargo && s.origin == origin);
            if (stock == null)
            {
                stock = new TransferStock { cargo = cargo, origin = origin };
                p.transfers.Add(stock);
            }
            int kept = Math.Max(0, Math.Min(units, storage - Held(p, cargo)));
            stock.units += kept;
            return kept;
        }
        /// <summary>Takes up to <paramref name="capacity"/> units from the fullest stock of <paramref name="cargo"/>, with their share of its credit.</summary>
        public static int Take(ProducerState p, Cargo cargo, int capacity, out int origin, out int credit)
        {
            TransferStock fullest = null;
            foreach (var s in p.transfers)
                if (s.cargo == cargo && s.units > 0 && (fullest == null || s.units > fullest.units))
                    fullest = s;
            origin = credit = 0;
            if (fullest == null || capacity <= 0)
                return 0;
            int units = Math.Min(capacity, fullest.units);
            credit = units == fullest.units ? fullest.credit : (int)((long)fullest.credit * units / fullest.units);
            fullest.units -= units;
            fullest.credit -= credit;
            origin = fullest.origin;
            return units;
        }
        /// <summary>"120 oil, 30 coal", or null when the city holds nothing.</summary>
        public static string Describe(ProducerState p)
        {
            var parts = new List<string>();
            foreach (Cargo cargo in Enum.GetValues(typeof(Cargo)))
            {
                int units = Held(p, cargo);
                if (units > 0)
                    parts.Add($"{units:N0} {IndustryCatalog.CargoName(cargo).ToLowerInvariant()}");
            }
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }
        /// <summary>Save check: only towns hold stock, of cargo they can hold, from a real source, within storage.</summary>
        public static bool Valid(ProducerState p, List<ProducerState> producers, int storage)
        {
            if (p.transfers == null)
                return false;
            var seen = new HashSet<long>();
            foreach (var s in p.transfers)
            {
                var source = s == null ? null : producers.Find(o => o.id == s.origin);
                if (source == null || !Enum.IsDefined(typeof(Cargo), s.cargo) || !Holds(p.kind, s.cargo) || !MapDefinition.Produces(source.kind, s.cargo) ||
                    s.units < 0 || s.credit < 0 || !seen.Add(((long)s.cargo << 32) | (uint)s.origin) || Held(p, s.cargo) > storage)
                    return false;
            }
            return true;
        }
    }
}
