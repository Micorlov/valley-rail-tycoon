using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>Track problems the AI repair looks for, in the order it tries them.</summary>
    public enum RepairKind { Gap, LooseJoin, DeadStem, CutLine }
    /// <summary>One track problem the AI found: where it is, and whether the plan repairs it (or why not).</summary>
    public sealed class RepairItem
    {
        public RepairKind kind; public Cell cell; public bool repaired; public string reason;
    }
    /// <summary>A defect on the working copy. Cells are map keys; arms and ports are direction bits.</summary>
    struct RailDefect
    {
        public RepairKind kind;
        /// <summary>The dangling piece A (gap, loose join) or the junction M.</summary>
        public int cell;
        /// <summary>A's dangling direction; -1 for junctions.</summary>
        public int port;
        /// <summary>The piece A should meet: a facing line end or a line's side; -1 for junctions.</summary>
        public int other;
        /// <summary>Junction arms leading to each of the two sides the repair must join.</summary>
        public int armsA, armsB;
        /// <summary>Ports of <see cref="cell"/> that must stop dangling.</summary>
        public int loose;
        public long Key => (((long)kind * RepairGrid.Cells + cell) * (RepairGrid.Cells + 1) + other + 1) * 5 + port + 1;
    }
    /// <summary>
    /// The AI track repair's working copy of the rail network: masks and prices by cell key, which cells a repair may
    /// change, and the network-wide checks that find defects. Nothing here touches the live world.
    /// </summary>
    sealed class RepairGrid
    {
        public const int Size = MapDefinition.Size, Cells = Size * Size;
        public readonly int[] mask = new int[Cells], paid = new int[Cells];
        /// <summary>Pieces a repair must keep as they are: station platforms, train routes and bridges.</summary>
        public readonly bool[] guarded = new bool[Cells];
        /// <summary>Station index + 1 on platform cells, else 0.</summary>
        public readonly int[] station = new int[Cells];
        readonly sbyte[] ground = new sbyte[Cells];
        readonly Func<Cell, bool> buildable;
        readonly int[] component = new int[Cells], queue = new int[Cells * 4];
        readonly int[] seenA = new int[Cells * 4], seenB = new int[Cells * 4], seenR = new int[Cells * 4];
        readonly List<int> sideA = new List<int>(), stationsA = new List<int>(), stationsB = new List<int>();
        readonly HashSet<int> trainParts = new HashSet<int>();
        readonly HashSet<long> pairs = new HashSet<long>();
        int stamp;
        /// <param name="buildable">True where repairs may lay or change track: open, dry ground off roads and bridges.</param>
        public RepairGrid(Func<Cell, bool> buildable)
        {
            this.buildable = buildable;
        }
        public void Clear()
        {
            Array.Clear(mask, 0, Cells);
            Array.Clear(paid, 0, Cells);
            Array.Clear(guarded, 0, Cells);
            Array.Clear(station, 0, Cells);
            Array.Clear(ground, 0, Cells);
        }
        public static int Neighbor(int key, int d)
        {
            int x = key % Size + Directions.Dx[d], z = key / Size + Directions.Dz[d];
            return x < 0 || z < 0 || x >= Size || z >= Size ? -1 : z * Size + x;
        }
        public bool Has(int key, int d) => (mask[key] >> d & 1) != 0;
        /// <summary>True when the piece has port d and the neighbour points back.</summary>
        public bool Live(int key, int d)
        {
            int n = Neighbor(key, d);
            return Has(key, d) && n >= 0 && Has(n, Directions.Opp(d));
        }
        /// <summary>True when a repair may change this cell: buildable ground holding no guarded piece.</summary>
        public bool Editable(int key)
        {
            if (key < 0 || guarded[key])
                return false;
            if (ground[key] == 0)
                ground[key] = (sbyte)(buildable(Cell.FromKey(key)) ? 1 : 2);
            return ground[key] == 1;
        }
        /// <summary>Every defect on the working copy, in repair order: kind, then cell, then port.</summary>
        public List<RailDefect> Scan()
        {
            var found = new List<RailDefect>();
            pairs.Clear();
            for (int k = 0; k < Cells; k++)
            {
                int m = mask[k];
                if (m == 0)
                    continue;
                for (int d = 0; d < 4; d++)
                {
                    int n = Neighbor(k, d);
                    if (!Has(k, d) || n < 0 || Has(n, Directions.Opp(d)))
                        continue;
                    if (mask[n] != 0)
                        found.Add(new RailDefect { kind = RepairKind.LooseJoin, cell = k, port = d, other = n, loose = 1 << d });
                    else
                        FindGaps(k, d, n, found);
                }
                // A four-way crossing lets trains turn onto either line, so a line ended on one cuts nothing.
                if (Directions.Count(m) == 3)
                    Junction(k, m, found);
            }
            found.Sort((a, b) => a.kind != b.kind ? a.kind.CompareTo(b.kind) : a.cell != b.cell ? a.cell.CompareTo(b.cell) : a.port != b.port ? a.port.CompareTo(b.port) : a.other.CompareTo(b.other));
            return found;
        }
        // A's line end points into empty cell e. Pair it with a piece straight ahead of e, or with another line end
        // (not parallel: double track and platform ends stay apart) aiming within two cells of e.
        void FindGaps(int a, int da, int e, List<RailDefect> found)
        {
            int ahead = Neighbor(e, da);
            if (ahead >= 0 && mask[ahead] != 0 && !Has(ahead, Directions.Opp(da)))
                AddGap(a, da, ahead, found);
            int ex = e % Size, ez = e / Size;
            for (int dz = -2; dz <= 2; dz++)
                for (int dx = Math.Abs(dz) - 2; dx <= 2 - Math.Abs(dz); dx++)
                {
                    int x = ex + dx, z = ez + dz;
                    if (x < 0 || z < 0 || x >= Size || z >= Size || mask[z * Size + x] != 0)
                        continue;
                    int f = z * Size + x;
                    for (int q = 0; q < 4; q++)
                    {
                        int b = Neighbor(f, q);
                        if (b >= 0 && b != a && Has(b, Directions.Opp(q)) && Directions.Opp(q) != da)
                            AddGap(a, da, b, found);
                    }
                }
        }
        void AddGap(int a, int da, int b, List<RailDefect> found)
        {
            if (pairs.Add((long)Math.Min(a, b) * Cells + Math.Max(a, b)))
                found.Add(new RailDefect { kind = RepairKind.Gap, cell = a, port = da, other = b, loose = 1 << da });
        }
        // A wye never passes straight across its two in-line arms. It is a defect when those arms lead to track that
        // cannot otherwise reach each other: a dead stem (the stub leads nowhere) or, between stations, a cut line.
        void Junction(int k, int m, List<RailDefect> found)
        {
            int missing = 0;
            while ((m >> missing & 1) != 0)
                missing++;
            int stem = Directions.Opp(missing), a = (missing + 1) % 4, b = (missing + 3) % 4;
            if (!Live(k, a) || !Live(k, b))
                return;
            bool deadStem = !Live(k, stem);
            if (Cut(k, 1 << a, 1 << b, !deadStem))
                found.Add(new RailDefect { kind = deadStem ? RepairKind.DeadStem : RepairKind.CutLine, cell = k, port = -1, other = -1, armsA = 1 << a, armsB = 1 << b, loose = deadStem ? 1 << stem : 0 });
        }
        /// <summary>
        /// True when trains cannot get from the track beyond <paramref name="armsA"/> to the track beyond
        /// <paramref name="armsB"/> anywhere on the network without passing junction <paramref name="m"/>; with
        /// <paramref name="stations"/>, only when the two sides lead to stations and not to the very same ones
        /// (joining them straight across would open no new trip).
        /// </summary>
        public bool Cut(int m, int armsA, int armsB, bool stations)
        {
            int a = ++stamp;
            sideA.Clear();
            stationsA.Clear();
            stationsB.Clear();
            Closure(m, armsA, seenA, a, sideA, stationsA);
            Closure(m, armsB, seenB, a, null, stationsB);
            if (stations && (stationsA.Count == 0 || stationsB.Count == 0 || Same(stationsA, stationsB)))
                return false;
            // Reverse every crossing on side A: trains heading back toward M. The line is cut unless one reaches side B.
            int head = 0, tail = 0;
            foreach (int s in sideA)
            {
                int n = Neighbor(s >> 2, s & 3);
                if (n < 0 || !Has(n, Directions.Opp(s & 3)))
                    continue;
                int r = n * 4 + Directions.Opp(s & 3);
                if (seenR[r] != a)
                {
                    seenR[r] = a;
                    queue[tail++] = r;
                }
            }
            while (head < tail)
            {
                int s = queue[head++];
                if (seenB[s] == a)
                    return false;
                Expand(m, s, seenR, a, ref tail);
            }
            return true;
        }
        static bool Same(List<int> a, List<int> b)
        {
            if (a.Count != b.Count)
                return false;
            foreach (int s in a)
                if (!b.Contains(s))
                    return false;
            return true;
        }
        // Marks every crossing (cell, exit) reachable from leaving m through the given arms, never re-entering m,
        // and lists the stations whose platforms it runs onto.
        void Closure(int m, int arms, int[] seen, int epoch, List<int> visited, List<int> stations)
        {
            int head = 0, tail = 0;
            for (int d = 0; d < 4; d++)
                if ((arms >> d & 1) != 0 && seen[m * 4 + d] != epoch)
                {
                    seen[m * 4 + d] = epoch;
                    queue[tail++] = m * 4 + d;
                }
            while (head < tail)
            {
                int s = queue[head++];
                visited?.Add(s);
                int at = Expand(m, s, seen, epoch, ref tail);
                if (at != 0 && !stations.Contains(at))
                    stations.Add(at);
            }
        }
        // Queues the crossings a train can take after crossing s; returns the station (index + 1) whose platform it enters.
        int Expand(int m, int s, int[] seen, int epoch, ref int tail)
        {
            int c = s >> 2, d = s & 3, n = Neighbor(c, d), entry = Directions.Opp(d);
            if (n < 0 || n == m || !Has(n, entry))
                return 0;
            for (int x = 0; x < 4; x++)
                if (Directions.Allows(mask[n], entry, x) && seen[n * 4 + x] != epoch)
                {
                    seen[n * 4 + x] = epoch;
                    queue[tail++] = n * 4 + x;
                }
            return station[n];
        }
        /// <summary>Numbers the connected railways of the working copy and notes which hold a train.</summary>
        public void Components(List<int> trainCells)
        {
            Array.Clear(component, 0, Cells);
            int next = 0;
            for (int k = 0; k < Cells; k++)
            {
                if (mask[k] == 0 || component[k] != 0)
                    continue;
                int id = ++next, head = 0, tail = 0;
                component[k] = id;
                queue[tail++] = k;
                while (head < tail)
                {
                    int c = queue[head++];
                    for (int d = 0; d < 4; d++)
                    {
                        int n = Neighbor(c, d);
                        if (Live(c, d) && component[n] == 0)
                        {
                            component[n] = id;
                            queue[tail++] = n;
                        }
                    }
                }
            }
            trainParts.Clear();
            foreach (int k in trainCells)
                trainParts.Add(component[k]);
        }
        /// <summary>True when the two pieces lie on different railways that each already run a train.</summary>
        public bool JoinsTwoTrains(int a, int b) =>
            component[a] != component[b] && trainParts.Contains(component[a]) && trainParts.Contains(component[b]);
    }
}
