using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>
    /// A light rail line's length in simulation units, measured like a train's (RailPathfinder.Length): half a cell at each
    /// stop, 1000 per straight cell and 785 per curved one between them. A tram runs round a loop of twice that length:
    /// positions 0 to <see cref="Half"/> go out from the transfer stop to the venue on one track, the rest come back on
    /// the other. Everything is integer, so the simulation and a replayed save agree to the unit.
    /// </summary>
    public sealed class TramRoute
    {
        public readonly List<Cell> cells;
        /// <summary>Where each cell's stretch starts on the way out, and how long it is.</summary>
        public readonly int[] start, length;
        /// <summary>The direction a tram travels on the way out as it enters each cell (the first cell: as it leaves).</summary>
        public readonly int[] entry, exit;
        public readonly int Half;
        public int Loop => 2 * Half;
        public TramRoute(List<Cell> cells)
        {
            this.cells = cells;
            int n = cells.Count;
            start = new int[n];
            length = new int[n];
            entry = new int[n];
            exit = new int[n];
            for (int i = 0; i < n; i++)
            {
                exit[i] = i + 1 < n ? Directions.Between(cells[i], cells[i + 1]) : Directions.Between(cells[i - 1], cells[i]);
                entry[i] = i > 0 ? Directions.Between(cells[i - 1], cells[i]) : exit[i];
                start[i] = Half;
                length[i] = Segment(cells, i);
                Half += length[i];
            }
        }
        public static int Segment(List<Cell> cells, int i)
        {
            if (i == 0 || i == cells.Count - 1)
                return 500;
            return Directions.Between(cells[i - 1], cells[i]) == Directions.Between(cells[i], cells[i + 1]) ? 1000 : 785;
        }
        public static int HalfLength(List<Cell> cells)
        {
            int half = 0;
            for (int i = 0; i < cells.Count; i++)
                half += Segment(cells, i);
            return half;
        }
        public static int LoopLength(List<Cell> cells) => 2 * HalfLength(cells);
        /// <summary>
        /// The cell a loop position lies on, how far along the way-out stretch of that cell it is (0 to its length), and
        /// whether the tram is on its way back (then it runs the stretch the other way, on the other track).
        /// </summary>
        public int Locate(int position, out int along, out bool returning)
        {
            int loop = Loop;
            position = ((position % loop) + loop) % loop;
            returning = position > Half;
            int p = returning ? loop - position : position;
            for (int i = 0; i < cells.Count; i++)
                if (p < start[i] + length[i] || i == cells.Count - 1)
                {
                    along = p - start[i];
                    return i;
                }
            along = 0;
            return 0;
        }
        /// <summary>Distance from <paramref name="from"/> forward round the loop to <paramref name="to"/> (0 up to Loop - 1).</summary>
        public int Ahead(int from, int to) => ((to - from) % Loop + Loop) % Loop;
        /// <summary>Loop positions where a tram enters and leaves cell <paramref name="i"/> on the way out (returning false) or back.</summary>
        public void Span(int i, bool returning, out int enter, out int leave)
        {
            if (!returning)
            {
                enter = start[i];
                leave = start[i] + length[i];
            }
            else
            {
                enter = Loop - (start[i] + length[i]);
                leave = Loop - start[i];
            }
        }
    }
}
