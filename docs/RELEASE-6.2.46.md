## Türkçe

- Mod çakışma kontrolü kimlik, takma ad ve koşulsuz dışlama indekslerini kullanır. Bütün mod çiftlerinin karşılaştırılması kaldırıldı; metadata doğrulaması, devre dışı modların atlanması ve Fabric Loader'a bırakılan sürüm koşulları korunur.
- Bağımsız oyun kütüphaneleri en fazla dört dosya paralel doğrulanır ve gerektiğinde onarılır. SHA-1, boyut, güvenli yol, geçici dosya ve atomik değiştirme kontrolleri korunur. Native arşivlerin açılması sıralı kalır.
- Bir dosya bozuksa oyun başlatılmaz. İptalde bütün çalışan doğrulamalar tamamlanmadan işlem dönmez; geçici dosyalar temizlenir.
- Minecraft, Java seçimi, Fabric modları, sunucu protokolü ve dünya dosyaları değiştirilmez.
- 6.2.45'teki arka plan mod güncellemesi ve Başlat sırasında ağ kontrolünü beklememe davranışı korunur.

## English

- Mod conflict checks index identities, provided aliases and unconditional exclusions instead of comparing every pair. Metadata validation and loader-specific predicate handling remain enforced.
- Independent runtime libraries are checked and repaired with at most four concurrent operations. Checksums, size checks, safe paths, temporary files and atomic replacement remain enforced. Native extraction stays sequential.
- Invalid files still block launch. Cancellation waits for all active operations and cleans temporary files.
- Minecraft, Java selection, Fabric mods, server protocol and world files keep their existing behavior.
- Includes the background mod update and non-blocking Play behavior from 6.2.45.

Validation covers conflict equivalence across 48 generated graphs, four simultaneous library repairs, checksum failure blocking launch, and cancellation cleanup. Real-game startup time and FPS have not been benchmarked.
