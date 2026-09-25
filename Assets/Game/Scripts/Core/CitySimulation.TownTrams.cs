namespace ValleyRail.Core
{
    /// <summary>
    /// Town trams: once a game minute, the first town of <see cref="CityBalance.tramLevel"/> or bigger that has no
    /// light-rail line yet opens one, as soon as its streets can hold a <see cref="TownTramLine"/>. The line itself is
    /// planned again by the view from the streets; the simulation keeps only the opening tick, which adds growth
    /// points (Rate) and passengers (Derive) and colours the line.
    /// </summary>
    public sealed partial class CitySimulation
    {
        const int TramPeriod = 1200, TramPhase = 900;

        void DevelopTrams()
        {
            if (!cb.tramsEnabled)
                return;
            RoadLanes lanes = null;
            foreach (var city in w.cities)
            {
                if (city.tram != 0 || city.level < cb.tramLevel)
                    continue;
                lanes = lanes ?? RoadLanes.Build(w, net);
                if (TownTramLine.Plan(w, city, lanes, net, TownTramLine.PlayerLines(w)) == null)
                    continue;
                city.tram = w.tick;
                Recount(city, Producer(city.producerId), b);
                w.cityRevision++;
                Notify(Notification.TownTramOpened, city.id, TownTramLine.ColourIndex(w, city));
                return; // one line a minute, so each opening gets its own notice
            }
        }
    }
}
