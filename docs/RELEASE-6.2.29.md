## Türkçe

- Sürüm kaldırma, oyun hazırlanırken/çalışırken engellenir. Başka profillerin doğrudan veya dolaylı kullandığı temel sürüm yanlışlıkla silinemez; dosya erişim hataları arayüzde açıklanır.
- Ana oyun JAR indirmesi geçici dosyada tamamlanır; boyut ve resmî SHA-1 doğrulanınca yerine alınır. Kesik, boş veya hatalı yanıt mevcut dosyayı bozmaz. Zaman aşımı yanıt gövdesini de kapsar.
- Ses/dil/doku dosyaları artık yalnızca varlıklarına bakılarak atlanmaz; boyut ve SHA-1 kontrol edilir. Aynı içerik bir kez indirilir, en fazla sekiz eş zamanlı işlem kullanılır ve başarısız indirme tamamlandı olarak gösterilmez.
- Temel Minecraft kurulumu başarısızsa Fabric profili kurulmuş gibi devam edilmez.
- Mod sayfasından ayrılınca ikon istekleri iptal edilir. Geç gelen sonuçlar yeni görünümü değiştirmez; hazır ikonlar yeniden indirilmez.
- Eş zamanlı günlük yazımları sıraya alındı. Düzeltme öncesi denemede 128 mesajdan 31'i kaydedilmişti; yeni kontrolde 128'i de birer kez kaydediliyor.

Resmî Minecraft 1.21.1 istemcisi ve örnek varlıkları gerçekten indirilip doğrulandı; bozulmuş varlık onarımı denendi. 1.8.9, 1.12.2, 1.16.5, 1.20.1, 1.21.1 ve 26.3 resmî varlık listeleri yeni sınırlarla kontrol edildi. Oyun içi oynanış ve fiziksel ekranlar arası DPI geçişleri ayrıca denenmedi.

## English

- Version removal is blocked during launch/play and while another profile depends directly or indirectly on that version. File access errors are shown in the interface.
- Main client downloads stage into temporary files and verify expected size and official SHA-1 before replacement. Truncated, empty or invalid transfers preserve existing files; timeouts also cover the response body.
- Asset installation verifies file sizes and SHA-1 instead of trusting existence alone, deduplicates identical content and limits parallel transfers to eight. Failed transfers no longer report a complete installation.
- Fabric installation stops if its base Minecraft installation fails.
- Mod icon requests are canceled when their view unloads; late results are ignored and rendered icons are reused.
- Concurrent diagnostic writes are serialized. The regression reproducer previously retained 31 of 128 messages; all 128 now appear exactly once.

The official Minecraft 1.21.1 client and sample assets were downloaded and verified, including damaged asset repair. Official asset metadata for six releases was checked against the new bounds. Actual gameplay and physical mixed-DPI display transitions were not tested separately.
