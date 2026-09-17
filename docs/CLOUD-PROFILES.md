# Bulut profilleri / Cloud profiles — 6.0.0-preview.5

**Historical guide / Eski sürüm kılavuzu:** In preview.8, cloud account UI and automatic profile synchronization were removed at the user's request. Settings now remain local. Existing remote records were not deleted. / Kullanıcının isteğiyle preview.8'de bulut hesabı arayüzü ve otomatik eşitleme kaldırıldı. Ayarlar yerel kalır; eski uzak kayıtlar silinmedi.

## Türkçe

**Gerekçe:** Kullanıcı profili, skin tercihi ve ayarların cihazlar arasında korunması gerekir. Mevcut `mistiklauncher-9eb4b` Realtime Database ve Firebase Authentication kullanılır; ek sunucu veya istemci bağımlılığı eklenmez.

**Uygulama:** Sağ üstteki oyuncu profiline tıklayın. Ayarlar → Bulut profili bölümünde e-posta ve en az altı karakterli parolayla hesap oluşturun veya giriş yapın. Mevcut bulut profili girişte ve uygulama açılışında geri yüklenir; yeni hesapta yerel profil yüklenir. Ayarlar kaydedildikten yaklaşık 1,5 saniye sonra buluta gönderilir. Buluta kaydet ve Buluttan getir düğmeleri manuel eşitleme sağlar. Aynı anda yapılan cihaz değişikliklerinde son başarılı kayıt geçerlidir; gerçek zamanlı cihazlar arası birleştirme yapılmaz. Bağlantı başarısızsa yerel ayarlar korunur; başlangıçta geri yükleme başarısız olduğunda manuel geri yükleme başarılı olana kadar otomatik yükleme durur.

Eşitlenen alanlar: oyuncu adı, seçili sürüm, RAM, dil, renk, pencere düğmesi stili, üst menü kısayolları, skin türü/kullanıcı adı veya PNG, skin sağlayıcısı, turbo/FPS tercihleri, otomatik kapanma ve launcher/Auto-MCS güncelleme tercihleri. PNG 64×32 veya 64×64, en fazla 64 KB olmalıdır. Oyun, mod, sunucu dosyaları, yerel dosya yolları ve sistem müdahalesi seçenekleri yüklenmez. Yerel PNG başka cihazda uygulamanın veri klasörüne güvenli biçimde geri yüklenir.

Parola saklanmaz. Firebase oturum belirteçleri `cloud-session.dat` içinde mevcut Windows hesabına bağlı DPAPI şifrelemesiyle tutulur. Çıkış yapmak bu dosyayı kaldırır; bulut profili hesabınızda kalır. Firebase hesabı Minecraft/Microsoft oyun kimlik doğrulamasının yerine geçmez.

Veritabanının önceki herkese açık okuma/yazma kuralları kapatıldı. `launcherProfiles/{uid}` yalnızca aynı Firebase kullanıcı kimliği tarafından okunur/yazılır; bilinmeyen alanlar ve geçersiz değerler reddedilir. Eski veriler silinmedi; eski anonim yönetim erişimi kapalıdır. Üretimde anonim giriş kapalıdır. İstemci API anahtarı proje tanımlayıcısıdır; erişim yetkisi vermez.

**Sonuç:** Kullanıcıya ait bulut profil işlemleri canlı Firebase üzerinde doğrulandı. Geçici test hesabı ve verisi test sonunda silinir.

## English

**Reasoning:** Profiles, skin preferences and settings should follow the user across devices. The existing Realtime Database and Firebase Authentication provide this without an additional server or client dependency.

**Implementation:** Click the player profile at the top right. Under Settings → Cloud profile, create an account or sign in using an email and a password of at least six characters. Existing cloud settings restore at sign-in and startup; a new account uploads its local profile. Saved settings upload after about 1.5 seconds. Save to cloud and Restore from cloud provide manual sync. The last successful upload wins across concurrent devices; there is no live merge. Offline failures preserve local settings. Failed startup restore suspends automatic upload until manual restoration succeeds.

Synced fields: player name, selected version, RAM, language, accent, window button style, toolbar shortcuts, skin type/name or PNG, skin provider, turbo/FPS preferences, auto-close and launcher/Auto-MCS update preferences. PNG skins must be 64×32 or 64×64 and at most 64 KB. Game/mod/server files, local paths and system modification settings remain local. Restored PNGs are written only within the launcher's data folder.

Passwords are never stored. Session tokens use current-user Windows DPAPI encryption in `cloud-session.dat`; signing out removes this local file. The cloud profile remains in the account. Firebase sign-in does not provide Minecraft/Microsoft game authentication.

Previously public root rules are now closed. Only the matching authenticated UID can read/write `launcherProfiles/{uid}`. Unknown fields, malformed types and oversized values are rejected. Existing database data was preserved. Anonymous sign-in is disabled in production. The public client API key identifies the project; it does not authorize data access.

**Conclusion:** Live Firebase checks cover upload/restore, persistent encrypted sessions, cross-user access denial and validation failures. Temporary test users and profiles are cleaned up afterward.

## Code and deployment

- `MistikLauncher/CloudProfiles.cs`: bounded profile mapping, Auth REST, refresh and debounced sync.
- `MistikLauncher/WindowsSecret.cs`: Windows DPAPI.
- `MistikLauncher/Pages/ModernPages.cs`: bilingual account controls.
- `firebase/database.rules.json`: authenticated UID ownership and field validation.
- `firebase/firebase.json`: email/password Auth; anonymous sign-in disabled.
- `Validation/CloudTests.cs`: local and live checks without printing credentials.

```powershell
firebase deploy --only auth,database --project mistiklauncher-9eb4b --config firebase/firebase.json
dotnet run --project Validation -c Release -- artifacts/screenshots --live-cloud
```

The live check creates and deletes an isolated test account; it never reads existing user profiles.
