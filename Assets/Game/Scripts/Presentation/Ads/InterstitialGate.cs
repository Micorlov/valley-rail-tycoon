namespace ValleyRail
{
    /// <summary>
    /// When a finished play session may be followed by an interstitial: never after the very first one of the first-ever launch,
    /// and at most once per <see cref="CooldownSeconds"/>. Pure, so the edit-mode tests drive it with their own clock.
    /// </summary>
    public sealed class InterstitialGate
    {
        public const float CooldownSeconds = 90;
        readonly bool firstLaunch;
        int gamesFinished;
        float lastShownAt = float.NegativeInfinity;

        /// <param name="launchedBefore">Read once at start-up, before this launch marks itself as seen.</param>
        public InterstitialGate(bool launchedBefore) { firstLaunch = !launchedBefore; }

        /// <summary>Call once per finished session; true when an ad may show now.</summary>
        public bool GameFinished(float now)
        {
            gamesFinished++;
            if (firstLaunch && gamesFinished == 1)
                return false;
            return now - lastShownAt >= CooldownSeconds;
        }

        public void Shown(float now) { lastShownAt = now; }
    }
}
