using System;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    /// <summary>The living guild hall: buildings, wandering heroes, vault, notices, main-quest tracker.</summary>
    public class HallScreen : UiScreen
    {
        class Bld
        {
            public string id; public RectTransform root; public Image house; public RectTransform label; public Text plate, status; public RectTransform bubble; public Image bubbleIcon; public Text bubbleText;
            public U.Bar bar; public Text timer; public Image lockIcon; public Image hammer; public float ground; public UButton btn; public Tw.H bounce, bubbleBob;
        }
        class Walker { public RectTransform rt; public Image img; public SpriteAnim anim; public Vector2 target; public float speed, wait; public int dir = 0, node, direction; public UnitInstance u; public int skin; public Tw.H bob; }

        static readonly Dictionary<string, string> Sprites = new Dictionary<string, string>
        { {"tavern","house_20"}, {"blacksmith","house_05"}, {"training","house_08"}, {"infirmary","house_13"}, {"barracks","house_19"}, {"treasury","house_23"}, {"library","house_25"} };
        static readonly Dictionary<string, int> PaintedBuildings = new Dictionary<string, int>
        { {"tavern",0}, {"blacksmith",1}, {"training",2}, {"infirmary",3}, {"barracks",4}, {"treasury",5}, {"library",6} };

        readonly Dictionary<string, Bld> blds = new Dictionary<string, Bld>();
        readonly List<Walker> walkers = new List<Walker>();
        RectTransform scene, world; Vector2 size; float nextSlow, nextSort;
        Text trackTitle, trackDesc; U.Bar trackBar; UButton trackBtn; Image dailyDot; Text eventCount; RectTransform eventBtn;
        float yardBottom, rowSpan, houseHeight;
        Vector2 pathCenter, pathRadius;
        GameContext C => App.Ctx;

        public override string Id => "Hall";
        public override void OnEnter() { Audio.PlayMusic("town"); }
        public override void OnExit() { U.Clear(UiHost.I.back); }

        public override void Build(RectTransform root)
        {
            walkers.Clear(); blds.Clear(); ground.Clear(); trackTitle = null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            size = root.rect.size; if (size.y < 100) size = new Vector2(1080, 1560);
            scene = U.Rt(root, "scene").Fill();
            if (GeneratedArt.Get("hall_ground")) GeneratedArt.Cover(scene, "hall_ground");
            else U.Solid(scene, Theme.Hex("#dce7b1"), "courtyard").Fill();
            world = U.Rt(scene, "world").Fill();
            PlaceVillage();

            // walkers
            foreach (var u in C.Roster.All.Where(x => !x.monster).OrderByDescending(x => x.level).Take(9)) SpawnWalker(u);

            BuildNotices();
            if (!Guided) BuildTracker();
            SortWorld();
        }

        readonly Dictionary<RectTransform, float> ground = new Dictionary<RectTransform, float>();
        void tag(RectTransform r, float y) { ground[r] = y; }

        static Text WorldLabel(Transform parent, string text, int size, Color color, bool bold = false)
        {
            var label = U.Txt(parent, text, size, color, TextAnchor.MiddleCenter, bold);
            var edge = label.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(1f, .98f, .9f, .95f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            return label;
        }

        static float LabelWidth(Text name, Text status = null, bool locked = false)
        {
            float width = name.preferredWidth + 28f;
            if (status) width = Mathf.Max(width, status.preferredWidth + (locked ? 48f : 28f));
            return Mathf.Clamp(width, 190f, 300f);
        }

        // --------------------------------------------------------------------------------
        bool Guided => App.I.hud.GuideActive;

        void PlaceVillage()
        {
            // Three airy terraces adapt to the smaller first-steps viewport. A building's
            // art, name and action label stay in the same row, rather than colliding.
            yardBottom = Guided ? 36f : 268f;
            rowSpan = Mathf.Max(220f, (size.y - yardBottom - 126f) / 3f);
            houseHeight = Mathf.Min(330f, rowSpan - 24f);
            float back = yardBottom + rowSpan * 2f, middle = yardBottom + rowSpan;
            BuildBuilding("barracks", .165f, back, .94f);
            BuildBuilding("library", .5f, back, .94f);
            BuildBuilding("treasury", .835f, back, .94f);
            BuildBuilding("blacksmith", .18f, middle, 1f);
            BuildBuilding("training", .82f, middle, 1f);
            BuildFountain(middle + 32f);
            pathCenter = new Vector2(size.x * .5f, middle + 75f);
            pathRadius = new Vector2(Mathf.Min(190f, size.x * .18f), Mathf.Min(116f, rowSpan * .42f));
            BuildBuilding("infirmary", .165f, yardBottom, .92f);
            BuildBuilding("tavern", .5f, yardBottom, 1.06f);
            BuildMarket(yardBottom);
        }

        void BuildBuilding(string id, float nx, float feet, float scale)
        {
            var def = C.Content.Upgrade(id); if (def == null) return;
            var sp = GeneratedArt.HallBuilding(PaintedBuildings[id]) ?? Art.Prop(Sprites[id]);
            if (!sp) return;
            float w = Mathf.Min(size.x * .29f, houseHeight * scale), h = houseHeight;
            var b = new Bld { id = id, ground = feet };
            b.root = U.Rt(world, "bld_" + id); b.root.anchorMin = b.root.anchorMax = Vector2.zero; b.root.pivot = new Vector2(0.5f, 0);
            b.root.anchoredPosition = new Vector2(nx * size.x, feet); b.root.sizeDelta = new Vector2(Mathf.Max(w, Mathf.Min(304f, size.x * .29f)), h);
            // soft shadow
            var shadow = Land.Contact(b.root, w * .76f); shadow.color = new Color(.28f, .29f, .17f, .18f); shadow.R().anchoredPosition += new Vector2(0, 68f);
            b.house = U.Img(b.root, sp, Color.white, "house", true); b.house.preserveAspect = true; b.house.At(0.5f, 0, 0, 30f, w, h - 26f, 0.5f, 0);
            b.btn = b.house.gameObject.AddComponent<UButton>(); b.btn.bg = b.house; b.btn.noTint = true; b.btn.sfx = "pop"; b.btn.onClick = () => OnBuildingTap(id);
            Hud.Reg("bld_" + id, b.root);
            // Unframed labels retain their full tap area over the painted courtyard.
            var plate = U.Solid(b.root, new Color(0, 0, 0, .001f), "label", true); b.label = plate.R(); plate.At(.5f, 0, 0, 0, 200, 112, .5f, 0);
            var plateBtn = plate.gameObject.AddComponent<UButton>(); plateBtn.bg = plate; plateBtn.noTint = true; plateBtn.sfx = "pop"; plateBtn.onClick = () => OnBuildingTap(id);
            b.plate = WorldLabel(plate.transform, "", 33, Theme.TextDark, true); b.plate.FitText(36); b.plate.StretchX(1, -5, 56, 12, 12);
            b.status = WorldLabel(plate.transform, "", 28, Theme.TextDark); b.status.FitText(27); b.status.StretchX(0, 4, 31, 12, 12);
            // progress
            float progressY = h * .52f;
            b.bar = U.ProgressBar(b.root, Theme.Accent, w * .8f, 28); b.bar.back.R().At(.5f, 0, 0, progressY, w * .8f, 28, .5f, .5f);
            var timePlate = U.Rt(b.root, "timer_label"); timePlate.At(.5f, 0, 0, progressY + 39f, 180, 64, .5f, .5f);
            b.timer = WorldLabel(timePlate.transform, "", 30, Theme.TextDark, true); b.timer.FitText(28); b.timer.Fill(8, 8, 2, 2);
            b.hammer = U.Icon(b.root, Icons.Get("hammer"), 52); b.hammer.At(.5f, 0, 0, progressY + 81f, 52, 52, .5f, .5f);
            b.lockIcon = U.Icon(plate.transform, Icons.Get("lock"), 24);
            // bubble
            var bub = U.Solid(b.root, new Color(0, 0, 0, .001f), "action_label", true); bub.At(.5f, 1, 0, -2, 264f, 76, .5f, 1); b.bubble = bub.R();
            var bbtn = bub.gameObject.AddComponent<UButton>(); bbtn.bg = bub; bbtn.noTint = true; bbtn.onClick = () => OnBubbleTap(id);
            b.bubbleIcon = U.Icon(bub.transform, Icons.Get("coin"), 34); b.bubbleIcon.At(0, .5f, 10, 0, 34, 34, 0, .5f);
            b.bubbleText = WorldLabel(bub.transform, "", 29, Theme.GreenDark, true); b.bubbleText.FitText(27); b.bubbleText.Fill(46, 8, 6, 6);
            b.bubble.gameObject.SetActive(false);
            blds[id] = b; tag(b.root, feet);
            UpdateBuilding(b, true);
        }

        void BuildMarket(float feet)
        {
            float w = Mathf.Min(size.x * .29f, houseHeight * .92f);
            var root = U.Rt(world, "market"); root.anchorMin = root.anchorMax = Vector2.zero; root.pivot = new Vector2(.5f, 0); root.anchoredPosition = new Vector2(.835f * size.x, feet); root.sizeDelta = new Vector2(w, houseHeight);
            var shadow = Land.Contact(root, w * .76f); shadow.color = new Color(.28f, .29f, .17f, .18f); shadow.R().anchoredPosition += new Vector2(0, 68f);
            var market = U.Img(root, GeneratedArt.HallBuilding(7) ?? Art.Prop("crate_08"), Color.white, "market_stall"); market.preserveAspect = true; market.At(.5f, 0, 0, 30f, w, houseHeight - 26f, .5f, 0);
            var hit = U.Solid(root, new Color(0, 0, 0, .001f), "hit", true); hit.Fill();
            var ub = hit.gameObject.AddComponent<UButton>(); ub.bg = hit; ub.noTint = true; ub.sfx = "pop"; ub.onClick = () => { Bag.OpenTab = 2; Hud.Targets.Remove("x"); App.I.hud.GoTab(Hud.Tab.Bag); };
            var plate = U.Rt(root, "label"); plate.At(.5f, 0, 0, 5, 170, 58, .5f, 0);
            var t = WorldLabel(plate.transform, L.T("Market"), 33, Theme.TextDark, true); t.FitText(32); t.Fill(12, 12, 6, 6);
            plate.Sz(LabelWidth(t), 76);
            tag(root, feet);
        }

        void BuildFountain(float feet)
        {
            var sp = GeneratedArt.HallBuilding(8); if (!sp) return;
            float w = Mathf.Min(220f, houseHeight * .78f);
            var fountain = U.Img(world, sp, Color.white, "fountain"); fountain.preserveAspect = true;
            fountain.At(0, 0, size.x * .5f, feet, w, w, .5f, 0);
            tag(fountain.R(), feet + w * .16f);
        }

        void SpawnWalker(UnitInstance u)
        {
            if (u.monster) return;
            var w = new Walker { u = u, skin = Art.SkinIndex(u.sprite) };
            w.node = walkers.Count * HallPath.Nodes / 9; w.direction = walkers.Count % 2 == 0 ? 1 : -1;
            var img = U.Img(world, null, Color.white, "walker", true);
            w.img = img; w.rt = img.R(); w.rt.anchorMin = w.rt.anchorMax = Vector2.zero;
            w.rt.anchoredPosition = HallPath.Point(pathCenter, pathRadius, w.node);
            w.anim = SpriteAnim.On(img, Walk(w.skin, 0), 6, true, 4.6f);
            w.speed = UnityEngine.Random.Range(55f, 90f); w.wait = UnityEngine.Random.Range(0.2f, 2.5f); w.target = w.rt.anchoredPosition;
            var ub = img.gameObject.AddComponent<UButton>(); ub.bg = img; ub.noTint = true; ub.sfx = "pop"; ub.onClick = () => Popups.Show(new HeroPopup(u.uid));
            var sh = U.Img(w.rt, Boxes.Glow, new Color(0, 0, 0, 0.4f), "sh"); sh.At(0.5f, 0, 0, 0, 80, 26, 0.5f, 0.5f); sh.transform.SetAsFirstSibling();
            walkers.Add(w); tag(w.rt, w.rt.anchoredPosition.y);
        }
        static Sprite[] Walk(int skin, int dir) => new[] { Art.Chara(skin, dir, 0), Art.Chara(skin, dir, 1), Art.Chara(skin, dir, 2), Art.Chara(skin, dir, 1) };

        // --------------------------------------------------------------------------------
        void UpdateBuilding(Bld b, bool force = false)
        {
            var def = C.Content.Upgrade(b.id); int lv = C.Upgrades.Level(b.id); var task = C.Upgrades.Pending(b.id);
            bool unlocked = C.Upgrades.Unlocked(def); bool building = task != null;
            var tintBase = Color.white;
            b.plate.text = L.T(def.name);
            if (lv == 0)
            {
                b.house.color = !unlocked ? new Color(.86f, .88f, .86f, .8f) : building ? new Color(.94f, .94f, .88f) : Color.white;
                b.status.text = L.T(!unlocked ? "Locked" : building ? "Upgrade" : "Build");
                b.status.color = !unlocked ? Theme.TextDark : Theme.GreenDark;
            }
            else
            {
                b.house.color = tintBase;
                b.status.text = L.T("Level") + " " + lv;
                b.status.color = Theme.Gold;
            }
            b.lockIcon.gameObject.SetActive(lv == 0 && !unlocked);
            float labelWidth = LabelWidth(b.plate, b.status, lv == 0 && !unlocked);
            b.label.Sz(labelWidth, 112);
            if (lv == 0 && !unlocked)
            {
                float statusWidth = Mathf.Min(b.status.preferredWidth + 4f, labelWidth - 48f);
                b.status.At(.5f, 0, 16, 4, statusWidth, 31, .5f, 0);
                b.lockIcon.At(.5f, 0, -statusWidth * .5f - 2, 7, 24, 24, .5f, 0);
            }
            else b.status.StretchX(0, 4, 31, 12, 12);
            b.hammer.gameObject.SetActive(building);
            b.bar.back.gameObject.SetActive(building); b.timer.transform.parent.gameObject.SetActive(building);
            if (building)
            {
                b.bar.Value = (float)C.Timers.Progress(task); b.timer.text = Util.Dur(C.Timers.Remaining(task));
                if (b.bounce == null) b.bounce = Tw.Bob(b.hammer.R(), 10, 0.5f);
            }
            else if (b.bounce != null) { b.bounce.Kill(); b.bounce = null; }

            // bubble
            string icon = null, txt = ""; bool show = false;
            if (!building)
            {
                if (b.id == "tavern") C.Recruiter.EnsureAffordableRecruit();
                if (b.id == "treasury" && lv > 0 && C.Vault.Pending >= Math.Max(5, C.Vault.Capacity * 0.25)) { icon = "coin"; txt = Util.Num(C.Vault.Pending); show = true; }
                else if (b.id == "tavern" && C.Recruiter.Pool.Any(p => C.Recruiter.CanHire(p)) && C.Roster.Count < 4) { icon = "heroes"; txt = L.T("Hire"); show = true; }
                else if (b.id == "infirmary" && lv > 0 && C.Roster.All.Any(u => C.Roster.HpFrac(u) < 0.5 && !C.Roster.IsBusy(u))) { icon = "hp"; txt = L.T("Heal"); show = true; }
                else if (lv > 0 && C.Quests.CurrentMain?.hint == "bld_" + b.id && C.Upgrades.CanStart(def)) { icon = "arrow_u"; txt = L.T("Upgrade"); show = true; }
                else if (lv == 0 && unlocked && C.Upgrades.CanStart(def)) { icon = "hammer"; txt = L.T("Build"); show = true; }
            }
            if (show)
            {
                if (!b.bubble.gameObject.activeSelf) { b.bubble.anchoredPosition = new Vector2(0, -2); b.bubble.gameObject.SetActive(true); Tw.Pop(b.bubble, .3f); b.bubbleBob = Tw.Bob(b.bubble, 5, .8f); }
                b.bubbleIcon.sprite = Icons.Get(icon); b.bubbleText.text = txt;
                float textWidth = Mathf.Clamp(b.bubbleText.preferredWidth, 90, 214);
                b.bubbleText.At(.5f, .5f, 21, 0, textWidth, 56, .5f, .5f);
                b.bubbleIcon.At(.5f, .5f, -(textWidth + 8) * .5f, 0, 34, 34, .5f, .5f);
            }
            else if (b.bubble.gameObject.activeSelf) { b.bubbleBob?.Kill(); b.bubbleBob = null; b.bubble.gameObject.SetActive(false); }
        }

        void OnBuildingTap(string id) { App.I.nav.Push(new FacilityScreen(id)); }
        void OnBubbleTap(string id)
        {
            if (id == "treasury" && C.Upgrades.Level("treasury") > 0 && C.Vault.Pending >= 1) { CollectVault(blds[id]); return; }
            OnBuildingTap(id);
        }

        void CollectVault(Bld b)
        {
            double g = C.Vault.Collect(); if (g < 1) return;
            Audio.Play("coin");
            var pos = b.root.anchoredPosition + new Vector2(0, b.root.sizeDelta.y + 20);
            Fx.Burst(world, pos, Icons.Get("coin"), 14, 520, null, 40, bottomOrigin: true);
            Fx.FloatText(world, pos + new Vector2(0, 40), "+" + Util.Num(g), Theme.Gold, 64, 150, 1.2f, true, bottomOrigin: true);
            UpdateBuilding(b);
        }

        // --------------------------------------------------------------------------------
        void BuildNotices()
        {
            var col = U.Rt(scene, "notices"); col.At(0.5f, 1, 0, -10, 420, 100, 0.5f, 1); U.HBox(col, 12, null, TextAnchor.MiddleCenter, true, true, false, false);
            var daily = U.IconBtn(col, Icons.Get("star"), Theme.PurpleDark, () => Popups.Show(new DailyPopup()), 84);
            dailyDot = U.Dot(daily.transform, Theme.Red, 34); dailyDot.At(1, 1, -2, -2, 34, 34, 1, 1); Tw.Pulse(dailyDot.transform, 0.2f, 0.6f);
            var ev = U.IconBtn(col, Icons.Get("scroll"), Theme.BlueDark, () => { if (C.Events.Pending.Count > 0) Popups.Show(new EventPopup(C.Content.EventMap[C.Events.Pending[0]])); else Toasts.Show(L.T("No notices right now")); }, 84);
            eventBtn = ev.R(); var ec = U.Box(ev.transform, Theme.Red, Theme.Black, "cnt"); ec.At(1, 1, -2, -2, 40, 40, 1, 1); eventCount = U.Txt(ec.transform, "", 28, Color.white); eventCount.Fill();
            var sh = U.IconBtn(col, Icons.Get("share"), Theme.GreenDark, () => Native.Share(Brand.Title, Brand.ShareText), 84);
        }

        void BuildTracker()
        {
            var p = U.Box(scene, Theme.PanelLight, Theme.ParchmentDark, "tracker", true); p.At(.5f, 0, 0, 16, size.x - 36, 252, .5f, 0);
            float textWidth = size.x - 318f;
            trackTitle = U.Txt(p.transform, "", 38, Theme.TextDark, TextAnchor.MiddleCenter); trackTitle.FitText(34); trackTitle.At(0, 1, 20, -10, textWidth, 44, 0, 1);
            trackDesc = U.Txt(p.transform, "", 32, Theme.Text, TextAnchor.MiddleCenter, false, true); trackDesc.At(0, 1, 20, -76, textWidth, 112, 0, 1);
            trackDesc.verticalOverflow = VerticalWrapMode.Truncate;
            trackBar = U.ProgressBar(p.transform, Theme.Green, textWidth, 36, "", 29); trackBar.label.FitText(27); trackBar.back.R().At(0, 0, 20, 12, textWidth, 36, 0, 0);
            trackBtn = U.Btn(p.transform, L.T("GO"), Theme.Green, OnTrack, 224, 104, 38); trackBtn.label.FitText(34); trackBtn.At(1, .5f, -20, 0, 224, 104, 1, .5f);
            Hud.Reg("hall_tracker", trackBtn);
            RefreshTracker();
        }

        void RefreshTracker()
        {
            if (!trackTitle) return;
            var q = C.Quests.CurrentMain;
            if (q == null) { trackTitle.text = L.T("Guild Master"); trackDesc.text = L.T("The main story is complete. Keep climbing the Spire!"); trackBar.Value = 1; trackBtn.gameObject.SetActive(false); return; }
            bool ready = C.Quests.State(q.id) == QuestSystem.Ready;
            trackTitle.text = L.T(q.title);
            trackDesc.text = L.T(q.desc);
            trackBar.Value = C.Quests.Fraction(q);
            var g = q.goals[0]; double cur = Math.Min(g.target, C.Quests.Progress(q, g)); trackBar.label.text = $"{Util.Num(cur)}/{Util.Num(g.target)}  {L.T(g.text ?? "")}";
            trackBtn.gameObject.SetActive(true);
            trackBtn.label.text = ready ? L.T("CLAIM") : L.T("GO");
            trackBtn.baseColor = ready ? Theme.Accent : Theme.Green; trackBtn.Apply();
            if (ready) Tw.Pulse(trackBtn.transform, 0.06f, 0.5f);
        }

        void OnTrack()
        {
            var q = C.Quests.CurrentMain; if (q == null) return;
            if (C.Quests.State(q.id) == QuestSystem.Ready) { Popups.Show(new QuestRewardPopup(q)); return; }
            var h = q.hint ?? "";
            if (h.StartsWith("tab_")) { var t = h.Substring(4); App.I.hud.GoTab((Hud.Tab)Enum.Parse(typeof(Hud.Tab), W.Cap(t))); }
            else if (h.StartsWith("bld_")) App.I.nav.Push(new FacilityScreen(h.Substring(4)));
            else Toasts.Show(L.T(q.desc));
        }

        // --------------------------------------------------------------------------------
        public override void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var w in walkers) MoveWalker(w, dt);
            double now = Time.realtimeSinceStartupAsDouble;
            if (now > nextSort) { nextSort = (float)(now + 0.15); SortWorld(); }
            if (now > nextSlow)
            {
                nextSlow = (float)(now + 0.5);
                foreach (var b in blds.Values) UpdateBuilding(b);
                RefreshTracker();
                dailyDot.gameObject.SetActive(!C.Daily.Claimed);
                int pe = C.Events.Pending.Count; eventCount.transform.parent.gameObject.SetActive(pe > 0); eventCount.text = pe.ToString();
                // keep walkers in sync with the roster
                if (walkers.Count < Math.Min(9, C.Roster.All.Count(u => !u.monster))) Redraw();
            }
        }

        void MoveWalker(Walker w, float dt)
        {
            var pos = w.rt.anchoredPosition;
            if (w.wait > 0) { w.wait -= dt; if (w.wait <= 0) PickTarget(w); return; }
            var d = w.target - pos; float dist = d.magnitude;
            if (dist < 6f) { w.wait = UnityEngine.Random.Range(1f, 4f); w.dir = 0; w.anim.playing = false; w.anim.Show(Art.Chara(w.skin, 0, 1)); return; }
            pos = Vector2.MoveTowards(pos, w.target, w.speed * dt); w.rt.anchoredPosition = pos; ground[w.rt] = pos.y;
            int dir = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? 2 : 1) : 0;
            if (dir != w.dir || !w.anim.playing) { w.dir = dir; w.anim.Play(Walk(w.skin, dir), 6, true); }
        }
        void PickTarget(Walker w)
        {
            // Walking to random points crossed the basin. Only adjacent ring nodes are connected.
            if (UnityEngine.Random.value < .12f) w.direction *= -1;
            w.node = HallPath.Next(w.node, w.direction);
            w.target = HallPath.Point(pathCenter, pathRadius, w.node);
        }

        readonly List<KeyValuePair<RectTransform, float>> sortBuf = new List<KeyValuePair<RectTransform, float>>();
        void SortWorld()
        {
            sortBuf.Clear();
            foreach (var kv in ground) if (kv.Key) sortBuf.Add(kv);
            sortBuf.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < sortBuf.Count; i++) sortBuf[i].Key.SetSiblingIndex(i);
        }
    }
}
