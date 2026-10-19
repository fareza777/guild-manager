using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    public sealed class JourneyPopup : Popup
    {
        Journey Journey => App.Ctx.Get<Journey>();
        int shownEnergy;
        Text recovery;
        public override string Title => StoryScript.Pick("Journey & stamina", "Perjalanan & stamina");
        public override float Height => 1480;
        public override void Build(RectTransform body)
        {
            shownEnergy = Journey.Energy;
            W.Info(body, $"{StoryScript.Pick("Stamina", "Stamina")}  {shownEnergy}/{Emberhall.Journey.Capacity}", 56, Theme.GreenDark);
            recovery = W.Info(body, RecoveryText(), 40, Theme.Text);
            var costs = U.Rt(body, "costs"); costs.Le(-1, 154); U.HBox(costs, 18, null, TextAnchor.MiddleCenter, true, true, true, true);
            string[] labels = { StoryScript.Pick("Trail", "Jalur"), StoryScript.Pick("Boss / Spire", "Boss / Spire"), StoryScript.Pick("Defeat", "Kalah") };
            int[] amounts = { 2, 3, 1 };
            for (int i = 0; i < 3; i++)
            {
                var stat = U.Rt(costs, "cost"); stat.Le(-1, 154, 1);
                U.Txt(stat, amounts[i].ToString(), 56, Theme.Gold, TextAnchor.MiddleCenter, true).StretchX(1, -4, 76);
                U.Txt(stat, labels[i], 32, Theme.TextDim, TextAnchor.MiddleCenter).StretchX(0, 8, 56);
            }
            W.Info(body, StoryScript.Pick("+1 every 5 min, even offline. Your first three trail clears are free.", "+1 tiap 5 menit, termasuk offline. Tiga clear pertama gratis."), 38, Theme.TextDim);
            W.Info(body, StoryScript.Pick("Train, forge, take exams and send contracts without stamina.", "Latihan, menempa, ujian, dan kontrak tidak memakai stamina."), 38, Theme.TextDim);
            W.Header(body, StoryScript.Pick("Your next challenge", "Tantangan berikutnya"));
            W.Info(body, StoryScript.Pick("8 trail stars → Regional boss → Guild exam → Next region", "8 bintang jalur → Boss wilayah → Ujian guild → Wilayah baru"), 40, Theme.Text);
            W.Info(body, StoryScript.Pick("Auto combat plays one encounter. You choose each new challenge.", "Battle otomatis memainkan satu pertempuran. Kamu memilih tantangan berikutnya."), 38, Theme.TextDim);
            var camp = U.Btn(body, Journey.CampUsed ? StoryScript.Pick("Camp ration used today", "Bekal kamp hari ini sudah dipakai") : StoryScript.Pick("Rest at camp · +10 stamina", "Istirahat di kamp · +10 stamina"), Theme.Green,
                () => { if (Journey.RestAtCamp()) { App.I.SaveNow(); Audio.Play("heal"); Redraw(); } }, 800, 116, 40, Icons.Get("clock"));
            camp.Le(-1, 116); camp.Interactable = Journey.CanRest;
            W.Info(body, StoryScript.Pick("One free camp ration per day. Use it at 20 stamina or below.", "Satu bekal kamp gratis per hari. Gunakan saat stamina 20 atau kurang."), 36, Theme.TextDim);
        }
        string RecoveryText() => Journey.Energy == Emberhall.Journey.Capacity ? StoryScript.Pick("Ready for the road.", "Siap menjelajah.") : StoryScript.Pick($"Next point in {Util.Dur(Journey.NextRecovery)}", $"Poin berikutnya dalam {Util.Dur(Journey.NextRecovery)}");
        public override void Tick() { if (shownEnergy != Journey.Energy) Redraw(); else if (recovery) recovery.text = RecoveryText(); }
    }
}
