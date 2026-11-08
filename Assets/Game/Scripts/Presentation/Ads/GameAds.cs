using UnityEngine;
#if UNITY_ANDROID && ADMOB_SDK
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
#endif

namespace ValleyRail
{
    /// <summary>
    /// Google AdMob on Android: consent first, then a bottom banner on the title and play screens (the UI's safe area is lifted
    /// above it, see <see cref="GameplayPresenter.BannerPixels"/>) and an interstitial when the player returns to the title.
    /// A failed load is retried later and a missing ad never holds the game up.
    /// Without the plugin (Tools/install_admob.sh, then Configure defines ADMOB_SDK) or off Android this does nothing.
    /// </summary>
    public sealed class GameAds : MonoBehaviour
    {
        const string LaunchedBeforeKey = "valleyrail.ads.launchedBefore";
        InterstitialGate gate;

        void Awake()
        {
            gate = new InterstitialGate(PlayerPrefs.GetInt(LaunchedBeforeKey, 0) == 1);
            PlayerPrefs.SetInt(LaunchedBeforeKey, 1);
            PlayerPrefs.Save();
#if UNITY_ANDROID && ADMOB_SDK
            if (!Application.isEditor)
                StartConsent();
#endif
        }

        /// <summary>The player has left a game for the title screen (it is saved and paused): the break where an interstitial may show.</summary>
        public void GameFinished()
        {
            if (gate == null || !gate.GameFinished(Time.realtimeSinceStartup))
                return;
#if UNITY_ANDROID && ADMOB_SDK
            if (ShowInterstitial())
                gate.Shown(Time.realtimeSinceStartup);
#endif
        }

#if UNITY_ANDROID && ADMOB_SDK
        static readonly System.TimeSpan BannerRetry = System.TimeSpan.FromSeconds(30);
        BannerView banner; bool bannerLoaded; System.DateTime bannerRetryAt;
        InterstitialAd interstitial; bool loadingInterstitial, started;

        void StartConsent()
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            // Main-thread callbacks wait for this object, which the SDK only makes when first used. Consent has to answer
            // before that on a fresh install, so without this its callback never arrives and ads never start.
            MobileAdsEventExecutor.Initialize();
            // Consent given on an earlier launch still counts, so ads need not wait for this launch's check.
            StartIfAllowed();
            ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
            {
                if (updateError != null)
                {
                    // Offline, or no consent message configured for the app: earlier consent (CanRequestAds) still decides.
                    Debug.LogWarning("Ad consent check failed: " + updateError.Message);
                    StartIfAllowed();
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                        Debug.LogWarning("Ad consent form failed: " + formError.Message);
                    StartIfAllowed();
                });
            });
        }

        void StartIfAllowed()
        {
            if (started || !ConsentInformation.CanRequestAds())
                return;
            started = true;
            MobileAds.Initialize(_ => { LoadInterstitial(); LoadBanner(); });
        }

        void Update()
        {
            if (!started)
                return;
            LoadBanner();
            GameplayPresenter.BannerPixels = banner != null && bannerLoaded ? banner.GetHeightInPixels() : 0;
        }

        void LoadBanner()
        {
            if (!started || banner != null || System.DateTime.UtcNow < bannerRetryAt)
                return;
            // Landscape: the side insets (nav bar / cutout) lie outside the banner's area, so FullWidth would overhang the right edge.
            var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(
                Mathf.FloorToInt(Screen.safeArea.width / MobileAds.Utils.GetDeviceScale()));
            var view = new BannerView(AdConfig.BannerUnit, size, AdPosition.Bottom);
            banner = view;
            bannerLoaded = false;
            view.OnBannerAdLoaded += () =>
            {
                bannerLoaded = true;
                // AdPosition.Bottom draws under the edge-to-edge navigation bar; sit the banner just above the bottom safe-area inset instead.
                float scale = MobileAds.Utils.GetDeviceScale();
                float h = view.GetHeightInPixels();
                view.SetPosition(Mathf.RoundToInt(Screen.safeArea.xMin / scale), Mathf.RoundToInt((Screen.height - Screen.safeArea.yMin - h) / scale));
            };
            view.OnBannerAdLoadFailed += error =>
            {
                Debug.LogWarning("Banner ad did not load: " + error);
                view.Destroy();
                banner = null;
                bannerLoaded = false;
                bannerRetryAt = System.DateTime.UtcNow + BannerRetry;
            };
            view.LoadAd(new AdRequest());
        }

        void LoadInterstitial()
        {
            if (interstitial != null && !interstitial.CanShowAd())
            {
                interstitial.Destroy();
                interstitial = null;
            }
            if (!started || interstitial != null || loadingInterstitial)
                return;
            loadingInterstitial = true;
            InterstitialAd.Load(AdConfig.InterstitialUnit, new AdRequest(), (ad, error) =>
            {
                loadingInterstitial = false;
                if (error != null || ad == null)
                    Debug.LogWarning("Interstitial ad did not load: " + error);
                else
                    interstitial = ad;
            });
        }

        bool ShowInterstitial()
        {
            LoadInterstitial();
            var ad = interstitial;
            interstitial = null;
            if (ad == null)
                return false;
            void Finish() { ad.Destroy(); LoadInterstitial(); }
            ad.OnAdFullScreenContentClosed += Finish;
            ad.OnAdFullScreenContentFailed += _ => Finish();
            ad.Show();
            return true;
        }

        void OnDestroy()
        {
            banner?.Destroy();
            interstitial?.Destroy();
        }
#endif
    }
}
