# Mistik Launcher 6.0.8 — Navigation render cleanup / Gezinme çizim temizliği

## What changed / Değişiklikler

- Cached pages no longer run a second visual-tree translation after their language-aware refresh.
- New pages still receive one translation pass; existing pages refresh only the content they own.
- This removes redundant WPF layout work during navigation while retaining bilingual labels.

## Validation / Doğrulama

- Normal validation: 258 checks passed.
- Protected-package validation and helper smoke tests: 260 checks passed.

SignPath Foundation review is pending; this release is currently unsigned. / SignPath Foundation incelemesi sürüyor; bu sürüm şu anda imzasızdır.
