# AdMob integration

Version 1.3.0 adds the official Google Mobile Ads Unity plugin **11.5.0**, installed from OpenUPM and pinned in
`Packages/manifest.json` / `packages-lock.json`. Its native Android dependencies are Google Mobile Ads **25.4.0**
and UMP **4.0.0**. EDM4U **1.2.187** resolves the Gradle dependencies during builds. No mediation adapters are included.

## Test configuration

`Assets/Resources/AdMobConfig.asset` is the single runtime configuration. `useTestIds` defaults to true.
`Emberhall → Configure AdMob` synchronizes the SDK's app IDs for the native manifest. The Android app ID is
`ca-app-pub-3940256099942544~3347511713`; the iOS sample app ID is
`ca-app-pub-3940256099942544~1458002511`. An optional publisher app ID overrides the sample app ID even while test
ad units remain enabled, so you can validate your own UMP messages without serving live ads.

| Format | Android test ad unit | iOS test ad unit |
| --- | --- | --- |
| Anchored adaptive banner | ca-app-pub-3940256099942544/9214589741 | ca-app-pub-3940256099942544/2435281174 |
| Interstitial | ca-app-pub-3940256099942544/1033173712 | ca-app-pub-3940256099942544/4411468910 |
| Rewarded | ca-app-pub-3940256099942544/5224354917 | ca-app-pub-3940256099942544/1712485313 |

