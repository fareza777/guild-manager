using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    public class SplashScreen : UiScreen
    {
        public bool Done;
        public override void Build(RectTransform root)
        {
            U.Clear(UiHost.I.back); GeneratedArt.Cover(UiHost.I.back, "splash", true);
            var logo = Brand.Logo(root, .86f); logo.StretchX(1, -42, 310, 48, 48);
            var cg = logo.gameObject.AddComponent<CanvasGroup>(); Tw.Fade(cg, 0, 1, .8f);
            var paper = U.Rt(root, "loading"); paper.StretchX(0, 72, 156, 110, 110);
            W.AirText(paper, StoryScript.Pick("A NEW ADVENTURE IS WAKING", "PETUALANGAN BARU MENANTI"), 30, Theme.TextDark, true).StretchX(1, -18, 48, 8, 8);
            var bar = U.ProgressBar(paper.transform, Theme.Accent, 600, 16); bar.back.StretchX(.5f, -2, 16, 40, 40);
            W.AirText(paper, StoryScript.Pick("Opening the guild doors…", "Membuka pintu guild…"), 28, Theme.TextDark).StretchX(0, 12, 44, 8, 8);
            Tw.To(3.3f, k => { if (bar.fill) bar.Value = k; }, Ease.InOutSine, 0, () => Done = true, bar.back);
        }
        public override void OnExit() { U.Clear(UiHost.I.back); }
    }

    public class TitleScreen : UiScreen
    {
        public override void OnExit() { U.Clear(UiHost.I.back); }
        public override bool OnBack() => false;
        public override void Build(RectTransform root)
        {
            U.Clear(UiHost.I.back); GeneratedArt.Cover(UiHost.I.back, "splash", true);
            Brand.Logo(root, .86f).StretchX(1, -34, 310, 42, 42);
            W.TextButton(root, L.Lang == "id" ? "ID / EN" : "EN / ID", () => { Prefs.Lang = L.Lang == "id" ? "en" : "id"; L.Load(Prefs.Lang); Redraw(); }, 28).At(1, 1, -24, -20, 210, 80, 1, 1);
            W.AirText(root, StoryScript.Tagline, 32, Theme.TextDark, true).StretchX(1, -340, 122, 86, 86);
            var menu = U.Rt(root, "menu_actions"); menu.StretchX(0, 56, App.I.HasSave ? 554 : 430, 120, 120);
            var col = U.Rt(menu.transform, "actions").Fill(28, 28, 28, 28); U.VBox(col, 14, null, TextAnchor.MiddleCenter, true, true, true, false);
            if (App.I.HasSave) U.Btn(col, L.T("CONTINUE"), Theme.Green, App.I.ContinueGame, 700, 100, 40, null, Color.white).Le(-1, 100);
            U.Btn(col, App.I.HasSave ? L.T("NEW GAME") : StoryScript.Pick("BEGIN YOUR ADVENTURE", "MULAI PETUALANGAN"), Theme.Accent, () =>
            {
                if (App.I.HasSave) Popups.Confirm(L.T("Start a new game?"), L.T("This will erase your current guild forever."), L.T("Erase & Start"), App.I.StartOnboarding, L.T("Cancel"), true);
                else App.I.StartOnboarding();
            }, 700, 108, 40, null, Theme.TextDark).Le(-1, 108);
            W.TextButton(col, StoryScript.Pick("THE STORY OF EMBERHALL", "KISAH EMBERHALL"), () => App.I.nav.Root(new OnboardingScreen(true)), 30).Le(-1, 94);
            var row = U.Rt(col, "utility"); row.Le(-1, 78); U.HBox(row, 14, null, TextAnchor.MiddleCenter, true, true, true, false);
            W.TextButton(row, L.T("Settings"), () => Popups.Show(new SettingsPopup()), 32).Le(-1, 78, 1);
            W.TextButton(row, L.T("About"), () => Popups.Show(new AboutPopup()), 32).Le(-1, 78, 1);
            W.AirText(root, Brand.Studio + "  ·  v" + App.Version, 24, Theme.TextDark).StretchX(0, 12, 42, 30, 30);
        }
    }

    /// <summary>Three moving story scenes, a practical guide, and a guild charter.</summary>
    public class OnboardingScreen : UiScreen
    {
        int page;
        const int Pages = 5;
        const float Foot = 164;
        readonly bool storyOnly;
        string guildName = "Emberhall";
        int emblem;
        string starter = "warrior";
        RectTransform footerRt, stageRt;
        Text narration;
        string fullNarration;
        float sceneTime;
        bool auto;
        public OnboardingScreen(bool storyOnly = false) { this.storyOnly = storyOnly; }
        int Count => storyOnly ? 3 : Pages;
        public override void OnEnter() { Audio.PlayMusic("story"); }
        public override void Tick()
        {
            sceneTime += Time.unscaledDeltaTime;
            if (narration && fullNarration != null)
            {
                int chars = Mathf.Min(fullNarration.Length, Mathf.FloorToInt(Mathf.Max(0, sceneTime - .3f) * 55));
                if (narration.text.Length != chars) narration.text = fullNarration.Substring(0, chars);
                if (auto && sceneTime > fullNarration.Length / 55f + 7f) Next();
            }
            if (!footerRt) return;
            float lift = 0;
            if (TouchScreenKeyboard.visible && Screen.height > 0)
            {
                float keyboardHeight = TouchScreenKeyboard.area.height > 1 ? TouchScreenKeyboard.area.height : Screen.height * .38f;
                lift = keyboardHeight * UiHost.CanvasSize.y / Screen.height + 16;
            }
            footerRt.offsetMin = new Vector2(0, lift); footerRt.offsetMax = new Vector2(0, Foot + lift);
            if (stageRt) stageRt.offsetMin = new Vector2(28, Foot + 24 + lift);
        }
        public override void OnExit() { U.Clear(UiHost.I.back); }
        void ChangePage(int next) { page = next; narration = null; fullNarration = null; scrolls.Clear(); Redraw(); }
        public override bool OnBack() { if (page > 0) ChangePage(page - 1); else App.I.GoTitle(); return true; }
        public override void Build(RectTransform root)
        {
            narration = null; fullNarration = null; sceneTime = 0;
            U.Clear(UiHost.I.back); U.Img(UiHost.I.back, Boxes.Gradient(Theme.Hex("#d3eef4"), Theme.Hex("#fbf0d7")), Color.white).Fill();
            var top = U.Rt(root, "chapter_header"); top.StretchX(1, -18, 78, 28, 28);
            var title = page < 3 ? StoryScript.Pick("THE EMBERHALL STORY", "KISAH EMBERHALL") : StoryScript.Pick("YOUR FIRST CHAPTER", "BAB PERTAMAMU");
            U.Txt(top.transform, title, 30, Theme.Gold, TextAnchor.MiddleCenter).StretchX(.5f, 0, 52, 184, 184);
            W.TextButton(top, storyOnly ? L.T("Close") : L.T("Skip"), () => { if (storyOnly) App.I.GoTitle(); else ChangePage(Pages - 1); }, 28).At(1, .5f, -14, 0, 154, 70, 1, .5f);
            var stage = U.Rt(root, "stage").Fill(28, 28, 116, Foot + 24); stage.gameObject.AddComponent<RectMask2D>();
            stageRt = stage;
            if (page < 3) StoryPage(stage); else if (page == 3) LoopPage(stage); else CreatePage(stage);
            var cg = stage.gameObject.AddComponent<CanvasGroup>(); Tw.Fade(cg, 0, 1, .4f);
            var foot = U.Box(root, Theme.PanelLight, Theme.Gold, "footer", false, .06f); footerRt = foot.R();
            footerRt.anchorMin = Vector2.zero; footerRt.anchorMax = new Vector2(1, 0); footerRt.pivot = new Vector2(.5f, 0);
            footerRt.offsetMin = Vector2.zero; footerRt.offsetMax = new Vector2(0, Foot);
            var dots = U.Rt(foot.transform, "progress"); dots.StretchX(1, -12, 24, 280, 280); U.HBox(dots, 12, null, TextAnchor.MiddleCenter, false, false, false, false);
            for (int i = 0; i < Count; i++) U.Dot(dots, i <= page ? Theme.Accent : Theme.ParchmentDark, i == page ? 18 : 12).Le(18, 18);
            string nextText = page == Count - 1 ? storyOnly ? StoryScript.Pick("BACK TO THE GUILD", "KEMBALI KE GUILD") : StoryScript.Begin : page == 2 ? StoryScript.Pick("PREPARE YOUR GUILD", "SIAPKAN GUILDMU") : StoryScript.Pick("CONTINUE", "LANJUTKAN");
            U.Btn(foot.transform, nextText, Theme.Accent, Next, 700, 90, 36, null, Theme.TextDark).StretchX(.5f, -12, 90, page > 0 ? 140 : 32, 32);
            if (page > 0) U.IconBtn(foot.transform, Icons.Get("arrow_l"), Theme.Panel, () => ChangePage(page - 1), 88).At(0, .5f, 28, -12, 88, 88, 0, .5f);
        }
        void Next()
        {
            if (Dirty) return;
            if (page < Count - 1) { ChangePage(page + 1); Audio.Play("swoosh"); return; }
            if (storyOnly) { App.I.GoTitle(); return; }
            if (string.IsNullOrWhiteSpace(guildName)) { Toasts.Show(L.T("Enter a guild name")); return; }
            App.I.BeginNewGame(guildName.Trim(), emblem, starter);
        }
        void StoryPage(RectTransform stage)
        {
            var beat = StoryScript.Prologue[page]; Canvas.ForceUpdateCanvases();
            float h = stage.rect.height; if (h < 200) h = UiHost.CanvasSize.y - Foot - 140;
            float artH = Mathf.Min(stage.rect.width * 2f / 3f + 20, h * .55f);
            const float cardH = 560;
            float topSpace = Mathf.Max(16, (h - artH - cardH - 24) * .5f);
            var wash = GeneratedArt.Cover(UiHost.I.back, beat.ArtKey); wash.color = new Color(1, 1, 1, .18f);
            var art = U.Rt(stage, "illustration"); art.StretchX(1, -topSpace, artH);
            GeneratedArt.Cover(U.Rt(art.transform, "illustration").Fill(10, 10, 10, 10), beat.ArtKey, true);
            UButton playback = null;
            playback = U.Btn(art.transform, auto ? StoryScript.Pick("AUTO ON", "OTOMATIS") : StoryScript.Pick("AUTO OFF", "MANUAL"), Theme.PanelLight, () => { auto = !auto; playback.label.text = auto ? StoryScript.Pick("AUTO ON", "OTOMATIS") : StoryScript.Pick("AUTO OFF", "MANUAL"); }, 190, 58, 24);
            playback.At(1, 1, -24, -24, 240, 84, 1, 1);
            var card = U.Box(stage, Theme.PanelLight, Theme.Gold, "narration_card", false, .08f); card.StretchX(1, -topSpace - artH - 24, Mathf.Min(cardH, h - artH - 40));
            var sc = MakeScroll(U.Rt(card.transform, "reading_area").Fill(24, 24, 20, 16), 14, new RectOffset(8, 8, 8, 14)); var col = sc.content;
            U.Txt(col, beat.Kicker, 30, Theme.Gold, TextAnchor.MiddleCenter).Le(-1, 40);
            U.Txt(col, beat.Title, 58, Theme.TextDark, TextAnchor.MiddleCenter).Le(-1, 106);
            narration = U.Txt(col, "", 36, Theme.Text, TextAnchor.MiddleCenter, false, true, "narration"); narration.Le(-1, 176); fullNarration = beat.Body;
            U.Txt(col, beat.Quote, 32, Theme.Gold, TextAnchor.MiddleCenter, false, true).Le(-1, 116);
        }
        void Heading(RectTransform col, string title, string body)
        {
            U.Txt(col, title, 58, Theme.TextDark, TextAnchor.MiddleCenter).Le(-1, 160);
            U.Txt(col, body, 34, Theme.TextDim, TextAnchor.MiddleCenter, false, true).Le(-1, 160);
        }
        void LoopPage(RectTransform stage)
        {
            var sc = MakeScroll(stage, 20, new RectOffset(8, 8, 4, 26)); var col = sc.content;
            Heading(col, StoryScript.Pick("A Little Guild. Big Possibilities.", "Guild Kecil. Peluang Besar."), StoryScript.Pick("Three simple steps. A world of choices. Maren will guide your first mission.", "Tiga langkah sederhana. Banyak pilihan. Maren akan memandu misi pertamamu."));
            string[] icons = { "heroes", "board", "bag" };
            string[] names = { StoryScript.Pick("Hire one companion", "Rekrut satu teman"), StoryScript.Pick("Win your first mission", "Menangkan misi pertama"), StoryScript.Pick("Equip your rewards", "Pasang perlengkapan") };
            string[] desc = {
                L.T("Hall") + " → " + L.T("Tavern") + " → " + L.T("Hire") + ". " + StoryScript.Pick("You have 600 gold and one equipped hero. Hire one affordable companion, then claim the guide reward.", "Ada 600 emas dan satu pahlawan bergear. Rekrut satu teman terjangkau, lalu klaim hadiah panduan."),
                L.T("Board") + " → " + L.T("Campaign") + " → " + L.T("Sunmeadow") + " → " + L.T("Slime Meadow") + ". " + StoryScript.Pick("Select both heroes, choose Balanced, then tap ", "Pilih kedua pahlawan, pakai Seimbang, lalu tekan ") + PartyPopup.FightLabel + ".",
                L.T("Bag") + " → " + L.T("Gear") + " → " + L.T("Auto equip") + ". " + StoryScript.Pick("Equip the mission rewards. Better gear raises your power. Maren's guide shows the next button.", "Pasang hadiah misi. Gear lebih baik meningkatkan kekuatan. Panduan Maren menunjukkan tombol berikutnya.") };
            for (int i = 0; i < 3; i++)
            {
                var box = U.Box(col, Theme.PanelLight, Theme.Gold, "guide_step", false, .08f); box.Le(-1, 390);
                U.Icon(box.transform, Icons.Get(icons[i]), 100).At(0, .5f, 22, 0, 100, 100, 0, .5f);
                U.Txt(box.transform, (i + 1).ToString("00") + "  " + names[i], 42, Theme.TextDark, TextAnchor.MiddleCenter).StretchX(1, -20, 112, 146, 24);
                U.Txt(box.transform, desc[i], 34, Theme.TextDim, TextAnchor.MiddleCenter, false, true).StretchX(1, -142, 228, 146, 24);
            }
            var hint = U.Box(col, Theme.Inset, Theme.Green, "tactic_hint", false, .06f); hint.Le(-1, 216);
            U.Txt(hint.transform, StoryScript.Pick("Choose a battle plan: Balanced, Assault, or Guard. Heroes use their skills automatically; your preparation makes the difference.", "Pilih strategi: Seimbang, Serbu, atau Bertahan. Pahlawan memakai skill otomatis; persiapanmu menentukan hasilnya."), 32, Theme.Text, TextAnchor.MiddleCenter, false, true).Fill(26, 26, 16, 16);
        }
        void CreatePage(RectTransform stage)
        {
            var sc = MakeScroll(stage, 16, new RectOffset(8, 8, 4, 30)); var col = sc.content; Heading(col, StoryScript.CharterTitle, StoryScript.CharterBody);
            var supplies = U.Box(col, Theme.Inset, Theme.Green, "starting_supplies", false, .06f); supplies.Le(-1, 216);
            U.Txt(supplies.transform, StoryScript.Pick("READY TO BEGIN: 600 gold · 1 equipped hero · 5 healing potions. Your first task is to hire a companion at the Tavern.", "BEKAL AWAL: 600 emas · 1 pahlawan bergear · 5 ramuan penyembuh. Tugas pertama: rekrut teman di Kedai."), 32, Theme.Text, TextAnchor.MiddleCenter, false, true).Fill(24, 24, 16, 16);
            U.Txt(col, L.T("Guild name"), 34, Theme.Gold, TextAnchor.MiddleCenter).Le(-1, 46);
            var field = U.Box(col, Theme.PanelLight, Theme.Gold, "guild_name", true, .08f); field.Le(-1, 92);
            var input = field.gameObject.AddComponent<InputField>();
            var txt = U.Txt(field.transform, guildName, 40, Theme.TextDark, TextAnchor.MiddleCenter); txt.Fill(26, 26, 16, 16);
            var ph = U.Txt(field.transform, L.T("Enter name..."), 40, Theme.TextDim, TextAnchor.MiddleCenter); ph.Fill(26, 26, 16, 16);
            input.textComponent = txt; input.placeholder = ph; input.text = guildName; input.characterLimit = 16; input.lineType = InputField.LineType.SingleLine; input.onValueChanged.AddListener(v => guildName = v);
            U.Txt(col, L.T("Banner colour"), 34, Theme.Gold, TextAnchor.MiddleCenter).Le(-1, 46);
            var flags = U.Rt(col, "banners"); flags.Le(-1, 110); U.HBox(flags, 12, null, TextAnchor.MiddleCenter, true, true, true, false);
            for (int i = 0; i < GuildGame.Emblems.Length; i++)
            {
                int k = i; var box = U.Box(flags, i == emblem ? Theme.Accent : Theme.PanelLight, Theme.Gold, "banner", true, .08f); box.Le(-1, 100, 1);
                var btn = box.gameObject.AddComponent<UButton>(); btn.bg = box; btn.noTint = true; btn.onClick = () => { emblem = k; Audio.Play("equip"); Redraw(); };
                Brand.ShieldImg(box.transform, Theme.Hex(GuildGame.Emblems[i]), 72).At(.5f, .5f, 0, 0, 62, 72);
            }
            U.Txt(col, L.T("Choose your first hero"), 34, Theme.Gold, TextAnchor.MiddleCenter).Le(-1, 46);
            U.Txt(col, StoryScript.StarterHint, 32, Theme.TextDim, TextAnchor.MiddleCenter, false, true).Le(-1, 128);
            foreach (var cid in new[] { "warrior", "archer", "mage" })
            {
                string id = cid; var cls = App.Ctx.Content.Class(id); bool sel = starter == id;
                var card = U.Box(col, sel ? Theme.Hex("#e4efd5") : Theme.PanelLight, sel ? Theme.GreenDark : Theme.Gold, "starter", true, .08f); card.Le(-1, 260);
                var btn = card.gameObject.AddComponent<UButton>(); btn.bg = card; btn.noTint = true; btn.onClick = () => { starter = id; Audio.Play("equip"); Redraw(); };
                int skin = id == "warrior" ? 0 : id == "archer" ? 6 : 2;
                var portrait = U.Img(card.transform, Art.Chara(skin, 0, 1), Color.white).At(0, .5f, 30, 0, 96, 120, 0, .5f);
                SpriteAnim.On(portrait, Art.FrontFrames("chara_" + skin), 4, true);
                U.Txt(card.transform, L.T(cls.name), 44, Theme.TextDark, TextAnchor.MiddleCenter).StretchX(1, -18, 56, 150, 72);
                U.Txt(card.transform, L.T(cls.role), 32, Theme.Gold, TextAnchor.MiddleCenter).StretchX(1, -96, 56, 150, 72);
                U.Txt(card.transform, StoryScript.Pick(id == "warrior" ? "Hold the front. Protect the party." : id == "archer" ? "Swift shots. Pick off threats." : "Powerful spells. Strike whole groups.", id == "warrior" ? "Jaga garis depan. Lindungi tim." : id == "archer" ? "Panah cepat. Habisi ancaman." : "Sihir kuat. Serang kelompok musuh."), 32, Theme.TextDim, TextAnchor.MiddleCenter).StretchX(0, 18, 92, 150, 72);
                if (sel) U.Icon(card.transform, Icons.Get("check"), 42).At(1, 1, -20, -24, 42, 42, 1, 1);
            }
        }
    }
}
