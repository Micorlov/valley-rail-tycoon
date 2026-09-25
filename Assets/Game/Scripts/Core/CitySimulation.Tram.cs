namespace ValleyRail.Core
{
    /// <summary>
    /// Light rail cells on the town grid (see <see cref="TramLineState"/>). They carry <see cref="Tram"/>, so buildings,
    /// squares, car parks, service areas, new towns and stations stay off them, while streets and highways may still grow
    /// onto them: the trams then run in the street. <see cref="TramMask"/> keeps each cell's tram ports so heavy track can
    /// only cross the line straight over at right angles (TramRules).
    /// </summary>
    public sealed partial class CitySimulation
    {
        public const byte Tram = 128;
        readonly byte[] tramMasks = new byte[MapDefinition.Size * MapDefinition.Size];
        /// <summary>Marks every light rail cell. Rebuild calls it after clearing the grid; it tolerates any saved data.</summary>
        void ReserveTrams()
        {
            System.Array.Clear(tramMasks, 0, tramMasks.Length);
            if (w.tramLines == null)
                return;
            foreach (var line in w.tramLines)
            {
                if (line?.cells == null)
                    continue;
                int n = line.cells.Count;
                for (int i = 0; i < n; i++)
                {
                    var c = line.cells[i];
                    if (!MapDefinition.InBounds(c))
                        continue;
                    grid[c.Key] |= Tram;
                    int mask = tramMasks[c.Key];
                    if (i > 0 && c.Distance(line.cells[i - 1]) == 1)
                        mask |= 1 << Directions.Between(c, line.cells[i - 1]);
                    if (i + 1 < n && c.Distance(line.cells[i + 1]) == 1)
                        mask |= 1 << Directions.Between(c, line.cells[i + 1]);
                    if (i == 0 || i == n - 1)
                        mask |= TramRules.Stop;
                    tramMasks[c.Key] = (byte)mask;
                }
            }
        }
        /// <summary>The light rail ports on a cell (bits 0-3) plus TramRules.Stop at a line's end; 0 off the lines.</summary>
        public int TramMask(Cell c) => MapDefinition.InBounds(c) ? tramMasks[c.Key] : 0;
        public bool HasTram(Cell c) => MapDefinition.InBounds(c) && (grid[c.Key] & Tram) != 0;
        /// <summary>The town grid bits on a cell (Building, Road, Plaza, Highway, Resort, Service, Tram); 0 off the map.</summary>
        public int Bits(Cell c) => MapDefinition.InBounds(c) ? grid[c.Key] : 0;
    }
}
