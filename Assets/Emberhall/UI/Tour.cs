using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    /// <summary>
    /// -gm-tour captures the full English flow and key Indonesian screens;
    /// -gm-tour-id captures the full Indonesian flow. -gm-tour-dir selects the output directory.
    /// Screenshots include inspectable text containment audits. Existing runs are preserved.
    /// App uses a separate save slot whenever Requested is true.
    /// </summary>
    public static class Tour
    {
        static string[] Args => Environment.GetCommandLineArgs();
        public static bool Requested => Args.Contains("-gm-tour") || Args.Contains("-gm-tour-id") || Verification;
        public static bool Indonesian => Args.Contains("-gm-tour-id");
        public static bool Verification => Args.Contains("-gm-verify");

        [Serializable]
        public sealed class LayoutIssue
        {
            public string kind, path, parent, text;
            public float rectWidth, rectHeight, preferredHeight, fittedHeight, preferredWidth;
            public int fontSize, renderedFontSize;
            public Vector2 minInParent, maxInParent;
            public Rect parentRect;
        }

        [Serializable]
        public sealed class ShotAudit
        {
            public string shot, language, screen;
            public int screenWidth, screenHeight, checkedTexts, skippedTexts;
            public List<LayoutIssue> issues = new List<LayoutIssue>();
        }

        [Serializable]
        public sealed class FlowCheck
        {
            public string name, detail;
            public bool passed;
        }

        [Serializable]
        public sealed class TourAudit
        {
            public string outputDirectory, requestedLanguage;
            public bool verificationMode;
            public int screenshots, checkedTexts, layoutIssues, failedFlowChecks;
            public List<ShotAudit> shots = new List<ShotAudit>();
            public List<FlowCheck> flowChecks = new List<FlowCheck>();
        }

        static TourAudit audit;

        static string OutputDirectory()
        {
            string chosen = null;
            var args = Args;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-gm-tour-dir" && i + 1 < args.Length && !args[i + 1].StartsWith("-")) chosen = args[i + 1];
                else if (args[i].StartsWith("-gm-tour-dir=")) chosen = args[i].Substring("-gm-tour-dir=".Length);
            }
            string run = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + (Indonesian ? "-id" : "-en-id");
            string dir = Path.GetFullPath(string.IsNullOrWhiteSpace(chosen) ? Path.Combine("shots", run) : chosen);
            if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any())
                dir = Path.Combine(dir, run + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(dir);
            return dir;
        }

        static IEnumerator Shot(string dir, string name, float wait = 0.5f)
        {
            float t = 0; while (t < wait) { t += Time.unscaledDeltaTime; yield return null; }
            Canvas.ForceUpdateCanvases();
            yield return null; // Let text generators and RectMask2D settle after layout.
            Canvas.ForceUpdateCanvases();
            Texture2D tex = null;
            bool captured = false;
            try
            {
                tex = CaptureCanvas();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                captured = true;
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(dir, "FAILED.txt"), "Offscreen capture failed for " + name + ": " + e);
                Debug.LogError("[GM] Offscreen visual capture failed: " + e);
                Application.Quit(1);
            }
            finally { if (tex != null) UnityEngine.Object.Destroy(tex); }
            if (!captured) yield break;
            var report = Audit(name);
            File.WriteAllText(Path.Combine(dir, name + ".audit.json"), JsonUtility.ToJson(report, true));
            audit.shots.Add(report); audit.screenshots++; audit.checkedTexts += report.checkedTexts; audit.layoutIssues += report.issues.Count;
            File.WriteAllText(Path.Combine(dir, "layout-audit.json"), JsonUtility.ToJson(audit, true));
            Debug.Log($"[GM] Visual shot {name} ({report.language}): {report.checkedTexts} texts checked, {report.issues.Count} layout issues");
        }

        static Texture2D CaptureCanvas()
        {
            if (UiHost.I == null || UiHost.I.cam == null) throw new InvalidOperationException("The UI camera is required for visual capture.");
            var camera = UiHost.I.cam;
            int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                // ScreenSpaceCamera UI renders into this texture even when the native player window
                // is hidden or minimized; screen-buffer capture is unavailable in that situation.
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                pixels.Apply(false, false);
                return pixels;
            }
            catch
            {
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
                throw;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (target != null) RenderTexture.ReleaseTemporary(target);
            }
        }

        static string ObjectPath(Transform node)
        {
            var parts = new List<string>();
            for (var n = node; n != null; n = n.parent) parts.Add(n.name + "[" + n.GetSiblingIndex() + "]");
            parts.Reverse(); return string.Join("/", parts);
        }

        static Rect BoundsIn(RectTransform child, RectTransform space)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in corners)
            {
                Vector2 p = space.InverseTransformPoint(corner); min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static bool Visible(Text text)
        {
            if (!text.isActiveAndEnabled || text.font == null || string.IsNullOrWhiteSpace(text.text) || text.color.a < 0.02f) return false;
            float alpha = text.color.a;
            for (var n = text.transform; n != null; n = n.parent)
            {
                var group = n.GetComponent<CanvasGroup>();
                if (group != null) { alpha *= group.alpha; if (alpha < 0.02f) return false; if (group.ignoreParentGroups) break; }
            }
            // Scroll children are expected to leave the viewport. Check visible cards against their
            // own parent; skip only completely clipped text, never compare it with a scroll viewport.
            for (var n = text.transform.parent; n != null; n = n.parent)
            {
                var scroll = n.GetComponentInParent<ScrollRect>();
                if (scroll == null || scroll.viewport != n) continue;
                var mask = n.GetComponent<RectMask2D>();
                var stencilMask = n.GetComponent<Mask>();
                bool clips = (mask != null && mask.isActiveAndEnabled) || (stencilMask != null && stencilMask.isActiveAndEnabled);
                if (clips && !BoundsIn(text.rectTransform, (RectTransform)n).Overlaps(((RectTransform)n).rect)) return false;
            }
            return true;
        }

        static ShotAudit Audit(string name)
        {
            var report = new ShotAudit { shot = name, language = L.Lang, screen = App.I?.nav?.Current?.Id ?? "", screenWidth = Screen.width, screenHeight = Screen.height };
            if (UiHost.I == null) return report;
            foreach (var text in UiHost.I.root.GetComponentsInChildren<Text>(false))
            {
                if (!Visible(text) || text.rectTransform.rect.width <= 1 || text.rectTransform.rect.height <= 1) { report.skippedTexts++; continue; }
                report.checkedTexts++;
                Rect box = text.rectTransform.rect;
                int renderedSize = text.fontSize;
                if (text.resizeTextForBestFit)
                {
                    int fitted = text.cachedTextGenerator.fontSizeUsedForBestFit;
                    if (fitted >= text.resizeTextMinSize && fitted <= text.resizeTextMaxSize) renderedSize = fitted;
                }
                var settings = text.GetGenerationSettings(new Vector2(box.width, 0));
                settings.resizeTextForBestFit = false; settings.fontSize = renderedSize; settings.verticalOverflow = VerticalWrapMode.Overflow;
                var generator = new TextGenerator();
                float fittedHeight = generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
                float preferredWidth = 0;
                if (text.horizontalOverflow == HorizontalWrapMode.Overflow)
                {
                    settings.generationExtents = Vector2.zero;
                    preferredWidth = generator.GetPreferredWidth(text.text, settings) / text.pixelsPerUnit;
                }
                var parent = text.transform.parent as RectTransform;
                LayoutIssue Issue(string kind) => new LayoutIssue
                {
                    kind = kind, path = ObjectPath(text.transform), parent = parent != null ? ObjectPath(parent) : "",
                    text = text.text, rectWidth = box.width, rectHeight = box.height, preferredHeight = text.preferredHeight,
                    fittedHeight = fittedHeight, preferredWidth = preferredWidth, fontSize = text.fontSize, renderedFontSize = renderedSize,
                    parentRect = parent != null ? parent.rect : default
                };
                const float tolerance = 2f;
                if (fittedHeight > box.height + tolerance) report.issues.Add(Issue("text_overflow_height"));
                if (preferredWidth > box.width + tolerance) report.issues.Add(Issue("text_overflow_width"));
                if (parent != null && parent.rect.width > 1 && parent.rect.height > 1)
                {
                    Rect bounds = BoundsIn(text.rectTransform, parent), pr = parent.rect;
                    if (bounds.xMin < pr.xMin - tolerance || bounds.xMax > pr.xMax + tolerance || bounds.yMin < pr.yMin - tolerance || bounds.yMax > pr.yMax + tolerance)
                    {
                        var issue = Issue("text_rect_outside_parent"); issue.minInParent = bounds.min; issue.maxInParent = bounds.max; report.issues.Add(issue);
                    }
                }
            }
            return report;
        }

        static IEnumerator OnboardingShots(App app, string dir, string prefix = "")
        {
            app.StartOnboarding();
            var ob = (OnboardingScreen)app.nav.Current;
            var pagesField = typeof(OnboardingScreen).GetField("Pages", BindingFlags.NonPublic | BindingFlags.Static);
            int pages = pagesField != null && pagesField.IsLiteral ? (int)pagesField.GetRawConstantValue() : 5;
            for (int i = 0; i < pages; i++)
            {
                if (i > 0)
                {
                    var footer = ob.Root.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(r => r.name == "footer");
                    var next = footer?.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.label != null && b.Interactable && b.onClick != null);
                    if (!Check(prefix + "onboarding_continue_" + i, Click(next), "Advance the onboarding through its actual footer button.")) yield break;
                }
                yield return Shot(dir, prefix + (2 + i).ToString("00") + "_onboard" + (1 + i), i < 3 ? 3.4f : 0.5f);
            }
        }

        static bool Check(string name, bool passed, string detail)
        {
            audit.flowChecks.Add(new FlowCheck { name = name, passed = passed, detail = detail });
            if (!passed) audit.failedFlowChecks++;
            File.WriteAllText(Path.Combine(audit.outputDirectory, "layout-audit.json"), JsonUtility.ToJson(audit, true));
            Debug.Log($"[GM] Flow check {name}: {(passed ? "PASS" : "FAIL")} — {detail}");
            return passed;
        }

        static UButton TargetButton(string key)
        {
            if (!Hud.Targets.TryGetValue(key, out var target) || target == null || !target.gameObject.activeInHierarchy) return null;
            return target.GetComponent<UButton>() ?? target.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.Interactable && b.onClick != null);
        }

        static bool Click(UButton button)
        {
            if (button == null || !button.isActiveAndEnabled || !button.Interactable || button.onClick == null) return false;
            button.OnPointerClick(null); // The same guarded click handler used by the player's tap.
            return true;
        }

        static void RevealInScroll(RectTransform target)
        {
            if (target == null) return;
            Canvas.ForceUpdateCanvases();
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null || scroll.viewport == null || !scroll.vertical) return;
            float range = Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height);
            float desired = scroll.content.rect.yMax - BoundsIn(target, scroll.content).center.y - scroll.viewport.rect.height * 0.5f;
            var p = scroll.content.anchoredPosition; p.y = Mathf.Clamp(desired, 0, range);
            scroll.StopMovement(); scroll.content.anchoredPosition = p;
        }

        static IEnumerator StartFromCharter(App app, string dir)
        {
            var root = app.nav.Current.Root;
            var name = root.GetComponentInChildren<InputField>(false);
            if (!Check("charter_name_input", name != null, "The charter must contain an editable guild name.")) yield break;
            name.text = "Ember Wardens";
            var archer = root.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.name == "starter" && b.GetComponentsInChildren<Text>(false).Any(t => t.text == L.T("Archer")));
            if (!Check("starter_choice_click", Click(archer), "Select Archer through the actual starter-card click handler.")) yield break;
            yield return null; // Redraw replaces the charter controls.
            var selected = app.nav.Current.Root.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.name == "starter" && b.GetComponentsInChildren<Text>(false).Any(t => t.text == L.T("Archer")));
            if (selected != null) RevealInScroll(selected.R());
            yield return Shot(dir, "06b_onboard_archer");
            var begin = app.nav.Current.Root.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.label != null && b.label.text == StoryScript.Begin);
            if (!Check("charter_begin_click", Click(begin), "Start through the actual charter footer button.")) yield break;
            yield return null;
            Check("charter_values_applied", app.InGame && App.Ctx.Roster.Count == 1 && App.Ctx.Roster.All[0].classId == "archer" && App.Ctx.Meta.GuildName == "Ember Wardens", "The game must create the named guild with exactly the selected Archer starter.");
        }

        static int PartyLabels(PartyPopup popup) => popup.Body.GetComponentsInChildren<Text>(false).Count(t => t.text == L.T("In party"));

        static UButton LabeledButton(Component scope, params string[] labels)
        {
            if (scope == null) return null;
            var localized = labels.Select(label => L.T(label)).ToArray();
            return scope.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.label != null && localized.Contains(b.label.text) && b.Interactable && b.onClick != null);
        }

        static IEnumerator ClaimGuidedQuest(string dir, string questId, string shotPrefix)
        {
            var quests = App.Ctx.Quests;
            if (!Check(questId + "_ready", quests.CurrentMain?.id == questId && quests.State(questId) == QuestSystem.Ready,
                "The completed chapter must be current and ready before its reward can be claimed.")) yield break;
            yield return Shot(dir, shotPrefix + "_ready");
            var coachClaim = LabeledButton(UiHost.I.top, "CLAIM REWARD");
            if (!Check(questId + "_coach_reward_click", Click(coachClaim) && Popups.Top is QuestRewardPopup,
                "Use the visible guidance card's CLAIM REWARD action.")) yield break;
            yield return Shot(dir, shotPrefix + "_reward");
            var claim = LabeledButton(Popups.Top.Root, "CLAIM");
            if (claim != null) RevealInScroll(claim.R());
            if (!Check(questId + "_claim_click", Click(claim) && quests.State(questId) == QuestSystem.Done,
                "Claim the earned chapter reward through the reward popup's actual button.")) yield break;
            yield return null;
        }

        static IEnumerator FirstLoop(App app, string dir)
        {
            var C = App.Ctx;
            Popups.CloseAll();
            if (!Check("open_starting_tavern", Click(TargetButton("bld_tavern")), "Open the Tavern from the hall's real building button.")) yield break;
            yield return Shot(dir, "07b_first_tavern");
            var hire = TargetButton("btn_hire");
            if (hire != null) RevealInScroll(hire.R());
            yield return Shot(dir, "07c_first_hire_ready");
            int rosterBefore = C.Roster.Count; double goldBefore = C.Wallet.Get("gold");
            if (!Check("affordable_hire_click", Click(hire), "Tap the game's enabled hire button, without adding gold or changing candidate prices.")) yield break;
            yield return Shot(dir, "07d_first_hire_complete");
            Check("starting_hire_spends_gold", C.Roster.Count == rosterBefore + 1 && C.Wallet.Get("gold") < goldBefore,
                $"Heroes {rosterBefore} → {C.Roster.Count}; gold {goldBefore:0} → {C.Wallet.Get("gold"):0}.");
            app.hud.GoTab(Hud.Tab.Hall, true);
            yield return null;
            if (!Check("open_first_quest_reward", Click(LabeledButton(UiHost.I.top, "CLAIM REWARD")) && Popups.Top is QuestRewardPopup, "The first-steps guide opens the completed hire quest's reward.")) yield break;
            yield return Shot(dir, "07e_first_quest_reward");
            var claim = Popups.Top.Root.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.label != null && b.label.text == L.T("CLAIM"));
            if (!Check("claim_first_quest", Click(claim) && C.Quests.State("q_m01") == QuestSystem.Done, "Claim the earned reward through its button.")) yield break;
            if (!Check("open_starting_board", Click(TargetButton("tab_board")), "Use the real Board navigation button.")) yield break;
            yield return Shot(dir, "07f_first_board");
            if (!Check("open_first_party", Click(TargetButton("btn_main")) && Popups.Top is PartyPopup, "The first campaign mission's button must open party preparation.")) yield break;
            yield return Shot(dir, "07g_first_party");
            var popup = (PartyPopup)Popups.Top;
            int labelsBefore = PartyLabels(popup);
            var heroRow = popup.Body.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.name == "h" && b.onClick != null);
            if (!Check("party_member_toggle", Click(heroRow), "Tap a party hero row to remove its auto-selected member.")) yield break;
            yield return Shot(dir, "07h_first_party_changed");
            Check("party_selection_updates", PartyLabels(popup) == labelsBefore - 1, $"Visible 'In party' labels {labelsBefore} → {PartyLabels(popup)}.");
            heroRow = popup.Body.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.name == "h" && b.onClick != null);
            if (!Check("party_member_restore_click", Click(heroRow), "Tap the actual hero row again to restore the first party.")) yield break;
            yield return Shot(dir, "07i_first_party_restored");
            if (!Check("party_selection_restores", PartyLabels(popup) == labelsBefore && TargetButton("btn_party_go")?.Interactable == true, "Selecting the hero again restores the ready party and enables FIGHT.")) yield break;
            int clearsBefore = C.Missions.Clears("m_r1_1");
            if (!Check("first_fight_click", Click(TargetButton("btn_party_go")), "Start the first battle through the real FIGHT button, with the recruited party and original resources.")) yield break;
            yield return Shot(dir, "07j_first_battle", 1.2f);
            if (!Check("first_battle_screen", app.nav.Current is BattleScreen, "FIGHT must navigate to the live battle screen.")) yield break;
            float battleGuard = 0;
            while (!(Popups.Top is ResultPopup) && battleGuard < 60) { battleGuard += Time.unscaledDeltaTime; yield return null; }
            if (!Check("first_battle_finishes", Popups.Top is ResultPopup, "The real starting battle must show its result within 60 seconds.")) yield break;
            yield return Shot(dir, "07k_first_victory", 1.5f);
            var result = (ResultPopup)Popups.Top;
            if (!Check("first_battle_victory", result.Title == L.T("VICTORY!") && C.Missions.Clears("m_r1_1") > clearsBefore,
                "The recruited starting party must win Slime Meadow and record the first clear.")) yield break;
            var leave = LabeledButton(result.Root, "CONTINUE", "Continue", "Back");
            if (!Check("first_result_return_click", Click(leave), "Use the actual result button that returns to the Board.")) yield break;
            yield return null;
            if (!Check("first_result_returns_board", app.nav.Current is BoardScreen && !(Popups.Top is ResultPopup), "Leaving the result must restore the Board and its next-step guidance.")) yield break;
            int failuresBefore = audit.failedFlowChecks;
            yield return ClaimGuidedQuest(dir, "q_m02", "07l_first_win_quest");
            if (audit.failedFlowChecks > failuresBefore) yield break;
            if (!Check("first_bag_tab_click", Click(TargetButton("tab_bag")), "Navigate to the Bag through its actual bottom-tab button.")) yield break;
            yield return Shot(dir, "07m_first_bag");
            var autoEquip = TargetButton("btn_main");
            if (!Check("first_auto_equip_click", app.nav.Current is BagScreen && Click(autoEquip), "Use the Bag's actual auto-equip action to fit the best owned gear.")) yield break;
            yield return Shot(dir, "07n_first_equipped");
            if (!Check("first_equip_quest_ready", C.Quests.State("q_m03") == QuestSystem.Ready || C.Quests.State("q_m03") == QuestSystem.Done,
                "The equipment chapter must recognize the owned and equipped starting gear.")) yield break;
            if (C.Quests.State("q_m03") == QuestSystem.Ready)
            {
                failuresBefore = audit.failedFlowChecks;
                yield return ClaimGuidedQuest(dir, "q_m03", "07o_first_equip_quest");
                if (audit.failedFlowChecks > failuresBefore) yield break;
            }
            if (!Check("first_fourth_chapter_unlocks", C.Quests.State("q_m03") == QuestSystem.Done && C.Quests.CurrentMain?.id == "q_m04",
                "Claiming the equipment reward must unlock the fourth chapter with an actionable next mission.")) yield break;
            yield return Shot(dir, "07p_first_next_chapter");
            if (!Check("first_journal_open", Click(TargetButton("tab_quests")) && Click(LabeledButton(app.nav.Current.Root, "Journal")),
                "The Journal tab opens from the quest page.")) yield break;
            yield return Shot(dir, "07q_story_journal");
            Check("first_journal_history", app.nav.Current.Root.GetComponentsInChildren<Text>(false).Any(t => t.text == L.T(C.Content.Quests.First(q => q.id == "q_m01").story)),
                "The journal preserves the completed opening chapter's authored story.");
            Check("first_journal_no_spoilers", !app.nav.Current.Root.GetComponentsInChildren<Text>(false).Any(t => t.text == L.T(C.Content.Quests.First(q => q.id == "q_m25").story)),
                "Unreached story chapters remain hidden.");
            Check("first_journal_return_story", Click(LabeledButton(app.nav.Current.Root, "Story")), "The Story tab returns to the current actionable quest.");
            if (!Check("first_return_hall_tab", Click(TargetButton("tab_hall")), "Return to the Hall using the actual navigation tab.")) yield break;
            yield return null;
        }

        static IEnumerator LocalizedShots(App app, string dir)
        {
            L.Load("id"); Popups.CloseAll();
            app.hud.GoTab(Hud.Tab.Heroes, true); yield return Shot(dir, "id_09_heroes");
            app.hud.GoTab(Hud.Tab.Board, true); yield return Shot(dir, "id_11_board");
            Popups.Show(new PartyPopup(App.Ctx.Content.Mission("m_r1_1"))); yield return Shot(dir, "id_12_party"); Popups.CloseAll();
            app.hud.GoTab(Hud.Tab.Bag, true);
            Popups.Show(new ItemPopup(App.Ctx.Inventory.Gear.First().uid)); yield return Shot(dir, "id_16_item"); Popups.CloseAll();
            Popups.Show(new SettingsPopup()); yield return Shot(dir, "id_25_settings"); Popups.CloseAll();
            app.GoTitle(); yield return Shot(dir, "id_01_title");
            yield return OnboardingShots(app, dir, "id_");
        }

        static UButton NamedButton(Component scope, string name) => scope == null ? null :
            scope.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.name == name && b.onClick != null && b.Interactable);

        static UButton ButtonContaining(Component scope, string text, bool enabled = true)
        {
            if (scope == null) return null;
            return scope.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.onClick != null && (!enabled || b.Interactable) &&
                b.GetComponentsInChildren<Text>(false).Any(t => t.text == text));
        }

        static UButton MenuButton => UiHost.I.hud.GetComponentsInChildren<UButton>(false)
            .FirstOrDefault(b => b.icon != null && b.icon.sprite == Icons.Get("menu") && b.onClick != null);

        static bool CloseTop(string check)
        {
            var popup = Popups.Top;
            return Check(check, popup != null && Click(NamedButton(popup.Root, "close")) && !popup.IsOpen,
                "The popup's close control must dismiss its own panel.");
        }

        static UButton RowButton(Component scope, string cardName, string text = null)
        {
            if (scope == null) return null;
            var rows = scope.GetComponentsInChildren<RectTransform>(false).Where(r => r.name == cardName);
            if (text != null) rows = rows.Where(r => r.GetComponentsInChildren<Text>(false).Any(t => t.text == text));
            return rows.SelectMany(r => r.GetComponentsInChildren<UButton>(false)).FirstOrDefault(b => b.onClick != null && b.Interactable);
        }

        static IEnumerator OpenFacility(App app, string id)
        {
            Popups.CloseAll();
            if (!Check("v_hall_navigation_" + id, Click(TargetButton("tab_hall")), "Use the Hall navigation tab before opening a facility.")) yield break;
            yield return null;
            Check("v_facility_open_" + id, Click(TargetButton("bld_" + id)) && app.nav.Current is FacilityScreen page && page.FacilityId == id,
                "The hall building control must open the matching facility page.");
            yield return new WaitForSecondsRealtime(.35f);
        }

        static IEnumerator CompleteUiTimer(App app, TimedTask task, string cardName, string check)
        {
            if (!Check(check + "_pending", task != null && App.Ctx.Timers.Tasks.Contains(task) && !task.done,
                "The action must create a real pending timer before completion.")) yield break;
            double gems = App.Ctx.Wallet.Get("gems");
            int price = App.Ctx.Timers.SpeedCost(task);
            var button = RowButton(app.nav.Current.Root, cardName);
            if (!Check(check + "_finish_click", Click(button), "Complete the timer through its visible gem speed-up button.")) yield break;
            yield return new WaitForSecondsRealtime(.35f);
            double spent = gems - App.Ctx.Wallet.Get("gems");
            Check(check + "_finished", (task.done || !App.Ctx.Timers.Tasks.Contains(task)) && spent > 0 && spent <= price,
                $"The timer completed and gems were charged once ({spent:0}, displayed cost {price}).");
        }

        static IEnumerator VerifySettings(App app, string dir)
        {
            Popups.CloseAll(); app.hud.GoTab(Hud.Tab.Hall, true);
            if (!Check("v_menu_open", Click(MenuButton) && Popups.Top is MenuPopup, "The HUD menu must open its actual menu panel.")) yield break;
            if (!Check("v_settings_open", Click(LabeledButton(Popups.Top.Root, "Settings")) && Popups.Top is SettingsPopup,
                "Open Settings through the menu action.")) yield break;
            var settings = (SettingsPopup)Popups.Top;
            float music = Prefs.Music, sfx = Prefs.Sfx; int fps = Prefs.Fps;
            var sliders = settings.Body.GetComponentsInChildren<Slider>(false);
            if (!Check("v_audio_sliders_present", sliders.Length == 2, "Both music and sound effect sliders must be available.")) yield break;
            sliders[0].value = .27f; sliders[1].value = .41f;
            Check("v_audio_sliders_apply", Mathf.Abs(Prefs.Music - .27f) < .001f && Mathf.Abs(Prefs.Sfx - .41f) < .001f,
                "Slider value-change events must update both audio preferences.");
            sliders[0].value = music; sliders[1].value = sfx;
            var keys = new[] { "Vibration", "Tutorial hints", "Battle effects" };
            Func<bool>[] getters = { () => Prefs.Vibrate, () => Prefs.Hints, () => Prefs.BattleFx };
            for (int i = 0; i < keys.Length; i++)
            {
                bool before = getters[i]();
                var toggle = RowButton(settings.Body, "card", L.T(keys[i]));
                if (!Check("v_toggle_" + i, Click(toggle) && getters[i]() != before, "The " + keys[i] + " switch must update its setting.")) yield break;
                if (!Check("v_toggle_restore_" + i, Click(toggle) && getters[i]() == before, "Restore the original switch setting through the same control.")) yield break;
            }
            if (!Check("v_frame_rate_click", Click(LabeledButton(settings.Root, "30 FPS")) && Prefs.Fps == 30 && Application.targetFrameRate == 30,
                "The 30 FPS control must apply the running frame rate.")) yield break;
            yield return null;
            if (!Check("v_frame_rate_restore", Click(LabeledButton(settings.Root, fps + " FPS")) && Prefs.Fps == fps,
                "Restore the original frame rate through its control.")) yield break;
            yield return null;
            var screen = app.nav.Current;
            if (!Check("v_language_indonesian_click", Click(LabeledButton(settings.Root, L.DisplayName("id"))) && L.Lang == "id" && Prefs.Lang == "id",
                "Manual Indonesian selection must update the language and its persisted preference.")) yield break;
            yield return new WaitForSecondsRealtime(.35f);
            Check("v_language_keeps_screen", app.nav.Current == screen && Popups.Top is SettingsPopup && settings.TitleText.text == L.T("Settings"),
                "Changing language must preserve the open game page and translate the settings panel.");
            yield return Shot(dir, "verify_01_settings_id");
            if (!Check("v_language_english_click", Click(LabeledButton(settings.Root, L.DisplayName("en"))) && L.Lang == "en" && Prefs.Lang == "en",
                "Manual English selection must restore English and save the preference.")) yield break;
            yield return new WaitForSecondsRealtime(.35f);
            Check("v_hud_language_updates", TargetButton("tab_hall").GetComponentsInChildren<Text>(false).Any(t => t.text == "Hall"),
                "Persistent navigation labels must follow the language change.");
            if (!Check("v_reset_confirmation_open", Click(LabeledButton(settings.Root, "Reset all progress")) && Popups.Top is MsgPopup,
                "Reset opens an explicit confirmation before changing progress.")) yield break;
            int count = App.Ctx.Roster.Count;
            if (!Check("v_reset_cancel_preserves_guild", Click(LabeledButton(Popups.Top.Root, "Cancel")) && App.Ctx.Roster.Count == count && app.InGame,
                "Cancel must preserve the test guild, roster, and current session.")) yield break;
            yield return null;
            CloseTop("v_settings_close");
            if (!Check("v_about_menu_open", Click(MenuButton) && Click(LabeledButton(Popups.Top.Root, "About")) && Popups.Top is AboutPopup,
                "About must open through the actual menu action.")) yield break;
            CloseTop("v_about_close");
        }

        static UButton BagGearButton(App app, ItemInstance wanted)
        {
            var C = App.Ctx;
            var list = C.Inventory.Gear.OrderByDescending(g => C.Content.Rarities.FindIndex(r => r.id == g.rarity))
                .ThenByDescending(g => C.Content.Item(g.defId).tier).ThenByDescending(g => g.enh).ToList();
            int index = list.IndexOf(wanted);
            var buttons = app.nav.Current.Root.GetComponentsInChildren<UButton>(false).Where(b => b.name == "item" && b.onClick != null).ToArray();
            return index >= 0 && index < buttons.Length ? buttons[index] : null;
        }

        static IEnumerator OpenGear(App app, ItemInstance gear, string check)
        {
            Popups.CloseAll(); Bag.OpenTab = 0; Bag.Filter = 0; Bag.Sort = 0;
            if (!Check(check + "_bag", Click(TargetButton("tab_bag")), "Open Bag through the actual navigation tab.")) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            Check(check + "_item", Click(BagGearButton(app, gear)) && Popups.Top is ItemPopup && Popups.Top.Title == App.Ctx.Content.Item(gear.defId).name,
                "The inventory cell must open the matching item detail panel.");
            yield return new WaitForSecondsRealtime(.2f);
        }

        static IEnumerator VerifyInventory(App app, string dir)
        {
            var C = App.Ctx;
            var hero = C.Roster.All[0];
            var gear = C.Inventory.AddItem("w_bow_2", 1, C.Rng, "rare");
            yield return OpenGear(app, gear, "v_gear_open");
            if (!(Popups.Top is ItemPopup)) yield break;
            var popup = Popups.Top;
            if (!Check("v_item_lock", Click(LabeledButton(popup.Root, "Lock")) && gear.locked, "Lock must protect the selected gear.")) yield break;
            yield return null;
            var sell = popup.Root.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.label != null && b.label.text.StartsWith(L.T("Sell for {0}", "")));
            double gold = C.Wallet.Get("gold");
            if (sell != null) sell.OnPointerClick(null);
            Check("v_locked_sell_blocked", sell != null && !sell.Interactable && C.Inventory.Get(gear.uid) == gear && C.Wallet.Get("gold") == gold,
                "A locked item's sale control must be disabled and its guarded click must leave the item and gold intact.");
            if (!Check("v_item_unlock", Click(LabeledButton(popup.Root, "Unlock")) && !gear.locked, "Unlock must restore availability of the item actions.")) yield break;
            yield return null;
            if (!Check("v_item_equip_picker", Click(LabeledButton(popup.Root, "Equip")) && Popups.Top is HeroPickPopup,
                "The item's Equip action must open hero selection.")) yield break;
            if (!Check("v_item_equip", Click(ButtonContaining(Popups.Top.Body, hero.name)) && gear.owner == hero.uid && hero.equip[0] == gear.uid,
                "Selecting the compatible Archer equips this exact item and updates both owner and weapon slot.")) yield break;
            yield return null;
            Popups.CloseAll();
            if (!Check("v_heroes_navigation", Click(TargetButton("tab_heroes")), "Open the Heroes tab.")) yield break;
            yield return null;
            if (!Check("v_hero_detail_open", Click(ButtonContaining(app.nav.Current.Root, hero.name)) && Popups.Top is HeroPopup,
                "The hero card must open its hero details.")) yield break;
            var heroPopup = Popups.Top;
            var weaponSlot = heroPopup.Body.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.name == "slot");
            if (!Check("v_weapon_picker_open", Click(weaponSlot) && Popups.Top is ItemPickerPopup, "The hero's weapon slot opens the compatible equipment picker.")) yield break;
            if (!Check("v_unequip_click", Click(LabeledButton(Popups.Top.Root, L.T("Unequip {0}", C.Content.Item(gear.defId).name))) &&
                string.IsNullOrEmpty(gear.owner) && string.IsNullOrEmpty(hero.equip[0]), "Unequip must clear the owner and hero weapon slot without destroying gear.")) yield break;
            yield return null;
            Check("v_hero_detail_refresh", heroPopup.Body.GetComponentsInChildren<UButton>(false).First(b => b.name == "slot")
                .GetComponentsInChildren<Text>(false).Any(t => t.text == L.T("Empty")),
                "The still-open hero details must redraw the empty equipment slot after unequipping.");
            CloseTop("v_hero_close");
            yield return OpenGear(app, gear, "v_enhance_open");
            int enh = gear.enh; gold = C.Wallet.Get("gold"); int ore = C.Inventory.Count("iron_ore");
            if (!Check("v_enhance_click", Click(LabeledButton(Popups.Top.Root, "ENHANCE")) && gear.enh == enh + 1 && C.Wallet.Get("gold") < gold && C.Inventory.Count("iron_ore") < ore,
                "The guaranteed first enhancement must improve gear and charge both gold and ore once.")) yield break;
            yield return Shot(dir, "verify_06_enhanced_item");
            var salvage = Popups.Top.Root.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.label != null && b.label.text.StartsWith(L.T("Salvage") + "  ") && b.Interactable);
            if (!Check("v_salvage_confirmation", Click(salvage) && Popups.Top is MsgPopup, "Salvage requires an explicit confirmation panel.")) yield break;
            if (!Check("v_salvage_cancel", Click(LabeledButton(Popups.Top.Root, "Cancel")) && C.Inventory.Get(gear.uid) == gear,
                "Cancel preserves the enhanced item.")) yield break;
            yield return null;
            salvage = Popups.Top.Root.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.label != null && b.label.text.StartsWith(L.T("Salvage") + "  ") && b.Interactable);
            ore = C.Inventory.Count("iron_ore");
            if (!Check("v_salvage_confirm", Click(salvage) && Click(LabeledButton(Popups.Top.Root, "Salvage")) &&
                C.Inventory.Get(gear.uid) == null && C.Inventory.Count("iron_ore") > ore, "Confirming salvage removes the item and adds the displayed salvage materials.")) yield break;
            yield return null;
            var spare = C.Inventory.AddItem("w_bow_1", 1, C.Rng, "common");
            yield return OpenGear(app, spare, "v_sell_open");
            gold = C.Wallet.Get("gold"); double value = C.Inventory.SellValue(spare);
            if (!Check("v_sell_item", Click(LabeledButton(Popups.Top.Root, L.T("Sell for {0}", Util.Num(value)))) && C.Inventory.Get(spare.uid) == null &&
                Math.Abs(C.Wallet.Get("gold") - gold - value) < .001, "Selling free gear removes the exact item and pays its displayed gold value once.")) yield break;
            yield return null;
            Check("v_sold_item_removed_from_bag", app.nav.Current.Root.GetComponentsInChildren<UButton>(false).Count(b => b.name == "item" && b.onClick != null) == C.Inventory.Used,
                "The redrawn Bag cell count must match the remaining inventory after the sale.");
            if (!Check("v_items_tab", Click(LabeledButton(app.nav.Current.Root, "Items")) && Bag.OpenTab == 1, "The Bag Items tab must switch to material and consumable stacks.")) yield break;
            yield return null;
            var stacks = C.Inventory.Stacks.Where(s => s.Value > 0).Select(s => (def: C.Content.Item(s.Key), n: (int)s.Value)).Where(x => x.def != null)
                .OrderBy(x => x.def.type).ThenBy(x => x.def.name).ToList();
            int potionIndex = stacks.Where(s => s.def.type == "consumable").ToList().FindIndex(s => s.def.id == "potion_s");
            var cons = app.nav.Current.Root.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(r => r.name == "cons");
            var potionCell = cons?.GetComponentsInChildren<UButton>(false).Where(b => b.name == "stack").ElementAtOrDefault(potionIndex);
            int potions = C.Inventory.Count("potion_s");
            C.Roster.SetHp(hero, C.Roster.MaxHp(hero) * .4);
            double hp = C.Roster.Hp(hero);
            if (!Check("v_potion_detail_open", Click(potionCell) && Popups.Top is StackPopup, "The potion stack cell must open its stack details.")) yield break;
            if (!Check("v_potion_use_picker", Click(LabeledButton(Popups.Top.Root, "Use")) && Popups.Top is HeroPickPopup,
                "Use must ask which hero receives the potion.")) yield break;
            if (!Check("v_potion_use", Click(ButtonContaining(Popups.Top.Body, hero.name)) && C.Inventory.Count("potion_s") == potions - 1 && C.Roster.Hp(hero) > hp,
                "Using a potion must consume exactly one potion and heal the selected hero.")) yield break;
            yield return null;
            var materials = stacks.Where(s => s.def.type == "material").ToList();
            int oreIndex = materials.FindIndex(s => s.def.id == "iron_ore");
            var mats = app.nav.Current.Root.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(r => r.name == "mats");
            var oreCell = mats?.GetComponentsInChildren<UButton>(false).Where(b => b.name == "stack").ElementAtOrDefault(oreIndex);
            if (!Check("v_material_detail_open", Click(oreCell) && Popups.Top is StackPopup, "The actual ore stack cell must open its material details.")) yield break;
            ore = C.Inventory.Count("iron_ore"); gold = C.Wallet.Get("gold");
            if (!Check("v_stack_sell_one", Click(LabeledButton(Popups.Top.Root, "Sell 1")) && C.Inventory.Count("iron_ore") == ore - 1 && C.Wallet.Get("gold") > gold,
                "Sell 1 must consume one material and credit its gold value.")) yield break;
            yield return null; CloseTop("v_stack_close");
            yield return null;
            if (!Check("v_market_tab", Click(LabeledButton(app.nav.Current.Root, "Market")) && Bag.OpenTab == 2, "The Market tab must display merchant offers.")) yield break;
            yield return null;
            var offer = C.Market.Offers.First(o => !o.sold);
            int itemBefore = C.Inventory.Count(offer.itemId); double currency = C.Wallet.Get(offer.cur);
            var offerCard = app.nav.Current.Root.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(r => r.name == "offer" &&
                r.GetComponentsInChildren<Text>(false).Any(t => t.text == L.T(C.Content.Item(offer.itemId).name) + (offer.qty > 1 ? " x" + offer.qty : "")));
            if (!Check("v_market_purchase", Click(RowButton(offerCard, "offer")) && offer.sold && C.Inventory.Count(offer.itemId) == itemBefore + offer.qty &&
                Math.Abs(C.Wallet.Get(offer.cur) - currency + offer.price) < .001, "Buying an offer must consume the displayed currency, add its full quantity, and mark it sold.")) yield break;
            yield return Shot(dir, "verify_07_market_sold");
        }

        static IEnumerator VerifyFacilities(App app, string dir)
        {
            var C = App.Ctx;
            // This fixture is introduced only after the untouched charter, hire, first victory,
            // quest claims and auto-equip flow have passed. It makes later unlocked UI reachable.
            C.Wallet.Add("gold", 20000); C.Wallet.Add("gems", 2000);
            foreach (var def in C.Content.Items.Where(i => i.type == "material")) C.Inventory.AddItem(def.id, 100, C.Rng);
            C.Counters.Set("missions.won", Math.Max(6, C.Counters.Get("missions.won")));
            C.Ranks.Load("{\"i\":2}"); C.Mods.Dirty(); C.Roster.Dirty();
            // Exercise the widest authored name through every later hero, item and battle flow.
            C.Roster.All[0].name = "Garrick Moonwhisper";
            if (C.Roster.Count > 1) C.Roster.All[1].name = "Brenna Moonwhisper";
            foreach (var hero in C.Roster.All) C.Roster.SetHp(hero, C.Roster.MaxHp(hero));
            C.Changed();
            Check("v_later_game_fixture", C.Ranks.Index == 2 && C.Wallet.Get("gold") >= 20000,
                "After the real first loop, test-only Rank D and resources unlock later controls; the player's save and preferences use separate test slots.");
            foreach (var id in new[] { "blacksmith", "training", "infirmary", "barracks", "treasury", "library" })
            {
                yield return OpenFacility(app, id);
                if (!(app.nav.Current is FacilityScreen page) || page.FacilityId != id) yield break;
                int before = C.Upgrades.Level(id); double gold = C.Wallet.Get("gold");
                if (!Check("v_build_" + id, Click(LabeledButton(page.Root, "BUILD")) && C.Upgrades.Pending(id) != null && C.Wallet.Get("gold") < gold,
                    "BUILD must charge the facility cost and create its pending construction timer.")) yield break;
                yield return new WaitForSecondsRealtime(.35f);
                yield return CompleteUiTimer(app, C.Upgrades.Pending(id), "up", "v_build_" + id);
                if (!Check("v_build_level_" + id, C.Upgrades.Level(id) == before + 1 && C.Upgrades.Pending(id) == null,
                    "Completing construction unlocks the facility's tools and clears the build timer.")) yield break;
                yield return Shot(dir, "verify_02_built_" + id);
                var back = page.Root.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.icon != null && b.icon.sprite == Icons.Get("arrow_l"));
                if (!Check("v_facility_back_" + id, Click(back) && app.nav.Current is HallScreen, "The facility's back arrow returns to the Hall.")) yield break;
                yield return null;
            }
            yield return OpenFacility(app, "tavern");
            int tavernLevel = C.Upgrades.Level("tavern");
            if (!Check("v_tavern_upgrade", Click(LabeledButton(app.nav.Current.Root, "UPGRADE")) && C.Upgrades.Pending("tavern") != null,
                "UPGRADE starts the next Tavern level rather than rebuilding the existing level.")) yield break;
            yield return new WaitForSecondsRealtime(.35f);
            yield return CompleteUiTimer(app, C.Upgrades.Pending("tavern"), "up", "v_tavern_upgrade");
            Check("v_tavern_upgraded_level", C.Upgrades.Level("tavern") == tavernLevel + 1, "The Tavern's upgrade must increase its level exactly once.");
            var candidates = C.Recruiter.Pool.Select(u => u.uid).ToList(); double gems = C.Wallet.Get("gems");
            var refresh = RowButton(app.nav.Current.Root, "hdr");
            if (!Check("v_tavern_refresh", Click(refresh) && !candidates.SequenceEqual(C.Recruiter.Pool.Select(u => u.uid)),
                "The recruitment refresh control replaces the displayed candidate pool.")) yield break;
            yield return new WaitForSecondsRealtime(.35f);
            Check("v_tavern_refresh_cards", C.Recruiter.Pool.All(u => app.nav.Current.Root.GetComponentsInChildren<Text>(false).Any(t => t.text == u.name)) && C.Wallet.Get("gems") <= gems,
                "Every new recruit has a corresponding visible card after refresh.");
            if (!Check("v_beast_den_tab", Click(LabeledButton(app.nav.Current.Root, "Beast Den")), "The unlocked Beast Den tab must respond.")) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            var tame = LabeledButton(app.nav.Current.Root, "TAME"); int roster = C.Roster.Count;
            if (roster < C.Roster.Capacity && tame != null)
            {
                if (!Check("v_monster_tame", Click(tame) && C.Roster.Count == roster + 1 && C.Roster.All.Last().monster,
                    "Taming adds exactly one unlocked monster to the roster.")) yield break;
                yield return new WaitForSecondsRealtime(.2f);
                Check("v_monster_tame_card", app.nav.Current.Root.GetComponentsInChildren<Text>(false).Any(t => t.text == L.T("Tamed")),
                    "The tamed monster's card must show its completed state.");
            }
            yield return OpenFacility(app, "blacksmith");
            var craft = LabeledButton(app.nav.Current.Root, "CRAFT");
            int gearCount = C.Inventory.Used; double goldBefore = C.Wallet.Get("gold");
            if (!Check("v_forge_start", Click(craft) && C.Crafting.Active.Count() == 1 && C.Wallet.Get("gold") < goldBefore,
                "CRAFT must spend recipe resources and occupy a real forge slot.")) yield break;
            var craftTask = C.Crafting.Active.First(); var recipe = C.Content.RecipeMap[craftTask.refId]; int madeBefore = C.Inventory.Count(recipe.result);
            yield return new WaitForSecondsRealtime(.35f);
            yield return Shot(dir, "verify_03_forge_active");
            yield return CompleteUiTimer(app, craftTask, "craft", "v_forge");
            if (!Check("v_forge_result", !C.Crafting.Active.Any() && C.Inventory.Count(recipe.result) >= madeBefore + recipe.n && C.Inventory.Used > gearCount,
                "Finishing the forge adds the chosen recipe's gear and frees its slot.")) yield break;
            if (!Check("v_enhance_tab", Click(LabeledButton(app.nav.Current.Root, "Enhance")), "The Blacksmith enhancement tab must respond.")) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            Check("v_enhance_grid", NamedButton(app.nav.Current.Root, "item") != null, "The enhancement tab must display clickable owned gear.");

            yield return OpenFacility(app, "training");
            var station = C.Stations.ByKind("train");
            var trainee = C.Roster.All.First(u => C.Stations.Blocker(station, u) == null);
            int level = trainee.level; double xp = trainee.xp; goldBefore = C.Wallet.Get("gold");
            if (!Check("v_training_picker", Click(RowButton(app.nav.Current.Root, "slot")) && Popups.Top is HeroPickPopup,
                "An empty training slot opens the hero picker.")) yield break;
            if (!Check("v_training_start", Click(ButtonContaining(Popups.Top.Body, trainee.name)) && C.Roster.IsBusy(trainee) && C.Wallet.Get("gold") < goldBefore,
                "Selecting the eligible hero occupies a slot and charges the training fee.")) yield break;
            yield return new WaitForSecondsRealtime(.35f);
            if (!Check("v_training_busy_heroes", Click(TargetButton("tab_heroes")), "A training hero remains visible in the roster.")) yield break;
            yield return null;
            if (!Check("v_training_busy_details", Click(ButtonContaining(app.nav.Current.Root, trainee.name)) && Popups.Top is HeroPopup,
                "The busy hero's card still opens its status details.")) yield break;
            var dismiss = ButtonContaining(Popups.Top.Root, L.T("Dismiss hero"), false); int rosterBefore = C.Roster.Count;
            if (dismiss != null) dismiss.OnPointerClick(null);
            if (!Check("v_training_busy_dismiss_blocked", dismiss != null && !dismiss.Interactable && C.Roster.Count == rosterBefore && C.Timers.UnitTask(trainee.uid)?.kind == "train",
                "A hero with active training cannot be dismissed or orphan its timer.")) yield break;
            CloseTop("v_training_busy_details_close");
            yield return OpenFacility(app, "training");
            yield return CompleteUiTimer(app, C.Timers.UnitTask(trainee.uid), "slot", "v_training");
            if (!Check("v_training_result", !C.Roster.IsBusy(trainee) && (trainee.level > level || trainee.xp > xp),
                "Training completion grants experience and releases the hero.")) yield break;
            yield return Shot(dir, "verify_04_training_done");
            yield return OpenFacility(app, "infirmary");
            C.Roster.SetHp(trainee, C.Roster.MaxHp(trainee) * .35); app.nav.Current.Redraw();
            yield return new WaitForSecondsRealtime(.2f);
            if (!Check("v_healing_picker", Click(RowButton(app.nav.Current.Root, "slot")) && Popups.Top is HeroPickPopup,
                "An empty Infirmary bed opens injured hero selection.")) yield break;
            if (!Check("v_healing_start", Click(ButtonContaining(Popups.Top.Body, trainee.name)) && C.Timers.UnitTask(trainee.uid)?.kind == "heal",
                "Choosing an injured hero starts the healing timer.")) yield break;
            yield return new WaitForSecondsRealtime(.35f);
            yield return CompleteUiTimer(app, C.Timers.UnitTask(trainee.uid), "slot", "v_healing");
            Check("v_healing_result", !C.Roster.IsBusy(trainee) && C.Roster.HpFrac(trainee) >= .999,
                "Completed healing restores full health and makes the hero available.");
            yield return OpenFacility(app, "library");
            if (!Check("v_research_start", Click(LabeledButton(app.nav.Current.Root, "RESEARCH")) && C.Timers.OfKind("research").Any(),
                "The Library's research action starts an unlocked research timer.")) yield break;
            var researchTask = C.Timers.OfKind("research").First(); string researchId = researchTask.refId;
            yield return new WaitForSecondsRealtime(.35f);
            yield return CompleteUiTimer(app, researchTask, "res", "v_research");
            Check("v_research_result", C.Upgrades.Level(researchId) == 1 && C.Upgrades.Pending(researchId) == null,
                "Completing research applies its first level and clears the scholar slot.");
            yield return Shot(dir, "verify_05_research_done");
            yield return OpenFacility(app, "treasury");
            C.Vault.Load("{\"last\":" + (Util.Now - 3600).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}");
            yield return new WaitForSecondsRealtime(.35f);
            goldBefore = C.Wallet.Get("gold"); double pending = Math.Floor(C.Vault.Pending);
            if (!Check("v_treasury_collect", pending >= 1 && Click(TargetButton("btn_collect")) && C.Wallet.Get("gold") >= goldBefore + pending && C.Vault.Pending < 1,
                "The visible COLLECT control credits accumulated gold and resets the vault balance.")) yield break;
        }

        static IEnumerator VerifyContractsAndRewards(App app, string dir)
        {
            var C = App.Ctx;
            Popups.CloseAll();
            foreach (var u in C.Roster.All) C.Roster.SetHp(u, C.Roster.MaxHp(u));
            if (!Check("v_contract_board_tab", Click(TargetButton("tab_board")), "Open Board from the actual navigation control.")) yield break;
            yield return null;
            if (!Check("v_contracts_tab", Click(LabeledButton(app.nav.Current.Root, "Contracts")) && BoardState.Tab == 1,
                "The Contracts tab must display expedition controls.")) yield break;
            yield return null;
            var oldIds = C.Missions.Board.Select(m => m.id).ToList();
            double gems = C.Wallet.Get("gems");
            var refresh = LabeledButton(app.nav.Current.Root, L.T("Refresh") + " " + (int)C.Content.Bal("board.refresh_gems", 5));
            if (!Check("v_contract_refresh", Click(refresh) && C.Wallet.Get("gems") < gems && !oldIds.SequenceEqual(C.Missions.Board.Select(m => m.id)),
                "Refresh spends the shown gems and replaces the contract board.")) yield break;
            yield return new WaitForSecondsRealtime(.25f);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                string key = "v_contract_" + (attempt + 1);
                int before = C.Missions.Expeditions.Count();
                if (!Check(key + "_party_open", Click(LabeledButton(app.nav.Current.Root, "SEND")) && Popups.Top is PartyPopup,
                    "The current contract's SEND action opens party preparation.")) yield break;
                if (!Check(key + "_auto", Click(LabeledButton(Popups.Top.Root, PartyPopup.AutoTeamLabel)) && PartyLabels((PartyPopup)Popups.Top) > 0,
                    "Auto must select an available party for the contract.")) yield break;
                yield return null;
                if (!Check(key + "_send", Click(TargetButton("btn_party_go")) && C.Missions.Expeditions.Count() == before + 1,
                    "The real SEND button starts exactly one expedition.")) yield break;
                yield return new WaitForSecondsRealtime(.35f);
                var task = C.Missions.Expeditions.Last();
                Check(key + "_heroes_busy", task.units.All(uid => C.Roster.IsBusy(C.Roster.Get(uid))) && task.successChance > 0,
                    "All departing party members are busy and their forecast is captured at departure.");
                var speed = RowButton(app.nav.Current.Root, "exp");
                if (!Check(key + "_speedup_open", Click(speed) && Popups.Top is SpeedupPopup, "The expedition's gem control opens speed-up details.")) yield break;
                gems = C.Wallet.Get("gems");
                var finish = Popups.Top.Footer.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.Interactable && b.onClick != null);
                if (!Check(key + "_speedup_finish", Click(finish) && task.done && C.Wallet.Get("gems") < gems,
                    "Finishing an expedition charges gems and marks it returned without claiming its rewards.")) yield break;
                yield return new WaitForSecondsRealtime(.35f);
                double gold = C.Wallet.Get("gold");
                if (!Check(key + "_claim", Click(LabeledButton(app.nav.Current.Root, "CLAIM")) && Popups.Top is ExpeditionResultPopup &&
                    !C.Timers.Tasks.Contains(task) && C.Wallet.Get("gold") > gold,
                    "The returned expedition's CLAIM button grants its result once and removes the claimed timer, including the second consecutive expedition.")) yield break;
                Check(key + "_heroes_returned", task.units.All(uid => !C.Roster.IsBusy(C.Roster.Get(uid))), "Claiming releases every party member.");
                yield return Shot(dir, "verify_08_contract_result_" + (attempt + 1));
                if (!Check(key + "_result_continue", Click(LabeledButton(Popups.Top.Root, "Continue")) && !(Popups.Top is ExpeditionResultPopup),
                    "The expedition result's Continue action closes the result panel.")) yield break;
                yield return new WaitForSecondsRealtime(.35f);
            }
            if (!Check("v_daily_menu_open", Click(MenuButton) && Click(LabeledButton(Popups.Top.Root, "Daily Reward")) && Popups.Top is DailyPopup,
                "Daily Reward opens from the menu.")) yield break;
            var reward = C.Daily.TodayReward.Select(r => new Res(r.id, r.n)).ToList();
            var balances = reward.Select(r => C.Content.IsCurrency(r.id) ? C.Wallet.Get(r.id) : C.Inventory.Count(r.id)).ToList();
            if (!Check("v_daily_claim", !C.Daily.Claimed && Click(LabeledButton(Popups.Top.Root, "CLAIM TODAY'S REWARD")) && C.Daily.Claimed,
                "Claiming today's reward records the daily claim.")) yield break;
            Check("v_daily_rewards_paid", reward.Select((r, i) => (C.Content.IsCurrency(r.id) ? C.Wallet.Get(r.id) : C.Inventory.Count(r.id)) >= balances[i] + r.n).All(v => v),
                "Each advertised daily reward is paid in full.");
            yield return new WaitForSecondsRealtime(.25f);
            Check("v_daily_duplicate_disabled", LabeledButton(Popups.Top.Root, "CLAIM TODAY'S REWARD") == null,
                "The claimed day must no longer offer a second claim button.");
            yield return Shot(dir, "verify_09_daily_claimed"); CloseTop("v_daily_close");
            var ev = C.Content.EventMap["ev_merchant"]; int potions = C.Inventory.Count("potion_m"); double goldBefore = C.Wallet.Get("gold");
            C.Events.Load("{\"pending\":[\"ev_merchant\"],\"next\":" + (Util.Now + 3600).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}");
            Popups.Show(new EventPopup(ev));
            if (!Check("v_event_choice", Click(LabeledButton(Popups.Top.Root, ev.choices[0].text)) && C.Counters.Get("events.done") > 0 &&
                C.Inventory.Count("potion_m") == potions + 2 && Math.Abs(C.Wallet.Get("gold") - goldBefore + 100) < .001,
                "The merchant choice charges 100 gold, grants two potions, and records the resolved event.")) yield break;
            yield return Shot(dir, "verify_10_event_outcome");
            Check("v_event_continue", Click(LabeledButton(Popups.Top.Root, "Continue")) && !(Popups.Top is EventPopup),
                "A resolved story event closes through Continue.");
        }

        static IEnumerator VerifyHeroAndBattle(App app, string dir)
        {
            var C = App.Ctx; var hero = C.Roster.All[0];
            Popups.CloseAll();
            // Eligible promotion state is part of the later-game fixture, never the opening balance.
            hero.level = C.Roster.LevelCap(hero); hero.xp = 0; C.Roster.Dirty();
            if (!Check("v_promotion_heroes_tab", Click(TargetButton("tab_heroes")), "Open Heroes for promotion.")) yield break;
            yield return null;
            if (!Check("v_promotion_details", Click(ButtonContaining(app.nav.Current.Root, hero.name)) && Popups.Top is HeroPopup,
                "The promoted hero's details open from the real roster card.")) yield break;
            if (!Check("v_promotion_panel", Click(LabeledButton(Popups.Top.Root, "Promote")) && Popups.Top is PromotePopup,
                "The eligible hero's Promote action opens its cost and effect panel.")) yield break;
            int promo = hero.promo; double power = C.Roster.Power(hero), gold = C.Wallet.Get("gold");
            if (!Check("v_promotion_confirm", Click(LabeledButton(Popups.Top.Root, "PROMOTE")) && hero.promo == promo + 1 && C.Roster.Power(hero) > power && C.Wallet.Get("gold") < gold,
                "Promotion raises this hero's promotion and power, pays its cost, and returns to hero details.")) yield break;
            yield return new WaitForSecondsRealtime(.25f);
            Check("v_promotion_parent_refresh", Popups.Top is HeroPopup && Popups.Top.Body.GetComponentsInChildren<Text>(false).Any(t => t.text.Contains("*")),
                "The parent hero details must display its new promotion mark without reopening.");
            CloseTop("v_promotion_hero_close");

            yield return OpenFacility(app, "infirmary");
            foreach (var u in C.Roster.All) C.Roster.SetHp(u, 0);
            app.nav.Current.Redraw(); yield return new WaitForSecondsRealtime(.2f);
            if (!Check("v_readiness_heal_picker", Click(RowButton(app.nav.Current.Root, "slot")) && Popups.Top is HeroPickPopup,
                "Prepare the live healing-readiness scenario through an actual Infirmary bed.")) yield break;
            if (!Check("v_readiness_heal_start", Click(ButtonContaining(Popups.Top.Body, hero.name)) && C.Timers.UnitTask(hero.uid)?.kind == "heal",
                "The wounded hero begins healing through selection.")) yield break;
            var healTask = C.Timers.UnitTask(hero.uid);
            // Advance only this test timer so natural GameContext.Tick completion can be observed
            // while a party dialog is open, without waiting for a real ten-minute healing session.
            healTask.end = Util.Now + 2.5;
            if (!Check("v_readiness_board", Click(TargetButton("tab_board")), "Navigate away while the healing timer runs.")) yield break;
            yield return null;
            if (!Check("v_readiness_campaign", Click(LabeledButton(app.nav.Current.Root, "Campaign")), "Switch back to campaign missions.")) yield break;
            yield return null;
            if (!Check("v_readiness_party_open", Click(LabeledButton(app.nav.Current.Root, "FIGHT")) && Popups.Top is PartyPopup,
                "Open an existing campaign mission while every hero is still unavailable.")) yield break;
            var party = (PartyPopup)Popups.Top;
            if (!Check("v_readiness_recovery_prompt", LabeledButton(party.Root, "OPEN HEROES") != null,
                "With no ready heroes, party preparation must show its recovery action.")) yield break;
            yield return new WaitForSecondsRealtime(3.3f);
            if (!Check("v_readiness_live_refresh", Popups.Top == party && C.Roster.Ready(hero) &&
                LabeledButton(party.Root, "OPEN HEROES") == null && party.Footer.GetComponentsInChildren<UButton>(false).Any(b => b.label != null && b.label.text == PartyPopup.FightLabel),
                "Natural healing completion must replace the recovery action with FIGHT while the same party dialog remains open.")) yield break;
            if (!Check("v_party_auto_after_heal", Click(LabeledButton(party.Root, PartyPopup.AutoTeamLabel)), "Auto team must select the newly healed hero.")) yield break;
            yield return null;
            if (!Check("v_party_assault_plan", Click(LabeledButton(party.Root, "Assault")), "The Assault battle plan is selectable.")) yield break;
            yield return null;
            Check("v_party_assault_description", party.Body.GetComponentsInChildren<Text>(false).Any(t => t.text == L.T(MissionSystem.DoctrineDescription(BattleDoctrine.Assault))),
                "Selecting Assault must update the real plan description.");
            if (!Check("v_party_guard_plan", Click(LabeledButton(party.Root, "Guard")), "The Guard battle plan is selectable.")) yield break;
            yield return null;
            if (!Check("v_live_battle_start", Click(TargetButton("btn_party_go")) && app.nav.Current is BattleScreen,
                "FIGHT starts a live battle using the prepared party and selected Guard doctrine.")) yield break;
            var battle = app.nav.Current;
            app.host.OnBackKey?.Invoke();
            Check("v_battle_hardware_back", app.nav.Current == battle && !(Popups.Top is ResultPopup),
                "The same hardware Back handler used by Escape/Android Back must keep the active battle and show the skip guidance.");
            int speed = Prefs.BattleSpeed;
            foreach (int n in new[] { 2, 3, 1 })
                if (!Check("v_battle_speed_" + n, Click(LabeledButton(battle.Root, "x" + n)) && Prefs.BattleSpeed == n && Math.Abs(Tw.TimeScale - n) < .001,
                    "The battle speed control must update both preference and current animation timing.")) yield break;
            yield return Shot(dir, "verify_11_live_battle", .35f);
            var liveBattle = (BattleScreen)battle;
            Check("v_battle_pause", Click(TargetButton("btn_pause")) && liveBattle.IsPaused && Tw.TimeScale == 0, "Pause freezes battle playback and animation timing.");
            int pausedActions = liveBattle.DisplayedActions;
            yield return new WaitForSecondsRealtime(.8f);
            Check("v_battle_paused_actions", liveBattle.DisplayedActions == pausedActions, "No combat action advances while paused.");
            Check("v_battle_paused_speed", Click(LabeledButton(battle.Root, "x3")) && Tw.TimeScale == 0 && Prefs.BattleSpeed == 3,
                "Changing the selected speed must keep a paused battle paused.");
            yield return Shot(dir, "verify_11b_battle_paused");
            Check("v_battle_resume", Click(TargetButton("btn_pause")) && !liveBattle.IsPaused && Tw.TimeScale == 3, "Resume restores the selected battle speed.");
            yield return new WaitForSecondsRealtime(.2f);
            Check("v_battle_pause_before_skip", Click(TargetButton("btn_pause")) && liveBattle.IsPaused, "Pause again to verify Skip can finish a paused battle.");
            int clears = C.Missions.Clears("m_r1_1");
            if (!Check("v_battle_skip", Click(TargetButton("btn_skip")), "SKIP must finish the active battle through its actual button.")) yield break;
            float guard = 0;
            while (!(Popups.Top is ResultPopup) && guard < 8) { guard += Time.unscaledDeltaTime; yield return null; }
            if (!Check("v_battle_skip_result", Popups.Top is ResultPopup && C.Missions.Clears("m_r1_1") == clears + 1 && Tw.TimeScale == 1,
                "Skipping awards the real simulation's victory exactly once and restores normal interface timing.")) yield break;
            yield return Shot(dir, "verify_12_skip_result");
            if (!Check("v_result_retry", Click(LabeledButton(Popups.Top.Root, "Retry")) && Popups.Top is PartyPopup && app.nav.Current is BoardScreen,
                "Retry returns to Board and opens a fresh preparation panel for the same mission.")) yield break;
            yield return null;
            if (!Check("v_retry_fight", Click(TargetButton("btn_party_go")) && app.nav.Current is BattleScreen, "The retry preparation can start its next live battle.")) yield break;
            if (!Check("v_retry_skip", Click(TargetButton("btn_skip")), "The retried battle retains its working skip control.")) yield break;
            guard = 0;
            while (!(Popups.Top is ResultPopup) && guard < 8) { guard += Time.unscaledDeltaTime; yield return null; }
            if (!Check("v_retry_result_once", Popups.Top is ResultPopup && C.Missions.Clears("m_r1_1") == clears + 2,
                "The retry result adds one further clear, with no duplicate completion.")) yield break;
            if (!Check("v_result_next", Click(LabeledButton(Popups.Top.Root, "Next")) && Popups.Top is PartyPopup && app.nav.Current is BoardScreen,
                "Next opens preparation for the unlocked next mission.")) yield break;
            var next = C.Content.Mission("m_r1_2");
            Check("v_result_next_target", Popups.Top.Body.GetComponentsInChildren<Text>(false).Any(t => t.text == W.MissionName(next.name)),
                "The Next panel must show the next mission's actual name.");
            CloseTop("v_next_party_close");
            Prefs.BattleSpeed = speed;
            foreach (var u in C.Roster.All) C.Roster.SetHp(u, C.Roster.MaxHp(u));
        }

        static IEnumerator VerifyPersistence(App app, string dir)
        {
            var C = App.Ctx; Popups.CloseAll();
            if (!Check("v_save_menu_open", Click(TargetButton("tab_hall")) && Click(MenuButton) && Popups.Top is MenuPopup,
                "Open the save action through Hall and the real menu.")) yield break;
            double gold = C.Wallet.Get("gold"), gems = C.Wallet.Get("gems");
            string guild = C.Meta.GuildName; int roster = C.Roster.Count, gear = C.Inventory.Used, rank = C.Ranks.Index;
            var heroIds = C.Roster.All.Select(u => u.uid).ToArray();
            var equips = C.Roster.All.Select(u => string.Join(",", u.equip)).ToArray();
            var promos = C.Roster.All.Select(u => u.promo).ToArray();
            var levels = C.Content.Upgrades.Select(u => C.Upgrades.Level(u.id)).ToArray();
            string inventory = C.Inventory.Save(); var sold = C.Market.Offers.Select(o => o.sold).ToArray();
            if (!Check("v_save_exit_click", Click(LabeledButton(Popups.Top.Root, "Save & exit to title")) && !app.InGame && app.nav.Current is TitleScreen && app.HasSave,
                "Save & exit must write the isolated verification save and return to the title.")) yield break;
            yield return Shot(dir, "verify_13_saved_title");
            if (!Check("v_new_game_confirmation", Click(LabeledButton(app.nav.Current.Root, "NEW GAME")) && Popups.Top is MsgPopup,
                "NEW GAME must show a confirmation while a save exists.")) yield break;
            if (!Check("v_new_game_cancel", Click(LabeledButton(Popups.Top.Root, "Cancel")) && app.HasSave && app.nav.Current is TitleScreen,
                "Cancel keeps the existing save and title screen.")) yield break;
            yield return null;
            if (!Check("v_story_replay", Click(LabeledButton(app.nav.Current.Root, "THE STORY OF EMBERHALL")) && app.nav.Current is OnboardingScreen,
                "The title's story action opens the cinematic replay.")) yield break;
            var auto = LabeledButton(app.nav.Current.Root, "AUTO OFF");
            if (!Check("v_story_auto_toggle", Click(auto) && auto.label.text == "AUTO ON" && Click(auto) && auto.label.text == "AUTO OFF",
                "The story playback toggle changes and restores automatic advance without restarting the scene.")) yield break;
            if (!Check("v_story_close", Click(LabeledButton(app.nav.Current.Root, "Close")) && app.nav.Current is TitleScreen && app.HasSave,
                "Closing the replay returns to title while preserving the guild save.")) yield break;
            yield return null;
            if (!Check("v_continue_saved_guild", Click(LabeledButton(app.nav.Current.Root, "CONTINUE")) && app.InGame && app.nav.Current is HallScreen,
                "The real CONTINUE control loads the saved guild and returns to Hall.")) yield break;
            yield return new WaitForSecondsRealtime(.35f);
            Check("v_saved_wallet", Math.Abs(C.Wallet.Get("gold") - gold) < .001 && Math.Abs(C.Wallet.Get("gems") - gems) < .001,
                "Both gold and gems must restore exactly after save, title, and Continue.");
            Check("v_saved_guild_roster", C.Meta.GuildName == guild && C.Roster.Count == roster && C.Ranks.Index == rank &&
                heroIds.SequenceEqual(C.Roster.All.Select(u => u.uid)) && promos.SequenceEqual(C.Roster.All.Select(u => u.promo)),
                "The named guild, rank, exact hero identities and promotions must survive loading.");
            Check("v_saved_inventory_equipment", C.Inventory.Used == gear && C.Inventory.Save() == inventory && equips.SequenceEqual(C.Roster.All.Select(u => string.Join(",", u.equip))),
                "Owned gear, enhancement, locks, material stacks, and hero equipment must survive loading exactly.");
            Check("v_saved_facilities_market_daily", levels.SequenceEqual(C.Content.Upgrades.Select(u => C.Upgrades.Level(u.id))) &&
                sold.SequenceEqual(C.Market.Offers.Select(o => o.sold)) && C.Daily.Claimed && C.Quests.State("q_m01") == QuestSystem.Done,
                "All built facilities and research, sold market offers, claimed daily reward and completed opening quests must remain recorded.");
            Check("v_saved_language_english", L.Lang == "en" && Prefs.Lang == "en", "The explicitly selected English language must remain active after Continue.");
            yield return Shot(dir, "verify_14_restored_hall");
        }

        static IEnumerator VerifyDismissal(App app, string dir)
        {
            var C = App.Ctx; Popups.CloseAll();
            if (!Check("v_dismiss_heroes_tab", Click(TargetButton("tab_heroes")), "Open the roster after completed tasks and battles.")) yield break;
            yield return null;
            if (!Check("v_dismiss_auto_equip", Click(LabeledButton(app.nav.Current.Root, "Auto-Equip")), "Equip owned gear before checking its return on dismissal.")) yield break;
            yield return null;
            while (C.Roster.Count > 1)
            {
                var spare = C.Roster.All.Last(); int count = C.Roster.Count;
                var owned = C.Inventory.Gear.Where(g => g.owner == spare.uid).Select(g => g.uid).ToArray();
                string key = "v_dismiss_" + count;
                if (!Check(key + "_details", Click(ButtonContaining(app.nav.Current.Root, spare.name)) && Popups.Top is HeroPopup,
                    "Open an available spare hero's details from the actual roster card.")) yield break;
                if (!Check(key + "_confirmation", Click(LabeledButton(Popups.Top.Root, "Dismiss hero")) && Popups.Top is MsgPopup,
                    "Dismiss hero must request confirmation before removing anyone.")) yield break;
                yield return Shot(dir, "verify_12a_dismiss_confirmation_" + count);
                if (!Check(key + "_cancel", Click(LabeledButton(Popups.Top.Root, "Cancel")) && C.Roster.Count == count && C.Roster.Get(spare.uid) != null,
                    "Cancel keeps the exact hero and its equipped items.")) yield break;
                yield return null;
                if (!Check(key + "_confirm", Click(LabeledButton(Popups.Top.Root, "Dismiss hero")) && Click(LabeledButton(Popups.Top.Root, "Dismiss")) &&
                    C.Roster.Count == count - 1 && C.Roster.Get(spare.uid) == null,
                    "Confirm removes exactly the selected available spare hero in the isolated test save.")) yield break;
                Check(key + "_gear_returned", owned.All(uid => C.Inventory.Get(uid) != null && string.IsNullOrEmpty(C.Inventory.Get(uid).owner)),
                    "Every previously equipped item remains in the Bag with its owner cleared.");
                yield return new WaitForSecondsRealtime(.25f);
                Check(key + "_roster_refresh", app.nav.Current.Root.GetComponentsInChildren<UButton>(false).Count(b => b.name == "hero") == C.Roster.Count,
                    "The roster removes the dismissed hero's card when the details close.");
            }
            var finalHero = C.Roster.All[0];
            if (!Check("v_last_hero_details", Click(ButtonContaining(app.nav.Current.Root, finalHero.name)) && Popups.Top is HeroPopup,
                "The final hero's details remain accessible.")) yield break;
            var lastDismiss = ButtonContaining(Popups.Top.Root, L.T("Dismiss hero"), false);
            if (lastDismiss != null) lastDismiss.OnPointerClick(null);
            Check("v_last_hero_dismiss_blocked", lastDismiss != null && !lastDismiss.Interactable && C.Roster.Count == 1 && C.Roster.Get(finalHero.uid) != null,
                "The final hero cannot be dismissed, so the guild always retains a playable hero.");
            yield return Shot(dir, "verify_12b_last_hero"); CloseTop("v_last_hero_close");
        }

        static IEnumerator VerifyRankAndSpire(App app, string dir)
        {
            var C = App.Ctx; Popups.CloseAll();
            var hero = C.Roster.All[0]; hero.level = 50; hero.rarity = "legendary"; hero.traits.Clear();
            C.Roster.Dirty(); C.Roster.SetHp(hero, C.Roster.MaxHp(hero));
            BoardState.LastParty = new List<string> { hero.uid };
            BoardState.Tab = 0; app.hud.GoTab(Hud.Tab.Board, true);
            if (!Check("v_spire_tab", Click(LabeledButton(app.nav.Current.Root, "Endless Spire")) && BoardState.Tab == 2,
                "Rank D unlocks the Endless Spire tab.")) yield break;
            yield return Shot(dir, "verify_15_spire");
            if (!Check("v_spire_party", Click(LabeledButton(app.nav.Current.Root, "CLIMB!")) && Popups.Top is PartyPopup,
                "Climb opens the next floor's real party preparation.")) yield break;
            yield return Shot(dir, "verify_16_spire_party");
            if (!Check("v_spire_fight", Click(TargetButton("btn_party_go")) && app.nav.Current is BattleScreen && Click(TargetButton("btn_skip")),
                "The prepared Spire party can start and finish its floor.")) yield break;
            float elapsed = 0;
            while (!(Popups.Top is ResultPopup) && elapsed < 8) { elapsed += Time.unscaledDeltaTime; yield return null; }
            if (!Check("v_spire_floor_recorded", Popups.Top is ResultPopup && C.Missions.TowerBest == 1,
                "Winning the first floor records the climb and offers the next floor.")) yield break;
            yield return Shot(dir, "verify_17_spire_result");
            if (!Check("v_spire_next", Click(LabeledButton(Popups.Top.Root, "Next")) && Popups.Top is PartyPopup,
                "Spire Next opens preparation for floor two.")) yield break;
            CloseTop("v_spire_next_close");
            C.Counters.Max("fame", C.Ranks.Next.fame); C.Changed();
            int oldRank = C.Ranks.Index; string nextRank = C.Ranks.Next.id;
            if (!Check("v_rank_panel", Click(TargetButton("hud_rank")) && Popups.Top is RankPopup,
                "The rank indicator opens the guild promotion ladder.")) yield break;
            yield return Shot(dir, "verify_18_rank_exam");
            if (!Check("v_rank_exam_party", Click(LabeledButton(Popups.Top.Root, L.T("TAKE THE RANK {0} EXAM", nextRank))) && Popups.Top is PartyPopup,
                "An eligible guild can open its rank exam through the promotion panel.")) yield break;
            if (!Check("v_rank_exam_fight", Click(TargetButton("btn_party_go")) && app.nav.Current is BattleScreen && Click(TargetButton("btn_skip")),
                "Rank exams use the same working party and battle controls.")) yield break;
            elapsed = 0;
            while (!(Popups.Top is ResultPopup) && elapsed < 8) { elapsed += Time.unscaledDeltaTime; yield return null; }
            if (!Check("v_rank_exam_passed", Popups.Top is ResultPopup && C.Ranks.CanPromote,
                "The won exam unlocks promotion and persists its clear.")) yield break;
            if (!Check("v_rank_exam_back", Click(LabeledButton(Popups.Top.Root, "Back")), "Return from the exam result.")) yield break;
            if (!Check("v_rank_promote_open", Click(TargetButton("hud_rank")) && Popups.Top is RankPopup,
                "Reopen rank details after completing the exam.")) yield break;
            if (!Check("v_rank_promote", Click(LabeledButton(Popups.Top.Root, L.T("PROMOTE TO RANK {0}!", nextRank))) &&
                C.Ranks.Index == oldRank + 1 && Popups.Top is RankUpPopup,
                "Promotion raises the guild rank once and displays its celebration.")) yield break;
            yield return Shot(dir, "verify_19_rank_up");
            if (!Check("v_rank_celebration_close", Click(LabeledButton(Popups.Top.Root, "AWESOME!")) && !(Popups.Top is RankUpPopup),
                "The rank celebration closes through its actual action.")) yield break;
            BoardState.Tab = 0;
        }

        static IEnumerator FunctionalVerification(App app, string dir)
        {
            Debug.Log("[GM] Comprehensive UI verification started.");
            int failures = audit.failedFlowChecks;
            yield return VerifySettings(app, dir); if (audit.failedFlowChecks > failures) yield break;
            yield return VerifyFacilities(app, dir); if (audit.failedFlowChecks > failures) yield break;
            yield return VerifyInventory(app, dir); if (audit.failedFlowChecks > failures) yield break;
            yield return VerifyContractsAndRewards(app, dir); if (audit.failedFlowChecks > failures) yield break;
            yield return VerifyHeroAndBattle(app, dir); if (audit.failedFlowChecks > failures) yield break;
            yield return VerifyRankAndSpire(app, dir); if (audit.failedFlowChecks > failures) yield break;
            yield return VerifyDismissal(app, dir); if (audit.failedFlowChecks > failures) yield break;
            yield return VerifyPersistence(app, dir);
            Popups.Show(new AboutPopup());
            Check("v_external_actions_present", LabeledButton(Popups.Top.Root, "Share") != null && LabeledButton(Popups.Top.Root, "Rate us") != null,
                "Share and Rate controls are present; native external share/rating actions are intentionally excluded from unattended verification.");
            CloseTop("v_external_panel_close");
            yield return VerifyJourney(app, dir);
            yield return VerifyAds(app, dir);
        }

        static IEnumerator VerifyJourney(App app, string dir)
        {
            var c = app.ctx; string original = c.SaveBlobs(out _);
            Popups.CloseAll(); app.hud.GoTab(Hud.Tab.Board, true);
            var journey = c.Get<Journey>(); journey.Load("{\"energy\":0,\"rationDay\":-1}");
            foreach (var hero in c.Roster.All) hero.hp = -1;
            c.Roster.Dirty(); c.Changed();
            Popups.Show(new PartyPopup(c.Content.Mission("m_r1_1"))); yield return new WaitForSecondsRealtime(.15f);
            Check("v_stamina_empty_blocks_paid_entry", !TargetButton("btn_party_go").Interactable,
                "An exhausted guild sees the paid replay offer but cannot accidentally start it.");
            yield return Shot(dir, "verify_40_stamina_empty"); Popups.CloseAll();
            if (!Check("v_journey_guide_open", Click(TargetButton("btn_journey")) && Popups.Top is JourneyPopup, "The Board's stamina line opens the progression guide.")) yield break;
            yield return Shot(dir, "verify_41_journey_guide");
            Check("v_camp_free_ration", Click(LabeledButton(Popups.Top.Root, "Rest at camp · +10 stamina")) && journey.Energy == 10 && journey.CampUsed,
                "The real camp action restores ten stamina once, without ads or a currency payment.");
            Popups.CloseAll();
            var strong = c.Roster.All.First(c.Roster.Ready); strong.hp = -1; strong.level = 50; c.Roster.Dirty();
            c.Missions.Load("{\"p\":[{\"id\":\"m_r1_1\",\"clears\":1,\"stars\":3},{\"id\":\"m_r1_2\",\"clears\":1,\"stars\":3},{\"id\":\"m_r1_3\",\"clears\":1,\"stars\":3}]}");
            var mission = c.Content.Mission("m_r1_4");
            Popups.Show(new PartyPopup(mission));
            if (!Check("v_auto_team_selects", Click(LabeledButton(Popups.Top.Root, PartyPopup.AutoTeamLabel)), "Auto team selects ready heroes, separately from the Start auto battle action.")) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            if (!Check("v_auto_start", Click(TargetButton("btn_party_go")) && app.nav.Current is BattleScreen, "Start the multi-wave encounter through its actual primary button.")) yield break;
            Check("v_stamina_entry_charged_once", journey.Energy == 8 && journey.InBattle, "One paid trail deducts exactly two stamina at entry.");
            yield return new WaitForSecondsRealtime(.8f);
            Check("v_battle_no_nameplates", !app.nav.Current.Root.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "nameplate"), "Live units have health bars and no name plaques.");
            yield return Shot(dir, "verify_42_clean_auto_battle");
            float guard = 0;
            while (!(Popups.Top is ResultPopup) && guard < 50) { guard += Time.unscaledDeltaTime; yield return null; }
            Check("v_auto_finishes_without_skip", Popups.Top is ResultPopup && c.Missions.Clears(mission.id) == 1 && !journey.InBattle && journey.Energy == 8,
                "A multi-wave automatic battle reaches its victory and settles stamina without Skip or manual action input.");
            if (!(Popups.Top is ResultPopup)) yield break;
            Click(LabeledButton(Popups.Top.Root, "Back"));
            strong.hp = c.Roster.MaxHp(strong) * .12; c.Roster.Dirty();
            Popups.Show(new HeroPopup(strong.uid)); yield return Shot(dir, "verify_43_readable_hero_status"); Popups.CloseAll();
            Check("v_journey_fixture_restored", c.LoadBlobs(original), "Restore the pre-check guild, timers, mission history and stamina from its isolated snapshot.");
            app.hud.GoTab(Hud.Tab.Hall, true); app.SaveNow();
        }

        static IEnumerator VerifyAds(App app, string dir)
        {
            Popups.CloseAll(); app.hud.GoTab(Hud.Tab.Hall, true);
            var client = new TourAdsClient(); app.Ads.InstallVerificationClient(client);
            Check("v_ads_tutorial_gate", !app.Ads.TutorialDone && !app.Ads.RewardReady && app.Ads.BannerInset == 0,
                "No ads during the first four onboarding steps, independently of the tutorial-hints preference.");
            // Only this explicit test fixture completes chapter four. The unmodified opening was verified above.
            var fourth = app.ctx.Quests.Get("q_m04");
            if (!Check("v_ads_tutorial_fixture", fourth != null, "The opening flow created chapter four.")) yield break;
            fourth.state = QuestSystem.Done; app.ctx.Changed();
            yield return new WaitForSecondsRealtime(.4f);
            Check("v_ads_preload", app.Ads.RewardReady && client.InterstitialReady, "The injected flow double preloads both full-screen formats.");
            Check("v_ads_banner_reserved", client.BannerVisible && app.Ads.BannerInset > 0 && app.host.screens.offsetMin.y >= 188 + app.Ads.BannerInset - .5f,
                "The banner owns its own inset below navigation; it cannot overlap a game control.");
            yield return Shot(dir, "verify_30_ad_banner_layout");
            Popups.Show(new MenuPopup()); yield return new WaitForSecondsRealtime(.1f);
            Check("v_ads_banner_modal_hide", !client.BannerVisible && app.Ads.BannerInset == 0, "Opening a modal removes the banner and its inset.");
            if (!Check("v_ads_supply_open", Click(LabeledButton(Popups.Top.Root, "Guild supplies")) && Popups.Top is SuppliesPopup,
                "The real Menu control opens the optional supplies offer.")) yield break;
            yield return Shot(dir, "verify_31_ad_supplies");
            double gold = app.ctx.Wallet.Get("gold"), gems = app.ctx.Wallet.Get("gems");
            if (!Check("v_ads_supply_watch", Click(LabeledButton(Popups.Top.Root, "Watch video · claim supplies")) && app.Ads.Busy,
                "An explicit player action starts the rewarded flow and pauses game audio.")) yield break;
            Check("v_ads_no_click_reward", app.ctx.Wallet.Get("gold") == gold && AudioListener.pause, "Starting a video grants nothing and mutes game audio.");
            yield return Shot(dir, "verify_32_ad_callback_preview");
            if (!Check("v_ads_supply_earned", Click(LabeledButton(Popups.Top.Root, "Complete test video")), "Inject two earned callbacks through the preview control.")) yield break;
            yield return new WaitForSecondsRealtime(.15f);
            Check("v_ads_supply_once", app.ctx.Wallet.Get("gold") == gold + 150 && app.ctx.Wallet.Get("gems") == gems + 2 && !app.Ads.Busy && !AudioListener.pause,
                "Duplicate callbacks award exactly 150 gold and 2 gems, restore audio and unblock play.");
            yield return Shot(dir, "verify_33_ad_supplies_claimed");
            Popups.CloseAll();
            var ledger = app.ctx.Get<AdRewards>();
            gold = app.ctx.Wallet.Get("gold");
            app.Ads.Watch(ledger.VictoryTicket("verification-dismiss", 100));
            Check("v_ads_dismiss_control", Click(LabeledButton(Popups.Top.Root, "Close without reward")), "Dismiss the next rewarded flow without earning it.");
            Check("v_ads_dismiss_no_reward", app.ctx.Wallet.Get("gold") == gold && !app.Ads.Busy, "Dismissing a video grants no bonus and immediately restores play.");
            yield return new WaitForSecondsRealtime(.15f);
            app.Ads.Watch(ledger.VictoryTicket("verification-fail", 100));
            Check("v_ads_failure_control", Click(LabeledButton(Popups.Top.Root, "Simulate failure")), "Inject a full-screen presentation failure.");
            Check("v_ads_failure_no_reward", app.ctx.Wallet.Get("gold") == gold && !app.Ads.Busy && app.Ads.BannerInset == 0,
                "A failed show leaves currency unchanged and does not trap the screen or audio.");
            client = new TourAdsClient(); app.Ads.InstallVerificationClient(client);
            yield return new WaitForSecondsRealtime(.2f);
            Popups.Show(new AdsPrivacyPopup()); yield return Shot(dir, "verify_34_ad_privacy");
            Check("v_ads_preview_interstitial", Click(LabeledButton(Popups.Top.Root, "Preview interstitial")) && app.Ads.Busy,
                "Test-mode settings can explicitly preview an interstitial without bypassing natural-break limits in normal play.");
            Check("v_ads_preview_close", Click(LabeledButton(Popups.Top.Root, "Close test interstitial")) && !app.Ads.Busy,
                "Closing the simulated interstitial restores the settings dialog.");
            Popups.CloseAll();
            app.hud.GoTab(Hud.Tab.Board, true);
            var hero = app.ctx.Roster.All.First(app.ctx.Roster.Ready);
            hero.hp = -1; hero.level = 50; app.ctx.Roster.Dirty();
            app.nav.Push(new BattleScreen(app.ctx.Content.Mission("m_r1_1"), new List<UnitInstance> { hero }));
            yield return new WaitForSecondsRealtime(.25f);
            Check("v_ads_banner_battle_hide", !client.BannerVisible && app.Ads.BannerInset == 0, "A live battle has no banner or reserved ad space.");
            if (!Check("v_ads_battle_skip", Click(TargetButton("btn_skip")), "Finish the real battle to verify its actual rewarded placement.")) yield break;
            float guard = 0;
            while (!(Popups.Top is ResultPopup) && guard < 8) { guard += Time.unscaledDeltaTime; yield return null; }
            if (!Check("v_ads_real_victory", Popups.Top is ResultPopup, "The actual battle opens its result.")) yield break;
            yield return Shot(dir, "verify_35_ad_victory_offer");
            var bonus = Popups.Top.Root.GetComponentsInChildren<UButton>(false).FirstOrDefault(b => b.label != null && b.label.text.StartsWith("Watch video · +"));
            gold = app.ctx.Wallet.Get("gold");
            if (!Check("v_ads_victory_watch", Click(bonus) && app.Ads.Busy, "The result's actual bonus button works while the completed battle remains behind it.")) yield break;
            Check("v_ads_victory_earned", Click(LabeledButton(Popups.Top.Root, "Complete test video")), "Complete the optional victory video.");
            yield return new WaitForSecondsRealtime(.2f);
            Check("v_ads_victory_credit", app.ctx.Wallet.Get("gold") > gold && Popups.Top is ResultPopup &&
                Popups.Top.Body.GetComponentsInChildren<UnityEngine.UI.Text>(false).Any(t => t.text == "Video bonus claimed"),
                "The earned callback adds the bonus and replaces the result offer with its claimed state.");
            yield return Shot(dir, "verify_36_ad_victory_claimed");
            Check("v_ads_victory_return", Click(LabeledButton(Popups.Top.Root, "Back")) && !app.Ads.Busy && app.nav.Current is BoardScreen,
                "Returning after a rewarded video never immediately chains an interstitial.");
            var noFill = new TourAdsClient { FailLoads = true }; app.Ads.InstallVerificationClient(noFill);
            yield return new WaitForSecondsRealtime(.25f);
            Check("v_ads_no_fill_fallback", !app.Ads.RewardReady && !noFill.BannerVisible && app.Ads.BannerInset == 0 && !app.Ads.Busy,
                "All formats can fail to load without leaving blank banner space or blocking play.");
            app.SaveNow(); Check("v_ads_reward_persisted", app.save.Load(app.ctx) && app.ctx.Get<AdRewards>().SuppliesToday == 1,
                "The earned supply receipt and daily count survive a real save reload.");
            // Remove the injected client before the remaining screenshots and language tour.
            app.Ads.InstallVerificationClient(new NoAdsClient());
            Check("v_ads_no_native_claim", !app.Ads.Supported, "Verification finishes with ads disabled. Native serving still requires a physical device or emulator.");
        }

        public static IEnumerator Run(App app)
        {
            // Flatten nested coroutines so an assertion helper exception becomes a failed run,
            // rather than silently stopping capture and leaving an unattended player alive.
            var frames = new Stack<IEnumerator>(); frames.Push(RunInternal(app));
            while (frames.Count > 0)
            {
                object current = null; bool advanced = false; Exception failure = null;
                try { advanced = frames.Peek().MoveNext(); if (advanced) current = frames.Peek().Current; }
                catch (Exception e) { failure = e; }
                if (failure != null)
                {
                    if (audit != null)
                    {
                        Check("v_unhandled_exception", false, failure.ToString());
                        File.WriteAllText(Path.Combine(audit.outputDirectory, "FAILED.txt"), failure.ToString());
                    }
                    Debug.LogError("[GM] Tour failed: " + failure); Application.Quit(1); yield break;
                }
                if (!advanced) { frames.Pop(); continue; }
                if (current is IEnumerator nested) frames.Push(nested);
                else yield return current;
            }
        }

        static IEnumerator RunInternal(App app)
        {
            string dir = OutputDirectory();
            audit = new TourAudit { outputDirectory = dir, requestedLanguage = Indonesian ? "id" : "en", verificationMode = Verification };
            if (Verification) Check("v_default_english_entry", L.Lang == "en", "A verification install starts in English before the tour makes any language selection.");
            L.Load(audit.requestedLanguage); app.nav.Current?.Redraw();
            Debug.Log("[GM] Visual tour output: " + dir);
            yield return Shot(dir, "00_splash", 0.7f);
            float titleGuard = 0;
            while (!(app.nav.Current is TitleScreen) && titleGuard < 30) { titleGuard += Time.unscaledDeltaTime; yield return null; }
            if (!(app.nav.Current is TitleScreen))
            {
                File.WriteAllText(Path.Combine(dir, "FAILED.txt"), "Title screen did not appear within 30 seconds."); Application.Quit(1); yield break;
            }
            yield return Shot(dir, "01_title", 1.2f);
            yield return OnboardingShots(app, dir);
            yield return StartFromCharter(app, dir);
            if (!app.InGame)
            {
                File.WriteAllText(Path.Combine(dir, "FAILED.txt"), "The real charter controls did not start a new guild. See layout-audit.json flowChecks."); Application.Quit(1); yield break;
            }
            var C = App.Ctx;
            yield return Shot(dir, "07_hall", 1.5f);
            yield return FirstLoop(app, dir);
            if (audit.failedFlowChecks > 0)
            {
                yield return Shot(dir, "07z_first_loop_failure");
                File.WriteAllText(Path.Combine(dir, "FAILED.txt"), "The unmodified first gameplay loop failed. See layout-audit.json flowChecks."); Application.Quit(1); yield break;
            }

            if (Verification)
            {
                yield return FunctionalVerification(app, dir);
                if (audit.failedFlowChecks > 0)
                {
                    yield return Shot(dir, "verify_failure");
                    File.WriteAllText(Path.Combine(dir, "FAILED.txt"), "Comprehensive UI verification failed. See layout-audit.json flowChecks.");
                    Application.Quit(1); yield break;
                }
            }

            C.Wallet.Add("gold", 3000); C.Wallet.Add("gems", 50); C.Counters.Add("missions.won", 6); C.Counters.Add("fame", 30);
            int hires = Math.Min(3, C.Roster.Capacity - C.Roster.Count);
            for (int i = 0; i < hires; i++) { var cand = C.Recruiter.Pool[0]; cand.price = 0; C.Recruiter.Hire(cand); }
            C.Inventory.AddItem("w_sword_2", 3, C.Rng); C.Inventory.AddItem("a_plate_2", 2, C.Rng); C.Inventory.AddItem("x_ring_2", 2, C.Rng); C.Inventory.AddItem("iron_ore", 20, C.Rng); C.Inventory.AddItem("potion_m", 3, C.Rng);
            C.Inventory.AddItem("w_staff_3", 1, C.Rng, "epic"); C.Inventory.AutoEquipAll(); C.Changed();
            yield return Shot(dir, "08_hall_rich", 1.2f);
            app.hud.GoTab(Hud.Tab.Heroes); yield return Shot(dir, "09_heroes");
            Popups.Show(new HeroPopup(C.Roster.All[0].uid)); yield return Shot(dir, "10_hero_popup"); Popups.CloseAll();
            app.hud.GoTab(Hud.Tab.Board); yield return Shot(dir, "11_board");
            Popups.Show(new PartyPopup(C.Content.Mission("m_r1_1"))); yield return Shot(dir, "12_party"); Popups.CloseAll();
            BoardState.Tab = 1; app.hud.GoTab(Hud.Tab.Board, true); yield return Shot(dir, "13_contracts"); BoardState.Tab = 0;
            app.hud.GoTab(Hud.Tab.Bag); yield return Shot(dir, "14_bag");
            Bag.OpenTab = 2; app.nav.Current.Redraw(); yield return Shot(dir, "15_market"); Bag.OpenTab = 0;
            Popups.Show(new ItemPopup(C.Inventory.Gear.First().uid)); yield return Shot(dir, "16_item"); Popups.CloseAll();
            app.hud.GoTab(Hud.Tab.Quests); yield return Shot(dir, "17_quests");
            app.hud.GoTab(Hud.Tab.Hall);
            app.nav.Push(new FacilityScreen("tavern")); yield return Shot(dir, "18_tavern");
            app.nav.Pop(); app.nav.Push(new FacilityScreen("blacksmith")); yield return Shot(dir, "19_blacksmith");
            app.nav.Pop(); app.nav.Push(new FacilityScreen("training")); yield return Shot(dir, "20_training");
            app.nav.Pop();
            var party = C.Roster.All.Take(3).ToList();
            app.nav.Push(new BattleScreen(C.Content.Mission("m_r1_2"), party));
            yield return Shot(dir, "21_battle_a", 1.8f); yield return Shot(dir, "22_battle_b", 2.0f); yield return Shot(dir, "23_battle_c", 2.0f);
            float guard = 0; while (!(Popups.Top is ResultPopup) && guard < 90) { guard += Time.unscaledDeltaTime; yield return null; }
            yield return Shot(dir, "24_result", 2.0f); Popups.CloseAll();
            Popups.Show(new SettingsPopup()); yield return Shot(dir, "25_settings"); Popups.CloseAll();
            Popups.Show(new AboutPopup()); yield return Shot(dir, "26_about"); Popups.CloseAll();
            Popups.Show(new RankPopup()); yield return Shot(dir, "27_rank"); Popups.CloseAll();
            Popups.Show(new DailyPopup()); yield return Shot(dir, "28_daily"); Popups.CloseAll();
            Popups.Show(new EventPopup(C.Content.EventMap["ev_merchant"])); yield return Shot(dir, "29_event"); Popups.CloseAll();
            if (!Indonesian) yield return LocalizedShots(app, dir);
            var lines = new List<string> { $"Screenshots: {audit.screenshots}; checked texts: {audit.checkedTexts}; layout issues: {audit.layoutIssues}; flow checks: {audit.flowChecks.Count}; failed flow checks: {audit.failedFlowChecks}; comprehensive verification: {audit.verificationMode}", "Fitted text uses its rendered font size. Fully clipped scroll content is excluded. A 2-unit tolerance avoids pixel-rounding noise." };
            foreach (var check in audit.flowChecks) lines.Add((check.passed ? "PASS " : "FAIL ") + check.name + " | " + check.detail);
            foreach (var shot in audit.shots)
            {
                lines.Add($"{shot.shot} [{shot.language}]: {shot.issues.Count} issues");
                foreach (var issue in shot.issues) lines.Add("  " + issue.kind + " | " + issue.path + " | " + issue.text.Replace('\n', ' '));
            }
            File.WriteAllLines(Path.Combine(dir, "layout-audit.txt"), lines);
            File.WriteAllText(Path.Combine(dir, "DONE.txt"), $"Tour complete. Screenshots={audit.screenshots}; layoutIssues={audit.layoutIssues}; flowChecks={audit.flowChecks.Count}; failedFlowChecks={audit.failedFlowChecks}; report=layout-audit.json");
            Debug.Log($"[GM] Visual tour complete: {audit.screenshots} screenshots, {audit.layoutIssues} layout issues, {audit.flowChecks.Count} flow checks, {audit.failedFlowChecks} failed checks. {dir}");
            Application.Quit(audit.failedFlowChecks > 0 ? 1 : 0);
        }
    }
}
