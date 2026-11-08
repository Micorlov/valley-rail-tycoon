namespace ValleyRail
{
    /// <summary>Every AdMob unit ID the game uses, and the one switch between Google's sample units and ours.</summary>
    public static class AdConfig
    {
        /// <summary>Flip to false for a Play release; until then every build shows Google's sample ads.</summary>
        public const bool USE_TEST_ADS = false;

        // Google's public sample units.
        const string TestBanner = "ca-app-pub-3940256099942544/9214589741";
        const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
        const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";

        const string RealBanner = "ca-app-pub-2693008307172998/8660219994";
        const string RealInterstitial = "ca-app-pub-2693008307172998/8707275278";
        // Not used yet: the game has no reward hook (no lives, continues or hints). Kept so a later rewarded placement reads it from here.
        const string RealRewarded = "ca-app-pub-2693008307172998/8684270724";

        public static string BannerUnit => USE_TEST_ADS ? TestBanner : RealBanner;
        public static string InterstitialUnit => USE_TEST_ADS ? TestInterstitial : RealInterstitial;
        public static string RewardedUnit => USE_TEST_ADS ? TestRewarded : RealRewarded;
    }
}
