# Audit / Denetim

Base: `7d90ade`. Version: `6.0.0-preview.1`.

| Problem | Correction |
|---|---|
| Settings truncation and silent data loss | Locked temporary write, atomic replace, previous backup recovery |
| Invalid name/RAM/port | Central normalization and visible settings validation |
| Hardcoded admin credentials | Administrator access disabled and route blocked |
| Unauthenticated remote mod installation | Firebase command handler removed; remote administrative methods inert |
| Hardware ID/IP telemetry | Automatic telemetry implementation removed |
| Arbitrary recursive uninstall path | Canonical target and reparse-point checks; uninstall execution disabled |
| Silent installation and process termination | Portable startup replaces legacy automatic installation |
| Unsigned update validated only by size/MZ | Automatic replacement disabled |
| Plaintext MQTT | TLS configured; connection not exercised |
| Version names containing 26 overwritten | Invalid substring rejection removed |
| Language setting did not translate navigation | Paired embedded catalogs and runtime events |
| Shared buttons ignored padding/focus | TemplateBinding padding and keyboard/hover/disabled feedback |
| Invalid Discord invite/unrelated video link | Repository and releases links |

Türkçe: Ayar kurtarma, doğrulama, uzaktan yönetim, gizlilik, taşınabilir açılış, güncelleme güvenliği, TLS, dil kaynakları ve düğme erişilebilirliği iyileştirildi.

## Verified / Doğrulanan

- Release build: zero warnings/errors.
- Thirteen checks against ordinary and protected assemblies: settings validation, atomic backup, previous-backup recovery, disabled unsafe remote controls, uninstall target rejection/acceptance, locale key parity, nonempty translations, Turkish/English runtime text and persistence.
- Home/settings WPF renders in both languages reviewed.
- NuGet resolved dependencies: no known vulnerable packages reported at validation time.
- Obfuscar completed; protected resource/WPF/configuration tests passed.
- Tests isolate data using `MISTIK_DATA_DIR` and temporary directories.
- GitHub authentication and write access verified. Preview published on `codex/modernization-preview`; main remains unchanged.

Türkçe: Normal ve korumalı DLL on üç denetimi geçti. Derleme uyarısız/hatasız. İki dilde görüntüler incelendi. NuGet bilinen açık bildirmedi. GitHub erişimi doğrulandı ve önizleme dalı gönderildi; main değişmedi.

## Outstanding / Kalan işler

1. Complete all legacy labels, dynamic prompts/errors, installer XAML, guides and changelogs. `localization-remaining.txt` is a heuristic helper-literal inventory, not a complete count.
2. Verify actual game downloads/launches, Java selection, mod integrity/migration, skins and server lifecycles.
3. Review downloaded executables/mod authenticity, public broker trust, friend identity and tunnel exposure. TLS alone does not authenticate community users.
4. Separately review/migrate installation and uninstall; both remain disabled in the preview.
5. Benchmark launch/download responsiveness and protected-release performance; tests do not establish FPS or production performance.
6. Authenticate GitHub and complete release requirements before replacing the stable version. Historical binaries and `SourceBackup` are not rebuilt or certified by this preview.

Türkçe: Eski metinlerin tam çevirisi, oyun/mod/sunucu uçtan uca testleri, aktarım/indirme güvenliği, kurulum/kaldırma incelemesi, performans ölçümü ve GitHub oturum açma tamamlanmalıdır.

## Protection / Koruma

String hiding is reversible; public source remains readable. WPF/JSON exclusions preserve functionality. Checksums detect corruption, not publisher identity. No anti-cheat, antivirus or SmartScreen bypass is claimed.

Metin gizleme geri çevrilebilir; açık kaynak görünürdür. WPF/JSON istisnaları uyumluluğu korur. Sağlama toplamı bir yayıncı imzası değildir. Antivirüs veya anti-hile engelini aşma garantisi yoktur.

## Follow-up: server authentication / Sunucu kimlik doğrulaması

Removed automatic recursive rewriting of `online-mode=false` during tunnel startup. Added a regression test proving `server.properties` stays byte-for-byte unchanged. / Tünel açılışında otomatik kimlik doğrulama kapatma kaldırıldı; dosyanın değişmediği test edildi.

## Auto-MCS updater follow-up

- Replaced pinned mirrored v5.3.0 EXE with official stable-release discovery.
- Added manual update, persisted startup auto-update, progress/version/status and bilingual redesigned server page.
- Twenty-four offline/ordinary checks; optional live official v2.3.9 download/digest/extraction adds the twenty-fifth check. The third-party program was not executed by validation.
- Cases cover check-only, install, no repeat download, repair, digest failure, staging cleanup, running process, offline failure and untrusted/missing-digest metadata.
- Existing server/game data is preserved; updates replace only auto-mcs.exe and its version receipt.

Türkçe: Sabit indirme kaldırıldı; resmî sürüm, manuel/otomatik güncelleme, SHA-256 doğrulama ve iki dilli sunucu tasarımı eklendi. Gerçek paket indirildi/doğrulandı; uygulama test sırasında çalıştırılmadı.

## Launcher update and palette follow-up

Added official stable-release discovery, startup/30-minute automatic checks, user preference/manual check, verified complete ZIP manifests, external self-contained update helper, ready handshake, parent identity/exit checks, transactional replacement rollback and restart. Added matching-tag release workflow. No stable release or tag was published by this work. Historical v5.5.2 is ignored as a downgrade.

Tests cover upgrade/downgrade, preview promotion, preparation without target mutation, full payload replacement, settings/world preservation, locked-file rollback, path traversal, protected user-data paths, busy deferral, corrupt package rejection, untrusted host, staging tamper rejection, cached bilingual pages, actual package manifest and real helper handoff/restart with a test fixture. Source update tests do not execute Minecraft.

Türkçe: Launcher otomatik güncelleme ve modern palet eklendi. Gerçek yardımcı program kontrollü test uygulamasını kapattı, dosyaları değiştirdi ve yeniden açtı. Yeni kararlı sürüm/tag yayımlanmadı; önizleme dalı güncellendi.
