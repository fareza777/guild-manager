# Emberhall polish — 7 October 2026

## Version 1.2.0 — 12 October 2026

- Replaced the thin pixel UI font with Lato; increased body, label, resource, badge and rich-text sizes. Raised the
  fitting floor to 36 reference units and gave text enough height inside its controls.
- Reworked the opening guide, charter, long-name party slots, roster, stat panels, contracts, chapter rewards and
  result cards around readable centered text. Kept building name plaques compact.
- Simplified heavy panel borders and ornate buttons, removed the duplicate first-steps Hall tracker and reduced
  routine upgrade badges. Shortened prologue copy, enlarged its illustration area and used painted facility banners.
- Added a bright painted Lantern Road panorama. Reused it in the prologue, Campaign, Spire, Journal and chapter rewards.
- Wrote a connected 25-chapter journey about the five beacon lenses, Maren's family and the roads between villages.
  Added a Journal that shows reached chapters and short actionable current-quest instructions in English and Indonesian.
- Made Auto party selection encounter-aware with bounded deterministic simulation, healthy/busy hero checks,
  doctrine and synergy comparisons. Forecasting does not consume the campaign random sequence.
- Added battle Pause/Resume, speed changes while paused and Skip from pause. Enlarged battle characters and synchronized
  their sprite playback with battle timing.
- Added core regression cases for recommended battle/expedition parties. Extended the real interface tour to verify
  Journal history/no spoilers, pause/resume, Spire progression, rank exams, promotion and the rank celebration.
- Updated the source version to **1.2.0** and Android version code to **5**. English remains the default language.

## Version 1.1.2 — English default and functionality

- Made English the startup language on fresh installs and migrated older installs to English once. Subsequent manual
  English/Bahasa Indonesia choices remain saved; unsupported or blank values fall back to English.
- Isolated screenshot-tour preferences from player preferences, including language, audio and presentation settings.
- Prevented repeated sale/salvage payouts from removed gear and enhancement charges for gear no longer in the bag.
- Rejected sold or expired Market offers and non-pending/already resolved guild events. Invalid event choices are
  rejected; unaffordable choices leave the event pending.
- Selected daily quests only when their declared prerequisites are met.
- Guarded hero dismissal against missing/foreign heroes, busy tasks and the last guild member. Equipped gear returns
  safely to the bag; repeated dismissal is rejected. Promotion rejects missing, foreign and busy heroes before payment.
- Made hardware Back defer to the battle screen's handler. Closing a nested picker, promotion or item popup refreshes
  the remaining detail dialogs and visible non-battle page; list scrolling clamps after the content shrinks.
- Refreshed Contract return counts on every change, replaced expired Board/Tavern/Market cards, and updated the Tavern
  refresh action as its free-refresh eligibility changes.
- Refreshed Party readiness and preview caching when hero health or statistics change.
- Hid the inactive close button on mandatory-choice event dialogs; the choice/Continue flow remains available.
- Added 22 core test cases covering payment atomicity, facility/research deadlines and separate slots, gem speedups,
  training/healing capacity and offline completion, crafting quantity and offline completion, enhancement caps/stat
  updates, disposal safeguards, gold/gem Market purchases and full bags, login streaks, daily baselines, event rewards,
  rank promotion and save persistence, tower progression, replay bonuses and deferred expedition payouts.
- Enforced a free fallback choice for every guild event, keeping its mandatory choice flow usable.
- Added six dismissal/promotion regression cases covering real training/healing/expedition tasks, returned expeditions
  awaiting claim, last/foreign/repeated dismissal, equipment ownership after save, and promotion charges/health.
- Updated the source version to **1.1.2** and Android version code to **4**.

**Validation:** the final 1.1.2 EditMode run completed with **70 cases total: 69 passed, 0 failed, 1 explicitly skipped**
(`Logs/verify_tests_final.log`, `Logs/results_EditMode.xml`). All **59 CoreTests** and **9 language cases** passed, along with
`FirstSessionPacing`. `FullPlaythroughPacing` remains explicit with the reason “Long-running pacing simulation”.

Both comprehensive functional tours passed:

| Resolution | Screenshots | Texts checked | Passing flow checks | Layout issues | Failed flow checks |
|---|---|---|---|---|---|
| 540 × 960 | 80 | 2,890 | 289 | 0 | 0 |
| 432 × 864 | 80 | 3,065 | 289 | 0 | 0 |
| Combined | 160 | 5,955 | 578 | 0 | 0 |

