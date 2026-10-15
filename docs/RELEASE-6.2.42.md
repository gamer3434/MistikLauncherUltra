## Türkçe

- Modun seçili Minecraft ve yükleyici sürümüyle uyumlu dosyası yoksa işlem durur. Başka sürüme ait ilk dosyayı farklı havuza indirip sonra uyarmak kaldırıldı.
- JAR seçimi belirsizse bağımlılıklar veya dosyalar indirilmeden hata gösterilir. Sürüm uyumu tam Minecraft sürümünden denetlenir; 1.21.1 ile 1.21.11 aynı sayılmaz.
- Yakalanan mod kurulum, içe aktarma ve etkinleştirme hataları, anonim hata paylaşımı açıksa mevcut güvenli hata kodlarıyla admin paneline bildirilir. İptaller hata olarak gönderilmez.
- İlk etkinlik bildirimi başarısız olduğunda hata raporu göndermeden önce anonim kurulum kaydı tamamlanır; hata raporu tek başına aktif oturum oluşturmaz.
- Admin hata tablosuna kod, genel açıklama ve önerilen çözüm adımı eklendi. Destek ayrıntılarında son bildirim zamanı, öneri izni ve gösterilen açık/kapalı mod sayıları görünür. Kısaltılmış listelerin sayım kapsamı belirtilir.

Paylaşım kullanıcı tercihine bağlıdır; kapalı ayarlar herkes adına açılmaz. Ham hata mesajı, dosya yolu, hesap bilgileri ve günlük gönderilmez. Paneldeki rehber kesin tanı değildir; çevrimdışı kalan hata bildirimlerinin daha sonra ulaşması garanti edilmez.

Doğrulama: 855 normal, 867 paket kontrolü ve 48 web testi geçti. Mod testleri sahte API yanıtlarıyla yanlış sürüm ve belirsiz dosya durumlarında indirme yapılmadığını doğrular; gerçek kullanıcı modları değiştirilmedi.

## English

- Installation stops when no build matches the selected Minecraft version and loader. The fallback that downloaded the first unrelated build into another pool has been removed.
- Ambiguous JAR selections fail before dependency or artifact downloads. Minecraft versions match exactly; 1.21.1 and 1.21.11 are distinct.
- Caught mod installation, import and enable failures report only existing allowlisted codes when anonymous error sharing is enabled. Cancellations are excluded.
- If initial presence publication fails, error reporting first establishes the anonymous installation record without inventing an active session.
- Admin error rows include a code, general explanation and suggested action. Support details show last heartbeat, suggestion permission and displayed enabled/disabled mod counts, with accurate truncation labels.

Sharing remains optional and existing disabled preferences are preserved. Raw exception messages, paths, account data and logs are excluded. Guidance is not an exact diagnosis; delivery of offline error reports is not guaranteed.

Validation: 855 ordinary, 867 packaged checks and 48 web tests passed. Synthetic API responses verify that incompatible versions and ambiguous artifacts never trigger downloads; real user mods were not changed.
