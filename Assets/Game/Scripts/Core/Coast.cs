namespace ValleyRail.Core
{
    /// <summary>
    /// The valley's east coast: a sand beach on the last map columns, with shallow water and waves past the map edge. A Village within
    /// <see cref="Reach"/> of the coast gets a beach road, an ordinary highway (an <see cref="IntercityRoadState"/> whose
    /// far end is <see cref="Resort"/>) built a cell at a time like the others and ending at a car park by the sand. Once
    /// the road is finished the car park goes up in <see cref="ParkSteps"/> stages (IntercityRoadState.park); the last
    /// stage opens the beach to visitors. Only the car park is reserved land: the sand stays open ground.
    /// </summary>
    public static class Coast
    {
        /// <summary>IntercityRoadState.b of a beach road. Producer ids are positive, so it never names a town.</summary>
        public const int Resort = -1;
        /// <summary>The first sand column; the beach runs from here to the map edge.</summary>
        public const int SandFrom = MapDefinition.Size - 3;
        /// <summary>A car park is two columns between the road's last cell (on EntranceX) and the sand.</summary>
        public const int ParkX = SandFrom - 2, EntranceX = ParkX - 1;
        /// <summary>Cells along the coast: ParkBehind before the entrance's row, the rest after it.</summary>
        public const int ParkLength = 4, ParkBehind = 1, ParkCells = 2 * ParkLength;
        public const int ParkSteps = 5;
        /// <summary>How far west of the car park a town's plaza may be for the town to build a beach road.</summary>
        public const int Reach = 48;
        /// <summary>How many rows either side of the plaza's row the car park may move to find free ground.</summary>
        public const int SiteSearch = 12;

        public static bool Beach(Cell c) => MapDefinition.InBounds(c) && c.x >= SandFrom;
        /// <summary>Car park cell <paramref name="i"/> (0 to ParkCells - 1) of the beach road ending on <paramref name="entrance"/>.</summary>
        public static Cell ParkCell(Cell entrance, int i) => new Cell(ParkX + i % 2, entrance.z - ParkBehind + i / 2);
        /// <summary>A cell a beach road may end on: the entrance column, with the whole car park inside the map.</summary>
        public static bool IsEntrance(Cell c) =>
            c.x == EntranceX && c.z - ParkBehind >= 0 && c.z - ParkBehind + ParkLength <= MapDefinition.Size;
        public static Cell Entrance(IntercityRoadState road) => road.path[road.path.Count - 1];
        public static bool Open(IntercityRoadState road) => road.ToBeach && road.park >= ParkSteps;
    }
}
