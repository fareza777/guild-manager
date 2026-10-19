using ManagerKit;
using ManagerKit.UI;
using UnityEngine;

namespace Emberhall
{
    public sealed class RemoveAdsPopup : Popup
    {
        RemoveAdsService Store => App.I.RemoveAds;
        public override string Title => StoryScript.Pick("Remove ads", "Hapus iklan");
        public override float Height => 760;
        public override void Build(RectTransform body)
        {
            if (Store == null) return;
            if (Store.Owned)
            {
                W.Info(body, StoryScript.Pick("Ads are removed on this account.", "Iklan sudah dihapus di perangkat ini."), 44, Theme.Green);
                return;
            }
            W.Info(body, StoryScript.Pick("Keep every battle uninterrupted. Remove banner, interstitial and rewarded ads with one permanent purchase.", "Nikmati battle tanpa gangguan. Hapus banner, interstitial, dan rewarded ads dengan satu pembelian permanen."), 40, Theme.Text);
            W.Info(body, Store.DisplayPrice, 44, Theme.Gold);
            var buy = U.Btn(body, StoryScript.Pick("Remove ads", "Hapus iklan"), Theme.Accent, Store.Buy, 800, 128, 44, Icons.Get("star"));
            buy.Le(-1, 128); buy.interactable = Store.CanBuy;
            if (!Store.CanBuy) W.Info(body, StoryScript.Pick("Connecting to Google Play…", "Menghubungkan ke Google Play…"), 34, Theme.TextDim);
            U.Btn(body, StoryScript.Pick("Restore purchase", "Pulihkan pembelian"), Theme.PanelLight, Store.Restore, 800, 104, 38).Le(-1, 104);
        }
    }

    public sealed class SuppliesPopup : Popup
    {
        AdService Ads => App.I.Ads;
        AdRewards Rewards => App.Ctx.Get<AdRewards>();
        string lastStatus;
        public override string Title => StoryScript.Pick("Guild supplies", "Bekal guild");
        public override float Height => 940;
        string Status => !Ads.Supported ? StoryScript.Pick("Video bonuses are available on mobile.", "Bonus video tersedia di ponsel.")
            : !Ads.TutorialDone ? StoryScript.Pick("Finish the first four guild steps to unlock video bonuses.", "Selesaikan empat langkah awal guild untuk membuka bonus video.")
            : Rewards.SuppliesToday >= AdRewards.DailySupplyLimit ? StoryScript.Pick("All supplies claimed. Come back tomorrow.", "Semua bekal diambil. Kembali besok.")
            : Rewards.SupplyWait > 0 ? StoryScript.Pick($"Ready in {Util.Dur(Rewards.SupplyWait)}", $"Siap dalam {Util.Dur(Rewards.SupplyWait)}")
            : Ads.RewardReady ? StoryScript.Pick("Watch one video to receive these supplies.", "Tonton satu video untuk menerima bekal ini.")
            : StoryScript.Pick("No video ready yet. You can keep playing.", "Video belum siap. Kamu bisa tetap bermain.");
        public override void Build(RectTransform body)
        {
            W.Info(body, StoryScript.Pick("A little help for the road ahead.", "Bekal kecil untuk perjalanan berikutnya."), 42, Theme.Text);
            var holder = U.Rt(body, "supplies"); holder.Le(-1, 180);
            var row = W.RewardRow(holder, new[] { new Res("gold", AdRewards.SupplyGold), new Res("gems", AdRewards.SupplyGems) }, 112, 40);
            row.At(.5f, .5f, 0, 0, 440, 172);
            lastStatus = Status;
            W.Info(body, lastStatus, 40, Theme.Text);
            W.Info(body, StoryScript.Pick($"{Rewards.SuppliesToday}/{AdRewards.DailySupplyLimit} today · 10 min between claims", $"{Rewards.SuppliesToday}/{AdRewards.DailySupplyLimit} hari ini · Jeda 10 menit"), 36, Theme.TextDim);
            bool ready = Ads.TutorialDone && Ads.RewardReady && Rewards.CanSupply;
            var watch = U.Btn(body, StoryScript.Pick("Watch video · claim supplies", "Tonton video · ambil bekal"), Theme.Green, () =>
                Ads.Watch(Rewards.SupplyTicket(), Redraw, Redraw), 800, 120, 42, Icons.Get("bag"));
            watch.Le(-1, 120); watch.interactable = ready;
            if (Ads.Supported && !Ads.RewardReady && Rewards.CanSupply && Ads.TutorialDone)
                U.Btn(body, StoryScript.Pick("Try loading a video", "Coba muat video"), Theme.PanelLight, () => { Ads.Retry(); Redraw(); }, 800, 108, 40).Le(-1, 108);
        }
        public override void Tick() { if (lastStatus != Status) Redraw(); }
    }

