# V0-HUG-001 - Validate Hugin T300 integration contract

- Task ID: V0-HUG-001
- Status: Blocked
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.6
- PDF:I.6.1
- EXT:GIB-HUGIN-T300
- EXT:HUGIN-PRODUCT-PUBLIC
- EXT:HUGIN-PC-LINK-V1
- EXT:HUGIN-CLOUD-LINK-V1-T300
- EXT:HUGIN-S1-PC-LINK-PUBLIC

## Goal

Seçimi yeniden açmadan T300 payment, fiscal, timeout, unknown, cancellation, refund ve reconciliation sözleşmesini
model, firmware ve topology düzeyinde gerçek doküman ve erişimle doğrulamak.

## Owned surface

- `evidence/v0/integrations/V0-HUG-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- T300 model/firmware desteği, PC Link ile Cloud Link topology ayrımı, credentials, test device, request IDs, error
  codes ve recovery operations.
- Payment komutunun ALKAROS tarafından terminale gönderilmesi ile terminalin siparişi çekmesi arasındaki davranış
  farkı.

## Out of scope

- Production Hugin adapter uygulaması.
- Alternatif cihaz seçimi veya T300 yerine otomatik provider/model fallback'i.

## Dependencies

- V0-CMP-001
- V0-DOM-003

## Blocker

- GİB kaynağı T300 model kodunu `FT` olarak doğrular. Hugin Cloud Link belgesi T300'e özgü `FT` örneğiyle Cloud Link
  desteğini doğrular; ancak bu topology terminalin siparişi çektiği akıştır.
- PC Link v1; doğrudan payment, status, cancellation ve refund API yüzeylerini yayımlar. Kamuya açık örnek `FU`
  sicilini kullanır; GİB'de `FU` S1 modelidir. İncelenen Hugin ürün sayfası PC Link Kit'i S1 aksesuarı olarak
  yayımlar; T300 sayfasında aynı destek gösterilmez. Bu yokluk, T300 PC Link uyumsuzluğunu kanıtlamaz.
- İmzalı T300 model/firmware/protocol matrisi, entegrasyon sözleşmesi, erişilebilir T300 test cihazı ve
  success/decline/timeout/query/cancel/refund/daily-total transcript kanıtı çalışma alanında yoktur.
- Görev, Hugin'in T300 için kesin topology/endpoint desteğini yazılı doğrulaması ve test device/credential erişimi
  sağlandığında `Planned` durumuna alınabilir. Gerçek transcript'ler `Done` acceptance kanıtıdır. Olumsuz cevap başka
  cihaza otomatik geçiş yetkisi vermez; görev `Blocked` kalır ve ayrı, kullanıcı onaylı plan değişikliği gerekir.

## Deliverables

- T300 model, firmware, protocol ve topology kombinasyonunu sabitleyen tarihli evidence package.
- Endpoint capability matrisi ve success, decline, timeout, query, cancel, refund ile daily-total gerçek çıktıları.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- `FT` sicilli gerçek T300 üzerinde seçilen topology ile success, decline, timeout, query, cancel, refund ve
  daily-total/reconcile kanıtı vardır; ALKAROS'un payment başlatma davranışı açıkça doğrulanmıştır.

## Handoff

- V12-HUG-001
- V12-HUG-002
- V12-HUG-003
- V12-HUG-004
- V12-FSC-004
