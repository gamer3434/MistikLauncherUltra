## Türkçe

- Ely.by giriş yardımcısı resmî authlib-injector 1.2.8 sürümüne yükseltildi. Gerçek Java 25 açılış testinde eski 1.2.5 sürümünün 32 `Unsupported class file major version 69` hatası tekrar oluşmadı.
- Yardımcı dosyası boyutuyla değil, sabit SHA-256 değeriyle doğrulanır. Doğrulanmış önbellek ağ isteği yapmadan kullanılır. Eski veya bozuk dosya yalnızca doğrulanmış indirme tamamlandıktan sonra değiştirilir; önceki dosya `.bak` olarak korunur.
- Resmî sabit sürüm kaynakları, indirme boyutu sınırı, zaman aşımı ve eşzamanlı kurulum kilidi kullanılır. Başarısız indirme mevcut dosyayı korur ve geçici dosyaları temizler.
- Yardımcı hazırlanamazsa Ely.by profili yardımcısız başlatılmaz; Türkçe/İngilizce hata gösterilir. Hesap bilgileri, Java seçimi ve modlar değiştirilmez.

Bu sürüm 10 saniyelik açılış veya belirli FPS garantisi vermez. Ana menü testi başarıyla açılıp normal kapandı; ses motorunun hazır olması yaklaşık 48 saniye sürdü. Bu işaret, etkileşimli menü veya dünyaya giriş süresi değildir. Sade GC ve AOT denemeleri hız kazancını doğrulamadığı için ürün davranışına eklenmedi.

## English

- Updated the Ely.by helper to official authlib-injector 1.2.8. A real Java 25 launch no longer produced the 32 unsupported-class-version failures seen with 1.2.5.
- Reuse requires the pinned SHA-256, rather than a minimum file size. Valid cached files need no network request. Verified downloads replace the helper atomically and preserve the previous file as `.bak`.
- Downloads use pinned official sources, a size bound, timeout and cross-process replacement lock. Failed downloads preserve the current helper and clean temporary files.
- Ely.by launch stops with a localized error if the helper cannot be prepared. Account data, Java selection and mods are retained.

This does not establish a ten-second startup or an FPS guarantee. The successful menu smoke test reached audio initialization in approximately 48 seconds and exited normally. Audio readiness is not interactive-menu or world readiness. Simpler GC and AOT experiments did not establish a speed improvement and are not enabled in the product.
