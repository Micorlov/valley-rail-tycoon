using System;

namespace ValleyRail.Core
{
    /// <summary>
    /// The barrier of one level crossing. Its lamps start flashing as soon as a train comes near; after a short warning the
    /// booms come down, and once the train has passed they go back up. Road traffic may drive on only while the booms are
    /// fully up. Cosmetic: the railway never waits for a barrier, so the timings are in game seconds and never saved.
    /// </summary>
    public readonly struct CrossingBarrier
    {
        /// <summary>Game seconds the lamps flash before the booms start down.</summary>
        public const float Warning = .3f;
        /// <summary>Game seconds a boom takes to come down, and to go back up. The fastest train is 1.8 s out when a crossing closes.</summary>
        public const float LowerTime = .9f, RaiseTime = 1f;
        /// <summary>0 while the booms stand up, 1 once they lie across the road.</summary>
        public readonly float lowered;
        /// <summary>Game seconds the lamps have flashed since the train came near, up to <see cref="Warning"/>.</summary>
        public readonly float warned;
        public readonly bool trainNear;

        CrossingBarrier(bool trainNear, float lowered, float warned)
        {
            this.trainNear = trainNear;
            this.lowered = lowered;
            this.warned = warned;
        }
        /// <summary>A barrier that has already settled: down under or ahead of a train, otherwise up.</summary>
        public static CrossingBarrier Settled(bool trainNear) => new CrossingBarrier(trainNear, trainNear ? 1 : 0, trainNear ? Warning : 0);
        /// <summary>Cars may drive onto the crossing: no train near and the booms fully up.</summary>
        public bool Open => !trainNear && lowered <= 0;
        /// <summary>The lamps flash from the moment a train comes near until the booms are back up.</summary>
        public bool Flashing => !Open;

        /// <summary>The barrier <paramref name="seconds"/> later. A boom still on its way up turns straight back down.</summary>
        public CrossingBarrier Advance(bool near, float seconds)
        {
            seconds = Math.Max(0, seconds);
            if (!near)
                return new CrossingBarrier(false, Math.Max(0, lowered - seconds / RaiseTime), 0);
            float warning = lowered > 0 ? 0 : Math.Min(seconds, Warning - warned);
            return new CrossingBarrier(true, Math.Min(1, lowered + (seconds - warning) / LowerTime), Math.Min(Warning, warned + warning));
        }
    }
}
