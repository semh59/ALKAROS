# ASVS 5.0.0 bölüm haritası

**Kaynak kayıt:** V0-SEC-001 (`security-verification-baseline.md`, onaylı). Bu belge o kaydın kararını (ASVS Level 3, STRIDE + DREAD) değiştirmez; yalnız kategori başına yazılan numaraları gerçek ASVS 5.0.0 bölümlerine eşler.

## Neden gerekli

Onaylı kayıttaki kategori aralıkları (ör. Authentication `2.1-2.19`, Session `3.1-3.14`, Access Control `4.1-4.11`, Cryptography `7.1-7.13`) ASVS **4.0** bölüm numaralarına uyar. ASVS 5.0.0'da bölümler yeniden düzenlenmiştir (Authentication V6, Session V7, Authorization V8, Cryptography V11 gibi) ve istek kimliği `v<sürüm>-<bölüm>.<kesim>.<madde>` biçimindedir; örnek `v5.0.0-6.2.1`. Bağımsız değerlendirme (V20-SEC-001) kapsamı eski numaralarla çıkarırsa yanlış maddeleri doğrular.

## Eşleme

| Onaylı kayıttaki kategori | ASVS 5.0.0 bölümü | Kesimler (başlıklar) |
| --- | --- | --- |
| Architecture | V15 Secure Coding and Architecture (4.0'daki V1 Architecture bölümü 5.0'da kaldırıldı, kesimleri ilgili bölümlere dağıtıldı; eşleme birebir değil, en yakın bölümdür) | 15.1 Documentation, 15.2 Security Architecture and Dependencies, 15.3 Defensive Coding, 15.4 Safe Concurrency |
| Authentication | V6 Authentication | 6.1 Documentation, 6.2 Password Security, 6.3 General Authentication Security, 6.4 Factor Lifecycle and Recovery, 6.5 General MFA, 6.6 Out-of-Band, 6.7 Cryptographic Authentication, 6.8 Identity Provider |
| Session Management | V7 Session Management | 7.1 Documentation, 7.2 Fundamental Session Security, 7.3 Timeout, 7.4 Termination, 7.5 Defenses Against Session Abuse, 7.6 Federated Re-authentication |
| Access Control | V8 Authorization | 8.1 Documentation, 8.2 General Authorization Design, 8.3 Operation Level Authorization, 8.4 Other Considerations |
| Input Validation | V2 Validation and Business Logic | 2.1 Documentation, 2.2 Input Validation |
| Output Encoding | V1 Encoding and Sanitization | 1.1 Architecture, 1.2 Injection Prevention, 1.3 Sanitization, 1.4 Memory/String/Unmanaged Code, 1.5 Safe Deserialization |
| Cryptography | V11 Cryptography | 11.1 Inventory and Documentation, 11.2 Secure Implementation, 11.3 Encryption Algorithms, 11.4 Hashing, 11.5 Random Values, 11.6 Public Key, 11.7 In-Use Data |
| Error Handling | V16 Security Logging and Error Handling | 16.5 Error Handling |
| Logging | V16 Security Logging and Error Handling | 16.1 Documentation, 16.2 General Logging, 16.3 Security Events, 16.4 Log Protection |
| Data Protection | V14 Data Protection | 14.1 Documentation, 14.2 General Data Protection, 14.3 Client-side Data Protection |
| Communications | V12 Secure Communication | 12.1 General TLS, 12.2 HTTPS for External Facing Services, 12.3 Service to Service |
| HTTP Security | V3 Web Frontend Security, V4 API and Web Service | 3.2 Unintended Content Interpretation, 3.3 Cookie Setup, 3.4 Security Headers, 3.5 Origin Separation, 3.6 External Resource Integrity, 3.7 Other; 4.1 Generic Web Service Security, 4.2 HTTP Message Structure Validation |
| Business Logic | V2 Validation and Business Logic | 2.3 Business Logic Security, 2.4 Anti-automation |
| Supply Chain | V15 Secure Coding and Architecture | 15.2 Security Architecture and Dependencies |
| Operational | V13 Configuration, V16 Security Logging and Error Handling | 13.1 Documentation, 13.2 Backend Communication, 13.3 Secret Management, 13.4 Unintended Information Leakage; 16.3 Security Events |

**Not:** Onaylı kayıttaki "Malicious Controls" kategorisinin ASVS 5.0.0'da bu adla bir bölümü yoktur; hangi 5.0.0 isteklerine denk geldiği bu belgede doğrulanmadı ve değerlendirici tarafından V15 ve V13 üzerinden eşlenmelidir. 5.0.0 klasöründe ayrıca şu bölümler bulunur (V5 File Handling, V9 Self-contained Tokens, V10 OAuth and OIDC, V17 WebRTC); ALKAROS'un dosya yükleme, JWT/OAuth veya WebRTC yüzeyi varsa değerlendirme kapsamına o bölümlerin ilgili kesimleri de eklenmelidir.

## Seviyeler

Her istek 1, 2 veya 3 seviyesiyle işaretlidir. OWASP kendisinin hiçbir satıcıyı, doğrulayıcıyı veya yazılımı sertifikalandırmadığını belirtir; ASVS'ye uygunluk iddia eden güvence veya belge OWASP onaylı değildir (`0x04-Assessment_and_Certification.md`). Sertifika benzeri bir rapor; kapsamı, kontrol edilen isteklerin özetini, istisna notlanan istekleri ve çözüm önerilerini içermelidir.

## Açık kalanlar (bu belge karar vermez)

- Onaylı kayıt Level 3 seçmiştir; Level 2 reddedilmiştir (gerekçe o kayıtta). Seviye kararı sahibindedir; değerlendirme maliyeti ve takvimi buna bağlıdır.
- Onaylı kaydın imza satırlarında ad-soyad yer tutucudur (`[Ad Soyad]`) ve imza alanları boştur; gerçek adlarla imzalanması gerekir.

## Kaynaklar (2026-10-17 okundu)

- OWASP ASVS v5.0.0 etiketi, `5.0/en` klasörü: https://github.com/OWASP/ASVS/tree/v5.0.0/5.0/en (bölüm dosyaları `0x10-V1-...` ile `0x26-V17-WebRTC`; kesim başlıkları aynı dosyalardan alınmıştır).
- Kimlik biçimi ve seviye açıklaması: aynı klasördeki `0x03-What-is-the-ASVS.md`.
