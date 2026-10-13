using System;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;

namespace Emberhall
{
    /// <summary>Ad placement, consent, retry and presentation lifecycle. The game never waits for inventory.</summary>
    public sealed class AdService : MonoBehaviour
    {
        sealed class Slot
        {
            public bool loading;
            public int failures;
            public double next, started;
            public void Complete(bool ok, double now)
            {
                loading = false; failures = ok ? 0 : Math.Min(7, failures + 1);
                next = ok ? now : now + Math.Min(300, 5 * Math.Pow(2, failures - 1));
            }
            public bool Due(double now) => !loading && now >= next;
            public void Begin(double now) { loading = true; started = now; }
        }
        App app;
        IAdsClient client;
        AdMobConfig config;
        AdBreakPolicy breaks;
        Slot banner = new Slot(), interstitial = new Slot(), rewarded = new Slot();
        bool initialized, initializing, paused, bannerLoaded, bannerVisible, audioWasPaused, disposed;
        double initNext;
        float bannerPixels;
        int bannerWidth;
        Rect bannerSafe;
        public bool Busy { get; private set; }
        public bool Supported => client != null && client.Supported && config.enableAds;
        public bool TutorialDone => app?.ctx?.Quests.State("q_m04") == QuestSystem.Done;
        public bool RewardReady => initialized && !Busy && client.RewardedReady;
        public bool PrivacyRequired => Supported && client.PrivacyRequired;
        public bool TestMode => config.useTestIds;
        public AdMobConfig Config => config;
        public float BannerInset { get; private set; }
        public event Action Changed;
        double Now => Time.realtimeSinceStartupAsDouble;
        bool SafePage => app.InGame && app.nav.Current != null && !(app.nav.Current is IFullScreenPage);