These are [Google's sample units](https://developers.google.com/admob/unity/test-ads). They do not generate revenue.
The IDs are not secrets. Normal desktop builds offer no real ads. Unity Editor previews use Google's placeholder
implementation; a phone is needed to check native serving, safe-area behavior, external ad clicks and UMP forms.

## Placements

- **Banner:** anchored adaptive, below the bottom navigation with a separate inset and tap gap. Its height comes
  from the native SDK and is converted to canvas units. Android placement reserves the native system navigation
  inset even while the bar is hidden, plus 8 dp beneath the ad. The game reserves that same space above it; visible
  system bars are not counted twice. An app-owned native parent contains the SDK view so Show/window callbacks
  cannot overwrite that reserve. A pre-draw guard hides any intermediate unsafe layout. Placement follows native
  bar changes; only a safe-width change requires a new creative.
  No banner during the first four story quests, title,
  onboarding, battle, modal dialogs, consent/full-screen ads or backgrounding. A failed load leaves no empty inset.
- **Interstitial:** only when the player taps Back from battle results and returns to a menu. Requires three distinct
  campaign victories after the previous full-screen ad, 120 seconds of session warm-up and at least 180 seconds since
  another full-screen ad. Maximum five automatic interstitials per session. Rank exams and Spire battles do not
  count or trigger one. Defeats, Retry and Next never trigger one. Missing inventory is skipped immediately.
- **Rewarded victory:** a voluntary button adds 50% of the battle's base earned gold, rounded down. It appears only
  after the initial tutorial, on campaign victories, once per result. Gear, XP and base rewards are unchanged.
- **Rewarded supplies:** Menu → Guild supplies offers 150 gold + 2 gems per completed video, with three claims per
  UTC day and at least ten minutes between claims. There is no currency cost for a failed or dismissed video.

Only the SDK's earned callback redeems a ticket. The receipt, cap and wallet changes are saved together immediately.
Duplicate callbacks are ignored. New guilds and reloaded saves invalidate outstanding tickets. This is an offline
game with local rewards; the ledger is not a server-side anti-cheat system.

All SDK callbacks enter Unity's main thread. Used or expired full-screen inventory is disposed and replaced.
Load failures retry with backoff (5–300 seconds), stalled loads can recover, and banner sizes refresh after a
safe-width change. Game audio pauses only for actual full-screen presentations, then restores its prior state.
Consent/network initialization itself does not freeze gameplay.
If an asynchronous UMP refresh requires a form after the player has entered battle, presentation waits for a safe
menu. Consent is still checked through UMP before requesting ads.

## Consent and test controls

UMP refreshes consent information each launch before the first ad request. The first four story quests are ad
free even if the three-step quick guide is skipped; automatic initialization starts after them. `CanRequestAds()` gates SDK initialization and every new client
after privacy changes. No locally invented consent flag is used. Privacy changes discard cached ads. The SDK
content rating is capped at PG and publisher first-party ID is disabled. These settings do not classify the app's
target audience. Since 1.4.1, publisher first-party ID is disabled after the SDK initialization callback and before
any ad request: the [native Android API](https://developers.google.com/admob/android/reference/com/google/android/gms/ads/MobileAds#putPublisherFirstPartyIdEnabled(boolean))
requires that order. A failed initialization releases the pending state so loading can retry.

Settings → Ads & privacy links the app policy and Google advertising privacy. When UMP requires it, **Manage privacy
choices** opens Google's form. While test IDs are enabled, the same page provides **Reload test ads**,
**Preview interstitial**, and **Preview rewarded · no bonus**. Those previews grant no resources and are removed
from the UI when test mode is turned off. Normal reward offers are the way to test actual game bonuses.

## Moving to publisher IDs

1. Register the Android app in AdMob and create banner, interstitial and rewarded units. Fill the app ID and all
   three unit IDs in `AdMobConfig.asset` for the target platform. Keep test mode enabled while validating.
2. Configure the applicable UMP messages in that AdMob account. For regional form testing, follow
   [Google's UMP test-device instructions](https://developers.google.com/admob/unity/privacy). Never reset consent
   automatically in a production build. A sample app ID cannot manage your publisher's consent messages.
3. Set your published privacy-policy URL and complete the store's advertising/data disclosures for the final SDK
   configuration. The source policy is included in `Store/Privacy-policy.md`.
4. Disable `useTestIds` only for the final publisher build. Build validation rejects missing app/unit IDs and any
   sample ID in publisher mode. Rebuild rather than editing the manifest or APK manually.

No AdMob account, live ad unit or third-party monetization service was created by this integration. iOS IDs are
provided, but this Windows project has only produced an Android build; iOS native behavior is unverified.

## Verification

EditMode tests cover no reward on cancellation, duplicate callbacks, exact bonus amounts, simultaneous tickets,
daily cap/cooldown, midnight rollover, old saves, reset/reload invalidation and interstitial frequency limits.
The full `-gm-verify` desktop tour injects a **clearly labelled flow/layout double** into an isolated save. It checks
the actual Menu, supplies and battle-result buttons, failed/dismissed/earned callbacks, audio restoration, banner
insets, no-fill fallback and reward persistence. Its preview screenshots are not native ads or impressions.

Build with `Tools/unity.ps1 -Method Emberhall.EditorTools.BuildTools.BuildApk -Extra @('-buildTarget','Android')`.
The pinned SDK, tracked Gradle templates and SDK settings make the native dependencies reproducible.
See [current physical-device evidence](Validation/1.4.2/README.md) for the exact checks completed and remaining limits.

### Native Android check

An explicit QA launch mode uses the real `GoogleAdsClient`, official sample ad units and an isolated guild. It is
enabled only while `useTestIds` is true. Install the signed APK on a connected compatible Android device, force-stop
the app, then launch:

```powershell
adb shell am start -n com.pixelgamemaster.emberhall/com.unity3d.player.UnityPlayerGameActivity --ez emberhall-adcheck true
```

This mode uses its own save slot and preference prefix; it does not reset the player's guild. It completes only the
ad-free story quest fixtures in that isolated guild and writes `native-ad-report.json` to the app's external files
directory. Check that `client` is `GoogleAdsClient`, initialization succeeds, and real banner/interstitial/rewarded
inventory loads. Write one command to `native-ad-command.txt` in that directory:

- `banner`: show the Hall, allowing the native banner to appear below navigation.
- `stress`: run 80 transitions through Hall, Bag and a supplies popup, repeatedly hiding/showing the real SDK ad and
  replacing its child LayoutParams as SDK repositioning does. Reports include protected native hosts, visible
  pre-draw checks, guarded intermediate layouts and unsafe draws. Reveal Android navigation and test Home/return
  during and after this run; a settled banner must retain the native navigation inset and the additional 8 dp gap.
- `interstitial`: present a loaded test interstitial. Check actual opened and closed counts after closing it.
- `rewarded`: request a real test rewarded video and a supplies ticket. Complete the video; check the earned callback
  and wallet increase of exactly 150 gold and 2 gems. Closing before earning must grant nothing.
- `retry`: retry initialization or loading; `finish`: save the isolated guild and exit.

The directory is normally `/sdcard/Android/data/com.pixelgamemaster.emberhall/files`. Native reports distinguish a
request from an SDK presentation; a requested ad alone is not evidence of an impression. This mode never injects
the desktop flow double. Force-stop and launch without the extra to return to the player's normal save.

Official references: [setup](https://developers.google.com/admob/unity/quick-start),
[adaptive banners](https://developers.google.com/admob/unity/banner/anchored-adaptive),
[interstitials](https://developers.google.com/admob/unity/interstitial),
[rewarded](https://developers.google.com/admob/unity/rewarded), and
[privacy](https://developers.google.com/admob/unity/privacy).
