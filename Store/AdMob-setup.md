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
  from the native SDK and is converted to canvas units. No banner during the first four tutorial chapters, title,
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
safe-area/width change. Game audio pauses only for actual full-screen presentations, then restores its prior state.
Consent/network initialization itself does not freeze gameplay.

## Consent and test controls

UMP refreshes consent information each launch before the first ad request. The first four tutorial chapters are ad
free; automatic initialization starts after them. `CanRequestAds()` gates SDK initialization and every new client
after privacy changes. No locally invented consent flag is used. Privacy changes discard cached ads. The SDK
content rating is capped at PG and publisher first-party ID is disabled. These settings do not classify the app's
target audience.

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
See [release evidence](Validation/1.3.0/README.md) for the exact checks completed and the remaining device checks.

Official references: [setup](https://developers.google.com/admob/unity/quick-start),
[adaptive banners](https://developers.google.com/admob/unity/banner/anchored-adaptive),
[interstitials](https://developers.google.com/admob/unity/interstitial),
[rewarded](https://developers.google.com/admob/unity/rewarded), and
[privacy](https://developers.google.com/admob/unity/privacy).
