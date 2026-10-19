# Emberhall 1.3.0 — AdMob test integration

[Download the signed Android APK](https://github.com/fareza777/guild-manager/raw/refs/tags/v1.3.0/Releases/Emberhall-1.3.0.apk)

Includes Google's official SDK with test banner, interstitial and rewarded units. Adaptive banners sit below
navigation. Interstitials are limited to Back after campaign victories; optional videos add victory gold or guild
supplies. The first four tutorial chapters stay free of automatic ads. Consent, privacy choices, retry and reward
receipts are integrated. Test ads generate no advertising revenue.

| Field | Value |
| --- | --- |
| Version / Android code | 1.3.0 / 7 |
| Package | com.pixelgamemaster.emberhall |
| Platform | Android 8.0+ (API 26), ARM64, OpenGL ES 3 |
| Target API | 36 |
| Size | 75,024,418 bytes |
| Default language | English; Bahasa Indonesia in Settings |
| Build | Unity 6000.6.1f1, IL2CPP Release, signed APK |
| Ad mode | Google sample app ID and sample ad units |

SHA-256: `f94a9d2bc0bcd440cb37cf0ef8fc33204b21ffe157c9d3da6b19b4ef93fb49fe`

81 EditMode tests passed; one explicit long pacing simulation was skipped. The isolated desktop tour passed all
338 interface checks and 3519 visible-text checks across 96 screenshots, with zero layout issues. Native SDK
classes, test IDs, package metadata and APK signature were inspected. The flow/layout ad double does not serve
native ads. No compatible Android device was connected, so native serving, UMP forms and installation remain
unverified on a phone.

[AdMob setup and placements](../Store/AdMob-setup.md) · [Privacy policy](../Store/Privacy-policy.md) ·
[Validation evidence](../Store/Validation/1.3.0/README.md) · [Previous 1.2.1 release](1.2.1.md)
