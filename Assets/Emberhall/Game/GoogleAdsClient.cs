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
        readonly Func<bool> canPresentConsent;
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
        public int InterstitialOpens { get; private set; }
        public GoogleAdsClient(AdMobConfig settings, Func<bool> canPresentConsent = null) { config = settings; this.canPresentConsent = canPresentConsent; }
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
                if (error != null) { Debug.LogWarning("[Ads] Privacy update: " + error.Message); InitializeIfAllowed(done); return; }
                bool formRequired = ConsentInformation.ConsentStatus == ConsentStatus.Required;
                void PresentWhenSafe()
                {
                    if (disposed) return;
                    if (formRequired && canPresentConsent != null && !canPresentConsent())
                    { MobileAdsEventExecutor.ExecuteInUpdate(PresentWhenSafe); return; }
                    if (formRequired) OverlayChanged?.Invoke(true);
                    ConsentForm.LoadAndShowConsentFormIfRequired(formError => Main(() =>
                    {
                        if (formRequired) OverlayChanged?.Invoke(false);
                        InitializeIfAllowed(done);
                    }));
                }
                PresentWhenSafe();
            }));
        }
        void InitializeIfAllowed(Action<bool> done)
        {
            try
            {
                if (!ConsentInformation.CanRequestAds()) { initializing = false; done(false); return; }
                if (sdkInitialized) { initializing = false; initialized = true; done(true); return; }
                MobileAds.SetRequestConfiguration(new RequestConfiguration { MaxAdContentRating = MaxAdContentRating.PG });
                MobileAds.Initialize(status => Main(() =>
                {
                    try
                    {
                        if (status != null)
                        {
                            // Android requires SDK initialization before changing publisher first-party ID.
                            // Apply this before reporting ready, so no ad request precedes the privacy setting.
                            var request = MobileAds.GetRequestConfiguration();
                            request.MaxAdContentRating = MaxAdContentRating.PG;
                            request.PublisherFirstPartyIdEnabled = false;
                            MobileAds.SetRequestConfiguration(request);
                        }
                        initializing = false; initialized = status != null; sdkInitialized = initialized; done(initialized);
                    }
                    catch (Exception e) { InitializationFailed(done, e); }
                }));
            }
            catch (Exception e) { InitializationFailed(done, e); }
        }
        void InitializationFailed(Action<bool> done, Exception error)
        {
            initializing = false; initialized = false;
            Debug.LogWarning("[Ads] Initialization unavailable: " + error.Message);
            done(false);
        }
        public void LoadBanner(Action<bool, float> done)
        {
            int generation = ++bannerGeneration;
            banner?.Destroy(); banner = null;
            var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(MobileAds.Utils.GetDeviceSafeWidth());
            var ad = new BannerView(config.BannerId, size, AdPosition.Bottom);
            banner = ad; bannerVisible = false; ad.Hide();
            AndroidBannerArea.Refresh(true);
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
            if (visible) { banner.Show(); AndroidBannerArea.Refresh(true); } else banner.Hide();
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
                Debug.Log("[Ads] Interstitial load: " + (error == null ? "ready" : error.GetMessage()));
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
                Debug.Log("[Ads] Rewarded load: " + (error == null ? "ready" : error.GetMessage()));
                done(RewardedReady);
            }));
        }
        public void ShowInterstitial(Action<bool> closed)
        {
            if (!InterstitialReady) { closed(false); return; }
            var ad = interstitial; activeInterstitial = ad; interstitial = null; bool ended = false;
            ad.OnAdFullScreenContentOpened += () => Main(() => { InterstitialOpens++; Debug.Log("[Ads] Interstitial opened"); });
            Action<bool> finish = ok => Main(() => { if (ended) return; ended = true; ad.Destroy(); activeInterstitial = null; closed(ok); });
            ad.OnAdFullScreenContentClosed += () => finish(true);
            ad.OnAdFullScreenContentFailed += error => finish(false);
            try { ad.Show(); } catch (Exception) { finish(false); }
        }
        public void ShowRewarded(Action earned, Action<bool> closed)
        {
            if (!RewardedReady) { closed(false); return; }
            var ad = rewarded; activeRewarded = ad; rewarded = null; bool ended = false;
            ad.OnAdFullScreenContentOpened += () => Main(() => Debug.Log("[Ads] Rewarded opened"));
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