        public void Bind(App owner)
        {
            app = owner; config = AdMobConfig.Load(); breaks = new AdBreakPolicy(Now);
            // Automated desktop verification must never contact an ad network or display consent.
            client = Tour.Requested ? new NoAdsClient() : new GoogleAdsClient(config);
            client.OverlayChanged += SetBusy;
            app.nav.Changed += NavigationChanged;
            Popups.Opened += PopupOpened;
        }
        void NavigationChanged(UiScreen screen) { HideBanner(); }
        void PopupOpened(Popup popup) { HideBanner(); }
        void Update()
        {
            if (disposed || app == null || !Supported || paused) return;
            if (!initialized)
            {
                if (!initializing && Now >= initNext && SafePage && TutorialDone && Popups.Count == 0) Initialize();
                return;
            }
            if (Busy) { HideBanner(); return; }
            // A stalled load cannot permanently disable a format. Late native callbacks are generation-checked.
            if (banner.loading && Now - banner.started > 60) banner.Complete(false, Now);
            if (interstitial.loading && Now - interstitial.started > 60) interstitial.Complete(false, Now);
            if (rewarded.loading && Now - rewarded.started > 60) rewarded.Complete(false, Now);
            if (bannerWidth != Screen.width || bannerSafe != Screen.safeArea)
            {
                HideBanner(); bannerLoaded = false; banner.loading = false; banner.next = Now;
                bannerWidth = Screen.width; bannerSafe = Screen.safeArea;
            }
            if (!bannerLoaded && banner.Due(Now))
            {
                banner.Begin(Now);
                TryLoad(() => client.LoadBanner((ok, pixels) =>
                {
                    bannerLoaded = ok && pixels > 0; bannerPixels = pixels; banner.Complete(bannerLoaded, Now); Changed?.Invoke();
                }), banner);
            }
            if (!client.InterstitialReady && interstitial.Due(Now))
            {
                interstitial.Begin(Now); TryLoad(() => client.LoadInterstitial(ok => { interstitial.Complete(ok, Now); Changed?.Invoke(); }), interstitial);
            }
            if (!client.RewardedReady && rewarded.Due(Now))
            {
                rewarded.Begin(Now); TryLoad(() => client.LoadRewarded(ok => { rewarded.Complete(ok, Now); Changed?.Invoke(); }), rewarded);
            }
            bool show = bannerLoaded && TutorialDone && SafePage && Popups.Count == 0 && !Busy;
            float inset = show ? bannerPixels / Mathf.Max(.01f, app.host.canvas.scaleFactor) + 28f : 0;
            SetInset(inset);
            if (bannerVisible != show) { bannerVisible = show; client.SetBannerVisible(show); }
        }
        void TryLoad(Action load, Slot slot)
        {
            try { load(); } catch (Exception e) { slot.Complete(false, Now); Debug.LogWarning("[Ads] Load deferred: " + e.GetType().Name); Changed?.Invoke(); }
        }
        public void Initialize()
        {
            if (!Supported || initialized || initializing || Busy) return;
            initializing = true;
            try
            {
                client.Initialize(ok =>
                {
                    if (disposed) return;
                    initialized = ok; initializing = false; initNext = Now + 60; SetBusy(false); Changed?.Invoke();
                });
            }
            catch (Exception e)
            {
                initializing = false; initNext = Now + 60; SetBusy(false);
                Debug.LogWarning("[Ads] Initialization deferred: " + e.GetType().Name);
            }
        }
        public void Retry()
        {
            if (Busy) return;
            banner.next = interstitial.next = rewarded.next = Now;
            if (!initialized) { initNext = Now; Initialize(); }
        }
        void SetInset(float inset)
        {
            if (Mathf.Abs(BannerInset - inset) < .5f) return;
            BannerInset = inset; app.hud?.SetAdInset(inset);
        }
        public void HideBanner()
        {
            if (bannerVisible) { bannerVisible = false; client?.SetBannerVisible(false); }
            SetInset(0);
        }
        void SetBusy(bool value)
        {
            if (Busy == value) return;
            Busy = value;
            if (value) { app.SaveNow(); audioWasPaused = AudioListener.pause; AudioListener.pause = true; HideBanner(); }
            else AudioListener.pause = audioWasPaused;
            Changed?.Invoke();
        }
        public void BattleFinished(string key, bool campaign) { breaks.BattleFinished(key, campaign && TutorialDone); }
        public bool TryInterstitialAtBreak()
        {
            if (!Supported || !breaks.CanShow(Now, TutorialDone, initialized && client.InterstitialReady, SafePage && Popups.Count == 0 && !Busy && !paused)) return false;
            breaks.InterstitialShown(Now); SetBusy(true);
            try { client.ShowInterstitial(ok => { SetBusy(false); interstitial.Complete(ok, Now); Changed?.Invoke(); }); }
            catch (Exception) { SetBusy(false); interstitial.Complete(false, Now); }
            return true;
        }
        public bool Watch(AdRewards.Ticket ticket, Action earned = null, Action finished = null)
        {
            var ledger = app.ctx.Get<AdRewards>();
            if (ticket == null || !RewardReady || !(SafePage || Popups.Top is ResultPopup) || !TutorialDone || paused)
            {
                ledger.Cancel(ticket); Toasts.Show(StoryScript.Pick("No video ready. Try again shortly.", "Video belum siap. Coba lagi sebentar.")); return false;
            }
            breaks.RewardedShown(Now); SetBusy(true);
            bool credited = false;
            try { client.ShowRewarded(() =>
            {
                if (disposed || credited || !ledger.GrantEarned(ticket)) return;
                credited = true; app.SaveNow(); earned?.Invoke(); Changed?.Invoke();
                Toasts.Show(StoryScript.Pick("Video bonus added to your guild!", "Bonus video sudah masuk ke guild!"), Icons.Get("coin"), Theme.Green);
            }, ok =>
            {
                if (!ok) ledger.Cancel(ticket);
                SetBusy(false); rewarded.Complete(ok, Now); finished?.Invoke(); Changed?.Invoke();
                if (!credited) Toasts.Show(StoryScript.Pick(ok ? "Video closed. Your regular rewards are safe." : "Video unavailable. Please try again later.", ok ? "Video ditutup. Hadiah biasa tetap aman." : "Video belum tersedia. Silakan coba lagi nanti."));
            }); }
            catch (Exception)
            {
                ledger.Cancel(ticket); SetBusy(false); rewarded.Complete(false, Now); finished?.Invoke();
                Toasts.Show(StoryScript.Pick("Video unavailable. Please try again later.", "Video belum tersedia. Silakan coba lagi nanti."));
                return false;
            }
            return true;
        }
        public void ShowPrivacy()
        {
            if (!PrivacyRequired || Busy) return;
            SetBusy(true);
            try { client.ShowPrivacy(() =>
            {
                SetBusy(false); initialized = false; initNext = Now + 1;
                // Discard cached inventory after a privacy choice; UMP gates all subsequent requests.
                client.Dispose(); client = new GoogleAdsClient(config); client.OverlayChanged += SetBusy;
                bannerLoaded = false; banner = new Slot(); interstitial = new Slot(); rewarded = new Slot(); Changed?.Invoke();
            }); }
            catch (Exception) { SetBusy(false); Toasts.Show(StoryScript.Pick("Privacy choices are unavailable. Please try again.", "Pilihan privasi belum tersedia. Silakan coba lagi.")); }
        }
        public void PreviewInterstitial() { Preview(false); }
        public void PreviewRewarded() { Preview(true); }
        void Preview(bool isRewarded)
        {
            if (!TestMode || !Supported || Busy || !SafePage) return;
            if (!initialized || (isRewarded ? !client.RewardedReady : !client.InterstitialReady))
            { Retry(); Toasts.Show(StoryScript.Pick("Test ad is loading. Try again shortly.", "Iklan uji sedang dimuat. Coba lagi sebentar.")); return; }
            SetBusy(true);
            breaks.RewardedShown(Now);
            Action<bool> done = ok => { SetBusy(false); Changed?.Invoke(); };
            try { if (isRewarded) client.ShowRewarded(() => { }, done); else client.ShowInterstitial(done); }
            catch (Exception) { SetBusy(false); }
        }
        void OnApplicationPause(bool value) { paused = value; if (value) HideBanner(); }
        void OnDestroy()
        {
            disposed = true;
            if (app != null) app.nav.Changed -= NavigationChanged;
            Popups.Opened -= PopupOpened;
            if (client != null) { client.OverlayChanged -= SetBusy; client.Dispose(); }
            if (Busy) AudioListener.pause = audioWasPaused;
        }
        // This entry point is restricted to the isolated, explicitly requested verification run.
        public void InstallVerificationClient(IAdsClient replacement)
        {
            if (!Tour.Verification) throw new InvalidOperationException("Verification mode required.");
            HideBanner(); client.Dispose(); client = replacement; client.OverlayChanged += SetBusy;
            initialized = initializing = false; initNext = 0;
            bannerLoaded = false; banner = new Slot(); interstitial = new Slot(); rewarded = new Slot();
        }
    }
    public sealed class NoAdsClient : IAdsClient
    {
        public bool Supported => false;
        public bool InterstitialReady => false;
        public bool RewardedReady => false;
        public bool PrivacyRequired => false;
        public event Action<bool> OverlayChanged { add { } remove { } }
        public void Initialize(Action<bool> done) => done(false);
        public void LoadBanner(Action<bool, float> done) => done(false, 0);
        public void SetBannerVisible(bool visible) { }
        public void LoadInterstitial(Action<bool> done) => done(false);
        public void LoadRewarded(Action<bool> done) => done(false);
        public void ShowInterstitial(Action<bool> closed) => closed(false);
        public void ShowRewarded(Action earned, Action<bool> closed) => closed(false);
        public void ShowPrivacy(Action done) => done();
        public void Dispose() { }
    }
}
