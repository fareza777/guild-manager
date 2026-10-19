# Emberhall 1.4.2 — Short guide and stable Android banner

[Download the signed Android APK](https://github.com/fareza777/guild-manager/raw/refs/tags/v1.4.2/Releases/Emberhall-1.4.2.apk)

The optional quick guide now ends after recruiting one hero, the first victory and equipping the party. SKIP GUIDE
lets the player continue immediately. Later trails, buildings and guild-level goals remain ordinary quests without
automatic arrows. Skipping is saved for that guild and preserves quests, rewards and the advertising grace period.

An app-owned native banner parent protects the Android navigation reserve from SDK Show/resize callbacks. A
pre-draw guard hides intermediate unsafe layouts. System-bar changes no longer reload an unchanged-width ad.
The connected Android 13 phone passed 80 menu/popup transitions, native Home/Recent/Back checks, interstitial return,
reward cancellation with no payout, and a completed rewarded video with exactly 150 gold + 2 gems. The original
guild save and backup stayed unchanged. All ads still use official Google test IDs.

| Field | Value |
| --- | --- |
| Version / Android code | 1.4.2 / 10 |
| Package | com.pixelgamemaster.emberhall |
| Platform | Android 8.0+ (API 26), ARM64, OpenGL ES 3 |
| Target API | 36 |
| Size | 75,056,854 bytes |
| Default language | English; Bahasa Indonesia in Settings |
| Build | Unity 6000.6.1f1, IL2CPP Release, signed v2 APK |
| Ad mode | Official Google sample app ID and ad units |

SHA-256: `1245ba2a8f4e2222c87c4faafdf9e697bb68252f529ae8534a537252c97b53dc`

Validation: 92 core tests passed, zero failed; one explicit long pacing simulation skipped. Desktop verification:
354 successful flows, 105 screenshots, 3,717 texts checked, zero layout issues. Signed APK classes and IDs passed
inspection. See the evidence for the checks completed and the limits of this device run.

[Validation evidence](../Store/Validation/1.4.2/README.md) · [AdMob setup](../Store/AdMob-setup.md) ·
[Previous 1.4.1 release](1.4.1.md)
