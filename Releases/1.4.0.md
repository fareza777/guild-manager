# Emberhall 1.4.0 — A clearer journey

[Download the signed Android APK](https://github.com/fareza777/guild-manager/raw/refs/tags/v1.4.0/Releases/Emberhall-1.4.0.apk)

A brighter, open title menu, clearer hero names and levels, fountain-safe Hall walking, and a clean battle arena
without unit names. Automatic battles now complete their waves reliably; Auto team and Start auto battle are explicit.

The campaign uses renewable stamina (30 maximum, +1 every five minutes), free first three trail clears and a free
daily +10 camp ration. Trails cost 2, bosses/Spire 3, and defeats/interrupted runs at most 1. Eight trail stars open
a regional boss; the next region also requires that boss and the guild rank. Exams and contracts use no stamina.
See [gameplay design](../Store/Gameplay-design.md).

| Field | Value |
| --- | --- |
| Version / Android code | 1.4.0 / 8 |
| Package | com.pixelgamemaster.emberhall |
| Platform | Android 8.0+ (API 26), ARM64, OpenGL ES 3 |
| Target API | 36 |
| Size | 75,046,642 bytes |
| Default language | English; Bahasa Indonesia in Settings |
| Build | Unity 6000.6.1f1, IL2CPP Release, signed APK |
| Ad mode | Google sample app ID and sample ad units; no revenue |

SHA-256: `573d38d8fa9b3745dea32ed6f2946641aa22e3777c8b6a9c5152b576fcd3ad9f`

92 EditMode tests passed; one explicit long pacing simulation was skipped. All 347 desktop interface checks passed,
with 3512 texts across 100 screenshots and zero detected layout issues. The multi-wave Auto test reached victory
without Skip. APK metadata, native SDK classes, test IDs and v2 signature were verified.

The APK installed on the Android emulator, which stopped at a Unity graphics initialization error before the game
or ads started. No physical phone was connected. Native ad serving and UMP forms still require device testing;
desktop ad previews only verify game callbacks and layout. An isolated real-SDK phone QA mode is included.

[AdMob setup](../Store/AdMob-setup.md) · [Privacy policy](../Store/Privacy-policy.md) ·
[Validation evidence](../Store/Validation/1.4.0/README.md) · [Previous 1.3.0 release](1.3.0.md)
