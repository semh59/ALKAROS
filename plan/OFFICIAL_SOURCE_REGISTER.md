# Resmî Kaynak Kayıt Defteri

Bu kayıt, PDF dışında kullanılan değişebilir bilgilerin kaynağını ve sınırını
sabitler. Erişim tarihi her satırda ayrıca kaydedilir. Bir kaynağın burada
bulunması private contract veya gerçek sandbox kanıtının var olduğu anlamına
gelmez.

| Source ID | Kurum ve sürüm | URL | Kanıtlanan sınır | Erişim tarihi | Erişim | Tüketen görevler |
| --- | --- | --- | --- | --- | --- | --- |
| `GIB-YNOKC-GUIDE` | GİB, YN ÖKC kullanım rehberi | <https://www.gib.gov.tr/duyuru-arsivi/guncel/15314_yeni_nesil_odeme_kaydedici_cihaz_yn_okc_kullanimina_iliskin_rehber_yayimlandi> | Güncel YN ÖKC rehber kaydı; işletmeye uygulanabilirlik ayrıca karar gerektirir. | 2026-07-29 | Public | `V0-CMP-001`, `V12-FSC-005` |
| `GIB-TK2-4.0` | GİB, TK-2 v4.0 | <https://ynokc.gib.gov.tr/UploadedFiles/Files/ynokc2.pdf> | YN ÖKC teknik tanımları ve asgari cihaz özellikleri. | 2026-07-29 | Public | `V0-CMP-001`, `V0-MCD-001` |
| `GIB-EADISYON` | GİB, e-Belge tebliğ metni | <https://gib.gov.tr/mevzuat/kanun/434/teblig/7885> | e-Adisyon'a dahil olan mükellefler için belge ilişkisi ve geçiş bildirim sınırı; hedef işletmenin dahil oluşu ayrıca doğrulanır. | 2026-08-02 | Public | `V0-CMP-001` |
| `GIB-VUK509-2026` | GİB, VUK Genel Tebliği No. 509 güncel metin | <https://cdn.gib.gov.tr/api/gibportal-file/file/getFile?objectKey=MEVZUAT_TEBLIGLER%2FUNIVERSAL%2F2026%2FMEVZUAT_TEBLIGLER_2026_VukTeb509_Guncel.pdf> | IV.12, masada servis ve gerçek usul koşulu, dahil olma ve geçiş bildirimi; işletme profili veya bildirimi olmadan hedef kapsam sonucu üretmez. | 2026-08-02 | Public | `V0-CMP-001` |
| `GIB-YNOKC-SSS` | GİB, YN ÖKC sık sorulan sorular | <https://ynokc.gib.gov.tr/Home/SSS> | YN ÖKC muafiyet koşullarının istisna olabileceğini gösterir; işletme uygulanabilirliği için tek başına karar değildir. | 2026-08-02 | Public | `V0-CMP-001` |
| `GIB-HUGIN-T300` | GİB onaylı cihaz listesi | <https://ynokc.gib.gov.tr/Home/OnayAlanFirmalar/1003> | HUGIN T300, model kodu FT, EFT-POS özellikli cihaz olarak listelenir; aynı liste S1 model kodunu FU olarak verir. | 2026-07-30 | Public | `V0-HUG-001`, `V12-FSC-004`, `V20-INT-001` |
| `HUGIN-PRODUCT-PUBLIC` | Hugin T300 ürün sitesi | <https://hugin.com.tr/tr/hugin-t300-yazarkasa> | Güncel ürün yüzeyi ve donanım/bağlantı bilgisi; integration contract değildir. | 2026-07-30 | Public/limited | `V0-HUG-001` |
| `HUGIN-PC-LINK-V1` | Hugin PC Link API v1 | <https://hugin-pc-link.docs.buildwithfern.com/hugin-pc-link> | Yerel HTTPS REST payment/status/cancel/refund/report yüzeyi; T300 model desteğini tek başına kanıtlamaz. | 2026-07-30 | Public/contract required | `V0-HUG-001`, `V20-INT-001` |
| `HUGIN-CLOUD-LINK-V1-T300` | Hugin Cloud Link v1 | <https://developer.hugin.com.tr/docs/cloud-link/get-discount/> | `FT` sicil örneğiyle T300'e özgü Cloud Link endpoint desteği; PC Link desteğini kanıtlamaz. | 2026-07-30 | Public/contract required | `V0-HUG-001`, `V20-INT-001` |
| `HUGIN-S1-PC-LINK-PUBLIC` | Hugin S1 ürün sitesi | <https://hugin.com.tr/tr/s1-android-yazarkasa-pos> | S1'i Android cihaz ve S1 PC Link Kit ile yayımlar; T300 için aynı kit kanıtı değildir. | 2026-07-30 | Public/limited | `V0-HUG-001` |
| `QNB-API-PUBLIC` | QNB eSolutions public API | <https://www.qnbesolutions.com.tr/api-docs-tr-final.html> | Kamuya açık e-belge işlemleri; sayfada bulunmayan webhook/iptal private kanıt olmadan varsayılmaz. | 2026-07-29 | Public/limited | `V0-QNB-001`, `V12-FSC-005`, `V13-QNB-005`, `V20-INT-002`, `V20-CMP-001` |
| `YSP-PARTNER-2.0.2` | Yemeksepeti Partner API v2.0.2 | <https://developer.yemeksepeti.com/api-specifications> | OAuth2, order, webhook ve catalog sözleşmesi; credential/sandbox sağlamaz. | 2026-07-29 | Public/credential required | `V0-YSP-001`, `V20-INT-003` |
| `DOTNET-SUPPORT-2026-07` | Microsoft .NET support policy | <https://dotnet.microsoft.com/en-us/platform/support/policy> | .NET 10 LTS, 10.0.10 patch ve 2028-11-14 support sonu. | 2026-07-29 | Public | `V0-ARC-007` |
| `POSTGRESQL-18.4` | PostgreSQL 18.4 documentation | <https://www.postgresql.org/docs/18/> | PostgreSQL 18'in desteklenen güncel dokümantasyon dalı. | 2026-07-29 | Public | `V0-ARC-007`, `V0-BKP-001`, `V0-DAT-006` |
| `OWASP-ASVS-5.0.0` | OWASP ASVS 5.0.0 | <https://owasp.org/www-project-application-security-verification-standard/> | Web application security verification tabanı; hedef seviye V0 kararıdır. | 2026-07-29 | Public | `V0-SEC-001`, `V15-SEC-002`, `V20-SEC-001` |
| `OWASP-AUTH` | OWASP Authentication Cheat Sheet | <https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html> | Authentication, throttling, lockout ve izleme rehberi. | 2026-07-29 | Public | `V0-SEC-001`, `V15-SEC-002` |
| `OWASP-SESSION` | OWASP Session Management Cheat Sheet | <https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html> | Session token, lifecycle ve transport güvenliği rehberi. | 2026-07-29 | Public | `V0-SEC-001`, `V15-SEC-002` |
| `OWASP-LOGGING` | OWASP Logging Cheat Sheet | <https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html> | Uygulama ve security event logging rehberi. | 2026-07-29 | Public | `V0-SEC-001`, `V15-OBS-001` |
| `WCAG-2.2` | W3C WCAG 2.2 | <https://www.w3.org/WAI/standards-guidelines/wcag/> | Güncel erişilebilirlik standardı; conformance seviyesi V0 kararıdır. | 2026-07-29 | Public | `V0-CMP-005` |
| `CYCLONEDX-1.7` | CycloneDX 1.7 / ECMA-424 | <https://cyclonedx.org/specification/overview/> | Machine-readable SBOM formatı ve resmî media type bilgisi. | 2026-07-29 | Public | `V0-ARC-008`, `V20-REL-001` |
| `SLSA-1.2` | SLSA specification 1.2 | <https://slsa.dev/spec/v1.2/> | Build/source provenance ve attestation çerçevesi; hedef seviye V0 kararıdır. | 2026-07-29 | Public | `V0-ARC-008`, `V20-REL-001` |

## Private veya henüz bulunmayan kanıtlar

| Evidence ID | Eksik kanıt | Etkilenen görev |
| --- | --- | --- |
| `PRIVATE-HUGIN-CONTRACT` | T300 model/firmware/protocol matrisi, contract ve gerçek cihaz transcript'i | `V0-HUG-001` |
| `PRIVATE-QNB-SANDBOX` | Test tenant, lifecycle transcript'i ve public dokümanda olmayan capability sözleşmesi | `V0-QNB-001` |
| `PRIVATE-YSP-SANDBOX` | Partner credential ve gerçek webhook/sandbox transcript'i | `V0-YSP-001` |
| `PRIVATE-MEAL-CARD` | Onaylı provider listesi, contract ve gerçek sandbox/cihaz kanıtı | `V0-MCD-001` |
| `PRIVATE-PRINTER-DEVICE` | Onaylı model ve gerçek hata/retry cihaz transcript'i | `V0-PRN-001` |

Bu private kayıtlar kaynak değil blocker'dır. İlgili kanıt çalışma alanına
alınmadan görev `Done` yapılamaz.
