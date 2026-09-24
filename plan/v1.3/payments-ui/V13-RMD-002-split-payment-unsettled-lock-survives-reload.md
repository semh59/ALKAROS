# V13-RMD-002 - Make the split-payment screen's "manuel mutabakat gerekiyor" lock survive a page reload

- Task ID: V13-RMD-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`V1-RMD-258` (2026-09-23), sunucu tarafında mükerrer bir BankCard tahsilat
denemesini gerçekten kilitli hale getirmişti: `GET .../tenders/` artık gerçek
bir `unsettledPayment` alanı döndürüyor (kalıcılaşmış, `Pending`/`Unknown`/
`ReconciliationRequired` durumundaki bir Payment varsa) ve böyle bir kayıt
varken yeni bir deneme `409 TENDER_UNSETTLED_PAYMENT_EXISTS` ile reddediliyor.
Ama `src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js` bu
yeni alanı hiç okumuyordu — kilidi yalnız o sayfa yüklemesi sırasında taze bir
`RequiresReconciliation` sonucu geldiğinde bellekteki `state.locked`
bayrağıyla tutuyordu. Bu eksiklik `docs/engineering/e2e-playwright-master-
test-plan.md` (§0, bulgu 1) yazılırken, hiçbir şey çalıştırılmadan, kod
okumasıyla bulundu — gerçek, açıkça belgelenmiş ama düzeltilmemiş bir boşluk.
Düzeltmeden önceki net etki: sayfa yenilemesi artık mükerrer tahsilat riski
taşımıyordu (sunucu zaten engelliyordu), ama kasiyer ikinci bir denemede
niyetlenen "manuel mutabakat gerekiyor" banner'ı yerine belirsiz/genel bir
hata mesajı görüyordu.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  (V13-PUI-001 sahipliğinde kalır — yalnız `loadEverything`/`refreshSummary`'nin
  kilit türetme mantığı ve `submitTender`'ın hata dalı değişti, başka bir şey
  dokunulmadı)
- `plan/v1.3/payments-ui/V13-RMD-002-split-payment-unsettled-lock-survives-reload.md`

## In scope

1. `loadEverything()`: `state.locked` artık her ilk sayfa yüklemesinde
   `summary.unsettledPayment`'tan türetiliyor (doluysa kilitli), yalnız
   sayfa-içi bir `RequiresReconciliation` olayından değil. Kilidin yalnız bu
   sayfanın kendi oturumunda tutulduğunu iddia eden artık bayat doc yorumu
   kaldırıldı.
2. `refreshSummary()`: `state.locked` her yenilemede de sunucudan yeniden
   türetiliyor — bu hem taze bir `RequiresReconciliation` geldiğinde kilidi
   kuruyor, hem de sunucu tarafında gerçek bir çözüm oluştuğunda (ör. ileride
   `V13-HUG-001`'in gerçek terminali denemeyi çözünce) kilidi doğru şekilde
   KALDIRIYOR.
3. `submitTender()`'ın hata dalı: bir `409 TENDER_UNSETTLED_PAYMENT_EXISTS`
   yanıtı (eşzamanlı bir sekme, ya da bu düzeltmeden önce bayat bir
   yenilemenin kilidi düşürmüş olması) artık genel 409 hata metnini
   bırakmak yerine `refreshSummary()` + yeniden render tetikliyor, böylece
   kasiyer gerçek uyarı banner'ını görüyor, belirsiz bir başarısızlık
   bildirimi değil.

## Out of scope

- Herhangi bir backend değişikliği (`V1-RMD-258`'in sunucu tarafı düzeltmesi
  zaten doğru, burada değiştirilmedi).
- Aynı master-plan §0 listesindeki diğer 4 bulgu (WaiterPwa'nın `TABLE_STATUS`
  ham-fallback'i, PosTerminal'in `RelaySettings` exhaustive-olmayan etiket
  haritası, CustomerWeb'in Order Entry not-uzunluğu boşluğu, CustomerWeb'in
  Order Entry sekme-yeniden-açma boşluğu) — her biri kendi ayrı remediation
  görevi.
- Bu tam senaryo için yeni bir Playwright spec'i — gerçek HTTP-seviyesi kanıt
  zaten var (`tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs`
  içindeki `BankCardRequiresReconciliationIsPersistedAndSurvivesAFreshClientLoad`,
  `V1-RMD-258` tarafından eklendi, hâlâ yeşil); aynı özelliğin tarayıcı-
  seviyesi regresyon testi `docs/engineering/e2e-playwright-master-test-
  plan.md`'nin kendi §3.1.J Faz-1 önerisi — o inisiyatifin bir parçası olarak
  ele alınacak, bu küçük düzeltmeye paketlenmedi.

## Dependencies

- V13-PUI-001
- V1-RMD-258

## Acceptance evidence

- `node --check src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js`
  → temiz (sözdizimi hatası yok).
- Gerçek backend sözleşmesine karşı elle izlendi: `src/Host/DualScreen/
  DualScreenApplication.Payments.cs` ve gerçek, hâlâ yeşil `tests/Host/
  Experience/PaymentTender/PaymentTenderHttpTests.cs` okunarak doğrulandı —
  `GET .../tenders/` `unsettledPayment: { paymentId, status, reason } | null`
  döndürüyor; istemci artık tam olarak bu şekli okuyor.
- Bu düzeltme için gerçek tarayıcı (Playwright) doğrulaması yapılmadı — bu
  istemcinin JS unit-test altyapısı yok (WaiterPwa/Cashier'ın kendi
  yerleşik deseniyle aynı, kasıtlı olarak yalnız E2E), ve yeni bir Playwright
  spec'i eklemek (kendi Bill-seed fixture'ıyla) bu küçük, dar kapsamlı
  düzeltme için Out of scope notuna göre kapsam dışı bırakıldı. Açıkça
  belirtiliyor, tarayıcıda doğrulanmış diye iddia edilmiyor.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
