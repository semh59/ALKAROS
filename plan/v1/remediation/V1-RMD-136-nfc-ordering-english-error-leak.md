# V1-RMD-136 - Independent audit: NFC ordering raw English error leaks

- Task ID: V1-RMD-136
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09, QR/NFC müşteri sipariş yüzeyi turu)
bulgusu: `NfcOrderingEndpoints.cs`'teki üç doğrulama dalı (`tableId`,
`request.Items`, `request.Id` boş kontrolü), aynı dosyadaki tamamen
Türkçe eşlemeli `NfcOrderingExceptionFilter`'ın önüne geçen inline
`Results.BadRequest` çağrılarıydı ve ham İngilizce metin
("TableId cannot be empty." vb.) doğrudan müşterinin telefon ekranına
(`NfcOrder.tsx`, mesajı olduğu gibi basıyor) sızıyordu — bugün sabah
`V1-RMD-127`'de `OrderManagementEndpoints.cs`'te düzeltilen kusurun
birebir aynısı; o tarama bu dosyayı atlamıştı.

## Owned surface

- `plan/v1/remediation/V1-RMD-136-nfc-ordering-english-error-leak.md`
  (yeni)
- Sınırlı ek — aşağıdaki yol ilgili görevin sahipliğinde kalır (yol
  geri-tik olmadan yazıldı ki denetleyici bunu sahiplik iddiası olarak
  parse etmesin):
  - src/Host/Experience/NfcOrdering/NfcOrderingEndpoints.cs (V12-NFC-001
    sahipliğinde) — yalnız üç `Results.BadRequest` çağrısındaki `message`
    metni Türkçeye çevrildi; `code` alanları, filtre, route tanımı ve
    dosyanın geri kalanı değişmedi.
  - tests/Host/Experience/NfcOrdering/NfcOrderingHttpTests.cs
    (V12-NFC-001 sahipliğinde) — `EmptyItemsIsRejected` yeniden
    adlandırılıp gövde metnini de doğrulayacak şekilde genişletildi; iki
    yeni test (`AnEmptyTableIdIsRejectedWithATurkishMessage`,
    `AnEmptySubmissionIdIsRejectedWithATurkishMessage`) eklendi.

## In scope

1. Üç literal Türkçeye çevrildi: "TableId cannot be empty." →
   "Masa kimliği boş olamaz.", "Order items cannot be empty." →
   "Sipariş kalemleri boş olamaz.", "Id cannot be empty." →
   "Gönderim kimliği boş olamaz." — `code` alanları (`INVALID_TABLE`,
   `EMPTY_ITEMS`, `INVALID_SUBMISSION_ID`) değişmedi.
2. Üç yeni/genişletilmiş test, HTTP gövdesinde Türkçe metnin
   döndüğünü ve İngilizce "cannot be empty" ifadesinin artık hiç
   geçmediğini doğruluyor.

## Out of scope

- Aynı denetimin diğer bulguları (K1 — yaş kısıtlı ürün onayının fiilen
  işlemediği ve `PendingConfirmation`'dan çıkışın hiçbir aksiyonu
  olmadığı; Y2 — `RelayAbusePolicy` bağlanmamış; O1/O2; D1-D3) — ayrı,
  bazıları kullanıcıyla görüşülecek tasarım kararı gerektiren görevler.
- QR kanalının kendi müşteri sayfası (`V12-CWB-001`/`002`, hâlâ
  "Planned") — bu görevin kapsamı dışında.

## Dependencies

- V12-NFC-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- `docker compose -f compose.test.yaml run --build --rm test dotnet test
  tests/Host/Experience/NfcOrdering/ALKAROS.Host.Experience.NfcOrdering.Tests.csproj
  -c Release`: gerçek Postgresql'e karşı **Passed! Failed: 0, Passed: 15,
  Skipped: 0, Total: 15**, real exit code 0 (ilk koşuda
  `ConcurrentIdenticalFirstSubmissionsResolveToTheSameOrder` geçici bir
  503 ile tek başına flake etti — izole tekrar çalıştırmada anında geçti;
  bu değişiklikle ilgisiz, bilinen bir Postgres-eşzamanlılık testi
  gürültüsü; ikinci tam koşu 15/15 temiz).
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var
  olan, ilgisiz ihlal (değişmedi), yeni ihlal yok.

## Handoff

- None
