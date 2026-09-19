# Mistik Launcher 6.0.7 — Download telemetry and smoother navigation / İndirme bilgisi ve akıcı gezinme

## What changed / Değişiklikler

- Launcher and Auto-MCS update cards show downloaded size, total size, current speed and estimated remaining time.
- Progress notifications are throttled to 120 ms, preventing the WPF dispatcher queue from being flooded by fast downloads.
- First navigation no longer renders a newly created page twice; cached pages still refresh correctly after language/theme changes.
- Existing background mod synchronization, target-version display and downgrade protection remain included.

## Validation / Doğrulama

- Normal validation: 258 checks passed.
- Protected-package validation and helper smoke tests: 260 checks passed.
- Portable ZIP and online/offline installers generated with matching SHA-256 records.

SignPath Foundation review is pending; this release is currently unsigned. / SignPath Foundation incelemesi sürüyor; bu sürüm şu anda imzasızdır.
