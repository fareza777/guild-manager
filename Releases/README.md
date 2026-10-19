# Emberhall 1.4.1 — Native ads and Android navigation

[Download the signed Android APK](https://github.com/fareza777/guild-manager/raw/refs/tags/v1.4.1/Releases/Emberhall-1.4.1.apk)

Fixes native AdMob initialization and keeps banners above the Android Back/Home/Recent area, with an extra 8 dp
gap. The same space is reserved in the game. System navigation remains clear when temporarily revealed.
Includes all 1.4.0 visual, automatic-combat and campaign-progression improvements.

Tested on a physical Android 13 phone: native banner display, Home/return, interstitial open/close, no reward on
early dismissal, and exactly 150 gold + 2 gems after a completed rewarded video. The original guild save and its
backup stayed unchanged. Google sample IDs remain enabled; these ads generate no revenue.

| Field | Value |
| --- | --- |
| Version / Android code | 1.4.1 / 9 |
| Package | com.pixelgamemaster.emberhall |
| Platform | Android 8.0+ (API 26), ARM64, OpenGL ES 3 |
| Target API | 36 |
| Size | 75,052,382 bytes |
| Default language | English; Bahasa Indonesia in Settings |
| Build | Unity 6000.6.1f1, IL2CPP Release, signed v2 APK |
| Ad mode | Official Google sample app ID and ad units |

SHA-256: `546875392e0918e0a6e84f71f716a03e074c3066c3e8790081cd87ab077df952`

92 core tests passed; one explicit long pacing simulation was skipped. Native package classes, IDs and signature
passed inspection. Publisher UMP regional forms, production ads, native network-loss cases and iOS are unverified.

[Physical-device evidence](../Store/Validation/1.4.1/README.md) · [AdMob setup](../Store/AdMob-setup.md) ·
[Privacy policy](../Store/Privacy-policy.md) · [Previous 1.4.0 release](1.4.0.md)
