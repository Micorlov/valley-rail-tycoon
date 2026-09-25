using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>The town fires (<see cref="CityFires"/>) and what they need to know from the town drawing.</summary>
    public sealed partial class WorldView
    {
        CityFires fires;
        /// <summary>The town fires: at most one burning at a time, with its fire engine and crew.</summary>
        public CityFires Fires => fires;
        /// <summary>A finished building rather than a construction site: only these catch fire or send out an engine.</summary>
        internal bool Standing(BuildingState bs) => !UnderConstruction(bs);
    }
}
