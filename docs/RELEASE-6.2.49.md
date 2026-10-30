## Türkçe

- Oyun açılışında başlangıç heap'i, seçilen maksimum bellek sınırına eşitlenmez. En fazla 2 GB ile başlanır; daha düşük maksimum seçilmişse o değer kullanılır. Heap gerektiğinde seçilen maksimuma kadar büyüyebilir.
- Bu değişiklik özellikle yüksek bellek sınırı seçilmiş bilgisayarlarda başlangıç bellek taahhüdünü azaltır. İşletim sistemi, native kütüphaneler, GPU ve heap dışı Java belleği için de alan gerekir; toplam işlem belleği heap sınırına eşit değildir.
- Oyuncunun maksimum heap sınırı, Java seçimi, modları ve dünya dosyaları değiştirilmez. Forge bootstrap parametreleri korunur.
- Bellek yardım metni maksimum sınır ile başlangıç boyutunun farkını açıklar.

## English

- Initial heap is no longer forced to equal the selected maximum. It starts at the smaller of 2 GB or the selected limit, and can grow to that limit as needed.
- This reduces initial heap commitment for large memory settings. Native libraries, the operating system, GPU resources and non-heap Java allocations still need memory; total process memory can exceed the heap limit.
- Maximum heap preferences, Java selection, mods and world files retain their existing behavior. Forge bootstrap parameters remain intact.
- Settings help distinguishes initial heap from maximum heap.

Validation checks heap ceilings from 256 MB through 32 GB and caps initial heap without exceeding those limits. A real 1.21.11 Fabric session with the installed mod set started and exited successfully with unchanged mod hashes.

This release does not establish a ten-second startup or a frame-rate guarantee. Controlled gameplay comparisons are ongoing; startup timing varies with resource loading and filesystem cache state.
