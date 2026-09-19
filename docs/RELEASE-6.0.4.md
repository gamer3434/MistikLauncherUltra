# Mistik Launcher 6.0.4 — Stable update reliability / Kararlı güncelleme güvenilirliği

## What changed / Değişiklikler

- Fixed launcher and Auto-MCS update failures caused by GitHub API `403 rate limit exceeded` responses.
- Successful public release metadata is stored atomically under the user's launcher data directory.
- ETag conditional requests prevent repeated downloads; a recent verified cache is used for temporary 403/429 and server failures.
- Update payload hash, size, manifest and path-safety checks remain mandatory before installation.
- Existing settings, worlds, mods and Auto-MCS data are preserved.

## Validation / Doğrulama

- Normal validation: 257 checks passed.
- Cache, ETag, rate-limit fallback and rollback paths are covered.
- Protected portable ZIP plus online/offline Windows installers generated.

SignPath Foundation review is pending; this release is currently unsigned. / SignPath Foundation incelemesi sürüyor; bu sürüm şu anda imzasızdır.