Each run follows the English functional flow and includes **68 English and 12 selected Indonesian captures**. The
narrow run is not a full Indonesian tour. Reports are `Builds/Win/verify-final-en/layout-audit.json` and
`Builds/Win/verify-final-narrow/layout-audit.json`. Coverage includes settings/language, facilities/research,
crafting/training/healing, gear/consumables/Market, contracts/rewards, hero promotion/dismissal, battle/result controls
and save/exit/Continue persistence. Later-feature fixtures are introduced after the untouched opening has passed.

The final Windows build succeeded with the latest Indonesian strings (`Logs/verify_windows_final.log`). The final
Android build succeeded (`Logs/verify_android_final.log`): `Builds/Emberhall-1.1.2.apk`, **64,671,355 bytes**.
APK inspection confirms `com.pixelgamemaster.emberhall`, version **1.1.2**, version code **4**, ARM64, minimum API **26**
and target API **36**. APK signature verification passed with v2 signing.

No Android device was connected for installation/device verification. Native Share/Rate integrations were reviewed
in code and their UI controls checked; external platform dialogs were not invoked. The sections below are earlier
release records.

## Version 1.1.1 — Hall, battle and label refinement

- Added four bright painted Hall/battle assets, bringing the generated-art set to twelve assets while retaining all
  18 item icons.
- Added `hall_ground.png`, a 1024 × 1536 courtyard, and `hall_buildings.png`, a 1254 × 1254 atlas with true transparent
  alpha and a 3 × 3 arrangement of nominal 418 × 418 cells.
- The atlas contains Tavern, Blacksmith, Training Hall, Infirmary, Barracks, Treasury, Library, Market and Fountain.
  Painted Hall assets use smooth sampling; animated characters retain their original sprite style.
- Adjusted the runtime infirmary/library cut to the transparent gap at source row 810, preserving the full library spire
  and preventing neighboring sprite fragments.
- Refined the Hall layout with larger centered building names and status labels on slimmer plaques, and a clearer quest tracker.
- Refined decorative diamond placement at panel corners and increased and centered interface labels.
- Made popup heights follow their content and the available viewport.
- Added `battle_meadow.png` and `battle_ruins.png`, both 1024 × 1536. Sunmeadow/Whisperwood use the painted meadow;
  other regions, the Spire and exams use the bright stone courtyard.
- Replaced the tall battle footer with a 292-pixel bottom strip and a horizontal hero roster. Names remain 32 pixels;
  class/level labels and HP remain 28 pixels. Larger parties can scroll sideways. Role descriptions remain on Party/Hero screens.
- Added compact mission/wave controls, subtle cream name chips sized to the name, and green/red team accents.
  Formations occupy the arena floor, with headroom for large bosses and their combat feedback. Front/side sprite faces
  remain visible. Battle simulation, event order, replay waits and the result flow retain their existing behavior.
- Anchored unit names, HP bars and projectile aim to the visible sprite bounds, removing the gap caused by transparent
  frame padding.
- Saved the generated-art prompts and slicing notes in `Store/Hall-art-notes.md`.
- Saved the battle-background prompts in `Store/Battle-art-notes.md`.
- Updated the source version to **1.1.1** and Android version code to **3**.

**Validation:** Windows and Android builds succeeded (`Logs/final_polish_windows.log`, `Logs/final_polish_android.log`).
The APK is `Builds/Emberhall-1.1.1.apk`, **64,605,319 bytes**.
APK inspection confirms `com.pixelgamemaster.emberhall`, version **1.1.1**, version code **3**, ARM64, minimum API **26**
and target API **36**.

| Tour | Resolution | Captures | Texts checked | Layout issues | Opening failures |
|---|---|---|---|---|---|
| English, plus key Indonesian screens | 540 × 960 | 59 | 2,127 | 0 | 0 |
| Full Indonesian | 432 × 864 | 48 | 1,886 | 0 | 0 |
| Combined | — | 107 | 4,013 | 0 | 0 |

Reports are `Builds/Win/final-polish-en/layout-audit.json` and `Builds/Win/final-polish-id/layout-audit.json`. Both
actual opening flows succeed through paid recruitment, the first battle victory, claims for chapters 1–3, auto-equip
and chapter 4. Neither runtime log reported exceptions or failed flow checks.
The core suite was not rerun for this UI pass; its earlier 32-pass/one-explicit-skip record remains in the 1.1.0 section.

## Version 1.1.0

### Presentation and opening

- Added eight generated art assets: a bright painted splash, three prologue illustrations, a phoenix app-icon source,
  and three six-cell atlases with 18 painted item icons for equipment, potions, materials and a crest.
