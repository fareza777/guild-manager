using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    public static class Vfx
    {
        static Sprite slash, orb, ring;
        public static Sprite Slash
        {
            get
            {
                if (slash == null)
                {
                    var pc = new PixelCanvas(24, 24);
                    for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++)
                        {
                            float dx = x + 0.5f - 6, dy = y + 0.5f - 12; float r = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                            if (r < 17 && r > 11 - (1 - Mathf.Abs(a) / 70f) * 6.5f && Mathf.Abs(a) < 70) pc.Px(x, y, Color.white);
                        }
                    slash = pc.ToSprite(24f);
                }
                return slash;
            }
        }
        public static Sprite Ring { get { if (ring == null) { var pc = new PixelCanvas(32, 32); pc.Ring(16, 16, 15f, 11f, Color.white); ring = pc.ToSprite(32f); } return ring; } }
        public static Sprite Orb { get { if (orb == null) { var pc = new PixelCanvas(12, 12); pc.Disc(6, 6, 5.5f, new Color32(255, 255, 255, 255)); pc.Disc(6, 6, 3.2f, new Color32(255, 255, 255, 255)); orb = pc.ToSprite(12f); } return orb; } }
        public static Color Tint(string vfx) { switch (vfx) { case "fire": return new Color(1f, 0.55f, 0.15f); case "bolt": return new Color(0.5f, 0.9f, 1f); case "dark": return new Color(0.7f, 0.35f, 1f); case "heal": return new Color(0.45f, 1f, 0.55f); case "bite": return new Color(1f, 0.35f, 0.3f); case "arrow": return new Color(1f, 0.95f, 0.7f); default: return Color.white; } }
    }

    public class BattleScreen : UiScreen, IFullScreenPage
    {
        class BUnit
        {
            public Combatant c; public RectTransform root; public Image body; public SpriteAnim anim; public U.Bar hp; public Image shadow; public Text label; public RectTransform status;
            public Vector2 home; public bool hero, dead; public float height = 120; public Sprite[] idle, atk; public bool flip; public double curHp; public int dir => hero ? 1 : -1; public Tw.H bob;
        }

        readonly MissionDef m; readonly List<UnitInstance> party; readonly BattleDoctrine doctrine;
        BattleSetup setup; BattleResult res; readonly Dictionary<int, BUnit> units = new Dictionary<int, BUnit>(); readonly Dictionary<int, double> startHp = new Dictionary<int, double>();
        RectTransform arena, bottom; Vector2 asz; Text waveT, logT, skillT, roundT, missionT, planT, speedLabel; UButton skipButton;
        readonly List<UButton> speedBtns = new List<UButton>(); readonly List<Text> heroRoleT = new List<Text>(); int displayedWave;
        readonly List<Image> heroCards = new List<Image>(); int heroActions;
        static readonly Dictionary<Sprite, float> visibleSpriteTops = new Dictionary<Sprite, float>();
        U.Bar[] heroBars; Text[] heroHpT; Coroutine co; bool skip, finished, paused; int speed;
        UButton pauseButton;
        public bool IsPaused => paused;
        public int DisplayedActions => heroActions;
        GameContext C => App.Ctx;
        public override string Id => "Battle";

        public BattleScreen(MissionDef mission, List<UnitInstance> p, BattleDoctrine plan = BattleDoctrine.Balanced)
        {
            m = mission; party = p; doctrine = plan; speed = Mathf.Clamp(Prefs.BattleSpeed, 1, 3);
            setup = C.Missions.BuildSetup(m, party, doctrine: doctrine);
            for (int i = 0; i < setup.heroes.Count; i++) startHp[i] = setup.heroes[i].hp;
            res = BattleSim.Run(setup);
        }
        public override bool OnBack() { Toasts.Show(L.T("Tap SKIP to finish the battle")); return true; }

        public void RefreshLanguage()
        {
            if (missionT) missionT.text = W.MissionName(m.name);
            if (planT) planT.text = L.T("Battle plan") + ": " + L.T(doctrine.ToString());
            if (waveT && displayedWave > 0) waveT.text = L.T("Wave {0}/{1}", displayedWave, setup.waves.Count);
            if (roundT) roundT.text = L.T("Round {0}", Mathf.Max(1, Mathf.CeilToInt(heroActions / (float)Mathf.Max(1, party.Count))));
            if (speedLabel) speedLabel.text = L.T("Speed");
            if (skipButton && skipButton.label) skipButton.label.text = L.T("SKIP");
            if (pauseButton && pauseButton.label) pauseButton.label.text = L.T(paused ? "Resume" : "Pause");
            for (int i = 0; i < heroRoleT.Count && i < party.Count; i++)
            {
                var u = party[i]; var cls = C.Content.Class(u.classId);
                if (heroRoleT[i]) heroRoleT[i].text = L.T(cls?.name ?? u.classId);
            }
            foreach (var unit in units.Values) if (unit.label) FitUnitName(unit);
        }

        public override void OnEnter()
        {
            Audio.PlayMusic("battle"); Screen.sleepTimeout = SleepTimeout.NeverSleep;
            speed = Mathf.Clamp(Prefs.BattleSpeed, 1, 3); Tw.TimeScale = speed;
            co = App.I.StartCoroutine(Play());
        }
        public override void OnExit()
        {
            if (co != null && App.I) App.I.StopCoroutine(co);
            Tw.TimeScale = 1; Screen.sleepTimeout = SleepTimeout.SystemSetting; Audio.PlayMusic("town"); U.Clear(UiHost.I.back);
        }

        // =============================== build ===============================
        public override void Build(RectTransform root)
        {
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            var size = root.rect.size; if (size.y < 100) size = new Vector2(1080, 1920);
            var back = UiHost.I.back; U.Clear(back); U.Solid(back, Theme.Bg0, "bg").Fill();
            float topH = 146, botH = 424;
            asz = new Vector2(size.x, size.y - topH - botH);
            // A compact mission header leaves the painted arena as the main stage.
            var top = U.Solid(root, Theme.PanelLight, "top"); top.At(0.5f, 1, 0, 0, size.x, topH, 0.5f, 1);
            U.Solid(top.transform, Color.Lerp(Theme.PanelLight, Theme.Gold, .35f), "rule").StretchX(0, 0, 2);
            var nm = U.Txt(top.transform, W.MissionName(m.name), 40, Theme.TextDark, TextAnchor.MiddleLeft); nm.StretchX(1, -12, 48, 28, 284);
            missionT = nm;
            var waveChip = U.Box(top.transform, Color.Lerp(Theme.PanelLight, Theme.Blue, .12f), null, "wave_chip", false, .04f);
            waveChip.At(1, 1, -24, -12, 232, 64, 1, 1);
            waveT = U.Txt(waveChip.transform, "", 30, Theme.BlueDark, TextAnchor.MiddleCenter); waveT.Fill(12, 12, 4, 4);
            planT = U.Txt(top.transform, L.T("Battle plan") + ": " + L.T(doctrine.ToString()), 28, Theme.TextDim, TextAnchor.MiddleLeft); planT.StretchX(0, 12, 38, 28, 284);
            roundT = U.Txt(top.transform, L.T("Round {0}", 1), 28, Theme.TextDim, TextAnchor.MiddleRight); roundT.At(1, 0, -28, 12, 232, 38, 1, 0);
            // arena
            arena = U.Rt(root, "arena"); arena.At(0.5f, 1, 0, -topH, asz.x, asz.y, 0.5f, 1); arena.gameObject.AddComponent<RectMask2D>();
            string bgName = !string.IsNullOrEmpty(m.bg) ? m.bg : (C.Content.RegionMap.TryGetValue(m.region, out var rg) ? rg.bg : "PlainA");
            string paintedArena = m.region == "r1" || m.region == "r2" ? "battle_meadow" : "battle_ruins";
            if (GeneratedArt.Get(paintedArena)) GeneratedArt.Cover(arena, paintedArena);
            else
            {
                float sc = Mathf.Max(asz.x / 320f, asz.y / 240f);
                var bg = U.Img(arena, Art.Bg(bgName), Color.white, "bg"); bg.At(0.5f, 0.5f, 0, 0, 320 * sc, 240 * sc);
            }
            // One shallow roster and control strip replaces the tall stack of hero cards.
            bottom = U.Solid(root, Theme.PanelLight, "bottom").R(); bottom.At(0.5f, 0, 0, 0, size.x, botH, 0.5f, 0);
            U.Solid(bottom, Color.Lerp(Theme.PanelLight, Theme.Gold, .35f), "rule").StretchX(1, 0, 2);
            BuildBottom(size.x, botH);
            // banner texts
            skillT = U.Txt(arena, "", 46, Theme.TextDark, TextAnchor.MiddleCenter); skillT.StretchX(1, -34, 72, 36, 36); skillT.gameObject.SetActive(false);
            var skillOutline = skillT.gameObject.AddComponent<Outline>(); skillOutline.effectColor = new Color(1f, .98f, .88f, .9f); skillOutline.effectDistance = new Vector2(2, -2);
            var logPlate = U.Box(arena, Theme.PanelLight, Color.Lerp(Theme.PanelLight, Theme.Gold, .25f), "combat_log", false, .04f);
            logPlate.At(.5f, 0, 0, 16, Mathf.Min(920, asz.x - 72), 80, .5f, 0);
            logT = U.Txt(logPlate.transform, "", 30, Theme.TextDark, TextAnchor.MiddleCenter); logT.Fill(20, 20, 7, 7);
        }

        void BuildBottom(float w, float h)
        {
            speedBtns.Clear(); heroCards.Clear(); heroRoleT.Clear();
            var ctl = U.Rt(bottom, "ctl"); ctl.At(0.5f, 0, 0, 16, w - 48, 100, 0.5f, 0); U.HBox(ctl, 12, null, TextAnchor.MiddleCenter, true, true, false, true);
            for (int i = 1; i <= 3; i++) { int sp = i; var b = U.Btn(ctl, "x" + i, sp == speed ? Theme.Accent : Theme.Inset, () => SetSpeed(sp), 108, 96, 32); b.Le(108, 96); speedBtns.Add(b); }
            var spacer = U.Rt(ctl, "sp"); spacer.Le(-1, 10, 1);
            pauseButton = U.Btn(ctl, L.T("Pause"), Theme.Panel, TogglePause, 220, 96, 32); pauseButton.Le(220, 96);
            Hud.Reg("btn_pause", pauseButton);
            var sk = U.Btn(ctl, L.T("SKIP"), Theme.BlueDark, () => { skip = true; paused = false; Tw.TimeScale = speed; }, 220, 96, 32, Icons.Get("arrow_r")); sk.Le(220, 96);
            skipButton = sk;
            Hud.Reg("btn_skip", sk);
            // Sideways scrolling keeps longer names and roles readable in larger parties.
            RectTransform row;
            float cardW = Mathf.Min(440, (w - 48 - 12 * Mathf.Max(0, party.Count - 1)) / Mathf.Max(1, party.Count));
            if (party.Count <= 3)
            {
                row = U.Rt(bottom, "party"); row.At(.5f, 1, 0, -14, w - 48, 280, .5f, 1);
                U.HBox(row, 12, null, TextAnchor.MiddleCenter, true, true, false, true);
            }
            else
            {
                var roster = U.ScrollH(bottom, 12, new RectOffset(0, 0, 0, 0), "party_strip");
                roster.sr.R().At(.5f, 1, 0, -14, w - 48, 280, .5f, 1); row = roster.content; cardW = 344;
                roster.sr.movementType = ScrollRect.MovementType.Clamped;
            }
            heroBars = new U.Bar[party.Count]; heroHpT = new Text[party.Count];
            for (int i = 0; i < party.Count; i++)
            {
                var u = party[i]; var card = U.Img(row, HeroPlate(i, false), Color.white, "hero_status"); card.Le(cardW, 280); heroCards.Add(card);
                var pic = W.Portrait(card.transform, u.sprite, 70); pic.transform.parent.GetComponent<RectTransform>().At(0, 0, 10, 36, 66, 70, 0, 0);
                U.Txt(card.transform, $"Lv{u.level}", 28, Theme.Gold, TextAnchor.MiddleCenter).At(0, 0, 8, 112, 82, 46, 0, 0);
                var n = U.Txt(card.transform, u.name, 32, Theme.TextDark, TextAnchor.MiddleCenter); n.StretchX(1, -10, 112, 12, 12);
                var cls = C.Content.Class(u.classId);
                var role = U.Txt(card.transform, L.T(cls?.name ?? u.classId), 28, Theme.TextDim, TextAnchor.MiddleCenter); role.StretchX(1, -136, 48, 98, 12);
                heroRoleT.Add(role);
                var hb = U.ProgressBar(card.transform, Theme.Green, 200, 16); hb.back.R().StretchX(0, 16, 16, 18, 18); heroBars[i] = hb;
                var ht = U.Txt(card.transform, "", 28, Theme.TextDim, TextAnchor.MiddleCenter); ht.StretchX(0, 48, 46, 98, 12); heroHpT[i] = ht;
                SetHeroBar(i, startHp[i], setup.heroes[i].maxHp);
            }
        }

        Sprite HeroPlate(int index, bool active, bool knockedOut = false)
        {
            var fill = knockedOut ? Theme.GreyDark : Color.Lerp(Theme.PanelLight, active ? Theme.Green : Theme.Inset, active ? .25f : .18f);
            var tint = knockedOut ? Theme.RedDark : active ? Theme.Gold : Art.RarityColor(C, party[index].rarity);
            return Boxes.Get(fill, Color.Lerp(fill, tint, .35f), .04f);
        }

        void SetSpeed(int s) { speed = s; Prefs.BattleSpeed = s; Tw.TimeScale = paused ? 0 : s; for (int i = 0; i < speedBtns.Count; i++) { speedBtns[i].SetColor(i + 1 == s ? Theme.Accent : Theme.PanelLight); } }
        void TogglePause()
        {
            if (finished || skip) return;
            paused = !paused; Tw.TimeScale = paused ? 0 : speed;
            if (pauseButton && pauseButton.label) pauseButton.label.text = L.T(paused ? "Resume" : "Pause");
        }
        void SetHeroBar(int i, double hp, double max)
        {
            if (i < 0 || i >= heroBars.Length || heroBars[i] == null) return;
            float f = (float)Math.Max(0, hp / Math.Max(1, max)); heroBars[i].Value = f;
            heroBars[i].fill.sprite = Boxes.Get(f < 0.3f ? Theme.Red : f < 0.6f ? Theme.Warn : Theme.Green, null, 0.35f);
            heroHpT[i].text = $"{Util.Num(Math.Max(0, hp))}/{Util.Num(max)}";
            if (hp <= 0) heroHpT[i].text = L.T("KO");
            if (hp <= 0 && i < heroCards.Count) heroCards[i].sprite = HeroPlate(i, false, true);
        }

        // =============================== unit views ===============================
        Vector2 SlotPos(bool hero, int index, int count, bool front)
        {
            float H = asz.y, W_ = asz.x;
            if (hero)
            {
                float x = front ? 0.34f : 0.18f;
                float y = Mathf.Lerp(0.20f, 0.53f, count <= 1 ? 0.5f : index / (float)(count - 1));
                return new Vector2(x * W_, y * H);
            }
            else
            {
                float x = index % 2 == 0 ? 0.68f : 0.84f;
                float y = count <= 3 ? Mathf.Lerp(.37f, .66f, count <= 1 ? .5f : index / (float)(count - 1)) :
                    Mathf.Lerp(.35f, .65f, (index / 2) / (float)Mathf.Max(1, Mathf.CeilToInt(count / 2f) - 1)) + (index % 2) * .035f;
                if (count == 1) { x = .72f; y = .51f; }
                return new Vector2(x * W_, y * H);
            }
        }

        BUnit MakeUnit(Combatant c, Vector2 pos, bool hero)
        {
            var u = new BUnit { c = c, hero = hero, home = pos, curHp = hero ? c.hp : c.maxHp };
            u.root = U.Rt(arena, "u" + c.id); u.root.anchorMin = u.root.anchorMax = Vector2.zero; u.root.pivot = new Vector2(0.5f, 0); u.root.anchoredPosition = pos; u.root.sizeDelta = new Vector2(10, 10);
            u.shadow = U.Img(u.root, Boxes.Glow, new Color(.18f, .24f, .17f, .26f), "shadow"); u.shadow.At(0.5f, 0, 0, 0, 150, 46, 0.5f, 0.5f);
            u.body = U.Img(u.root, null, Color.white, "body"); u.body.At(0.5f, 0, 0, 0, 10, 10, 0.5f, 0);
            string key = c.sprite;
            if (hero && !c.monster)
            {
                int skin = Art.SkinIndex(key);
                u.idle = new[] { Art.Chara(skin, 2, 0), Art.Chara(skin, 2, 1), Art.Chara(skin, 2, 2), Art.Chara(skin, 2, 1) };
                u.atk = new[] { Art.Arpg(c.anim, skin, 2, 0), Art.Arpg(c.anim, skin, 2, 1), Art.Arpg(c.anim, skin, 2, 2), Art.Arpg(c.anim, skin, 2, 1) };
                u.anim = SpriteAnim.On(u.body, u.idle, 3, true, 6.5f); u.height = 144;
            }
            else if (key.StartsWith("bat:"))
            {
                var s = Art.Get(key); float target = Mathf.Clamp(s.rect.height * 3.1f, 180, 280) * (hero ? 1f : c.scale);
                float sc = target / s.rect.height; u.idle = new[] { s }; u.anim = SpriteAnim.On(u.body, u.idle, 1, true, sc); u.height = target; u.flip = hero;
                u.bob = Tw.To(1.6f, k => { if (u.body) u.body.rectTransform.localScale = new Vector3(u.flip ? -1 : 1, 1 + 0.025f * k, 1); }, Ease.InOutSine, UnityEngine.Random.value, null, u.body, true, true);
            }
            else
            {
                var sh = key.Substring(4); int row = sh.StartsWith("ADD") ? 0 : 1;
                u.idle = new[] { Art.Monster(sh, row, 0), Art.Monster(sh, row, 1), Art.Monster(sh, row, 2), Art.Monster(sh, row, 1) };
                float sc = 4.4f * c.scale * (c.boss ? 1.1f : 1f);
                u.anim = SpriteAnim.On(u.body, u.idle, 4.5f, true, sc); u.height = 48 * sc * 0.68f;
            }
            u.anim.battleTime = true;
            u.anim.Show(u.idle[0]);
            u.height = VisibleHeight(u.idle, u.anim.scale, u.height);
            // Leave space above large bosses for their labels and rising combat feedback.
            pos.y = Mathf.Clamp(pos.y, 140, Mathf.Max(140, asz.y - u.height - 310));
            u.home = pos; u.root.anchoredPosition = pos;
            u.root.sizeDelta = new Vector2(312f, u.height + 120f);
            if (u.flip) u.body.rectTransform.localScale = new Vector3(-1, 1, 1);
            float sw = Mathf.Max(100, u.height * 0.8f);
            u.shadow.R().sizeDelta = new Vector2(Mathf.Clamp(sw, 100, 220), 40);
            u.hp = U.ProgressBar(u.root, hero ? Theme.Green : Theme.Red, 120, 16); u.hp.back.R().At(0.5f, 0, 0, u.height + 8, 120, 16, 0.5f, 0);
            u.hp.Value = (float)(u.curHp / c.maxHp);
            if (!hero && c.boss) { u.hp.back.R().sizeDelta = new Vector2(180, 20); }
            var nameTint = hero ? Theme.GreenDark : Theme.RedDark;
            var nameFill = Color.Lerp(Theme.PanelLight, hero ? Theme.Green : Theme.Red, .09f);
            var nameplate = U.Box(u.root, nameFill, Color.Lerp(nameFill, nameTint, .24f), "nameplate", false, .04f);
            nameplate.At(.5f, 0, 0, u.height + 26f, 220f, 64f, .5f, 0);
            nameplate.gameObject.AddComponent<RectMask2D>();
            U.Solid(nameplate.transform, nameTint, "team_mark").At(0, .5f, 0, 0, 3, 28, 0, .5f);
            u.label = U.Txt(nameplate.transform, c.name, hero ? 30 : 28, Theme.TextDark, TextAnchor.MiddleCenter); u.label.Fill(12, 12, 4, 4);
            FitUnitName(u);
            units[c.id] = u; return u;
        }

        static float VisibleHeight(Sprite[] frames, float scale, float fallback)
        {
            float height = 0;
            foreach (var frame in frames)
            {
                if (!frame || !frame.texture || !frame.texture.isReadable) return fallback;
                if (!visibleSpriteTops.TryGetValue(frame, out var top))
                {
                    var rect = frame.rect; var pixels = frame.texture.GetPixels32();
                    int x0 = Mathf.RoundToInt(rect.x), y0 = Mathf.RoundToInt(rect.y);
                    int width = Mathf.RoundToInt(rect.width), rows = Mathf.RoundToInt(rect.height);
                    top = 0;
                    for (int y = rows - 1; y >= 0 && top <= 0; y--)
                        for (int x = 0; x < width; x++)
                            if (pixels[(y0 + y) * frame.texture.width + x0 + x].a > 20)
                            { top = y + 1 - frame.pivot.y; break; }
                    visibleSpriteTops[frame] = top;
                }
                height = Mathf.Max(height, top * scale);
            }
            return height > 0 ? height : fallback;
        }

        void FitUnitName(BUnit u)
        {
            u.label.text = u.hero ? u.c.name.Split(' ')[0] : L.T(u.c.name);
            float width = Mathf.Clamp(u.label.preferredWidth + 28, 124, 360);
            u.label.transform.parent.R().sizeDelta = new Vector2(width, 64);
        }

        // =============================== playback ===============================
        IEnumerator Wait(float s) { float t = 0; while (t < s || paused) { if (skip) yield break; if (!paused) t += Time.unscaledDeltaTime * speed; yield return null; } }

        IEnumerator Play()
        {
            yield return null;
            int wavesTotal = setup.waves.Count;
            // heroes enter
            var fronts = setup.heroes.Where(h => h.front).ToList(); var backs = setup.heroes.Where(h => !h.front).ToList();
            for (int i = 0; i < setup.heroes.Count; i++)
            {
                var h = setup.heroes[i]; bool fr = h.front; var group = fr ? fronts : backs; int idx = group.IndexOf(h);
                var pos = SlotPos(true, i, setup.heroes.Count, fr);
                // stagger columns so they don't overlap: alternate front/back x
                if (fronts.Count == 0 || backs.Count == 0) pos.x = (i % 2 == 0 ? .34f : .18f) * asz.x;
                var u = MakeUnit(h, pos, true); pos = u.home;
                u.root.anchoredPosition = pos + new Vector2(-300, 0); Tw.Move(u.root, u.root.anchoredPosition, pos, 0.5f, Ease.OutCubic, i * 0.08f);
                u.hp.Value = (float)(startHp[i] / h.maxHp); u.curHp = startHp[i];
            }
            yield return Wait(0.6f);
            foreach (var ev in res.log)
            {
                if (skip) break;
                switch (ev.type)
                {
                    case "wave": yield return DoWave(ev, wavesTotal); break;
                    case "act": yield return DoAct(ev); break;
                    case "tick": yield return DoTick(ev); break;
                }
            }
            if (!skip) yield return Wait(0.5f);
            Finish();
        }

        IEnumerator DoWave(BattleEvent ev, int total)
        {
            // clear previous enemies
            foreach (var u in units.Values.Where(x => !x.hero).ToList()) { if (u.root) UnityEngine.Object.Destroy(u.root.gameObject); units.Remove(u.c.id); }
            var foes = setup.waves[ev.wave];
            displayedWave = ev.wave + 1; waveT.text = L.T("Wave {0}/{1}", displayedWave, total);
            var ordered = foes.OrderByDescending(f => f.boss).ToList();
            for (int i = 0; i < foes.Count; i++)
            {
                var pos = SlotPos(false, i, foes.Count, true);
                var u = MakeUnit(foes[i], pos, false);
                pos = u.home;
                u.root.anchoredPosition = pos + new Vector2(0, 280); var cg = u.root.gameObject.AddComponent<CanvasGroup>(); Tw.Fade(cg, 0, 1, 0.25f, i * 0.08f);
                Tw.Move(u.root, pos + new Vector2(0, 280), pos, 0.38f, Ease.OutCubic, i * 0.08f);
            }
            var banner = U.Txt(arena, foes.Any(f => f.boss) ? L.T("BOSS: {0}", foes.First(f => f.boss).name) : L.T("Wave {0}", ev.wave + 1), foes.Any(f => f.boss) ? 48 : 44, foes.Any(f => f.boss) ? Theme.Bad : Theme.TextDark, TextAnchor.MiddleCenter, true);
            banner.StretchX(0.78f, 0, 100, 32, 32); Tw.Pop(banner.transform, 0.35f); Tw.FadeG(banner, 1, 0, 0.4f, 1.0f, () => { if (banner) UnityEngine.Object.Destroy(banner.gameObject); });
            if (foes.Any(f => f.boss)) Audio.Play("alert");
            yield return Wait(1.2f);
        }

        Combatant Find(int id) => units.TryGetValue(id, out var u) ? u.c : null;

        IEnumerator DoTick(BattleEvent ev)
        {
            foreach (var h in ev.hits) { if (units.TryGetValue(h.target, out var tu)) { Apply(tu, h, "poison"); } }
            yield return Wait(0.35f);
        }

        IEnumerator DoAct(BattleEvent ev)
        {
            if (!units.TryGetValue(ev.actor, out var a) || a.dead) yield break;
            if (a.hero) { heroActions++; roundT.text = L.T("Round {0}", Mathf.Max(1, Mathf.CeilToInt(heroActions / (float)Mathf.Max(1, party.Count)))); }
            for (int i = 0; i < heroCards.Count; i++) if (heroCards[i] && heroBars[i].Value > 0)
                heroCards[i].sprite = HeroPlate(i, a.c.unitUid == party[i].uid);
            if (ev.skill == "stunned") { Fx.FloatText(arena, a.root.anchoredPosition + new Vector2(0, a.height + 70), L.T("Stunned!"), Theme.Warn, 44, bottomOrigin: true); yield return Wait(0.4f); yield break; }
            var skill = a.c.skills.Find(s => s.id == ev.skill) ?? a.c.skills[0];
            if (skill.cd > 0) { skillT.text = L.T(skill.name); skillT.gameObject.SetActive(true); Tw.Pop(skillT.transform, 0.25f); }
            logT.text = $"{a.c.name}: {L.T(skill.name)}";
            var targets = ev.hits.Select(h => (h, units.TryGetValue(h.target, out var tu) ? tu : null)).Where(x => x.Item2 != null).ToList();
            bool support = skill.type == "heal" || skill.type == "buff";
            bool ranged = skill.vfx == "arrow" || skill.vfx == "bolt" || skill.vfx == "fire" || skill.vfx == "dark" || (support);
            bool aoe = targets.Count > 1;
            if (support)
            {
                if (a.atk != null) a.anim.Play(a.atk, 9, false, () => { if (a.anim) a.anim.Play(a.idle, 3, true); });
                Audio.Play(skill.type == "heal" ? "heal" : "magic", 0.8f);
                yield return Wait(0.25f);
                foreach (var (h, tu) in targets) { Aura(tu, Vfx.Tint("heal")); Apply(tu, h, skill.status); }
                yield return Wait(0.4f);
            }
            else if (!ranged && targets.Count > 0)
            {
                var first = targets[0].Item2;
                Vector2 from = a.home, wind = from + new Vector2(-a.dir * 36f, 0f);
                Vector2 to = new Vector2(a.home.x + (first.root.anchoredPosition.x - a.home.x) - a.dir * 150, Mathf.Lerp(a.home.y, first.root.anchoredPosition.y, 0.55f));
                if (aoe) to = new Vector2(a.home.x + a.dir * 90, a.home.y);
                Tw.Move(a.root, from, wind, 0.09f, Ease.OutQuad); yield return Wait(0.09f);
                Tw.Move(a.root, wind, to, 0.12f, Ease.OutCubic); yield return Wait(0.1f);
                if (a.atk != null) a.anim.Play(a.atk, 14, false, () => { if (a.anim) a.anim.Play(a.idle, 3, true); });
                else { Tw.Punch(a.body.transform, 0.25f, 0.2f); }
                Audio.Play("swoosh", 0.4f);
                yield return Wait(0.1f);
                foreach (var (h, tu) in targets) { SlashFx(tu, skill.vfx); Apply(tu, h, skill.status); }
                yield return Wait(0.25f);
                Tw.Move(a.root, to, from, 0.18f, Ease.OutCubic); yield return Wait(0.2f);
            }
            else if (targets.Count > 0)
            {
                if (a.atk != null) a.anim.Play(a.atk, 10, false, () => { if (a.anim) a.anim.Play(a.idle, 3, true); }); else Tw.Punch(a.body.transform, 0.2f, 0.25f);
                Audio.Play(skill.vfx == "arrow" ? "arrow" : "magic", 0.7f);
                yield return Wait(0.16f);
                var col = Vfx.Tint(skill.vfx);
                int landed = 0;
                foreach (var (h, tu) in targets)
                {
                    var th = tu; var hh = h;
                    Projectile(a, th, skill.vfx, col, () => { SlashFx(th, skill.vfx == "arrow" ? "slash" : skill.vfx); Apply(th, hh, skill.status); landed++; });
                }
                float guard = 0; while (landed < targets.Count && guard < 2f && !skip) { guard += Time.unscaledDeltaTime * speed; yield return null; }
                yield return Wait(0.2f);
            }
            skillT.gameObject.SetActive(false);
        }

        // =============================== effects ===============================
        void Projectile(BUnit a, BUnit t, string vfx, Color col, Action onHit)
        {
            var from = a.root.anchoredPosition + new Vector2(a.dir * 40, a.height * 0.62f); var to = t.root.anchoredPosition + new Vector2(0, t.height * 0.55f);
            Image img;
            if (vfx == "arrow") { img = U.Img(arena, Art.Prop("arrow_shot_RIGHT"), Color.white, "arrow"); var s = img.sprite; img.preserveAspect = true; img.Sz(s != null ? s.rect.width * 4 : 64, s != null ? s.rect.height * 4 : 16); }
            else { img = U.Img(arena, Vfx.Orb, col, "orb"); img.Sz(46, 46); var g = U.Img(img.transform, Boxes.Glow, new Color(col.r, col.g, col.b, 0.55f), "g"); g.Sz(130, 130); }
            var r = img.R(); r.anchorMin = r.anchorMax = Vector2.zero; r.anchoredPosition = from;
            float dur = Mathf.Clamp(Vector2.Distance(from, to) / 1400f, 0.18f, 0.42f);
            bool arc = vfx != "arrow";
            var aim = to - from; float ang = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            if (vfx == "arrow") r.localRotation = Quaternion.Euler(0, 0, ang);
            Tw.To(dur, k => { if (!r) return; var p = Vector2.Lerp(from, to, Ease.OutQuad(k)); if (arc) p.y += Mathf.Sin(k * Mathf.PI) * 48; r.anchoredPosition = p; r.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, k); }, Ease.Linear, 0, () => { if (r) UnityEngine.Object.Destroy(r.gameObject); onHit?.Invoke(); }, r);
        }

        void SlashFx(BUnit t, string vfx)
        {
            if (!Prefs.BattleFx) return;
            var pos = t.root.anchoredPosition + new Vector2(0, t.height * 0.5f);
            if (vfx == "slash" || vfx == "bite")
            {
                var img = U.Img(arena, Vfx.Slash, vfx == "bite" ? new Color(1, 0.4f, 0.35f) : Color.white, "slash"); img.Sz(24 * 9, 24 * 9); var r = img.R(); r.anchorMin = r.anchorMax = Vector2.zero; r.anchoredPosition = pos;
                float rot = UnityEngine.Random.Range(-35f, 35f); r.localRotation = Quaternion.Euler(0, 0, rot); if (!t.hero) r.localScale = new Vector3(-1, 1, 1);
                Tw.To(0.22f, k => { if (img) { r.localScale = new Vector3((t.hero ? 1 : -1) * (0.6f + 0.6f * k), 0.6f + 0.6f * k, 1); var c = img.color; c.a = 1 - k * k; img.color = c; } }, Ease.OutCubic, 0, () => { if (img) UnityEngine.Object.Destroy(img.gameObject); }, img);
            }
            else
            {
                var col = Vfx.Tint(vfx);
                var ring = U.Img(arena, Vfx.Ring, col, "ring"); var r = ring.R(); r.anchorMin = r.anchorMax = Vector2.zero; r.anchoredPosition = pos; r.sizeDelta = Vector2.one * 40;
                Tw.To(0.3f, k => { if (ring) { r.sizeDelta = Vector2.one * Mathf.Lerp(40, 230, k); var c = ring.color; c.a = 1 - k; ring.color = c; } }, Ease.OutCubic, 0, () => { if (ring) UnityEngine.Object.Destroy(ring.gameObject); }, ring);
                Fx.Burst(arena, pos, Boxes.White, 8, 380, col, 12, 0.45f, 500, bottomOrigin: true);
            }
        }

        void Aura(BUnit t, Color col)
        {
            var pos = t.root.anchoredPosition + new Vector2(0, t.height * 0.4f);
            Fx.Burst(arena, pos, Icons.Get("plus"), 6, 160, null, 34, 0.9f, -200, bottomOrigin: true);
            Fx.Rays(arena, pos, 260, col, bottomOrigin: true);
        }

        void Apply(BUnit t, Hit h, string status)
        {
            var pos = t.root.anchoredPosition + new Vector2(UnityEngine.Random.Range(-20f, 20f), t.height + 30);
            if (h.miss) { Fx.FloatText(arena, pos, L.T("MISS"), Theme.TextDim, 44, bottomOrigin: true); Audio.Play("miss", 0.6f); return; }
            if (h.heal > 0)
            {
                t.curHp = Math.Min(t.c.maxHp, t.curHp + h.heal); Fx.FloatText(arena, pos, "+" + h.heal, Theme.Good, 54, 140, 0.9f, true, bottomOrigin: true); UpdateBar(t);
            }
            else if (h.dmg > 0 || h.shield > 0)
            {
                t.curHp -= h.dmg; UpdateBar(t);
                string txt = h.status == "poison" && h.dmg > 0 && status == "poison" ? "-" + h.dmg : (h.crit ? h.dmg + "!" : h.dmg.ToString());
                var col = h.status == "poison" && status == "poison" ? Theme.Good : h.crit ? Theme.Gold : (t.hero ? Theme.Bad : Theme.TextDark);
                if (h.dmg > 0) Fx.FloatText(arena, pos, txt, col, h.crit ? 74 : 54, h.crit ? 190 : 140, h.crit ? 1.1f : 0.9f, true, bottomOrigin: true);
                if (h.shield > 0) Fx.FloatText(arena, pos + new Vector2(0, 50), "(" + h.shield + ")", Theme.Blue, 36, bottomOrigin: true);
                if (h.dmg > 0) { Audio.Play(h.crit ? "crit" : "hit", 0.7f); HitFlash(t); if (h.crit) Tw.Shake(arena, 14, 0.25f); }
                if (t.hero) { int i = party.FindIndex(p => p.uid == t.c.unitUid); SetHeroBar(i, t.curHp, t.c.maxHp); }
            }
            if (!string.IsNullOrEmpty(h.status) && h.status != "poison" && !h.kill)
            {
                string[] nice = { "stun", "Stunned!", "weaken", "Weakened", "atk_up", "ATK up!", "def_up", "DEF up!", "shield", "Shield!", "taunt", "Taunt!", "spd_up", "SPD up!" };
                for (int i = 0; i < nice.Length; i += 2) if (nice[i] == h.status) { var c = h.status == "stun" || h.status == "weaken" ? Theme.Warn : Theme.Blue; Fx.FloatText(arena, pos + new Vector2(0, 66), L.T(nice[i + 1]), c, 36, 100, 1.0f, bottomOrigin: true); }
            }
            if (h.kill) Die(t);
        }

        void UpdateBar(BUnit t)
        {
            float f = (float)Math.Max(0, t.curHp / t.c.maxHp); t.hp.Value = f;
            t.hp.fill.sprite = Boxes.Get(t.hero ? (f < 0.3f ? Theme.Red : Theme.Green) : Theme.Red, null, 0.35f);
            if (t.hero) { int i = party.FindIndex(p => p.uid == t.c.unitUid); SetHeroBar(i, t.curHp, t.c.maxHp); }
        }

        void HitFlash(BUnit t)
        {
            var img = t.body; if (!img) return;
            img.color = new Color(1f, 0.72f, 0.66f);
            Tw.After(0.1f, () => { if (img) img.color = Color.white; }, img);
            var r = t.root; var p0 = t.home; var body = t.body.rectTransform;
            Tw.To(0.22f, k =>
            {
                if (!r) return;
                float hit = Mathf.Sin(k * Mathf.PI);
                r.anchoredPosition = p0 + new Vector2((t.hero ? -1 : 1) * 28f * hit, 6f * hit);
                body.localScale = new Vector3((t.flip ? -1f : 1f) * (1f + 0.08f * hit), 1f - 0.1f * hit, 1f);
            }, Ease.Linear, 0, () => { if (r && !t.dead) { r.anchoredPosition = p0; body.localScale = new Vector3(t.flip ? -1f : 1f, 1, 1); } }, r);
        }

        void Die(BUnit t)
        {
            if (t.dead) return; t.dead = true; t.curHp = 0;
            Audio.Play("death", 0.7f);
            t.hp.back.gameObject.SetActive(false); t.label.transform.parent.gameObject.SetActive(false);
            if (t.anim) t.anim.playing = false;
            var r = t.root; var body = t.body; var cg = r.gameObject.GetComponent<CanvasGroup>() ?? r.gameObject.AddComponent<CanvasGroup>();
            Fx.Burst(arena, r.anchoredPosition + new Vector2(0, t.height * 0.4f), Boxes.White, t.hero ? 8 : 14, 320, t.hero ? new Color(0.8f, 0.8f, 1f) : new Color(1f, 0.8f, 0.5f), 14, 0.6f, 500, bottomOrigin: true);
            Tw.To(0.45f, k => { if (!r) return; float e = Ease.OutCubic(k); cg.alpha = 1 - e; body.rectTransform.localRotation = Quaternion.Euler(0, 0, (t.hero ? -1f : 1f) * 55f * e); body.rectTransform.localScale = new Vector3(1f - 0.15f * e, 1f - 0.35f * e, 1f); r.anchoredPosition = t.home + new Vector2((t.hero ? -1f : 1f) * 18f * e, -12f * e); }, Ease.Linear, 0.05f, null, r);
            if (t.hero) { int i = party.FindIndex(p => p.uid == t.c.unitUid); SetHeroBar(i, 0, t.c.maxHp); }
        }

        // =============================== finish ===============================
        void Finish()
        {
            if (finished) return; finished = true;
            Tw.TimeScale = 1;
            var outcome = C.Missions.Complete(m, party, setup, res);
            Audio.Play(outcome.win ? "win" : "lose");
            if (App.I) App.I.SaveNow();
            Popups.Show(new ResultPopup(outcome, m, party, () => App.I.nav.Pop()));
        }
    }

    // =====================================================================================
    public class ResultPopup : Popup
    {
        readonly MissionOutcome o; readonly MissionDef m; readonly List<UnitInstance> party; readonly Action onBack; bool starsDone;
        readonly string battleKey = Guid.NewGuid().ToString("N");
        bool adBattleRecorded, lastAdReady, leaving;
        public ResultPopup(MissionOutcome outcome, MissionDef mission, List<UnitInstance> p, Action back) { o = outcome; m = mission; party = p; onBack = back; }
        GameContext C => App.Ctx;
        public override string Title => o.win ? L.T("VICTORY!") : L.T("DEFEAT");
        public override float Height => 0;
        public override bool CloseOnDim => false;
        public override bool ShowClose => false;
        public override bool HasFooter => true;
        public override Color Tint => o.win ? Theme.Panel : Theme.PanelDark;
        public override bool OnBack() { Leave(); return true; }

        public override void OnShown()
        {
            if (!adBattleRecorded) { adBattleRecorded = true; App.I.Ads.BattleFinished(battleKey, o.win && m.kind == "battle" && m.region != "tower"); }
            TitleText.color = o.win ? Theme.Gold : Theme.Bad;
            if (o.win) Fx.Rays(Panel, new Vector2(0, 600), 1100, Theme.Gold);
        }

        public override void Build(RectTransform body)
        {
            if (o.win)
            {
                var sr = W.Stars(body, 0, 3, 120); var starRow = sr.GetComponent<LayoutElement>(); sr.GetComponent<LayoutElement>().preferredHeight = 130; sr.GetComponent<LayoutElement>().preferredWidth = 400;
                var holder = U.Rt(body, "starsH"); holder.Le(-1, 140); sr.SetParent(holder, false); sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
                var imgs = sr.GetComponentsInChildren<Image>();
                if (!starsDone) { starsDone = true; for (int i = 0; i < o.stars && i < imgs.Length; i++) { int k = i; imgs[i].sprite = Icons.Get("star_off"); Tw.After(0.35f + 0.35f * i, () => { if (imgs[k]) { imgs[k].sprite = Icons.Get("star"); Tw.Pop(imgs[k].transform, 0.35f); Audio.Play("star", 1f, 1f + 0.15f * k); Fx.Burst(Panel, new Vector2(-130 + 130 * k, Panel.rect.height / 2 - 250), Icons.Get("star"), 6, 300, null, 26); } }, imgs[i]); } }
                else for (int i = 0; i < o.stars && i < imgs.Length; i++) imgs[i].sprite = Icons.Get("star");
                var sub = U.Txt(body, o.first ? L.T("First clear!") : L.T("Cleared in {0} rounds", o.rounds), 38, o.first ? Theme.Gold : Theme.TextDim, TextAnchor.MiddleCenter, true); sub.Le(-1, 50);
                if (o.fallen.Count == 0) { var pf = U.Txt(body, L.T("Flawless - no heroes fell!"), 30, Theme.Good, TextAnchor.MiddleCenter); pf.Le(-1, 40); }
            }
            else
            {
                U.Icon(body, Icons.Get("skull"), 140).Le(140, 140);
                W.Info(body, L.T("Your party was defeated. Train, upgrade gear, or bring a healer and try again!"), 36, Theme.Text);
            }
            // rewards
            if (o.win)
            {
                W.Header(body, L.T("Rewards"), Icons.Get("coin"));
                var res = o.res.Concat(o.drops.Where(d => d.res != null).Select(d => d.res)).ToList();
                var rr = W.RewardRow(body, res, 96, 30); var holder = U.Rt(body, "rwh"); holder.Le(-1, 156); rr.SetParent(holder, false); rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
                var gear = o.drops.Where(d => d.gear != null).ToList();
                if (gear.Count > 0)
                {
                    var grid = U.Rt(body, "gear"); U.Grid(grid, new Vector2(130, 130), new Vector2(12, 12), 5, null, TextAnchor.UpperCenter); grid.Fit();
                    foreach (var d in gear) { var cell = W.ItemCell(grid, d.gear, 130, () => { }, false); Tw.Pop(cell.transform, 0.35f, 0.4f); }
                }
            }
            // xp & level ups
            if (o.win && m.kind == "battle" && m.region != "tower" && App.I.Ads.TutorialDone)
            {
                double gold = o.res.Where(r => r.id == "gold").Sum(r => r.n);
                var ledger = C.Get<AdRewards>();
                if (gold >= 2)
                {
                    if (ledger.Claimed(battleKey)) W.Info(body, StoryScript.Pick("Video bonus claimed", "Bonus video sudah diambil"), 38, Theme.Good);
                    else
                    {
                        var bonus = U.Btn(body, StoryScript.Pick($"Watch video · +{Util.Num(Math.Floor(gold * .5))} gold", $"Tonton video · +{Util.Num(Math.Floor(gold * .5))} gold"), Theme.Green,
                            () => App.I.Ads.Watch(ledger.VictoryTicket(battleKey, gold), Redraw, Redraw), 800, 112, 40, Icons.Get("coin"));
                        bonus.Le(-1, 112); bonus.interactable = App.I.Ads.RewardReady;
                        W.Info(body, StoryScript.Pick(App.I.Ads.RewardReady ? "Optional bonus · your loot is already saved" : "Optional bonus · no video ready yet", App.I.Ads.RewardReady ? "Bonus opsional · hadiahmu sudah disimpan" : "Bonus opsional · video belum siap"), 36, Theme.TextDim);
                    }
                }
                lastAdReady = App.I.Ads.RewardReady;
            }
            W.Header(body, L.T("Heroes"), Icons.Get("heroes"));
            W.Info(body, L.T("+{0} XP each", Util.Num(o.xp)), 32, Theme.Blue);
            foreach (var u in party)
            {
                var row = U.Box(body, Theme.PanelDark, Theme.Black, "h"); row.Le(-1, 158, 1);
                var pic = W.Portrait(row.transform, u.sprite, 88); pic.transform.parent.GetComponent<RectTransform>().At(0, 0.5f, 16, -2, 80, 88, 0, 0.5f);
                var n = U.Txt(row.transform, u.name, 34, Theme.Text, TextAnchor.MiddleLeft, true); n.At(0, 1, 110, -10, 560, 56, 0, 1);
                var hb = U.ProgressBar(row.transform, C.Roster.HpFrac(u) < 0.3 ? Theme.Red : Theme.Green, 360, 22, "", 18); hb.Value = (float)C.Roster.HpFrac(u); hb.back.R().At(0, 0, 110, 16, 360, 22, 0, 0);
                var l = U.Txt(row.transform, $"Lv {u.level}", 36, Theme.Gold, TextAnchor.MiddleRight, true); l.At(1, 1, -20, -14, 190, 56, 1, 1);
                if (o.levelUps.TryGetValue(u.uid, out var up)) { var lu = U.Chip(row.transform, L.T("LEVEL UP!"), Theme.GreenDark, 26, Color.white, 14, 40); lu.At(1, 0, -20, 14, 210, 60, 1, 0); Tw.Pulse(lu.transform, 0.1f, 0.5f); }
                else if (o.fallen.Contains(u.name)) { var ko = U.Chip(row.transform, L.T("WOUNDED"), Theme.RedDark, 26, Color.white, 14, 40); ko.At(1, 0, -20, 14, 210, 60, 1, 0); }
            }
            if (o.win && C.Ranks.FameReached && !C.Ranks.ExamDone) { var tip = U.Chip(body, L.T("Rank exam unlocked! Open the Quest Board."), Theme.AccentDark, 30, Color.white, 24, 60); }
            if (o.win && C.Ranks.CanPromote) { var tip = U.Chip(body, L.T("You can be promoted! Tap your rank."), Theme.AccentDark, 30, Color.white, 24, 60); }
        }

        MissionDef NextStage()
        {
            if (!o.win || m.kind != "battle") return null;
            if (m.region == "tower" && m.id.StartsWith("tower_") && int.TryParse(m.id.Substring(6), out var f)) return C.Missions.TowerMission(f + 1);
            var next = C.Missions.InRegion(m.region).OrderBy(x => x.order).FirstOrDefault(x => x.order > m.order);
            return next != null && C.Missions.Unlocked(next) ? next : null;
        }

        public override void BuildFooter(RectTransform f)
        {
            var back = U.Btn(f, L.T("Back"), Theme.PanelLight, Leave, 200, 100, 38); back.Le(200, 100);
            var retry = U.Btn(f, L.T("Retry"), o.win ? Theme.BlueDark : Theme.Accent, () => Again(m), 200, 100, 38, Icons.Get("sword")); retry.Le(-1, 100, 1);
            var next = NextStage();
            if (next != null) { var nb = U.Btn(f, L.T("Next"), Theme.Green, () => Again(next), 200, 100, 38, Icons.Get("arrow_r")); nb.Le(-1, 100, 1); Tw.Pulse(nb.transform, 0.04f, 0.7f); }
        }

        public override void Tick() { if (lastAdReady != App.I.Ads.RewardReady) { lastAdReady = App.I.Ads.RewardReady; Redraw(); } }
        void Leave()
        {
            if (leaving || App.I.Ads.Busy) return;
            leaving = true; Close(); onBack?.Invoke(); App.I.SaveNow();
            if (o.win && m.kind == "battle" && m.region != "tower") App.I.Ads.TryInterstitialAtBreak();
        }
        void Again(MissionDef target)
        {
            var ready = party.Where(C.Roster.Ready).ToList();
            if (ready.Count == 0) { Toasts.Show(L.T("Your heroes need rest or healing first")); return; }
            Close(); onBack?.Invoke();
            App.I.nav.Update();
            Popups.Show(new PartyPopup(target));
        }
    }
}
