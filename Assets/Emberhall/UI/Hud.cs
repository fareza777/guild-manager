using System;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    /// <summary>Persistent chrome: top status bar, bottom tab bar, tutorial pointer. Tabs swap the root screen.</summary>
    public class Hud
    {
        public enum Tab { Hall, Heroes, Board, Bag, Quests }
        public static readonly Dictionary<string, RectTransform> Targets = new Dictionary<string, RectTransform>();
        public static void Reg(string key, Component c) { if (c != null) Targets[key] = (RectTransform)c.transform; }
        const float TopH = 224, NavH = 188, CoachH = 288, CoachSpace = 312;

        readonly App app; readonly GameContext C; readonly UiHost host;
        RectTransform topBar, navBar, pointer; Image pointerImg;
        RectTransform coach, coachCard; Image[] ring; Text coachStep, coachTitle, coachLine; UButton coachAction;
        Text nameT, rankT, goldT, gemsT, fameT; U.Bar fameBar; Image emblem;
        readonly Image[] tabBg = new Image[5]; readonly Image[] tabIcon = new Image[5]; readonly Text[] tabLabel = new Text[5];
        readonly Image[] tabBadge = new Image[5]; readonly Text[] tabBadgeT = new Text[5];
        double showGold, showGems; int lastQuestReady = -1;
        public Tab Current { get; private set; } = Tab.Hall;
        bool visible, coachReserved; float popupCooldown; bool dailyShown;
        float adInset;
        public void SetAdInset(float value)
        {
            adInset = value;
            SetChrome(visible && !(app.nav.Current is IFullScreenPage));
            app.nav.Current?.Redraw();
        }
        public bool FullScreenPage; // battle etc. hides chrome

        public Hud(App a)
        {
            app = a; C = a.ctx; host = a.host;
            BuildTop(); BuildNav(); BuildPointer(); BuildCoach();
            C.OnChanged += () => dirty = true;
            L.Changed += RefreshLanguage;
            Popups.Closed += PopupClosed;
            nav_Changed();
            app.nav.Changed += s => nav_Changed();
        }
        bool dirty = true;

        void RefreshLanguage()
        {
            string[] names = { "Hall", "Heroes", "Board", "Bag", "Quests" };
            for (int i = 0; i < names.Length; i++) if (tabLabel[i]) tabLabel[i].text = L.T(names[i]);
            dirty = true;
        }

        void PopupClosed(Popup popup)
        {
            // Equipment pickers, promotions and item sales change the details behind
            // their modal. Refresh those views once when returning to them.
            Popups.RedrawAll();
            if (app.InGame && !(app.nav.Current is IFullScreenPage)) app.nav.Current?.Redraw();
        }

        void nav_Changed()
        {
            // Screen targets must not survive navigation: otherwise a new chapter can
            // point at a destroyed button from a previous Board, Bag or Tavern page.
            foreach (var key in Targets.Keys.Where(k => !k.StartsWith("tab_") && !k.StartsWith("hud_")).ToList()) Targets.Remove(key);
            var s = app.nav.Current; bool full = s is IFullScreenPage; SetChrome(visible && !full);
        }

        public void SetVisible(bool v) { visible = v; SetChrome(v); if (v) dirty = true; }
        void SetChrome(bool v)
        {
            if (topBar) topBar.gameObject.SetActive(v);
            if (navBar) navBar.gameObject.SetActive(v);
            if (pointer && !v) pointer.gameObject.SetActive(false);
            if (coach && !v) coach.gameObject.SetActive(false);
            coachReserved = v && FirstStepsActive();
            if (navBar) navBar.anchoredPosition = new Vector2(0, v ? adInset : 0);
            host.screens.offsetMin = new Vector2(0, v ? NavH + adInset : 0);
            host.screens.offsetMax = new Vector2(0, v ? -TopH - (coachReserved ? CoachSpace : 0) : 0);
        }

        bool FirstStepsActive()
        {
            var q = C.Quests.CurrentMain;
            return Prefs.Hints && q != null && q.type == "main" && q.order > 0 && q.order <= 4;
        }

        // ============================ top bar ============================
        void BuildTop()
        {
            var bar = U.Box(host.hud, Theme.Panel, Theme.Gold, "TopBar", true, 0.22f); topBar = bar.R();
            topBar.anchorMin = new Vector2(0, 1); topBar.anchorMax = new Vector2(1, 1); topBar.pivot = new Vector2(0.5f, 1); topBar.sizeDelta = new Vector2(0, TopH); topBar.anchoredPosition = Vector2.zero;
            var rankBtn = bar.gameObject.AddComponent<UButton>(); rankBtn.bg = bar; rankBtn.noTint = true; rankBtn.sfx = null;

            emblem = U.Img(topBar, Brand.Shield(Theme.Red), Color.white, "emblem", true); emblem.preserveAspect = true; emblem.At(0, 0.5f, 22, 0, 110, 128, 0, 0.5f);
            var eb = emblem.gameObject.AddComponent<UButton>(); eb.bg = emblem; eb.noTint = true; eb.onClick = () => Popups.Show(new RankPopup());
            nameT = U.Txt(topBar, "", 44, Theme.Text, TextAnchor.MiddleLeft, true); nameT.At(0, 1, 150, -16, 480, 52, 0, 1);
            rankT = U.Txt(topBar, "", 32, Theme.Gold, TextAnchor.MiddleLeft, true); rankT.At(0, 1, 150, -86, 480, 56, 0, 1);
            fameBar = U.ProgressBar(topBar, Theme.Accent, 440, 52, "", 24); fameBar.back.R().At(0, 0, 150, 16, 440, 52, 0, 0); fameT = fameBar.label;
            var fi = U.Icon(topBar, Icons.Get("flame"), 40); fi.At(0, 0, 596, 24, 40, 40, 0, 0);
            var fb = U.Btn(topBar, "", new Color(0, 0, 0, 0), () => Popups.Show(new RankPopup()), 480, 120); fb.noTint = true; fb.bg.color = new Color(0, 0, 0, 0); fb.At(0, 0.5f, 140, 0, 520, 150, 0, 0.5f);
            Reg("hud_rank", fb);

            // currencies
            var goldBox = U.Box(topBar, Theme.Inset, Theme.Black, "gold"); goldBox.At(1, 1, -126, -16, 280, 88, 1, 1);
            U.Icon(goldBox.transform, Art.ResIcon(C, "gold"), 50).At(0, 0.5f, 10, 0, 50, 50, 0, 0.5f);
            goldT = U.Txt(goldBox.transform, "0", 42, Theme.Gold, TextAnchor.MiddleCenter, true); goldT.Fill(72, 14, 4, 4);
            var gemBox = U.Box(topBar, Theme.Inset, Theme.Black, "gems"); gemBox.At(1, 1, -126, -116, 280, 88, 1, 1);
            U.Icon(gemBox.transform, Art.ResIcon(C, "gems"), 50).At(0, 0.5f, 10, 0, 50, 50, 0, 0.5f);
            gemsT = U.Txt(gemBox.transform, "0", 42, Theme.BlueDark, TextAnchor.MiddleCenter, true); gemsT.Fill(72, 14, 4, 4);
            Reg("hud_gold", goldBox); Reg("hud_gems", gemBox);

            var menu = U.IconBtn(topBar, Icons.Get("menu"), Theme.PanelLight, () => Popups.Show(new MenuPopup()), 96); menu.At(1, 0.5f, -14, 0, 96, 96, 1, 0.5f);
        }

        // ============================ nav bar ============================
        void BuildNav()
        {
            var bar = U.Box(host.hud, Theme.Panel, Theme.Gold, "NavBar", true, 0.22f); navBar = bar.R();
            navBar.anchorMin = new Vector2(0, 0); navBar.anchorMax = new Vector2(1, 0); navBar.pivot = new Vector2(0.5f, 0); navBar.sizeDelta = new Vector2(0, NavH); navBar.anchoredPosition = Vector2.zero;
            var row = U.Rt(navBar, "tabs"); row.Fill(8, 8, 10, 6); U.HBox(row, 6, null, TextAnchor.MiddleCenter, true, true, true, true);
            string[] names = { "Hall", "Heroes", "Board", "Bag", "Quests" }; string[] icons = { "hall", "heroes", "board", "bag", "quest" };
            for (int i = 0; i < 5; i++)
            {
                int k = i;
                var b = U.Box(row, Theme.Panel, Theme.Black, "tab" + names[i], true, 0.15f); b.Le(-1, -1, 1, 1);
                var ub = b.gameObject.AddComponent<UButton>(); ub.bg = b; ub.noTint = true; ub.onClick = () => GoTab((Tab)k);
                tabBg[i] = b;
                tabIcon[i] = U.Icon(b.transform, Icons.Get(icons[i]), 76); tabIcon[i].At(0.5f, 1, 0, -12, 76, 76, 0.5f, 1);
                tabLabel[i] = U.Txt(b.transform, L.T(names[i]), 30, Theme.Text, TextAnchor.MiddleCenter, true); tabLabel[i].StretchX(0, 10, 42, 8, 8);
                tabBadge[i] = U.Box(b.transform, Theme.Red, Theme.Black, "badge"); tabBadge[i].At(1, 1, -6, -6, 64, 64, 1, 1);
                tabBadgeT[i] = U.Txt(tabBadge[i].transform, "", 30, Color.white, TextAnchor.MiddleCenter, true); tabBadgeT[i].Fill();
                tabBadge[i].gameObject.SetActive(false);
                Reg("tab_" + names[i].ToLower(), b);
            }
            ApplyTabStyle();
        }

        void ApplyTabStyle()
        {
            for (int i = 0; i < 5; i++)
            {
                bool sel = (int)Current == i;
                tabBg[i].sprite = Boxes.Get(sel ? Theme.Accent : Theme.Panel, null, 0.2f);
                tabIcon[i].R().localScale = Vector3.one * (sel ? 1.12f : 0.92f);
                tabLabel[i].color = sel ? Theme.TextDark : Theme.TextDim;
            }
        }

        public void GoTab(Tab t, bool force = false)
        {
            if (!force && t == Current && app.nav.Depth == 1) { if (app.nav.Current != null) app.nav.Current.Redraw(); return; }
            Current = t; ApplyTabStyle(); Popups.CloseAll();
            UiScreen s;
            switch (t) { case Tab.Heroes: s = new HeroesScreen(); break; case Tab.Board: s = new BoardScreen(); break; case Tab.Bag: s = new BagScreen(); break; case Tab.Quests: s = new QuestsScreen(); break; default: s = new HallScreen(); break; }
            app.nav.Root(s, true); if (!force) Audio.Play("swoosh", 0.5f);
            if (t != Tab.Hall) Targets.Remove("bld_tavern");
        }

        public bool Back()
        {
            // Full-screen pages own Back while their simulation is running.
            // Let Nav reach BattleScreen.OnBack instead of popping it here.
            if (app.nav.Current is IFullScreenPage) return false;
            if (app.nav.Depth > 1) { app.nav.Pop(); return true; }
            if (Current != Tab.Hall) { GoTab(Tab.Hall); return true; }
            return false;
        }

        // ============================ pointer ============================
        void BuildPointer()
        {
            pointerImg = U.Img(host.top, Icons.Get("arrow_d", new Color32(255, 211, 78, 255)), new Color(1, 0.85f, 0.3f), "pointer"); pointerImg.Sz(120, 120);
            pointerImg.sprite = Icons.Get("arrow_d"); pointer = pointerImg.R(); pointer.gameObject.SetActive(false);
            var glow = U.Img(pointer, Boxes.Glow, new Color(1, 0.8f, 0.2f, 0.5f), "g"); glow.Sz(260, 260); glow.transform.SetAsFirstSibling();
            Tw.To(0.6f, k => { if (pointerImg) pointerImg.R().pivot = new Vector2(0.5f, 0.5f + 0.18f * k); }, Ease.InOutSine, 0, null, pointer, true, true);
        }

        void BuildCoach()
        {
            coach = U.Rt(host.top, "coach"); coach.Fill(); coach.gameObject.SetActive(false);
            ring = new Image[4];
            var gold = new Color(1f, 0.86f, 0.35f, 1f);
            for (int i = 0; i < 4; i++) { ring[i] = U.Solid(coach, gold, "ring" + i); ring[i].raycastTarget = false; }
            coachCard = U.Box(coach, Theme.Panel, Theme.Gold, "card", true, .06f).R();
            coachStep = U.Txt(coachCard, "", 28, Theme.Gold, TextAnchor.MiddleCenter, true); coachStep.StretchX(1, -12, 44, 24, 350);
            coachTitle = U.Txt(coachCard, "", 36, Theme.Text, TextAnchor.MiddleCenter, true); coachTitle.StretchX(1, -62, 96, 24, 350);
            coachLine = U.Txt(coachCard, "", 30, Theme.Text, TextAnchor.MiddleCenter, false, true); coachLine.StretchX(1, -168, 112, 24, 24);
            coachAction = U.Btn(coachCard, "", Theme.Accent, null, 304, 108, 28); coachAction.At(1, 1, -24, -38, 304, 108, 1, 1);
        }

        void HideCoach() { if (coach && coach.gameObject.activeSelf) coach.gameObject.SetActive(false); }

        public static void FocusTarget(RectTransform target)
        {
            if (target == null) return;
            Canvas.ForceUpdateCanvases();
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll == null || !scroll.vertical) return;
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            float centerY = scroll.viewport.InverseTransformPoint((corners[0] + corners[2]) * 0.5f).y;
            float max = Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height);
            var pos = scroll.content.anchoredPosition;
            pos.y = Mathf.Clamp(pos.y + scroll.viewport.rect.center.y - centerY, 0, max);
            scroll.StopMovement(); scroll.content.anchoredPosition = pos;
        }

        void UpdateCoach(QuestDef q, string hint, RectTransform target)
        {
            coach.anchorMin = host.safe.anchorMin; coach.anchorMax = host.safe.anchorMax;
            coach.offsetMin = coach.offsetMax = Vector2.zero;
            coach.gameObject.SetActive(true); coach.SetAsLastSibling();
            Vector2 bl = Vector2.zero, tr = Vector2.zero;
            if (target != null)
            {
                var corners = new Vector3[4]; target.GetWorldCorners(corners);
                bl = host.ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(host.cam, corners[0]), coach);
                tr = host.ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(host.cam, corners[2]), coach);
            }
            float halfW = coach.rect.width * 0.5f, halfH = coach.rect.height * 0.5f;
            const float pad = 18f;
            float x0 = bl.x - pad, y0 = bl.y - pad, x1 = tr.x + pad, y1 = tr.y + pad;
            bool onScreen = target != null && x1 > -halfW + 8 && x0 < halfW - 8 && y1 > -halfH + NavH && y0 < halfH - TopH - CoachSpace && x1 - x0 > 12f;
            x0 = Mathf.Max(x0, -halfW + 8); x1 = Mathf.Min(x1, halfW - 8);
            y0 = Mathf.Max(y0, -halfH + NavH + 4); y1 = Mathf.Min(y1, halfH - TopH - CoachSpace - 4);
            for (int i = 0; i < 4; i++) ring[i].gameObject.SetActive(onScreen);
            if (onScreen)
            {
                const float edge = 5f;
                float a = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 4f);
                var gold = new Color(1f, 0.86f, 0.35f, a);
                for (int i = 0; i < 4; i++) ring[i].color = gold;
                PlaceBand(ring[0].R(), x0, y1 - edge, x1 - x0, edge);
                PlaceBand(ring[1].R(), x0, y0, x1 - x0, edge);
                PlaceBand(ring[2].R(), x0, y0, edge, y1 - y0);
                PlaceBand(ring[3].R(), x1 - edge, y0, edge, y1 - y0);
            }
            coachCard.anchorMin = coachCard.anchorMax = new Vector2(0.5f, 0.5f); coachCard.pivot = new Vector2(0.5f, 0.5f);
            coachCard.sizeDelta = new Vector2(Mathf.Min(1000f, coach.rect.width - 48f), CoachH);
            coachCard.anchoredPosition = new Vector2(0f, halfH - TopH - CoachSpace * 0.5f);
            ConfigureCoach(q, hint);
        }

        void ConfigureCoach(QuestDef q, string hint)
        {
            coachStep.text = L.T("FIRST STEPS · {0}/4", q.order);
            if (C.Quests.State(q.id) == QuestSystem.Ready)
            {
                Coach(StoryScript.Pick("Reward ready", "Hadiah siap"), L.T("Claim this reward to unlock your next step."), L.T("CLAIM REWARD"), () => Popups.Show(new QuestRewardPopup(q)));
                return;
            }
            if (q.order == 1)
            {
                if (C.Roster.Full) { Coach(L.T("Make room for a new hero"), L.T("Upgrade the Barracks to make room for another hero."), L.T("BARRACKS"), () => app.nav.Push(new FacilityScreen("barracks"))); return; }
                bool inTavern = app.nav.Current is FacilityScreen fs && fs.FacilityId == "tavern";
                Coach(L.T("Recruit your second hero"), inTavern ? L.T("Tap HIRE on an affordable candidate, then claim your guide reward.") : L.T("Open the Tavern. Hire one companion using your starting gold."), L.T(inTavern ? "SHOW RECRUITS" : "OPEN TAVERN"), () =>
                {
                    if (app.nav.Current is FacilityScreen tavern && tavern.FacilityId == "tavern") tavern.ShowRecruitment();
                    else app.nav.Push(new FacilityScreen("tavern"));
                    Tw.After(0.12f, () => { if (Targets.TryGetValue("btn_hire", out var hire)) FocusTarget(hire); });
                });
                return;
            }
            if ((q.order == 2 || q.order == 4) && !C.Roster.All.Any(C.Roster.Ready))
            {
                Coach(L.T("Recover your heroes first"), L.T("Use a healing potion, or recover at the Infirmary."), L.T("OPEN HEROES"), () => GoTab(Tab.Heroes)); return;
            }
            if (q.order == 2 || q.order == 4)
            {
                bool onBoard = app.nav.Current is BoardScreen && BoardState.Tab == 0 && BoardState.Region == "r1";
                Coach(q.order == 2 ? L.T("Win your first battle") : L.T("Clear Mushroom Grove"), q.order == 2 ? L.T("Open Campaign, start Slime Meadow, and tap FIGHT.") : L.T(q.desc), L.T(onBoard ? "NEXT BATTLE" : "OPEN BOARD"), () =>
                {
                    BoardState.Tab = 0; BoardState.Region = "r1";
                    if (!(app.nav.Current is BoardScreen)) { GoTab(Tab.Board, true); return; }
                    var mission = C.Missions.InRegion("r1").OrderBy(x => x.order).FirstOrDefault(x => C.Missions.Unlocked(x) && !C.Missions.Cleared(x.id));
                    if (mission != null) Popups.Show(new PartyPopup(mission)); else GoTab(Tab.Board, true);
                }); return;
            }
            if (q.order == 3)
            {
                bool inBag = app.nav.Current is BagScreen && Bag.OpenTab == 0;
                Coach(L.T("Equip your party"), L.T("Open Bag → Gear and tap AUTO-EQUIP."), L.T(inBag ? "AUTO-EQUIP" : "OPEN BAG"), () =>
                {
                    if (app.nav.Current is BagScreen && Bag.OpenTab == 0) { BagScreen.EquipBest(); return; }
                    Bag.OpenTab = 0; Bag.Filter = 0; GoTab(Tab.Bag, true);
                });
            }
        }

        void Coach(string title, string line, string action, Action next)
        { coachTitle.text = title; coachLine.text = line; coachAction.label.text = action; coachAction.onClick = next; }

        static void PlaceBand(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0f, 0f);
            r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        string ResolveHint(string hint)
        {
            bool root = app.nav.Depth == 1;
            if (hint.StartsWith("tab_")) return root && string.Equals(Current.ToString(), hint.Substring(4), StringComparison.OrdinalIgnoreCase) ? "btn_main" : hint;
            if (hint.StartsWith("bld_"))
            {
                if (app.nav.Current is FacilityScreen fs)
                {
                    if (fs.FacilityId != hint.Substring(4)) return null;
                    if (Targets.ContainsKey("btn_hire")) return "btn_hire";
                    var goal = C.Quests.CurrentMain;
                    if (goal != null && goal.goals.Count > 0 && (goal.goals[0].stat == "hero.count" || goal.goals[0].stat == "hero.hired")) return null;
                    return "btn_main";
                }
                return root && Current == Tab.Hall ? hint : root ? "tab_hall" : null;
            }
            return hint;
        }

        void UpdatePointer()
        {
            string hint = null;
            var q = C.Quests.CurrentMain;
            if (q != null && C.Quests.State(q.id) == QuestSystem.Active && Prefs.Hints && !string.IsNullOrEmpty(q.hint)) hint = ResolveHint(q.hint);
            RectTransform target = null;
            bool show = visible && hint != null && Popups.Count == 0 && Targets.TryGetValue(hint, out target) && target != null && target.gameObject.activeInHierarchy;
            bool guided = visible && Prefs.Hints && Popups.Count == 0 && !(app.nav.Current is IFullScreenPage) && q != null && q.type == "main" && q.order > 0 && q.order <= 4;
            if (guided)
            {
                if (pointer.gameObject.activeSelf) pointer.gameObject.SetActive(false);
                if (C.Quests.State(q.id) == QuestSystem.Ready) target = null;
                UpdateCoach(q, hint, target); return;
            }
            if (!show) { HideCoach(); if (pointer.gameObject.activeSelf) pointer.gameObject.SetActive(false); return; }
            HideCoach();
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var topCenter = (corners[1] + corners[2]) * 0.5f;
            var sp = RectTransformUtility.WorldToScreenPoint(host.cam, topCenter);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(host.top, sp, host.cam, out var lp);
            pointer.anchoredPosition = lp + new Vector2(0, 70);
            if (!pointer.gameObject.activeSelf) pointer.gameObject.SetActive(true);
            pointer.SetAsLastSibling();
        }
        // ============================ per-frame ============================
        double nextBadge;
        public void Tick()
        {
            if (!visible) return;
            bool chrome = !(app.nav.Current is IFullScreenPage);
            if (coachReserved != (chrome && FirstStepsActive()))
            {
                SetChrome(chrome);
                app.nav.Current?.Redraw();
            }
            double g = C.Wallet.Get("gold"), gm = C.Wallet.Get("gems");
            if (Math.Abs(showGold - g) > 0.5) { showGold += (g - showGold) * Math.Min(1, Time.unscaledDeltaTime * 8) + Math.Sign(g - showGold) * 0.5; if (Math.Abs(showGold - g) < 1) showGold = g; }
            showGems = gm;
            goldT.text = Util.Num(showGold); gemsT.text = Util.Num(showGems);
            if (dirty || Util.Now > nextBadge) { dirty = false; nextBadge = Util.Now + 1; Refresh(); }
            UpdatePointer();
            popupCooldown -= Time.unscaledDeltaTime;
            if (popupCooldown <= 0 && Popups.Count == 0 && app.nav.Depth == 1 && !(app.nav.Current is IFullScreenPage)) { popupCooldown = 2.0f; CheckPopups(); }
        }

        void Refresh()
        {
            var meta = C.Meta; nameT.text = meta.GuildName;
            var r = C.Ranks.Current; rankT.text = L.T("Rank {0}", r.id);
            emblem.sprite = Brand.Shield(Theme.Hex(GuildGame.Emblems[Mathf.Clamp(meta.Emblem, 0, GuildGame.Emblems.Length - 1)]));
            var nx = C.Ranks.Next;
            if (nx != null) { fameBar.Value = C.Ranks.Progress; fameT.text = $"{Util.Num(C.Ranks.Fame)}/{Util.Num(nx.fame)}"; }
            else { fameBar.Value = 1; fameT.text = "MAX"; }
            int ready = C.Quests.ReadyCount;
            SetBadge(Tab.Quests, ready);
            int exped = C.Missions.Expeditions.Count(t => t.done) + (C.Ranks.FameReached && !C.Ranks.CanPromote ? 1 : 0) + (C.Ranks.CanPromote ? 1 : 0);
            SetBadge(Tab.Board, exped);
            SetBadge(Tab.Bag, C.Inventory.Gear.Count(g => g.isNew));
            int hall = (C.Vault.Pending >= Math.Max(1, C.Vault.Capacity * 0.5) ? 1 : 0) + C.Events.Pending.Count;
            SetBadge(Tab.Hall, hall);
            if (ready > lastQuestReady && lastQuestReady >= 0) Tw.Punch(tabIcon[(int)Tab.Quests].transform, 0.4f, 0.4f);
            lastQuestReady = ready;
        }
        void SetBadge(Tab t, int n)
        {
            int i = (int)t; tabBadge[i].gameObject.SetActive(n > 0);
            if (n > 0) { tabBadgeT[i].text = n > 9 ? "9+" : n.ToString(); }
        }

        // ============================ popups queue ============================
        public void CheckPopups()
        {
            if (!visible || Popups.Count > 0) return;
            var firstSteps = C.Quests.CurrentMain;
            if (firstSteps != null && firstSteps.order > 0 && firstSteps.order <= 4) return;
            if (!C.Daily.Claimed && C.Counters.Get("missions.won") >= 1 && !dailyShown) { dailyShown = true; Popups.Show(new DailyPopup()); return; }
            if (C.Events.Pending.Count > 0 && C.Counters.Get("missions.won") >= 2 && app.nav.Current is HallScreen)
            {
                var ev = C.Content.EventMap[C.Events.Pending[0]]; Popups.Show(new EventPopup(ev)); return;
            }
        }
    }

    public interface IFullScreenPage { }
}
