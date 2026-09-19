# Mistik Launcher 6.0.3 — Error reports and reliable RGB / Hata raporu ve RGB

## What changed / Değişiklikler

- Added a bilingual error-report center on the home page. Reports include redacted launcher/game diagnostics and can be copied, saved, or opened in Explorer.
- Added local report storage under `%APPDATA%\\.mistik_ultra\\reports`.
- The close button RGB outline now always runs a smooth six-second color cycle, independent of the Windows animation preference.
- Removed the unused startup relay polling loop and dispose relay resources during shutdown.
- Migrated stale configuration version codes to the running release.
- Forge selection, skin validation, bilingual UI, and protected packaging fixes from 6.0.2 remain included.

## Validation / Doğrulama

- Normal validation: 254 checks passed.
- Protected/obfuscated validation: 256 checks passed.
- Online and offline Windows installers plus portable ZIP generated.

This release is unsigned. / Bu sürüm kod imzasızdır.
