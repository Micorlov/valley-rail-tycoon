namespace ValleyRail.Core
{
    /// <summary>Upgrades older snapshots in memory after decoding. The file itself is only rewritten by the next save.</summary>
    public static class SaveMigration
    {
        public const int CurrentMapVersion = 5;
        public static void Upgrade(WorldState w, Balance b)
        {
            if (w == null || w.mapVersion != 1)
                return;
            // Map version 1 had no cities: give every town its day-0 layout inside the footprint that already blocked tracks.
            if (w.cities.Count == 0)
                foreach (var p in w.producers)
                    if (p.kind == ProducerKind.Town)
                        CitySimulation.Found(w, p, b);
            // Keep the original industrial layout: new plants could overlap player construction.
            w.mapVersion = 2;
        }
    }
}
