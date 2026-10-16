## Türkçe

- Güncelleme yardımcısı geçici klasöründe başlatılır ve eski launcher’lardan çağrılsa bile çalışma klasörünü kurulumdan ayırır. Ana işlemin kimlik doğrulaması ve kapanmasını bekleme korunur.
- SHA256 değeri aynı olan program dosyaları yeniden yazılmaz. Değişecek mevcut dosyaların kullanım kilitleri ve klasör çakışmaları ilk değişiklikten önce denetlenir.
- Bütün yeni dosyalar ve eski dosya yedekleri ilk değiştirmeden önce hazırlanır. Salt okunur kaynak niteliği yeni dosyalara taşınmaz.
- Geri alma, kullanımda olan DLL’nin içeriğini doğrudan ezmek yerine bitişik geçici dosya ve Windows dosya değiştirme işlemini kullanır. Tam geri alınamayan işlemlerin kurtarma yedekleri korunur.
- Hata penceresinde uzun istisna yığını yerine kısa yönlendirme ve yerel `update-error.txt` yolu gösterilir. Başarısız geri alma halinde dosyaların tamamen korunduğu yanlış biçimde söylenmez.

Kilitler işlem sırasında sonradan oluşabilir; bu yüzden geri alma koruması devam eder. Yeni yardımcı, test amaçlı launcher ile sınanır; gerçek oyun veya kullanıcı hesabı başlatılmaz.

858 normal ve 870 paket kontrolü ile 48 web testi geçti. Gerçek yardımcı testinde eski kurulum çalışma klasöründen başlatma, ana işlemin kapanması, dosya değiştirme ve yeniden başlatma doğrulandı. Değişen/değişmeyen kilitli dosyalar, klasör çakışması ve kilit altında geri alma ayrıca sınandı.

## English

- The update helper starts in its private staging directory and resets its working directory even for older callers. Parent identity verification and the exit wait remain enforced.
- Identical SHA256 files are skipped. Changed installed files are checked for locks and directory collisions before mutation.
- Every replacement and original backup is staged before the first commit. Read-only source attributes are not inherited.
- Rollback uses adjacent temporary files and Windows replacement rather than overwriting a loaded DLL in place. Incomplete recovery retains its backup.
- Failure dialogs show concise guidance and a local `update-error.txt` path instead of a huge exception list. They no longer falsely claim that every file was preserved after an incomplete rollback.

Locks can still appear after preflight, so rollback remains necessary. Helper execution is tested with an isolated launcher fixture; no real game or user account is launched.

858 ordinary and 870 packaged checks plus 48 web tests passed. The real helper fixture verified inherited installation working directory, parent exit, replacement and restart. Separate tests cover changed/unchanged locked files, directory collisions and restoration under delete-sharing locks.
