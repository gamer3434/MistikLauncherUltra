# Modları silmeden açma/kapatma / Non-destructive mod toggles

**Gerekçe / Reasoning:** Modu silmeden devre dışı bırakabilmek deneme, hata ayıklama ve mod setlerini yönetmeyi kolaylaştırır. Pencere stili seçimi de kullanıcının örneğindeki gibi gerçek görünümleri gösteren kartlarla yapılmalıdır.

**Uygulama / Implementation:**

- **Mod merkezi → Kurulu Modlar:** “Etkinleştir” veya “Devre dışı bırak” düğmesine basın. Dosya `.jar` ↔ `.jar.disabled` olarak yeniden adlandırılır; içerik korunur. Kapalı mod listede kalır ve durumu görünür. / **Mod center → Installed mods:** use Enable or Disable. Renaming preserves bytes and disabled entries remain visible.
- Değişiklik launcher listesinde hemen görünür. Minecraft/Forge/Fabric modları çalışırken güvenle boşaltılamadığı için oyun bir sonraki açılışta yeni durumu kullanır. Gerekli bağımlılıkları olan modları birlikte kontrol edin. / Launcher state updates immediately; restart the game to apply the load set. Check dependencies together.
- Aynı adlı hedef dosya varsa hiçbirinin üzerine yazılmaz. Kilitli dosyada hata gösterilir, dosya korunur. Mod havuzları `.jar.disabled` dosyalarını da taşıyarak sürüme dönüldüğünde kapalı durumu korur. / Collisions and locked files fail without overwriting data; per-version pools retain disabled state.
- **Ayarlar → Pencere düğmesi stili:** 16 kart; önizleme, kısa açıklama, seçili çerçeve ve onay işareti. Kart çizimleri gerçek düğmelerle aynı `WindowButtonPreview` metodunu kullanır. / **Settings → Window button style:** 16 preview cards share rendering with actual caption controls.

Kod / Code: `MistikLauncher/ModFiles.cs`, `Pages/VersionAndModPages.cs`, `MainWindow.xaml.cs`, `WindowAppearance.cs`, `Pages/ModernPages.cs`, locale catalogs. Verification: `Validation/ModToggleTests.cs` and UI/version-pool checks in `Validation/Program.cs`.

**Sonuç / Conclusion:** `6.0.0-preview.4`; **127 protected-package checks**, including byte preservation, name collisions, locks, directory validation, version-pool round trips and actual Enable/Disable button events. Turkish/English card screenshots: `screenshots/window-styles-tr.png`, `window-styles-en.png`. Actual Minecraft gameplay remains unverified.
