# 🚀 Mistik Launcher Ultra

Mistik Launcher Ultra, modern C# ve WPF (Windows Presentation Foundation) kullanılarak geliştirilmiş, **Riot Vanguard** anti-hile sistemleriyle %100 uyumlu, yüksek performanslı ve premium tasarıma sahip yeni nesil bir Minecraft başlatıcısıdır (Launcher).

---

## 🌟 Öne Çıkan Özellikler

* **🛡️ Vanguard Uyumlu Kurulum Sistemi:** Kısayol oluşturma ve dosya işlemlerinde hiçbir PowerShell betiği kullanılmaz, doğrudan yerel C# kütüphaneleri çalışır. Riot Vanguard anti-hile sistemi tarafından hile/virüs olarak algılanmaz.
* **🧹 Sahte Sürümlerden Arındırılmış Yapı:** Listede kafa karıştırıcı veya indirilemeyen geçici sürümler (1.26.x vb.) yer almaz. Sadece oynanabilir gerçek Mojang ve Fabric sürümlerini gösterir.
* **🔄 Dinamik Sürüm Güncelleme:** Arayüze entegre edilmiş **Yenile** butonu ile Mojang ve Fabric API'lerine anında bağlanıp en son çıkan sürümleri (örneğin en yeni Fabric sürümlerini) anında listeye çeker.
* **🗑️ Güvenli Kaldırma (Anti-Heuristic):** Uygulamayı kaldırmak istediğinde, Windows güvenlik duvarlarını ve anti-virüs sistemlerini tetikleyecek arka plan komut istemleri (`cmd.exe`) çalıştırmak yerine kendini geçici `%TEMP%` dizinine taşıyıp oradan güvenle ve tamamen kaldırır.
* **🔑 Dijital Sertifikalı & İmzalı Güvenlik:** Tüm `.exe` dosyaları geçerli bir yerel dijital sertifika ile imzalanmıştır, böylece Windows SmartScreen engellerine takılmaz.

---

## 🛠️ Kurulum Kılavuzu (Nasıl Yüklenir?)

Mistik Launcher Ultra'nın kurulumu son derece basittir:

1. **İndirme:** GitHub deposunda bulunan `MistikLauncherUltra.exe` veya `Mistik Ultra Kurulum Paketi.exe` dosyasını indir.
2. **Kurulum Sihirbazı:** İndirdiğin `.exe` dosyasına çift tıkla. Karşına modern, özel tasarımlı **Mistik Client Yükleme Sihirbazı** çıkacaktır.
3. **Yükle Butonu:** Sihirbaz üzerindeki **Yükle** butonuna tıkla. Sistem arka planda:
   * Gerekli dosyaları hazırlayacak,
   * Masaüstüne ve Başlat Menüsüne güvenli kısayollar yerleştirecek,
   * Denetim Masası "Program Ekle veya Kaldır" ekranına uygulamayı başarıyla kaydedecektir.
4. **Oyuna Giriş:** Kurulum bittikten sonra masaüstündeki kısayolu kullanarak Launcher'ı açabilir ve dilediğin sürümle Minecraft keyfine başlayabilirsin!

---

## 🎮 Kullanım Rehberi

### Sürümleri ve Modları Listeleme
* Sürüm listesinin en güncel Mojang ve Fabric sürümlerini içermesi için sürüm başlığının hemen yanındaki **🔄 Yenile** butonuna tıklayabilirsin. Bu işlem listeyi anında günceller.

### Programı Kaldırma (Uninstaller)
* Uygulamayı bilgisayarından tamamen silmek istersen:
  * Windows Arama çubuğuna "Program Ekle veya Kaldır" yaz.
  * Listeden **Mistik Launcher Ultra** programını bul ve **Kaldır** butonuna bas.
  * Karşına çıkan özel kırmızı/siyah temalı arayüzden tek tıkla tüm sistemi güvenle kaldırabilirsin.

---

## ❓ Sıkça Sorulan Sorular (S.S.S)

### 📌 "MistikLauncherCS" klasörünün programın çalışması için bilgisayarımda kalması zorunlu mu?
**Hayır, kesinlikle gerekli değildir.** 
`MistikLauncherCS` klasörü, programın **kaynak kodlarını (Source Code)** barındıran geliştirici klasörüdür. 
Programın çalışması için tek başına `MistikLauncherUltra.exe` (kurulu haliyle masaüstündeki kısayol) yeterlidir. Kaynak kod klasörünü silsen dahi kurduğun Launcher sorunsuz çalışmaya devam eder. Bu klasör sadece geliştirme yapmak ve projeyi yedeklemek içindir.

### 📌 Neden masaüstündeki tüm dosyaları depoya yüklemiyoruz?
Çünkü masaüstünde tarayıcı kısayolları, oyunlar, geçici loglar, mouse sürücüleri ve diğer program dosyaları gibi Minecraft ile hiçbir alakası olmayan kişisel dosyalar yer alıyor. Git deposuna sadece projenin temiz kaynak kodlarını ve derlenmiş Launcher dosyalarını yükleyerek profesyonel bir yazılım deposu oluşturduk. Böylece GitHub sayfan tertemiz ve sadece bu projeye özel kalmış oldu.
