using System;
using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>
    /// Finds the cheapest track layout for one small window of the map that repairs a defect. Only the window's
    /// editable cells (the patch) change. A layout is accepted only when every train movement that crossed the patch
    /// before still does, at most <see cref="RailRepairPlanner.MaxDetour"/> longer, and trains can now get from one side
    /// of the defect to the other. Nothing outside the patch changes and guarded track is never in it, so no working
    /// route anywhere is lost. Trains never reverse outside stations, so movements through the patch are all that matter.
    /// </summary>
    sealed class RailPatchSolver
    {
        public const int MaxCells = 16;
        const int MaxPorts = MaxCells * 4, NoRoute = int.MaxValue, ChangePenalty = 150, RemovePenalty = 150, MaxScore = 2500;
        // Every legal piece: empty, the six two-port pieces, the four wyes and the diamond.
        static readonly int[] Shapes = { 0, 3, 5, 6, 9, 10, 12, 7, 11, 13, 14, 15 };
        readonly RepairGrid grid; readonly Balance balance;
        readonly int[] key = new int[MaxCells], old = new int[MaxCells], paid = new int[MaxCells];
        readonly int[] mask = new int[MaxCells], bestMask = new int[MaxCells], root = new int[MaxCells];
        // Per cell side (cell * 4 + direction): the neighbouring patch cell, and the boundary port it opens onto.
        readonly int[] next = new int[MaxPorts], port = new int[MaxPorts];
        readonly int[] portCell = new int[MaxPorts], portDir = new int[MaxPorts];
        readonly int[][] shapes = new int[MaxCells][];
        readonly int[,] before = new int[MaxPorts, MaxPorts];
        readonly int[] reach = new int[MaxPorts], dist = new int[MaxPorts];
        readonly int[] stack = new int[MaxPorts];
        int count, ports, maxChanged, best, budget;
        ulong sideA, sideB, routed;
        RailDefect defect;
        Func<int[], int[], int, bool> allowed;
        public RailPatchSolver(RepairGrid grid, Balance balance)
        {
            this.grid = grid;
            this.balance = balance;
        }
        public int Nodes { get; private set; }
        public bool Exhausted => Nodes > budget;
        public int Count => count;
        public int Key(int i) => key[i];
        public int Best(int i) => bestMask[i];
        /// <summary>Sets up the window's patch and the two sides of the defect; false when this window cannot repair it.</summary>
        public bool Prepare(int ox, int oz, int width, int height, RailDefect d)
        {
            defect = d;
            count = 0;
            for (int z = oz; z < oz + height; z++)
                for (int x = ox; x < ox + width; x++)
                    if (x >= 0 && z >= 0 && x < RepairGrid.Size && z < RepairGrid.Size && grid.Editable(z * RepairGrid.Size + x) && count < MaxCells)
                    {
                        int k = z * RepairGrid.Size + x;
                        key[count] = k;
                        old[count] = grid.mask[k];
                        paid[count] = grid.paid[k];
                        count++;
                    }
            if (count == 0)
                return false;
            ports = 0;
            for (int i = 0; i < count; i++)
                for (int d4 = 0; d4 < 4; d4++)
                {
                    int n = RepairGrid.Neighbor(key[i], d4), j = Index(n);
                    next[i * 4 + d4] = j;
                    port[i * 4 + d4] = -1;
                    if (j < 0 && n >= 0 && grid.Has(n, Directions.Opp(d4)))
                    {
                        port[i * 4 + d4] = ports;
                        portCell[ports] = i;
                        portDir[ports++] = d4;
                    }
                }
            for (int i = 0; i < count; i++)
                shapes[i] = Candidates(i);
            if (!Sides())
                return false;
            routed = 0;
            for (int p = 0; p < ports; p++)
            {
                Transit(old, p);
                for (int q = 0; q < ports; q++)
                {
                    before[p, q] = reach[q];
                    if (reach[q] != NoRoute)
                        routed |= 1UL << p;
                }
            }
            return true;
        }
        /// <summary>Searches for the cheapest acceptable layout within the node budget; true when one was found.</summary>
        public bool Solve(int nodeBudget, Func<int[], int[], int, bool> check)
        {
            allowed = check;
            budget = nodeBudget;
            Nodes = 0;
            best = MaxScore;
            maxChanged = count <= 9 ? 9 : 10;
            bool found = false;
            Search(0, 0, 0, ref found);
            return found;
        }
        int Index(int k)
        {
            for (int i = 0; i < count; i++)
                if (key[i] == k)
                    return i;
            return -1;
        }
        // Shapes this cell may take, cheapest first. Toward cells outside the patch a piece keeps every connection it
        // has, may connect to track that points in, and never grows a new port into nothing; a new piece must meet
        // every line end that points at it, so no new loose joins appear.
        int[] Candidates(int i)
        {
            int forced = 0, open = 15, must = 0;
            for (int d = 0; d < 4; d++)
            {
                if (next[i * 4 + d] >= 0)
                    continue;
                bool had = (old[i] >> d & 1) != 0, pointsIn = port[i * 4 + d] >= 0;
                if (pointsIn && had)
                    forced |= 1 << d;
                else if (pointsIn)
                    must |= old[i] == 0 ? 1 << d : 0;
                else if (!had)
                    open &= ~(1 << d);
            }
            var list = new List<int>();
            foreach (int s in Shapes)
                if ((s & forced) == forced && (s & ~open) == 0 && (old[i] != 0 || s == 0 || (s & must) == must))
                    list.Add(s);
            list.Sort((a, b) => Score(i, a) != Score(i, b) ? Score(i, a).CompareTo(Score(i, b)) : a.CompareTo(b));
            return list.ToArray();
        }
        int Score(int i, int s)
        {
            if (s == old[i])
                return 0;
            if (old[i] == 0)
                return balance.PieceCost(s);
            return s == 0 ? RemovePenalty : ChangePenalty + Math.Max(0, balance.PieceCost(s) - paid[i]);
        }
        void Search(int i, int score, int changed, ref bool found)
        {
            if (++Nodes > budget)
                return;
            if (i == count)
            {
                if (Accept())
                {
                    best = score;
                    Array.Copy(mask, bestMask, count);
                    found = true;
                }
                return;
            }
            foreach (int s in shapes[i])
            {
                int total = score + Score(i, s);
                if (total >= best)
                    break;
                int c = changed + (s != old[i] ? 1 : 0);
                if (c > maxChanged || !Consistent(i, s))
                    continue;
                mask[i] = s;
                Search(i + 1, total, c, ref found);
                if (Nodes > budget)
                    return;
            }
        }
        // Shared edges with earlier cells: both ports or neither, except a line end that already pointed at its neighbour
        // may keep doing so while that neighbour stays empty (or keeps its track).
        bool Consistent(int i, int s)
        {
            for (int d = 0; d < 4; d++)
            {
                int j = next[i * 4 + d];
                if (j < 0 || j > i)
                    continue;
                int o = Directions.Opp(d), a = s >> d & 1, b = mask[j] >> o & 1;
                if (a == b)
                    continue;
                bool kept = a == 1
                    ? (old[i] >> d & 1) == 1 && (old[j] >> o & 1) == 0 && (old[j] == 0) == (mask[j] == 0)
                    : (old[j] >> o & 1) == 1 && (old[i] >> d & 1) == 0 && (old[i] == 0) == (s == 0);
                if (!kept)
                    return false;
            }
            return true;
        }
        bool Accept()
        {
            if (!Resolved() || !Joined())
                return false;
            bool gain = false;
            for (int p = 0; p < ports && !gain; p++)
            {
                if ((sideA >> p & 1) == 0)
                    continue;
                Transit(mask, p);
                for (int q = 0; q < ports; q++)
                    if (q != p && (sideB >> q & 1) != 0 && reach[q] != NoRoute && before[p, q] == NoRoute)
                        gain = true;
            }
            if (!gain)
                return false;
            for (int p = 0; p < ports; p++)
            {
                if ((routed >> p & 1) == 0)
                    continue;
                Transit(mask, p);
                for (int q = 0; q < ports; q++)
                    if (before[p, q] != NoRoute && (reach[q] == NoRoute || reach[q] - before[p, q] > RailRepairPlanner.MaxDetour))
                        return false;
            }
            return allowed(key, mask, count);
        }
        // The defect's own dangling ports must now connect or be gone.
        bool Resolved()
        {
            for (int d = 0; d < 4; d++)
            {
                if ((defect.loose >> d & 1) == 0 || (MaskAt(defect.cell) >> d & 1) == 0)
                    continue;
                int n = RepairGrid.Neighbor(defect.cell, d);
                if (n < 0 || (MaskAt(n) >> Directions.Opp(d) & 1) == 0)
                    return false;
            }
            return true;
        }
        int MaskAt(int k)
        {
            int i = Index(k);
            return i >= 0 ? mask[i] : grid.mask[k];
        }
        // Cheap necessary check before routing: some side-A port and side-B port lie on one connected piece of the patch.
        bool Joined()
        {
            for (int i = 0; i < count; i++)
                root[i] = i;
            for (int i = 0; i < count; i++)
                for (int d = 0; d < 2; d++)
                {
                    int j = next[i * 4 + d];
                    if (j >= 0 && (mask[i] >> d & 1) != 0 && (mask[j] >> Directions.Opp(d) & 1) != 0)
                        root[Find(i)] = Find(j);
                }
            for (int p = 0; p < ports; p++)
                if ((sideA >> p & 1) != 0 && (mask[portCell[p]] >> portDir[p] & 1) != 0)
                    for (int q = 0; q < ports; q++)
                        if (q != p && (sideB >> q & 1) != 0 && (mask[portCell[q]] >> portDir[q] & 1) != 0 && Find(portCell[p]) == Find(portCell[q]))
                            return true;
            return false;
        }
        int Find(int i)
        {
            while (root[i] != i)
                i = root[i] = root[root[i]];
            return i;
        }
        // Shortest distance from entering the patch through port `from` to leaving through each port, over masks m.
        void Transit(int[] m, int from)
        {
            for (int q = 0; q < ports; q++)
                reach[q] = NoRoute;
            int start = portCell[from];
            if ((m[start] >> portDir[from] & 1) == 0)
                return;
            int states = count * 4;
            for (int s = 0; s < states; s++)
                dist[s] = NoRoute;
            dist[start * 4 + portDir[from]] = 0;
            ulong done = 0;
            while (true)
            {
                int bestState = -1, bestDist = NoRoute;
                for (int s = 0; s < states; s++)
                    if ((done >> s & 1) == 0 && dist[s] < bestDist)
                    {
                        bestDist = dist[s];
                        bestState = s;
                    }
                if (bestState < 0)
                    return;
                done |= 1UL << bestState;
                int i = bestState >> 2, entry = bestState & 3;
                for (int x = 0; x < 4; x++)
                {
                    if (!Directions.Allows(m[i], entry, x))
                        continue;
                    int nd = bestDist + RailPathfinder.Length(entry, x), q = port[i * 4 + x], j = next[i * 4 + x];
                    if (q >= 0)
                        reach[q] = Math.Min(reach[q], nd);
                    else if (j >= 0 && (m[j] >> Directions.Opp(x) & 1) != 0 && nd < dist[j * 4 + Directions.Opp(x)])
                        dist[j * 4 + Directions.Opp(x)] = nd;
                }
            }
        }
        // The boundary ports each side of the defect reaches over today's track inside the patch.
        bool Sides()
        {
            if (defect.kind == RepairKind.Gap || defect.kind == RepairKind.LooseJoin)
            {
                // The dangling port itself must be able to change: A or the cell it points into is in the patch.
                if (Index(defect.cell) < 0 && Index(RepairGrid.Neighbor(defect.cell, defect.port)) < 0)
                    return false;
                if (defect.kind == RepairKind.LooseJoin && Index(defect.other) < 0)
                    return false;
                sideA = PieceSide(defect.cell);
                sideB = PieceSide(defect.other);
            }
            else
            {
                int m = Index(defect.cell);
                if (m < 0)
                    return false;
                sideA = ArmSide(m, defect.armsA);
                sideB = ArmSide(m, defect.armsB);
            }
            return sideA != 0 && sideB != 0;
        }
        // Everywhere a train can leave the patch from this piece: walked from each of its ports when it is in the patch,
        // else the boundary ports it points into.
        ulong PieceSide(int k)
        {
            ulong side = 0;
            int i = Index(k);
            for (int p = 0; p < ports; p++)
                if (i < 0 && RepairGrid.Neighbor(key[portCell[p]], portDir[p]) == k)
                    side |= 1UL << p;
            if (i >= 0)
                for (int d = 0; d < 4; d++)
                    if ((old[i] >> d & 1) != 0)
                        side |= Walk(i, d);
            return side;
        }
        ulong ArmSide(int m, int arms)
        {
            ulong side = 0;
            for (int d = 0; d < 4; d++)
            {
                if ((arms >> d & 1) == 0)
                    continue;
                int q = port[m * 4 + d], j = next[m * 4 + d];
                if (q >= 0)
                    side |= 1UL << q;
                else if (j >= 0 && (old[j] >> Directions.Opp(d) & 1) != 0)
                    side |= Walk(j, Directions.Opp(d));
            }
            return side;
        }
        // Boundary ports reached over today's track by a train entering patch cell i through side `entry`.
        ulong Walk(int i, int entry)
        {
            ulong seen = 1UL << (i * 4 + entry), found = 0;
            int top = 0;
            stack[top++] = i * 4 + entry;
            while (top > 0)
            {
                int s = stack[--top], c = s >> 2, e = s & 3;
                for (int x = 0; x < 4; x++)
                {
                    if (!Directions.Allows(old[c], e, x))
                        continue;
                    int q = port[c * 4 + x], j = next[c * 4 + x];
                    if (q >= 0)
                        found |= 1UL << q;
                    else if (j >= 0 && (old[j] >> Directions.Opp(x) & 1) != 0 && (seen >> (j * 4 + Directions.Opp(x)) & 1) == 0)
                    {
                        seen |= 1UL << (j * 4 + Directions.Opp(x));
                        stack[top++] = j * 4 + Directions.Opp(x);
                    }
                }
            }
            return found;
        }
    }
}
