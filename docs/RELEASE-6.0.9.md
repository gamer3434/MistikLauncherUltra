# Mistik Launcher 6.0.9 — Legacy update-record repair / Eski kayıt uyumluluğu

## What changed / Değişiklikler

- The updater accepts legacy path normalization differences only when the Windows uninstall record confirms the active installation target.
- Relative ownership paths remain fully validated; unsafe, malformed or unrelated records are still rejected.
- A repaired marker is written with the active target path during the atomic update.

## Validation / Doğrulama

- Normal validation: 258 checks passed.
- Protected-package validation and helper smoke tests: 260 checks passed.

SignPath Foundation review is pending; this release is currently unsigned. / SignPath Foundation incelemesi sürüyor; bu sürüm şu anda imzasızdır.