    public sealed class AdsPrivacyPopup : Popup
    {
        AdService Ads => App.I.Ads;
        bool wasReady, wasPrivacy;
        public override string Title => StoryScript.Pick("Ads & privacy", "Iklan & privasi");
        public override float Height => 1340;
        public override void Build(RectTransform body)
        {
            W.Info(body, StoryScript.Pick("Video bonuses are optional. Regular battle rewards never require an ad.", "Bonus video bersifat opsional. Hadiah battle biasa tidak membutuhkan iklan."), 42, Theme.Text);
            W.Info(body, StoryScript.Pick("Banners stay below the menu. Short ad breaks are limited to your return from battle.", "Banner berada di bawah menu. Jeda iklan singkat dibatasi saat kembali dari battle."), 40, Theme.TextDim);
            if (Ads.PrivacyRequired)
                U.Btn(body, StoryScript.Pick("Manage privacy choices", "Atur pilihan privasi"), Theme.Accent, Ads.ShowPrivacy, 800, 116, 42, Icons.Get("gear")).Le(-1, 116);
            else W.Info(body, StoryScript.Pick("Google privacy choices appear here when required in your region.", "Pilihan privasi Google muncul di sini jika diperlukan di wilayahmu."), 36, Theme.TextDim);
            if (!string.IsNullOrWhiteSpace(Ads.Config.privacyPolicyUrl))
                U.Btn(body, StoryScript.Pick("Privacy policy", "Kebijakan privasi"), Theme.PanelLight, () => Application.OpenURL(Ads.Config.privacyPolicyUrl), 800, 108, 40).Le(-1, 108);
            U.Btn(body, StoryScript.Pick("Google advertising privacy", "Privasi periklanan Google"), Theme.PanelLight, () => Application.OpenURL("https://policies.google.com/technologies/ads"), 800, 108, 40).Le(-1, 108);
            if (Ads.TestMode)
            {
                W.Header(body, StoryScript.Pick("Test ads", "Iklan uji"), Icons.Get("info"));
                W.Info(body, StoryScript.Pick("Google sample IDs · no ad revenue", "ID contoh Google · tanpa pendapatan iklan"), 36, Theme.TextDim);
                W.Info(body, !Ads.Supported ? StoryScript.Pick("Native ad previews require Android or iOS.", "Pratinjau iklan membutuhkan Android atau iOS.")
                    : Ads.RewardReady ? StoryScript.Pick("Video ready", "Video siap") : StoryScript.Pick("Waiting for an available video", "Menunggu video tersedia"), 38, Theme.Text);
                if (Ads.Supported)
                {
                    U.Btn(body, StoryScript.Pick("Reload test ads", "Muat ulang iklan uji"), Theme.PanelLight, Ads.Retry, 800, 104, 40).Le(-1, 104);
                    U.Btn(body, StoryScript.Pick("Preview interstitial", "Pratinjau interstitial"), Theme.PanelLight, Ads.PreviewInterstitial, 800, 104, 40).Le(-1, 104);
                    U.Btn(body, StoryScript.Pick("Preview rewarded · no bonus", "Pratinjau rewarded · tanpa bonus"), Theme.PanelLight, Ads.PreviewRewarded, 800, 104, 40).Le(-1, 104);
                }
            }
            wasReady = Ads.RewardReady; wasPrivacy = Ads.PrivacyRequired;
        }
        public override void Tick() { if (wasReady != Ads.RewardReady || wasPrivacy != Ads.PrivacyRequired) Redraw(); }
    }
}
