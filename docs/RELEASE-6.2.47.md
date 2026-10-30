## Türkçe

- Açılış optimizasyonu seçtiğiniz görüş mesafesini korur. 30 chunk otomatik olarak 12'ye düşürülmez. FPS optimizasyonu açıksa simülasyon mesafesi en fazla 8 chunk kalır; kapalıysa oyun ayarları değiştirilmez.
- Kullanıcı adı skinleri açılışta 15 dakikalık doğrulanmış PNG önbelleğinden kullanılabilir. Bozuk veya eski önbellek ağ üzerinden yenilenir; başarısız yenileme mevcut skin paketini korur. Elle skin yenileme önbelleği atlar.
- Aynı paket formatına ve dokulara sahip skin paketleri tekrar oluşturulmaz. Skin, profil ve sürüm değişimi kontrolleri korunur.
- Zorlanan büyük metot derlemesi, inline boyutları, NUMA ve büyük sayfa JVM ayarları kaldırıldı. Java'nın uyarlamalı derleyicisi, mevcut bellek sınırı, GC seçimi ve Forge bootstrap parametreleri korunur.
- Loga launcher hazırlık süresi ve ses motorunun hazır olma süresi milisaniye olarak eklenir. Ses motoru kaydı, ana menünün tamamen hazır olduğunu veya dünya yüklemesinin tamamlandığını göstermez.

## English

- Startup preserves the selected render distance, including 30 chunks. FPS optimization still limits simulation distance to 8 chunks; disabling it preserves game options.
- Username skins can reuse a validated PNG cache for 15 minutes during launch. Expired or corrupt cache entries are refreshed; manual refresh bypasses the cache. Failed refreshes retain the installed pack.
- Identical skin textures and pack metadata are no longer rewritten. Selection and profile race guards remain enforced.
- Removed forced huge-method compilation, inline overrides, NUMA and large-page policies. Adaptive compilation, heap limits, existing GC selection and Forge bootstrap parameters remain intact.
- Local logs record preparation and audio-engine readiness times. Audio readiness is not a full main-menu or world-load benchmark.

Tests cover 30-chunk preservation, simulation limits, JVM arguments, cache reuse and freshness, corrupt-cache recovery, manual refresh, and existing skin race/rollback cases. A 10-second startup or stutter-free 30-chunk gameplay has not been established. Optional performance mods installed on the developer's local instance are not bundled with this release.
