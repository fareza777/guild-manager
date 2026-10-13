using System;
using ManagerKit.UI;
using UnityEngine;

namespace Emberhall
{
    /// <summary>Isolated flow/layout double, never an indication of a native ad impression.</summary>
    internal sealed class TourAdsClient : IAdsClient
    {
        bool interstitial, rewarded;
        RectTransform banner;
        public bool FailLoads;
        public bool BannerVisible;
        public bool Supported => true;
        public bool InterstitialReady => interstitial;
        public bool RewardedReady => rewarded;
        public bool PrivacyRequired => false;
        public event Action<bool> OverlayChanged { add { } remove { } }
        public void Initialize(Action<bool> done) => done(true);
        public void LoadBanner(Action<bool, float> done) => done(!FailLoads, FailLoads ? 0 : Screen.width / 6f);
        public void SetBannerVisible(bool visible)
        {
            BannerVisible = visible;
            if (visible && banner == null)
            {
                banner = U.Solid(UiHost.I.root, Theme.PanelLight, "VerificationBanner").R();
                banner.anchorMin = new Vector2(0, 0); banner.anchorMax = new Vector2(1, 0); banner.pivot = new Vector2(.5f, 0);
                banner.sizeDelta = new Vector2(0, Screen.width / 6f / UiHost.I.canvas.scaleFactor); banner.anchoredPosition = Vector2.zero;
                U.Txt(banner, "AD LAYOUT PREVIEW\nNative SDK is not running", 36, Theme.Text, TextAnchor.MiddleCenter, true, true).Fill(24, 24, 12, 12);
            }
            if (banner != null) banner.gameObject.SetActive(visible);
        }
        public void LoadInterstitial(Action<bool> done) { interstitial = !FailLoads; done(interstitial); }
        public void LoadRewarded(Action<bool> done) { rewarded = !FailLoads; done(rewarded); }
        public void ShowInterstitial(Action<bool> closed)
        {
            interstitial = false; Popups.Show(new Preview(false, null, closed));
        }
        public void ShowRewarded(Action earned, Action<bool> closed)
        {
            rewarded = false; Popups.Show(new Preview(true, earned, closed));
        }
        public void ShowPrivacy(Action done) => done();
        public void Dispose() { if (banner != null) UnityEngine.Object.Destroy(banner.gameObject); BannerVisible = false; }
        sealed class Preview : Popup
        {
            readonly bool video;
            readonly Action earned;
            readonly Action<bool> finished;
            bool ended;
            public Preview(bool video, Action earned, Action<bool> finished) { this.video = video; this.earned = earned; this.finished = finished; }
            public override string Title => "Flow verification";
            public override bool ShowClose => false;
            public override bool CloseOnDim => false;
            public override float Height => 980;
            public override void Build(RectTransform body)
            {
                W.Info(body, "Simulated ad callbacks only. No native SDK, network request or ad impression.", 42, Theme.Text);
                if (video) U.Btn(body, "Complete test video", Theme.Green, () => End(true, true), 800, 120, 42).Le(-1, 120);
                U.Btn(body, video ? "Close without reward" : "Close test interstitial", Theme.PanelLight, () => End(true, false), 800, 120, 42).Le(-1, 120);
                U.Btn(body, "Simulate failure", Theme.PanelLight, () => End(false, false), 800, 120, 42).Le(-1, 120);
            }
            public override bool OnBack() { End(true, false); return true; }
            void End(bool ok, bool reward)
            {
                if (ended) return; ended = true;
                if (reward) { earned?.Invoke(); earned?.Invoke(); }
                Close(); finished(ok);
            }
        }
    }
}
