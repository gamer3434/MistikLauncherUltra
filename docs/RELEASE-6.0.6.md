# Mistik Launcher 6.0.6 — Smooth version switching / Akıcı sürüm geçişi

## What changed / Değişiklikler

- Mod pool synchronization no longer runs on WPF's UI thread, so version selection and navigation stay responsive while files are scanned or moved.
- A latest-selection guard queues the newest version after an in-progress scan instead of silently dropping the user's choice.
- Synchronization failures marshal their status message through the WPF dispatcher, preventing cross-thread UI exceptions.
- The update card still shows the exact target release and the updater blocks older packages from replacing a newer installation.

## Validation / Doğrulama

- Normal validation: 258 checks passed.
- Protected-package validation and helper smoke tests: 260 checks passed.
- Portable ZIP and online/offline installers were generated with matching SHA-256 records.

SignPath Foundation review is pending; this release is currently unsigned. / SignPath Foundation incelemesi sürüyor; bu sürüm şu anda imzasızdır.
