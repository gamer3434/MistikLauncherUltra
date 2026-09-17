# PC doğrulaması / PC validation — 6.0.0-preview.7

## 1. Audit / Hata denetimi

**Reasoning / Gerekçe:** Test real launches without touching existing worlds or server settings. / Mevcut dünyalara ve sunucu ayarlarına dokunmadan gerçek başlatmaları doğrula.

**Implementation / Uygulama:** Separate `pc-test-20260917` data and runtime directories. Fixed incorrect Java 25 requirement for 1.21.11 using official inherited Java metadata; corrected release-date sorting and stale home selection; removed the remaining recursive forced `online-mode=false` mutation. Native monitor work-area bounds fix maximized taskbar overlap. Files: `GameProfiles.cs`, `MainWindow.xaml.cs`, `Pages/VersionAndModPages.cs`, `WindowAppearance.cs`.

**Conclusion / Sonuç:** Vanilla 1.21.11 and Forge 61.2.0 reached the menu, entered a local world, saved and exited with code 0. Launcher returned. / Her iki sürüm gerçek dünyaya girdi, kaydetti, 0 koduyla kapandı; launcher geri açıldı.

## 2. Features / Özellikler

**Reasoning / Gerekçe:** Startup failures often disappear before Minecraft writes a crash report. / Açılış hataları Minecraft rapor yazamadan kaybolabilir.

**Implementation / Uygulama:** Drain both Java output streams, retain bounded diagnostic output, prevent another launch while a game runs, restore launcher on nonzero exit, and show code, advice, fresh report paths and explicitly implicated mod filenames. `CrashDiagnostics.cs` and launch monitoring replace silent background-only reporting. Codes: `MLU-LAUNCH`, `MLU-EXIT`, `MLU-JAVA`, `MLU-CLASSPATH`, `MLU-MEMORY`, `MLU-MOD-MIXIN`, `MLU-MOD-DEPENDENCY`, `MLU-FILE`. Report copying is local; no automatic log upload.

**Conclusion / Sonuç:** Controlled real Java missing-main-class failure displayed exit 1 and captured error text. This is a controlled startup test, not a reproduced in-world Minecraft crash. A mod is listed only when its JAR filename appears in an error line; mod-ID-only attribution remains unsupported. / Kontrollü gerçek Java açılış hatası doğrulandı; yalnızca çıkış koduna bakarak bir mod suçlanmaz.

## 3. Languages / Diller

**Reasoning / Gerekçe:** Dynamic selections must retain WPF bindings during translation. / Çeviri sırasında dinamik seçimlerin WPF bağlantıları korunmalı.

**Implementation / Uygulama:** `Localization.TranslateTree` skips generated ComboBox content and preserves dependency-property bindings with `SetCurrentValue`. Turkish/English lighting labels, validation and diagnostics; paired locale status updates. Runtime language persistence and RGB edits are tested in both languages. Example: **Çıkış düğmesi aydınlatması / Close button lighting**, **Gökkuşağı / Rainbow**, **Kapalı / Off**.

**Conclusion / Sonuç:** New controls work in both languages. Full legacy-page localization remains incomplete. / Yeni kontroller iki dilde çalışır; eski sayfaların tam çevirisi henüz bitmedi.

## 4. Protection and signing / Koruma ve imzalama

**Reasoning / Gerekçe:** Reuse Windows signing and DPAPI without changing system trust or antivirus settings. / Sistem güvenini veya antivirüs ayarlarını değiştirmeden yerleşik Windows araçlarını kullan.

**Implementation / Uygulama:** Existing private-name/string obfuscation and encrypted sessions retained. `scripts/Sign-Release.ps1` signs own launcher EXE/DLL and updater EXE using SHA-256; signing precedes final package hashes. Only the public certificate and signing metadata ship. Local certificate: `CN=MistikLauncherLocalTestSigning`, RSA 3072, nonexportable private key. No trusted-root installation or timestamp. Tampered test copies of all three files returned `HashMismatch`.

**Conclusion / Sonuç:** Free local test signatures are present, but are not publicly trusted. PowerShell reports the untrusted root as `UnknownError`; this is explicitly recorded, not described as trusted signing. SmartScreen/AV warnings are not guaranteed to disappear. Obfuscation is reversible protection; public source remains readable. / Ücretsiz yerel test imzası genel yayıncı güveni sağlamaz; antivirüs uyarısızlık garantisi verilmez.

## 5. UX / Görünüm

**Reasoning / Gerekçe:** Reserve caption glow for close while keeping the requested style cards. / İstenen stil kartlarını koruyarak yalnızca çıkış düğmesini aydınlat.

**Implementation / Uygulama:** Circular close ring with Theme, custom RGB hex, rotating Rainbow and Off. Valid RGB edits apply immediately and persist; invalid RGB cannot prevent switching to Off. Minimize/maximize effects removed in all 16 styles. Rainbow respects Windows animation preference. Small control row under style cards; 20 theme colors retained. New preferences sync under owner-only Firebase rules. Screenshot: [PLACEHOLDER: native desktop screenshot is not archived]. Automated WPF renders: `artifacts/screenshots`.

**Conclusion / Sonuç:** Automated tests verify exclusive close glow, selected-value updates, immediate RGB application and Off in both languages. / Yalnızca çıkış aydınlatması, doğru seçili değer, anlık RGB ve Kapalı iki dilde doğrulandı.

## 6. Repository / Depo

**Reasoning / Gerekçe:** Publish reviewable source without calling unfinished legacy work stable. / Tamamlanmamış eski özellikleri kararlı ilan etmeden incelenebilir kaynak yayımla.

**Implementation / Uygulama:** Version `6.0.0-preview.7`; protected signed local portable ZIP, validation and signing scripts, Firebase rules and this report on `codex/modernization-preview`. Stable updater intentionally excludes prereleases.

**Conclusion / Sonuç:** Preview delivery; default branch/stable replacement is not complete. / Önizleme teslimidir; varsayılan dalın kararlı yeni sürümle değiştirilmesi tamamlanmadı.

## 7. Documentation and limits / Belgelendirme ve sınırlar

**Reasoning / Gerekçe:** Record real results rather than treating UI opening as full feature verification. / Arayüzün açılmasını tüm özelliklerin doğrulanması olarak sunma.

**Implementation / Uygulama:** Run the portable EXE with all package files together. Settings → Window button style → Close button lighting; choose RGB and enter `#RRGGBB` (e.g. `#00CCFF`), or Rainbow/Off. Use the top-right language selector. / Paketi tamamen çıkartın; Ayarlar → Pencere düğmesi stili → Çıkış düğmesi aydınlatması bölümünü kullanın. Dil sağ üstten değiştirilir.

Real Auto-MCS update installed v2.3.9 and launched. Its new-server wizard displayed an error. The isolated generated Vanilla server was separately started on localhost:25587, reached `Done`, accepted `list`/`stop`, saved and exited 0. Existing servers remained untouched. / Auto-MCS güncellemesi çalıştı; oluşturma sihirbazı hata verdi. Ayrı test sunucusu localhost üzerinde normal çalışıp kapandı.

**Conclusion / Sonuç:** Protected package passes **192 checks**, including actual manifest hashes and real update-helper lifecycle. Live Firebase/Modrinth validation passes **199 checks**. Microsoft/Ely.by login, public tunnels, every mod combination, multiple-monitor DPI behavior and destructive system optimizations were not tested. The third-party Auto-MCS wizard failure and full legacy localization remain open. / Tüm özellikler eksiksiz doğrulandı veya kusursuz kararlı sürüm hazır iddiası yoktur.
