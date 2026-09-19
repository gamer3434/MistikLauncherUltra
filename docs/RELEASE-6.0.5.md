# Mistik Launcher 6.0.5 — Targeted, rollback-safe updates / Hedef sürüm ve geri alma koruması

## What changed / Değişiklikler

- The launcher update card now displays the release target version after metadata is checked.
- The updater helper receives the selected release version and refuses a mismatched package.
- A package older than the installed update manifest is rejected before any file replacement, preventing rollback to an old launcher.
- Turkish and English target-version labels are included.

## Validation / Doğrulama

- Normal validation: 258 checks passed.
- Protected build validation and helper smoke tests pass.
- Portable ZIP and online/offline installers generated with matching SHA-256 records.

SignPath Foundation review is pending; this release is currently unsigned. / SignPath Foundation incelemesi sürüyor; bu sürüm şu anda imzasızdır.
