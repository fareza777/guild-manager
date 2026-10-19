using System;
using System.Collections;
using System.IO;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;

namespace Emberhall
{
    /// <summary>Explicit Android QA entry point. Uses Google's real test units and an isolated guild, never a fake ad client.</summary>
    public static class NativeAdCheck
    {
        static bool? requested;
        public static bool Requested
        {
            get
            {
                if (requested.HasValue) return requested.Value;
                bool active = false;
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                        active = intent.Call<bool>("getBooleanExtra", "emberhall-adcheck", false) && AdMobConfig.Load().useTestIds;
                }
                catch (Exception) { }
#endif
                requested = active; return active;
            }
        }
        [Serializable] sealed class Report
        {
            public string mode = "Google native SDK; official sample IDs; isolated QA guild", phase = "boot", version, client;
            public bool initialized, bannerLoaded, bannerVisible, interstitialReady, rewardedReady, busy, rewardedEarned;
            public int interstitialsClosed, interstitialsOpened;
            public int navigationPixels, bannerTop, bannerBottom, windowBottom;
            public int protectedHosts, visibleFrames, unsafeDraws, guardedFrames, stressSteps;
            public float extraBottomPixels;
            public bool navigationVisible, audioPaused;
            public double gold, gems, expectedGold, expectedGems;
            public string lastError;
        }
        public static IEnumerator Run(App app)
        {
            if (!Requested) yield break;
            var report = new Report { version = App.Version };
            string folder = Application.persistentDataPath;
            string commandPath = Path.Combine(folder, "native-ad-command.txt");
            string reportPath = Path.Combine(folder, "native-ad-report.json");
            if (File.Exists(commandPath)) File.Delete(commandPath);
            Prefs.Hints = false; Prefs.Lang = "en"; L.Load("en");
            app.BeginNewGame("AdMob Test Guild", 0, "warrior");
            app.ctx.Quests.Load("{\"q\":[{\"id\":\"q_m01\",\"state\":3,\"b\":[]},{\"id\":\"q_m02\",\"state\":3,\"b\":[]},{\"id\":\"q_m03\",\"state\":3,\"b\":[]},{\"id\":\"q_m04\",\"state\":3,\"b\":[]}]}");
            app.ctx.Changed(); Popups.CloseAll(); app.hud.GoTab(Hud.Tab.Hall, true); app.SaveNow();
            report.phase = "loading";
            bool awaitingInterstitial = false;
            var wait = new WaitForSecondsRealtime(.5f);
            Application.LogCallback error = (message, stack, kind) => { if (kind == LogType.Error || kind == LogType.Exception) report.lastError = message; };
            Application.logMessageReceived += error;
            while (Requested)
            {
                report.client = app.Ads.ClientName; report.initialized = app.Ads.Initialized;
                report.interstitialsOpened = app.Ads.NativeInterstitialOpens;
                report.bannerLoaded = app.Ads.BannerReady; report.bannerVisible = app.Ads.BannerInset > 0;
                report.interstitialReady = app.Ads.InterstitialReady; report.rewardedReady = app.Ads.RewardReady; report.busy = app.Ads.Busy;
                report.gold = app.ctx.Wallet.Get("gold"); report.gems = app.ctx.Wallet.Get("gems");
                report.navigationPixels = AndroidBannerArea.NavigationPixels; report.extraBottomPixels = AndroidBannerArea.ExtraPixels;
                report.navigationVisible = AndroidBannerArea.NavigationVisible; report.bannerTop = AndroidBannerArea.BannerTop;
                report.bannerBottom = AndroidBannerArea.BannerBottom; report.windowBottom = AndroidBannerArea.WindowBottom; report.audioPaused = AudioListener.pause;
                report.protectedHosts = AndroidBannerArea.ProtectedHosts; report.visibleFrames = AndroidBannerArea.VisibleFrames;
                report.unsafeDraws = AndroidBannerArea.UnsafeDraws; report.guardedFrames = AndroidBannerArea.GuardedFrames;
                if (report.phase == "stress")
                {
                    switch (report.stressSteps % 4)
                    {
                        case 0: app.hud.GoTab(Hud.Tab.Bag, true); break;
                        case 1: Popups.Show(new SuppliesPopup()); break;
                        case 2: Popups.CloseAll(); break;
                        case 3: app.hud.GoTab(Hud.Tab.Hall, true); break;
                    }
                    AndroidBannerArea.CheckSdkRelayout();
                    if (++report.stressSteps >= 80) report.phase = "stress_complete";
                }
                if (awaitingInterstitial && !report.busy) { report.interstitialsClosed++; awaitingInterstitial = false; report.phase = "interstitial_closed"; }
                if (report.phase == "loading" && report.bannerLoaded && report.interstitialReady && report.rewardedReady) report.phase = "ready";
                if (!report.busy && (report.phase == "loading" || report.phase == "ready" || report.phase == "banner")) Popups.CloseAll();
                if (File.Exists(commandPath))
                {
                    string command = File.ReadAllText(commandPath).Trim(); File.Delete(commandPath);
                    switch (command)
                    {
                        case "banner": Popups.CloseAll(); app.hud.GoTab(Hud.Tab.Hall, true); report.phase = "banner"; break;
                        case "stress": Popups.CloseAll(); report.stressSteps = 0; report.phase = "stress"; break;
                        case "interstitial":
                            if (report.interstitialReady)
                            {
                                report.phase = "interstitial_requested";
                                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                                app.Ads.PreviewInterstitial(); awaitingInterstitial = app.Ads.Busy;
                            }
                            break;
                        case "rewarded":
                            if (report.rewardedReady)
                            {
                                Popups.CloseAll(); Popups.Show(new SuppliesPopup()); report.phase = "rewarded_requested";
                                report.expectedGold = report.gold + AdRewards.SupplyGold; report.expectedGems = report.gems + AdRewards.SupplyGems;
                                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                                app.Ads.Watch(app.ctx.Get<AdRewards>().SupplyTicket(), () => { report.rewardedEarned = true; report.phase = "rewarded_earned"; }, () => { if (!report.rewardedEarned) report.phase = "rewarded_dismissed"; });
                            }
                            break;
                        case "retry": app.Ads.Retry(); report.phase = "loading"; break;
                        case "finish": Application.logMessageReceived -= error; app.SaveNow(); Application.Quit(); yield break;
                    }
                }
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                yield return wait;
            }
            Application.logMessageReceived -= error;
        }
    }
}
