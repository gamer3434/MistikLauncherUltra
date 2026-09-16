# Pencere ve üst çubuk / Window and toolbar

**Gerekçe / Reasoning:** Kullanıcının RedX ekran görüntüleri koyu, sakin yüzeyler ve renkli seçili öğeler gösteriyor. Üst çubuk, yan menü ve başlatma alanı aynı temayı kullanmalı; kişiselleştirme temel pencere işlevlerini korumalı.

**Uygulama / Implementation:**

- Ayarlar → Üst kısayol çubuğu: Ana panel, Sürümler, Mod merkezi, Karakter, Sunucular, Ayarlar seçeneklerini ayrı ayrı açıp kapatın. Tümünü kapatmak çubuğu gizler. / Settings → Top shortcut bar: toggle six shortcuts individually; disable all to hide it.
- Ayarlar → Pencere düğmesi stili: macOS, Windows, Minimal, Neon, Retro, Cam, Terminal, Hap, Cyberpunk, Balon, Aurora, Elmas, Neon çerçeve, Buzlu cam, Samuray, Hologram. Seçimler anında uygulanır ve kaydedilir. / Settings → Window button style: choose among 16 immediately applied, persisted appearances.
- Windows seçeneği standart sistem başlığını kullanır. Diğerleri WPF `WindowChrome` üzerinden sürükleme ve yeniden boyutlandırmayı korur; özel düğmeler küçültür, büyütür/geri yükler ve pencereyi kapatır. / Windows uses the native caption; custom appearances retain dragging and resizing through WPF WindowChrome.
- Yan menü ve üst çubuk Segoe MDL2 çizgi ikonlarını kullanır; aktif sayfa tema dolgusu, vurgu yazısı ve üst çubukta ince sınırla belirgindir. Klavye odağı ve iki dilde erişilebilir adlar bulunur. / Both navigation areas use consistent line icons, themed active states, visible keyboard focus and bilingual accessible names.
- 20 temada ana aksiyon yazısı siyah/beyaz olarak göreli parlaklığa göre seçilir ve en az 4.5:1 kontrast doğrulanır. / Main action text meets 4.5:1 in all 20 themes.

Kod / Code: `MistikLauncher/WindowAppearance.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `Pages/ModernPages.cs`, `ColorThemes.cs`, `Core.cs`, `Locales/tr.json`, `Locales/en.json`.

**Sonuç / Conclusion:** Sürüm / version `6.0.0-preview.3`; korumalı pakette / protected package **117 checks passed**, including 16 style persistence/render checks, minimize/maximize/restore, shortcut selection/persistence/navigation/hiding, contrast, languages and existing update/Forge checks. Actual Minecraft gameplay remains unverified. See `screenshots/home-tr.png`, `home-en.png`, `settings-tr.png`, `settings-en.png` for previews.
