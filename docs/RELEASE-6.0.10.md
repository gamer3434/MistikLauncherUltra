# Mistik Launcher 6.0.10 — Encrypted settings and smoother navigation / Şifreli ayarlar ve akıcı gezinme

## What changed / Değişiklikler

- Settings and their atomic backup now use Windows DPAPI encryption for the current user. Legacy plaintext files are read and encrypted on the next save. A recovered backup is preserved if the primary file is corrupt. / Ayarlar ve atomik yedek mevcut Windows kullanıcısı için DPAPI ile şifrelenir. Eski düz metin dosyalar okunur ve sonraki kayıtta şifrelenir. Ana dosya bozuksa sağlam yedek korunur.
- Removed duplicate Home and Settings rendering on page load. Settings update status has a theme-colored progress bar. / Ana panel ve Ayarlar sayfalarının açılıştaki çift çizimi kaldırıldı. Güncelleme durumuna tema renkli ilerleme çubuğu eklendi.
- Settings encryption does not include Minecraft worlds, mods, screenshots or logs. Encrypted settings cannot be moved to a different Windows account without re-entering them. / Şifreleme Minecraft dünyalarını, modları, ekran görüntülerini ve günlükleri kapsamaz. Şifreli ayarlar başka Windows hesabına taşınamaz; tercihler yeniden girilir.

## Validation / Doğrulama

- Normal validation: 263 checks passed, including encryption, legacy migration, backup recovery, bilingual navigation and UI controls. / Normal doğrulama: şifreleme, eski kayıt geçişi, yedek kurtarma, iki dilli gezinme ve arayüz kontrolleri dahil 263 kontrol geçti.
- Protected package and installer verification: 265 checks passed; the embedded offline payload passed its verification. / Korumalı paket ve kurulum doğrulaması: 265 kontrol geçti; yerel kurulumun gömülü paketi doğrulandı.

SignPath Foundation review is pending; this release is currently unsigned. / SignPath Foundation incelemesi sürüyor; bu sürüm şu anda imzasızdır.
