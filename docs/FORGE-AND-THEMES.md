# Forge ve renk temaları / Forge and color themes

## Türkçe

**Gerekçe:** Resmî Forge profilleri genellikle Vanilla sürümünü `inheritsFrom` ile kullanır ve kendi JAR dosyasına sahip değildir. Eski tarama bunları gizliyordu; eski kurulum ise Forge bileşenleri yerine Vanilla dosyalarını kopyalıyordu. Renk tercihi de yalnızca birkaç kontrolü değiştiriyordu.

**Uygulama:** `MistikLauncher/GameProfiles.cs` güvenli, döngü sınırlı profil tanıma sağlar. Kurulu Forge sürümleri ayrı ayrı listelenir, alt seçim kutusunda görünür ve seçim kaydedilir. Hatalı eski Forge kopyaları kurulu kabul edilmez. `ForgeInstaller.cs`, resmî promotions listesinden önerilen (yoksa en son) yapıyı seçer; Maven üzerindeki yükleyiciyi HTTPS üzerinden indirip yayımlanan SHA-1 dosyasıyla bütünlüğünü doğrular ve Java ile `--installClient` çalıştırır. SHA-1 kontrolü bir yayıncı imzası değildir. Eski kopyalar otomatik silinmez; Sürümler sayfasından Forge'u yeniden kurun. Java gereklidir; eski Forge sürümleri uygun Java 8 gerektirebilir.

Forge başlatmada profilin Windows JVM kuralları, modül parametreleri, `forgeclient` hedefi ve eski `FMLTweaker` argümanları korunur. Forge için deneysel JVM performans bayrakları uygulanmaz.

`ColorThemes.cs` beş renk ailesini tüm ortak yüzeylere, kartlara, giriş alanlarına ve aksiyonlara uygular. **Ayarlar → Renk temaları** üzerinden seçim anında uygulanır ve kaydedilir. Türkçe/İngilizce başlıklar ve açıklamalar iki JSON kataloğunda bulunur. RedX Library'nin belirli sürümüne görsel eşleşme, kullanıcı bağlantısı henüz sağlanmadığı için doğrulanmadı.

**Sonuç:** Korumalı pakette 58 otomatik kontrol geçti: Forge tanıma/seçim/kaydetme, başlatma komutu, beş tema, iki dil, güncelleme paketleri ve gerçek yardımcı süreç ile değiştirme/yeniden başlatma. Gerçek Minecraft/Forge oyununun başlatılması ve resmî Forge yükleyicisinin uçtan uca çalıştırılması bu bilgisayarda henüz doğrulanmadı. Ekran görüntüleri `screenshots/` altında; dağıtım önizlemedir.

## English

**Reasoning:** Official Forge profiles inherit the Vanilla client JAR and were hidden by the old own-JAR requirement. The old installation routine copied Vanilla files instead of installing Forge dependencies. Color settings affected only a few controls.

**Implementation:** `GameProfiles.cs` validates inherited profiles, rejects cycles/path traversal and obsolete fake Forge copies, and keeps installed builds individually selectable. `ForgeInstaller.cs` resolves the recommended/latest official promotion, downloads the official Maven installer over HTTPS, checks its published SHA-1 checksum, and invokes Java with `--installClient`. This checksum is not a publisher signature. Old fake copies are retained; reinstall Forge from Versions. Java is required; older Forge may require Java 8.

Forge launch commands retain Windows JVM rules, module options, the Forge client target and legacy FMLTweaker parameters. Experimental JVM tuning is disabled for Forge. `ColorThemes.cs` supplies five coordinated color families. Select **Settings → Color themes** for immediate application and automatic persistence. New labels/help are in both locale catalogs. Exact matching to a particular RedX Library design remains unverified pending the user's reference link.

**Conclusion:** The protected package passes 58 automated checks, including both languages, themes, Forge selection/arguments, update verification and real helper handoff/replacement/restart. Actual Minecraft/Forge gameplay and a complete official Forge installer run remain unverified on this computer. Screenshots are under `screenshots/`; this is a preview distribution.
