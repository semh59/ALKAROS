# V15-SUP-001 — 2026-09-22 sonradan bulunan/düzeltilen kusur

## Kusur

`DiagnosticBundleService.RedactEntry`, audit event'lerin `BeforeStateJson`/
`AfterStateJson`/`MetadataJson` alanlarını (bunlar zaten başka yerde
serialize edilmiş JSON METİNLERİdir — bkz. `src/Modules/Audit/EventStore/AuditEvent.cs`,
gerçek yazan örnek: `src/Host/Experience/Billing/BillingSplitApplication.cs:181-182`)
dış zarf JSON'una C# anonim tipin STRING alanları olarak gömüyordu.

`ObservabilityRedactionHook.RedactNode` yalnız `JsonObject`/`JsonArray`
düğümlerine iniyor; bir `JsonValue` (string) düğümüne geldiğinde hiçbir şey
yapmıyor. Sonuç: "Before"/"After"/"Metadata" alanının DEĞERİ (kendisi bir
JSON metni) hiç ayrıştırılıp içine inilmiyordu. Eğer bir modül
`beforeStateJson`/`afterStateJson` içine `{"password":"..."}` gibi
anahtar-bazlı hassas bir alan koyarsa, mevcut iki katmanlı redaksiyon
(key-bazlı `IRedactionHook` + value-bazlı `ISecretPatternScanner`) bunu
YAKALAMIYORDU, çünkü dış katmanda "Before" anahtarı hassas değil ve iç metne
hiç inilmiyor.

Not: value-PATTERN-şeklindeki sızıntılar (kart numarası, JWT/token benzeri
string) zaten önceden de yakalanıyordu, çünkü `SecretPatternScanner.Scan`
yapısal değil düz metin regex taraması yapıyor ve dış zarfın serialize
edilmiş metni üzerinde çalışıyor — nested olsa da dot/digit run'lar
görünür kalıyor. Asıl açık, yalnız KEY-adına dayanan (`password`, `pan`,
`secret`, vb.) redaksiyon için, nested JSON-metni alanlarında geçerliydi.

Ayrıca `PostgresAuditEventStore.AppendAsync`, `IAuditSanitizer` (farklı bir
modül, `src/Modules/Audit/EventStore/IAuditSanitizer.cs`) üzerinden
yazma anında da bir redaksiyon uyguluyor — bu, gerçek üretim akışında ilk
savunma katmanı. Ama `DiagnosticBundleService`'in KENDİ redaksiyonu ikinci
bağımsız katman olarak dokümante edilmiş durumda (bkz. sınıf üstü XML doc
yorumu: "passes through two independent redaction passes"); bu ikinci
katman, farklı/eksik sanitize eden alternatif `IAuditEventStore`
implementasyonlarına veya gelecekteki regresyonlara karşı bağımsız bir
güvenlik ağı olarak var olmalıydı ve olmuyordu.

## Düzeltme

`src/Modules/Support/DiagnosticBundle/DiagnosticBundleService.cs`:

- Yeni `RedactNestedStateJson(string? stateJson)` metodu: `stateJson` null/
  boş ise olduğu gibi döner; `JsonNode.Parse` başarısız olursa (geçerli JSON
  değilse) olduğu gibi döner (patlamaz); parse başarılıysa aynı iki geçişten
  (`_redactionHook.RedactJson` → `_secretScanner.Scan`) geçirilmiş halini
  döner.
- `RedactEntry`, `Before`/`After`/`Metadata` alanlarını dış zarfa gömmeden
  ÖNCE bu metottan geçiriyor.
- `ObservabilityRedactionHook`/`SecretPatternScanner` dosyalarına
  dokunulmadı.

## Revert-and-confirm (gerçek kanıt)

`DiagnosticBundleService.cs`'de `RedactEntry` geçici olarak eski (kusurlu)
haline döndürüldü (yalnızca `Before = auditEvent.BeforeStateJson` vb. düz
string gömme, `RedactNestedStateJson` çağrısı olmadan) ve yalnız yeni testler
çalıştırıldı:

```text
dotnet test tests/Modules/Support/DiagnosticBundle/ALKAROS.Support.DiagnosticBundle.Tests.csproj -c Release --filter "FullyQualifiedName~RedactsASensitiveKeyNestedInsideTheBeforeStateJsonText|FullyQualifiedName~RedactsASecretPatternValueNestedInsideTheAfterStateJsonText"
```

Sonuç (kusurlu haliyle):

```text
Başarısız ALKAROS.Support.DiagnosticBundle.Tests.DiagnosticBundleServiceTests.RedactsASensitiveKeyNestedInsideTheBeforeStateJsonText [205 ms]
  Hata İletisi:
   Assert.DoesNotContain() Failure: Sub-string found
                                  ↓ (pos 151)
String: ···"assword\\u0022:\\u0022gizli-deger-123\\u0022"···
Found:  "gizli-deger-123"

Başarısız! - Başarısız:     1, Başarılı:     1, Atlanan:     0, Toplam:     2
```

`gizli-deger-123` (seeded password) GERÇEKTEN sızdı — kanıtlandı. (Token
testi bu revert'te zaten yeşildi, çünkü `SecretPatternScanner`'ın düz metin
taraması nested olsa da token-şeklindeki değeri yakalıyor; asıl açık
KEY-bazlı redaksiyon içindi, açıklama yukarıda.)

Ardından düzeltme geri getirildi (`RedactNestedStateJson` çağrıları) ve tam
paket yeniden çalıştırıldı:

```text
dotnet test tests/Modules/Support/DiagnosticBundle/ALKAROS.Support.DiagnosticBundle.Tests.csproj -c Release
...
Başarılı!  - Başarısız:     0, Başarılı:    22, Atlanan:     0, Toplam:    22, Süre: 42 s
```

19 eski + 3 yeni test (2 yeni pozitif senaryo + 1 revert-and-confirm) dahil
22/22 yeşil.

## Etkilenen dosyalar

- `src/Modules/Support/DiagnosticBundle/DiagnosticBundleService.cs` (fix)
- `tests/Modules/Support/DiagnosticBundle/DiagnosticBundleServiceTests.cs`
  (3 yeni test + `UnsanitizedFakeAuditEventStore` yardımcı fake)
- `plan/v1.5/support/V15-SUP-001-diagnostic-bundle.md` (Acceptance evidence
  bölümüne madde eklendi)
