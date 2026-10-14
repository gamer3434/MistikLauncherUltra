# Web verisi ve hesaplama kapsamı

2026-10-14 kontrolü: mevcut Firebase projesinde `launcherMetrics` dalı boştu. Eski `stats`, `global_crashes` ve `activeSessions` dalları yeni katılım ölçümleriyle birleştirilmez. Veritabanında veya kullanıcıların paylaşım tercihinde değişiklik yapılmadı.

- **Katılan kurulum:** geçerli `firstSeen`, `lastSeen`, `version` alanları olan anonim kayıt. Kişi veya tüm indirmeler sayılmaz.
- **Şimdi aktif:** geçerli kurulum kaydıyla eşleşen, son beş dakikada sinyal gönderen oturum. Launcher kapanınca temizlenemeyen oturum bu süre sonunda etkinlikten düşer.
- **24 saat / 7 gün / 30 gün:** kurulumun son bildirimi kayan zaman aralığındadır. Geçmiş günlerin ayrı kullanım grafiği oluşturulamaz.
- **Yeni katılan:** ilk paylaşım bildirimi bu aralıktadır. Programın ilk kurulum tarihi değildir.
- **Hata kayıt grubu:** bir anonim kurulum ve hata kategorisinin birikimli sayacı. `version` son bildirimin sürümüdür; tüm eski olaylar o sürüme atfedilemez.
- **24 saatte güncellenen hata kaydı:** son bildirimi bu aralıkta olan gruplar. O aralıktaki gerçek olay toplamı bilinmez.
- **Dosya indirmeleri:** yalnızca son GitHub yayınındaki sürümle eşleşen program paketleri. SHA256SUMS hariçtir; tekrar indirmeler dahildir. Kullanıcı veya kurulum sayısına dönüştürülemez.

Panel negatif, kesirli veya gelecekteki geçersiz zamanları, bozuk sürümleri, sınır dışı sayaçları ve kurulumla eşleşmeyen oturumları saymaz. Sunucu kurallarıyla uyumlu 60 saniyelik saat toleransı uygulanır. Geçersiz kayıtların sayısı görünür.

Hata filtreleri tüm geçerli kayıtlar üzerinde çalışır; görünüm bundan sonra en yeni 50 kayıtla sınırlandırılır. Kurulum tablosu en son 100 kaydı gösterir. Sıra etiketleri görünüm içindir; ham kimlik gösterilmez. Kaynak veriler değiştirilmez.

Yeni kayıt yoksa panel `—` ve açıklama gösterir. Bu, toplam kullanıcı sayısının sıfır olduğu anlamına gelmez. İstatistik paylaşımı launcher ayarlarında isteğe bağlıdır.
