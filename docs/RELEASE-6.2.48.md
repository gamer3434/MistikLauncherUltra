## Türkçe

- Otomatik mod güncelleyici, her projenin bütün sürüm geçmişini ayrı ayrı indirmek yerine Modrinth'in toplu güncelleme API'sini kullanır. İstekler en fazla 100 dosya hash'i içerir.
- Minecraft sürümü, Fabric/Quilt yükleyicisi ve kararlı yayın filtresi sunucuya gönderilir. Gelen sonuçta proje kimliği, sürüm kimliği, yayın türü, Minecraft/yükleyici uyumu ve yayın tarihi tekrar denetlenir.
- Yeni dosyaların SHA512 kontrolü, Minecraft/Java/yükleyici koşulları, sabit bağımlılıklar, eşzamanlı dosya değişikliği kontrolü ve geri alınabilir yedekler korunur.
- API'den eksik veya değişmemiş sonuç geldiğinde mevcut modlar korunur. Başlat düğmesi hem kimlik sorgusunu hem toplu güncelleme sorgusunu beklemeden iptal edebilir.

## English

- Automatic updates use Modrinth's batch update API instead of fetching every project's full version history. Requests contain at most 100 file hashes.
- Requests filter by exact Minecraft version, loader and stable releases. Responses are independently checked for project identity, version identity, release channel, game/loader compatibility and publication date.
- Download checksums, runtime requirements, pinned dependencies, concurrent-file checks and recoverable backups remain enforced.
- Missing or unchanged results retain installed files. Play can immediately cancel either the identification or update lookup.

Validation covers 205 installed projects using six metadata requests, cross-project pinned updates, second-download failure retaining the complete original set, foreign-project responses, missing results, and cancellation during both lookup phases.

An isolated read-only measurement on 68 local JARs identified 63 projects and completed hashing plus two API requests in approximately 1.5 seconds. No installed mods were changed by that measurement. This measures metadata lookup, not Minecraft startup time or FPS.

API reference: [Modrinth batch updates](https://docs.modrinth.com/api/operations/getlatestversionsfromhashes/).
