using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Emberhall
{
    public interface IAdsClient : IDisposable
    {
        bool Supported { get; }
        bool InterstitialReady { get; }
        bool RewardedReady { get; }
        bool PrivacyRequired { get; }
        event Action<bool> OverlayChanged;
        void Initialize(Action<bool> done);
        void LoadBanner(Action<bool, float> done);
        void SetBannerVisible(bool visible);
        void LoadInterstitial(Action<bool> done);
        void LoadRewarded(Action<bool> done);
        void ShowInterstitial(Action<bool> closed);
        void ShowRewarded(Action earned, Action<bool> closed);
        void ShowPrivacy(Action done);
    }

    /// <summary>The real mobile SDK adapter. Every callback that touches game state is marshalled to Unity.</summary>
    public sealed class GoogleAdsClient : IAdsClient
    {
        readonly AdMobConfig config;
        BannerView banner;
        InterstitialAd interstitial;
        RewardedAd rewarded;
        InterstitialAd activeInterstitial;
        RewardedAd activeRewarded;
        bool disposed, bannerVisible, initializing, initialized;
        static bool sdkInitialized;
        int bannerGeneration, interstitialGeneration, rewardedGeneration;
        double interstitialLoaded, rewardedLoaded;
        public event Action<bool> OverlayChanged;
        public GoogleAdsClient(AdMobConfig settings) { config = settings; }
        public bool Supported => Application.isMobilePlatform || Application.isEditor;
        public bool PrivacyRequired => Supported && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
        public bool InterstitialReady => !disposed && interstitial != null && interstitial.CanShowAd() && Time.realtimeSinceStartupAsDouble - interstitialLoaded < 3000;
        public bool RewardedReady => !disposed && rewarded != null && rewarded.CanShowAd() && Time.realtimeSinceStartupAsDouble - rewardedLoaded < 3000;
        void Main(Action work) => MobileAdsEventExecutor.ExecuteInUpdate(() => { if (!disposed) work(); });
        public void Initialize(Action<bool> done)
        {
            if (disposed || !Supported || !config.HasUnitIds) { done(false); return; }
            if (initialized) { done(true); return; }
            if (initializing) return;
            initializing = true;
            MobileAdsEventExecutor.Initialize();
            // Ask UMP on every launch; never infer consent from a locally stored flag.
            ConsentInformation.Update(new ConsentRequestParameters(), error => Main(() =>
            {
                if (error != null) { InitializeIfAllowed(done); return; }
                bool formRequired = ConsentInformation.ConsentStatus == ConsentStatus.Required;
                if (formRequired) OverlayChanged?.Invoke(true);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => Main(() =>
                {
                    if (formRequired) OverlayChanged?.Invoke(false);
                    InitializeIfAllowed(done);
                }));
            }));
        }
        void InitializeIfAllowed(Action<bool> done)
        {
            if (!ConsentInformation.CanRequestAds()) { initializing = false; done(false); return; }
            if (sdkInitialized) { initializing = false; initialized = true; done(true); return; }
            MobileAds.SetRequestConfiguration(new RequestConfiguration { MaxAdContentRating = MaxAdContentRating.PG, PublisherFirstPartyIdEnabled = false });
            MobileAds.Initialize(status => Main(() =>
            {
                initializing = false; initialized = status != null; sdkInitialized = initialized; done(initialized);
            }));
        }
        public void LoadBanner(Action<bool, float> done)
        {
            int generation = ++bannerGeneration;
            banner?.Destroy(); banner = null;
            var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(MobileAds.Utils.GetDeviceSafeWidth());
            var ad = new BannerView(config.BannerId, size, AdPosition.Bottom);
            banner = ad; bannerVisible = false; ad.Hide();
            ad.OnBannerAdLoaded += () => Main(() =>
            {
                if (generation != bannerGeneration) return;
                if (!bannerVisible) ad.Hide();
                done(true, ad.GetHeightInPixels());
            });
            ad.OnBannerAdLoadFailed += error => Main(() =>
            {
                if (generation != bannerGeneration) return;
                ad.Hide(); done(false, 0);
                Debug.Log("[Ads] Banner load unavailable: " + error.GetCode());
            });
            ad.OnAdFullScreenContentOpened += () => Main(() => OverlayChanged?.Invoke(true));
            ad.OnAdFullScreenContentClosed += () => Main(() => OverlayChanged?.Invoke(false));
            ad.LoadAd(new AdRequest());
        }
        public void SetBannerVisible(bool visible)
        {
            bannerVisible = visible;
            if (banner == null) return;
            if (visible) banner.Show(); else banner.Hide();
        }
        public void LoadInterstitial(Action<bool> done)
        {
            int generation = ++interstitialGeneration;
            interstitial?.Destroy(); interstitial = null;
            InterstitialAd.Load(config.InterstitialId, new AdRequest(), (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (disposed || generation != interstitialGeneration) { ad?.Destroy(); return; }
                interstitial = error == null ? ad : null;
                if (error != null) ad?.Destroy();
                interstitialLoaded = Time.realtimeSinceStartupAsDouble;
                done(InterstitialReady);
            }));
        }
        public void LoadRewarded(Action<bool> done)
        {
            int generation = ++rewardedGeneration;
            rewarded?.Destroy(); rewarded = null;
            RewardedAd.Load(config.RewardedId, new AdRequest(), (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (disposed || generation != rewardedGeneration) { ad?.Destroy(); return; }
                rewarded = error == null ? ad : null;
                if (error != null) ad?.Destroy();
                rewardedLoaded = Time.realtimeSinceStartupAsDouble;
                done(RewardedReady);
            }));
        }
        public void ShowInterstitial(Action<bool> closed)
        {
            if (!InterstitialReady) { closed(false); return; }
            var ad = interstitial; activeInterstitial = ad; interstitial = null; bool ended = false;
            Action<bool> finish = ok => Main(() => { if (ended) return; ended = true; ad.Destroy(); activeInterstitial = null; closed(ok); });
            ad.OnAdFullScreenContentClosed += () => finish(true);
            ad.OnAdFullScreenContentFailed += error => finish(false);
            try { ad.Show(); } catch (Exception) { finish(false); }
        }
        public void ShowRewarded(Action earned, Action<bool> closed)
        {
            if (!RewardedReady) { closed(false); return; }
            var ad = rewarded; activeRewarded = ad; rewarded = null; bool ended = false;
            Action<bool> finish = ok => Main(() => { if (ended) return; ended = true; ad.Destroy(); activeRewarded = null; closed(ok); });
            ad.OnAdFullScreenContentClosed += () => finish(true);
            ad.OnAdFullScreenContentFailed += error => finish(false);
            try { ad.Show(reward => Main(earned)); } catch (Exception) { finish(false); }
        }
        public void ShowPrivacy(Action done)
        {
            if (!PrivacyRequired) { done(); return; }
            ConsentForm.ShowPrivacyOptionsForm(error => Main(done));
        }
        public void Dispose()
        {
            disposed = true; ++bannerGeneration; ++interstitialGeneration; ++rewardedGeneration;
            banner?.Destroy(); interstitial?.Destroy(); rewarded?.Destroy();
            activeInterstitial?.Destroy(); activeRewarded?.Destroy();
            banner = null; interstitial = null; rewarded = null;
        }
    }
}