- Large illustrations show hooded or helmeted heroes and views from behind. Front-facing pixel characters animate in play.
- Reworked the shared palette to cream, sage, sky blue and warm gold; reduced crowded HUD and party layouts.
- Added contained text, paragraph scrolling, adaptive grids and a horizontal strip for party synergies.
- Added bounded cream plaques with dark text behind battle-unit names for clear contrast.
- Added five onboarding pages: three short story scenes, the guild loop, and the guild charter.
- Authored English and Indonesian narration about restoring Emberhall, Maren's hearth and the shining Spire.
  Narration appears as paced text over three illustrated scenes with gentle motion. There is no prerecorded video or
  recorded voiceover. Story replay is available from the title screen.
- Matched the arriving starter to the selected portrait and explicitly initialized full health, progression and supplies.
- Clarified the first chapters with exact Tavern, Campaign and Gear destinations. Early battles and equips count,
  preventing completed actions from becoming a tutorial dead end.
- Gave Maren's first-step guidance a dedicated strip below the HUD, with direct actions and reward claims.
- Corrected the fourth-step guide to name Mushroom Grove, matching the active chapter objective in both languages.

### Gameplay

- Added Balanced, Assault and Guard battle plans. Forecast simulations and actual combat use the selected plan.
- Added deliberate enemy priorities and emergency-healing priorities; control skills spread status effects.
- Fixed double-applied shields, temporary-effect timing, multi-turn stun, and stale hero stats after upgrades.
- Added role-aware auto-equip and prevented wasting potions on healthy heroes.
- Improved the opening: first-clear supplies, an immediate first level-up, earlier special skills and infirmary access.
- Added post-victory recovery, revival after defeat, level-up healing and faster passive recovery.
- Guaranteed an initial mix of defender/healer/fighter recruits, fixed candidate rotation, and added a regular rare offer.
- Added safe/standard/demanding contract choices using unlocked templates; health, role variety and synergies affect power.
- Locked expedition odds at departure and retained the forecast in saves.
- Rejected duplicate/foreign party members, repeat hires, early/double expedition payouts and stack-selling gear.
- Added actionable readiness and mission errors with recovery, task-claim and navigation instructions.

### Controls

- **Begin your adventure** opens onboarding. **Continue** loads the existing guild.
- Onboarding **Continue** advances; the back arrow revisits a page. **Skip** jumps to the guild charter.
- **Auto On/Off** toggles automatic story progression. **The Story of Emberhall** replays the three scenes.
- The charter lets players name the guild, choose a banner colour, and select Warrior, Archer or Mage.
  **Light the Hearth** starts the named guild. Other classes can be recruited in play.
- Tap heroes in party selection to add them; tap occupied slots to remove them. **Auto** builds a suggested party.
- Choose **Balanced**, **Assault**, or **Guard**, inspect the updated estimate, then select **Fight**.
- Battle replay retains speed and skip controls. Contracts use **Send** and can be claimed after the party returns.

### Compatibility and validation

Save version 1 is retained. Older expedition records without a forecast use the existing calculation.
Content and balance changes apply to loaded guilds without requiring a reset.

Unity EditMode validation completed with **32 passed, 0 failed, and one explicit long-running pacing simulation skipped**
(33 cases total, historical 1.1.0 record). Coverage included all six starter classes across
12 battle seeds each, doctrine tradeoffs, status timing, healer priorities, save round-trips, recruiting, item handling,
expedition safeguards, charter/start invariants and early-action credit.

Both complete Windows tours passed their text-containment and opening-flow checks:

| Tour | Resolution | Captures | Texts checked | Layout issues | Opening failures |
|---|---|---|---|---|---|
| English, plus key Indonesian screens | 540 × 960 | 59 | 2,127 | 0 | 0 |
| Full Indonesian | 432 × 864 | 48 | 1,880 | 0 | 0 |
| Combined | — | 107 | 4,007 | 0 | 0 |

Reports are `Builds/Win/polish-release-en/layout-audit.json` and `Builds/Win/polish-release-id/layout-audit.json`; each
screenshot also has its own audit. Neither runtime log reported an exception or a failed opening-flow check.

The opening-flow checks succeeded in both languages using actual game buttons and original starting resources:
create the named Archer guild with 600 gold, pay for a second hero, claim chapter 1, select and restore party members,
fight and win Slime Meadow, return
through the battle result, claim chapter 2, navigate to the Bag, auto-equip, claim chapter 3 and verify chapter 4 unlocks.
Only after these checks pass does the tour add resources to cover later screens. Tours use a separate save, capture
offscreen through the UI camera, and preserve previous screenshot directories.

The final Windows build succeeded (`Logs/polish_windows_release.log`). The Android APK build also succeeded
(`Logs/polish_android_release.log`): `Builds/Emberhall-1.1.0.apk`, **49,960,195 bytes**. Inspection of the APK confirms
`com.pixelgamemaster.emberhall`, version **1.1.0**, version code **2**, ARM64, minimum API **26**, and target API **36**.
