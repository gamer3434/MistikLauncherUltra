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
- Twelve checks against ordinary and protected assemblies: settings validation, atomic backup, previous-backup recovery, disabled unsafe remote controls, uninstall target rejection/acceptance, locale key parity, nonempty translations, Turkish/English runtime text and persistence.
- Home/settings WPF renders in both languages reviewed.
- NuGet resolved dependencies: no known vulnerable packages reported at validation time.
- Obfuscar completed; protected resource/WPF/configuration tests passed.
- Tests isolate data using `MISTIK_DATA_DIR` and temporary directories.
- GitHub dry-run failed because credentials were unavailable. Remote repository was unchanged.

Türkçe: Normal ve korumalı DLL on iki denetimi geçti. Derleme uyarısız/hatasız. İki dilde görüntüler incelendi. NuGet bilinen açık bildirmedi. GitHub oturum bilgisi olmadığından uzak depo değiştirilmedi.

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
