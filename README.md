# 🚀 Mistik Launcher

[Türkçe](#türkçe) | [English](#english)

---

## Türkçe

Mistik Launcher, modern C# ve WPF (Windows Presentation Foundation) kullanılarak geliştirilmiş, **Riot Vanguard** anti-hile sistemleriyle %100 uyumlu, yüksek performanslı ve premium tasarıma sahip yeni nesil bir Minecraft başlatıcısıdır (Launcher).

### 🌟 Öne Çıkan Özellikler

* **🛡️ Vanguard Uyumlu Kurulum Sistemi:** Kısayol oluşturma ve dosya işlemlerinde hiçbir PowerShell betiği kullanılmaz, doğrudan yerel C# kütüphaneleri çalışır. Riot Vanguard anti-hile sistemi tarafından hile/virüs olarak algılanmaz.
* **🧹 Sahte Sürümlerden Arındırılmış Yapı:** Listede kafa karıştırıcı veya indirilemeyen geçici sürümler (1.26.x vb.) yer almaz. Sadece oynanabilir gerçek Mojang ve Fabric sürümlerini gösterir.
* **🔄 Dinamik Sürüm Güncelleme:** Arayüze entegre edilmiş **Yenile** butonu ile Mojang ve Fabric API'lerine anında bağlanıp en son çıkan sürümleri (örneğin en yeni Fabric sürümlerini) anında listeye çeker.
* **🗑️ Güvenli Kaldırma (Anti-Heuristic):** Uygulamayı kaldırmak istediğinde, Windows güvenlik duvarlarını ve anti-virüs sistemlerini tetikleyecek arka plan komut istemleri (`cmd.exe`) çalıştırmak yerine kendini geçici `%TEMP%` dizinine taşıyıp oradan güvenle ve tamamen kaldırır.
* **🔑 Dijital Sertifikalı & İmzalı Güvenlik:** Tüm `.exe` dosyaları geçerli bir yerel dijital sertifika ile imzalanmıştır, böylece Windows SmartScreen engellerine takılmaz.

### 🛠️ Kurulum Kılavuzu (Nasıl Yüklenir?)

Mistik Launcher'ın kurulumu son derece basittir:

1. **İndirme:** GitHub deposunda bulunan **`MistikLauncher.zip`** dosyasını indir ve içindeki dosyayı klasöre çıkart.
2. **Kurulum Sihirbazı:** Çıkarttığın `MistikLauncher.exe` dosyasına çift tıkla. Karşına modern, özel tasarımlı **Mistik Launcher Yükleme Sihirbazı** çıkacaktır.
3. **Yükle Butonu:** Sihirbaz üzerindeki **Yükle** butonuna tıkla. Sistem arka planda:
   * Gerekli dosyaları hazırlayacak,
   * Masaüstüne ve Başlat Menüsüne güvenli kısayollar (`Mistik Launcher.lnk`) yerleştirecek,
   * Denetim Masası "Program Ekle veya Kaldır" ekranına uygulamayı başarıyla kaydedecektir.
4. **Oyuna Giriş:** Kurulum bittikten sonra masaüstündeki kısayolu kullanarak Launcher'ı açabilir ve dilediğin sürümle Minecraft keyfine başlayabilirsin!

### 🎮 Kullanım Rehberi

* **Sürüm Listeleme:** Sürüm listesinin en güncel Mojang ve Fabric sürümlerini içermesi için sürüm başlığının hemen yanındaki **🔄 Yenile** butonuna tıklayabilirsin. Bu işlem listeyi anında günceller.
* **Programı Kaldırma (Uninstaller):** Uygulamayı bilgisayarından tamamen silmek istersen, Windows "Program Ekle veya Kaldır" ekranından **Mistik Launcher** programını bulup **Kaldır** butonuna basman yeterlidir.

### ❓ Sıkça Sorulan Sorular (S.S.S)

* **"MistikLauncherCS" klasörünün programın çalışması için bilgisayarımda kalması zorunlu mu?**
  **Hayır, kesinlikle gerekli değildir.** `MistikLauncherCS` klasörü geliştirici kaynak kodlarıdır. Çalışan sürüm için tek başına kurulu `MistikLauncher.exe` yeterlidir.
* **Neden masaüstündeki tüm dosyaları depoya yüklemiyoruz?**
  Masaüstün kişisel oyunların ve mouse/ses sürücülerinle doludur. GitHub sayfana sadece bu projeye özel tertemiz kaynak kodlarını ve derlenmiş Launcher dosyalarını yükleyerek profesyonel bir depo oluşturduk.

---

## English

Mistik Launcher is a next-generation Minecraft launcher featuring a high-performance, premium design built using modern C# and WPF (Windows Presentation Foundation). It is designed to be 100% compliant with **Riot Vanguard** anti-cheat systems.

### 🌟 Key Features

* **🛡️ Vanguard-Compliant Installation:** Uses native C# COM libraries for creating shortcuts and registry management instead of PowerShell scripts. This completely bypasses any heuristic warnings or anti-cheat flags from Riot Vanguard.
* **🧹 Clean Version Fetching:** Devoid of any fake, non-existent, or buggy versions (e.g., 1.26.x). It only displays authentic, playable Mojang and Fabric versions.
* **🔄 Dynamic Version Updates:** Features a **Refresh** button integrated into the UI to instantly query Mojang and Fabric APIs, refreshing the version list on-the-fly.
* **🗑️ Safe Uninstallation (Anti-Heuristic):** To prevent false-positives from anti-virus programs that flag silent CMD scripts, the uninstaller copies itself to the Windows `%TEMP%` directory, fully wipes the install directory, and cleanly terminates.
* **🔑 Digitally Signed Executables:** All `.exe` files are digitally signed with a local developer certificate, preventing Windows SmartScreen blocks.

### 🛠️ Installation Guide (How to Install)

Mistik Launcher installation is straightforward:

1. **Download:** Download **`MistikLauncher.zip`** from this repository and extract it.
2. **Setup Wizard:** Double-click the extracted `MistikLauncher.exe` file. You will be greeted by the custom-designed, sleek **Mistik Launcher Setup Wizard**.
3. **Install:** Click the **Install** button. The installer will:
   * Deploy all target application files,
   * Safely create desktop and Start Menu shortcuts (`Mistik Launcher.lnk`),
   * Register the application under Windows Control Panel (Add/Remove Programs).
4. **Play:** Use the desktop shortcut to start the launcher, select your preferred version, and jump into Minecraft!

### 🎮 Usage Guide

* **Updating Versions:** Click the **🔄 Refresh** button next to the version header to dynamically query Mojang and Fabric servers for the latest game versions.
* **Uninstalling:** Search for "Add or Remove Programs" in Windows, find **Mistik Launcher**, and click **Uninstall**. The custom red/black UI will guide you through a clean wipe.

### ❓ FAQ

* **Is the "MistikLauncherCS" folder required to run the launcher?**
  **No, absolutely not.** The source code directory is only for development and backup. The compiled and installed `MistikLauncher.exe` is completely self-contained and standalone.
* **Why aren't we committing the whole desktop?**
  The desktop contains personal shortcuts, video files, games, and unrelated drivers. Pushing it would clutter your repo. We only track the dedicated launcher source code and its official compiled executables.
