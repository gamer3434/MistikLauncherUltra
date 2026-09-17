# Hata denetimi ve koruma / Bug audit and protection — preview.6

## Gerekçe / Reasoning

Sürüm değiştirme, mod indirme, skin geri yükleme ve oturum açma yollarında dosya kaybı, yanlış yükleyici algısı ve güven sınırı hataları incelendi. Mevcut HttpClient/JSON, atomik dosya işlemleri, Windows DPAPI ve mevcut Obfuscar kullanıldı; yeni bağımlılık eklenmedi.

The audit focused on version switching, mod installation, skin restoration and account sessions. Existing libraries and native Windows features provide the fixes without new dependencies. This is a scoped audit, not a guarantee that every legacy application path is bug-free.

## Uygulama / Implementation

| Sorun / Problem | Düzeltme / Correction | Kod / Code |
|---|---|---|
| Failed pool moves followed by deletion of remaining active mods | Transactional moves, reverse-order rollback, no overwrite; failed sync keeps previous version state and blocks launch | `ModFiles.cs`, `MainWindow.xaml.cs` |
| Switching from Vanilla could discard unmanaged mods | Archive the previous set instead of clearing it | `ModFiles.cs` |
| Migration deleted the target version's existing mod set | Preserve it in a unique `mods_pool/migration-backup-*` folder; abort on failed synchronization | `Pages/VersionAndModPages.cs` |
| Download filenames could escape the mod directory | Reject paths, invalid filenames and non-JAR names before writing | `ModFiles.cs` |
| Mod downloads lacked integrity checks and wrote directly over files | Official HTTPS CDN, 128 MB response bound, required SHA-512, staged atomic replacement and `.backups` copies | `ModFiles.cs`, `Pages/VersionAndModPages.cs` |
| Updating a disabled mod could reactivate it | Preserve `.jar.disabled`; reject simultaneous active/disabled conflicts | `ModFiles.cs` |
| NeoForge contained the substring Forge and was misclassified | Check NeoForge first, distinguish its libraries and keep its inherited profiles selectable | `GameProfiles.cs`, `MainWindow.xaml.cs` |
| Multi-loader JARs were classified from only the first metadata file | Ambiguous multi-loader metadata remains active rather than being guessed | `MainWindow.xaml.cs` |
| Version-number extraction lost open-ended/exclusive operators | Heuristic version checks are advisory; do not auto-suspend from that incomplete result | `MainWindow.xaml.cs` |
| Definite Forge/NeoForge mismatch was not suspended | Preserve incompatible JARs in the pool, with safe pool names and no overwrite | `MainWindow.xaml.cs` |
| Unbounded ZIP metadata reads | Bound loader metadata entries to 256 KB before decoding | `MainWindow.xaml.cs` |
| Invalid cloud settings could overwrite the skin before configuration parsing failed | Validate the complete profile first; bound base64 before decoding and replace afterward | `CloudProfiles.cs` |
| Incomplete encrypted sessions appeared signed in | Validate UID, token fields and expiration before accepting or persisting a session | `CloudProfiles.cs` |
| Failed session writes could leave incorrect in-memory state | Persist first, then update state; clear temporary plaintext byte buffers | `CloudProfiles.cs` |
| Configuration version could contain path traversal | Reuse `GameProfiles.SafeId` during central normalization | `Core.cs` |
| Skin metadata used plaintext HTTP and arbitrary texture URLs | HTTPS, trusted hosts, bounded downloads, safe usernames and PNG validation | `MainWindow.xaml.cs` |
| Local labels used an obsolete hardcoded version | Read the assembly's actual version | `Core.cs` |
| Client JVM arguments forced server authentication properties off | Remove those properties | `MainWindow.xaml.cs` |

Modrinth file hashes and download metadata follow the [official version API](https://docs.modrinth.com/api/operations/getversion).

### Koruma / Protection

Dağıtım DLL'sinde özel adlar ve metinler mevcut Obfuscar ile gizlenir. Oturum belirteçleri Windows hesabına bağlı DPAPI ile gerçekten şifrelenir; bozulmuş şifreli veri reddedilir. Parolalar saklanmaz. PDB ve özel eşleme dosyaları dağıtım paketine girmez. Manifest her dağıtım dosyasının SHA-256 özetini içerir.

The distributed DLL uses private-name and string obfuscation. Session tokens use current-user DPAPI encryption; tampered ciphertext fails decryption. Passwords are not persisted. Debug symbols and private maps are excluded, and the package manifest covers all distributed files. Source in the public GitHub repository remains readable. Obfuscation raises reverse-engineering cost; it is not unbreakable encryption or a malware guarantee.

## Doğrulama ve sonuç / Verification and conclusion

- Protected package: **159 checks passed**, including its file hashes and real updater process handoff.
- Protected live Firebase plus official Modrinth download: **167 checks passed**. The downloaded mod was verified and installed in an isolated test folder without execution; temporary Firebase users/profiles were removed.
- Both locales have matching, nonempty resource keys; runtime language switching and all 20 theme contrasts remain verified.
- Release build: zero warnings/errors. Version: `6.0.0-preview.6`.

```powershell
./scripts/Build-Protected.ps1
dotnet run --project Validation -c Release -- artifacts/screenshots --live-cloud --live-mod
```

Gerçek Minecraft/Forge oyunu, her modun bağımlılık çözümü, tüm eski sayfaların çevirisi ve çok monitörlü görev çubuğu davranışı henüz doğrulanmadı. Sürüm şartları konusunda tam bir parser eklenmedi; uyum kontrolleri gerektiğinde Modrinth metadata ve modun belgelerinden yapılmalıdır. Bulut cihazlar arası birleştirme yapmaz; son başarılı yükleme geçerlidir.

Actual Minecraft/Forge gameplay, every dependency resolution path, full legacy-page translation and multi-monitor taskbar behavior remain unverified. Complete version-constraint parsing and cross-device conflict merging were not added; cloud synchronization retains the last successful upload.
