using System;
using System.Collections.Generic;
using System.Text;
namespace ValleyRail.Core
{
    /// <summary>The AI track repair's proposal: what it would build for how much, and every problem it looked at.</summary>
    public sealed class TrackRepair
    {
        public bool valid; public string reason;
        /// <summary>The whole repair as one build; null when nothing needs changing.</summary>
        public BuildPlan build;
        public List<RepairItem> items = new List<RepairItem>();
        /// <summary>Cells whose track the repair removes or trims, for the preview's demolition pins.</summary>
        public List<Cell> doomed = new List<Cell>();
    }
    /// <summary>
    /// "AI FIX" in the track tool. Scans the whole rail network for small breaks: line ends that stop short of each
    /// other, a line ending against another line's side, junctions a dragged line cut. It
    /// rebuilds a few cells around each so trains get through, never touching guarded track and never losing or much
    /// lengthening a route that works today. Planning only reads the world; <see cref="Apply"/> builds a quoted plan.
    /// </summary>
    public sealed class RailRepairPlanner
    {
        /// <summary>How much longer a working route may get to make room for a junction (a straight cell is 1000).</summary>
        public const int MaxDetour = 1200;
        const int MaxFixes = 40, WindowBudget = 60000, DefectBudget = 200000, RunBudget = 2000000;
        const string Park = "on a train's route or platform: park the train and clear its route first";
        const string TwoTrains = "would join two trains' railways";
        const string NoRoom = "no room to build a fix there";
        const string OutOfTime = "not reached this time: tap AI FIX again";
        readonly WorldState w; readonly RailNetwork net; readonly BuildService build; readonly Balance balance;
        readonly RepairGrid grid; readonly RailPatchSolver solver;
        readonly List<int> trainCells = new List<int>();
        // Cell key → index of the fix that last changed it.
        readonly Dictionary<int, int> changedAt = new Dictionary<int, int>();
        long budgetLeft;
        public RailRepairPlanner(WorldState w, RailNetwork net, BuildService build, Balance balance, CitySimulation cities)
        {
            this.w = w;
            this.net = net;
            this.build = build;
            this.balance = balance;
            // Roads and highways keep their level crossings, which need straight track at right angles: stay off town ground.
            grid = new RepairGrid(c => !MapDefinition.Water(c) && MapDefinition.Bridge(c) == 0 && !cities.Occupied(c) && build.Placeable(c));
            solver = new RailPatchSolver(grid, balance);
        }
        public TrackRepair Plan()
        {
            Load();
            var fixes = new List<RepairItem>();
            var failed = new Dictionary<long, (int at, string why)>();
            budgetLeft = RunBudget;
            List<RailDefect> defects;
            while (true)
            {
                grid.Components(trainCells);
                defects = grid.Scan();
                if (fixes.Count >= MaxFixes)
                    break;
                bool progressed = false;
                foreach (var d in defects)
                {
                    if (failed.TryGetValue(d.Key, out var f) && !ChangedNear(d.cell, f.at))
                        continue;
                    string why = Blocker(d) ?? Solve(d, fixes.Count);
                    if (why == null)
                    {
                        fixes.Add(new RepairItem { kind = d.kind, cell = Cell.FromKey(Anchor(d)), repaired = true, reason = "fixed" });
                        progressed = true;
                        break;
                    }
                    failed[d.Key] = (fixes.Count, why);
                }
                if (!progressed)
                    break;
            }
            var repair = new TrackRepair();
            repair.items.AddRange(fixes);
            foreach (var d in defects)
                repair.items.Add(new RepairItem { kind = d.kind, cell = Cell.FromKey(Anchor(d)), reason = failed.TryGetValue(d.Key, out var f) ? f.why : OutOfTime });
            var keys = new List<int>(changedAt.Keys);
            keys.Sort();
            var changes = new List<TrackPieceState>();
            foreach (int k in keys)
            {
                int live = LiveMask(k), now = grid.mask[k];
                if (now == live)
                    continue;
                changes.Add(new TrackPieceState { cell = Cell.FromKey(k), mask = now });
                if ((live & ~now) != 0)
                    repair.doomed.Add(Cell.FromKey(k));
            }
            if (changes.Count > 0)
            {
                repair.build = build.ValidateRepair(changes);
                repair.valid = repair.build.valid;
            }
            repair.reason = Summary(repair);
            return repair;
        }
        /// <summary>Plans again and builds the repair, refusing when it no longer matches the quote the player saw.</summary>
        public Result Apply(TrackRepair quoted = null)
        {
            var fresh = Plan();
            if (fresh.build == null)
                return Result.Fail("The AI found nothing to fix.");
            if (!fresh.valid)
                return Result.Fail(fresh.build.unaffordable ? $"The AI fix costs ${fresh.build.cost:N0}: not enough money." : fresh.build.reason);
            if (quoted != null && !SameBuild(quoted.build, fresh.build))
                return Result.Fail("The railway changed since the AI looked. Tap AI FIX again.");
            var built = build.CommitValidated(fresh.build);
            if (!built.ok)
                return built;
            int n = fresh.items.FindAll(i => i.repaired).Count;
            return Result.Good($"AI fixed {n} track problem{(n == 1 ? "" : "s")} for ${fresh.build.cost:N0}.");
        }
        static bool SameBuild(BuildPlan a, BuildPlan b)
        {
            if (a == null || a.cost != b.cost || a.changes.Count != b.changes.Count)
                return false;
            for (int i = 0; i < a.changes.Count; i++)
                if (!a.changes[i].cell.Equals(b.changes[i].cell) || a.changes[i].mask != b.changes[i].mask)
                    return false;
            return true;
        }
        void Load()
        {
            grid.Clear();
            changedAt.Clear();
            trainCells.Clear();
            foreach (var t in w.tracks)
            {
                int k = t.cell.Key;
                grid.mask[k] = t.mask;
                grid.paid[k] = t.paid;
                grid.guarded[k] = t.bridge != 0 || build.Protected(t.id);
            }
            for (int i = 0; i < w.stations.Count; i++)
                foreach (var c in StationLayout.Cells(w.stations[i]))
                    if (MapDefinition.InBounds(c))
                        grid.station[c.Key] = i + 1;
            // Where each train stands, as BuildService.Finish counts it for the one-train-per-railway rule.
            foreach (var t in w.trains)
            {
                var station = w.stations.Find(s => s.id == t.stationId);
                int id = t.path.Count > 0 ? t.path[Math.Min(t.step, t.path.Count - 1)].trackId : station != null ? net.At(StationLayout.Center(station, t.platform))?.id ?? -1 : -1;
                if (net.ids.TryGetValue(id, out var piece))
                    trainCells.Add(piece.cell.Key);
            }
        }
        int LiveMask(int k) => net.At(Cell.FromKey(k))?.mask ?? 0;
        static int Anchor(RailDefect d) =>
            d.kind == RepairKind.Gap ? RepairGrid.Neighbor(d.cell, d.port) : d.kind == RepairKind.LooseJoin ? d.other : d.cell;
        string Blocker(RailDefect d)
        {
            if (d.kind == RepairKind.Gap || d.kind == RepairKind.LooseJoin)
            {
                if (grid.JoinsTwoTrains(d.cell, d.other))
                    return TwoTrains;
                if (d.kind == RepairKind.LooseJoin && !grid.Editable(d.other))
                    return grid.guarded[d.other] ? Park : NoRoom;
                return null;
            }
            return grid.Editable(d.cell) ? null : grid.guarded[d.cell] ? Park : NoRoom;
        }
        // Tries each window around the defect; on success the fix goes into the working copy. Returns why it failed.
        string Solve(RailDefect d, int index)
        {
            long defectLeft = DefectBudget;
            foreach (var (x, z, width, height) in Windows(d))
            {
                long left = Math.Min(WindowBudget, Math.Min(defectLeft, budgetLeft));
                if (left <= 0)
                    break;
                if (!solver.Prepare(x, z, width, height, d))
                    continue;
                bool ok = solver.Solve((int)left, Allowed);
                defectLeft -= solver.Nodes;
                budgetLeft -= solver.Nodes;
                if (!ok)
                    continue;
                for (int i = 0; i < solver.Count; i++)
                {
                    int k = solver.Key(i), m = solver.Best(i);
                    if (m == grid.mask[k])
                        continue;
                    grid.mask[k] = m;
                    grid.paid[k] = m == 0 ? 0 : balance.PieceCost(m);
                    changedAt[k] = index;
                }
                return null;
            }
            return budgetLeft <= 0 ? OutOfTime : NoRoom;
        }
        // Windows to search, smallest first: a gap's two line ends with a margin, then 3×3 and 4×4 around the defect.
        List<(int x, int z, int w, int h)> Windows(RailDefect d)
        {
            var list = new List<(int, int, int, int)>();
            if (d.kind == RepairKind.Gap)
            {
                int ax = d.cell % RepairGrid.Size, az = d.cell / RepairGrid.Size, bx = d.other % RepairGrid.Size, bz = d.other / RepairGrid.Size;
                int x0 = Math.Min(ax, bx), z0 = Math.Min(az, bz), x1 = Math.Max(ax, bx), z1 = Math.Max(az, bz);
                if ((x1 - x0 + 3) * (z1 - z0 + 3) <= RailPatchSolver.MaxCells)
                    list.Add((x0 - 1, z0 - 1, x1 - x0 + 3, z1 - z0 + 3));
                int wide = Math.Max(3, x1 - x0 + 1), high = Math.Max(3, z1 - z0 + 1);
                if (wide * high <= RailPatchSolver.MaxCells)
                    list.Add((x0 - (wide - (x1 - x0 + 1)) / 2, z0 - (high - (z1 - z0 + 1)) / 2, wide, high));
            }
            int anchor = Anchor(d), cx = anchor % RepairGrid.Size, cz = anchor / RepairGrid.Size;
            list.Add((cx - 1, cz - 1, 3, 3));
            for (int dz = 2; dz >= 1; dz--)
                for (int dx = 2; dx >= 1; dx--)
                    list.Add((cx - dx, cz - dz, 4, 4));
            var unique = new List<(int, int, int, int)>();
            foreach (var win in list)
                if (!unique.Contains(win))
                    unique.Add(win);
            return unique;
        }
        // The whole repair so far plus this layout must still pass the build rules (one train per railway, track cap).
        bool Allowed(int[] keys, int[] masks, int n)
        {
            var list = new List<TrackPieceState>();
            var inPatch = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                inPatch.Add(keys[i]);
                if (masks[i] != LiveMask(keys[i]))
                    list.Add(new TrackPieceState { cell = Cell.FromKey(keys[i]), mask = masks[i] });
            }
            foreach (int k in changedAt.Keys)
                if (!inPatch.Contains(k) && grid.mask[k] != LiveMask(k))
                    list.Add(new TrackPieceState { cell = Cell.FromKey(k), mask = grid.mask[k] });
            return build.ValidateRepair(list, false).valid;
        }
        // A failed defect is retried once a later fix changes track within reach of its windows (4×4, at most two cells
        // off its anchor, or a gap's box around line ends up to four cells from it).
        bool ChangedNear(int cell, int since)
        {
            int x = cell % RepairGrid.Size, z = cell / RepairGrid.Size;
            foreach (var pair in changedAt)
                if (pair.Value >= since && Math.Abs(pair.Key % RepairGrid.Size - x) <= 4 && Math.Abs(pair.Key / RepairGrid.Size - z) <= 4)
                    return true;
            return false;
        }
        string Summary(TrackRepair r)
        {
            if (r.items.Count == 0)
                return "Track looks good: no gaps or broken junctions found.";
            var text = new StringBuilder(r.items.Count == 1 ? "1 track problem found." : $"{r.items.Count} track problems found.");
            var done = r.items.FindAll(i => i.repaired);
            if (done.Count > 0)
                text.Append("\nFixing: ").Append(Counts(done));
            var reasons = new List<string>();
            foreach (var i in r.items)
                if (!i.repaired && !reasons.Contains(i.reason))
                    reasons.Add(i.reason);
            foreach (string reason in reasons)
                text.Append("\nLeft alone: ").Append(Counts(r.items.FindAll(i => !i.repaired && i.reason == reason))).Append(", ").Append(reason);
            if (r.build == null)
                return text.ToString();
            int added = 0, rewired = 0, removed = 0;
            foreach (var t in r.build.changes)
            {
                if (t.mask == 0)
                    removed++;
                else if (t.id == 0)
                    added++;
                else
                    rewired++;
            }
            text.Append($"\nNew {added} · rewired {rewired} · removed {removed}");
            if (r.build.unaffordable)
                text.Append($"\nCosts ${r.build.cost:N0}: you have ${w.money:N0}.");
            else if (!r.build.valid)
                text.Append('\n').Append(r.build.reason);
            else
                text.Append($" · ${r.build.cost:N0}");
            return text.ToString();
        }
        static string Counts(List<RepairItem> items)
        {
            var parts = new List<string>();
            foreach (RepairKind kind in Enum.GetValues(typeof(RepairKind)))
            {
                int n = items.FindAll(i => i.kind == kind).Count;
                if (n > 0)
                    parts.Add($"{n} {Noun(kind)}{(n == 1 ? "" : "s")}");
            }
            return string.Join(", ", parts);
        }
        static string Noun(RepairKind kind)
        {
            switch (kind)
            {
                case RepairKind.Gap: return "gap";
                case RepairKind.LooseJoin: return "loose end";
                case RepairKind.DeadStem: return "dead stub";
                default: return "cut junction";
            }
        }
    }
}
