# V1-RMD-106 - Wave 34: independent audit Critical/High/Medium/Low fixes

- Task ID: V1-RMD-106
- Status: Done
- Assignee: claude-session-011Z3dQdMVJBZEXFgDQt5i6e
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Hepsini düzelt", 2026-09-06), altı bağımsız ajanla
sıfırdan yapılan tam denetimin (arayüz, backend, mimari sınırlar,
veritabanı, roller/yetkilendirme, API endpoint'leri) V1 kapsamındaki
tüm bulguları düzeltildi. V1.1 kapsamındaki bulgular (Inventory/Recipes
mimari ihlali, V1.1 migration wiring, StockMovement reversal race, vb.)
bu dalganın kapsamı DIŞINDA bırakıldı — depoda eşzamanlı başka bir
oturumun V1.1 Inventory görevlerini (V11-INV-001, V11-INV-002) aktif
olarak ilerlettiği tespit edildi; çakışmayı önlemek için o kapsam ayrı
bir göreve bırakıldı.

## Owned surface

- `plan/v1/remediation/V1-RMD-106-wave34-independent-audit-critical-and-high-fixes.md`
- `tests/Host/Experience/Orders/TableDraft/**` (yeni test projesi)
- Sınırlı ek — hiçbir yeni production yüzeyi sahiplenilmedi; aşağıdaki
  tüm yollar ilgili görevin sahipliğinde kalır, bu dalgada yalnız
  kanıtlanmış bulgu düzeltmesi yapıldı (yollar geri-tik olmadan
  yazıldı ki denetleyici bunları sahiplik iddiası olarak parse etmesin):
  - src/Host/Experience/Orders/OrderManagementStore.cs ve
    OrderManagementEndpoints.cs (V1-ORD-005 sahipliğinde) —
    CreateOrUpdateTableDraftAsync artık aynı masaya ikinci bir taslak
    geldiğinde mevcut kalemleri silmek yerine birleştiriyor
    (IOrderRepository.SaveAsync'in connection/transaction alan
    overload'ı kullanılarak, kilit sırasında ayrı bir bağlantıdan
    kaynaklanacak kilitlenmeyi de önleyerek); IdempotencyKeyReusedException
    409 eşlemesi eklendi.
  - src/Clients/Cashier/wwwroot/cashier-app.js (V1-CUI-004 sahipliğinde)
    ve src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-006/008
    sahipliğinde) — sipariş gönderimi artık table-draft'tan sonra
    submit-draft'ı da çağırıyor (önceden hiç çağrılmıyordu, sipariş
    mutfağa asla ulaşmıyordu); ham HTTP durum kodları kullanıcıya
    gösterilmiyor (yerel Türkçe çeviri fonksiyonu); WaiterPwa girişinde
    ağ hatası artık ham İngilizce mesaj yerine Türkçe metin gösteriyor.
  - src/Modules/Identity/Authorization/Grants/AuthorizationGrantService.cs
    (V1-IAM-019 sahipliğinde) — idempotency-key tekrar kontrolü artık
    izin kodu/subject/requester eşleşmesini de doğruluyor;
    IdempotencyKeyReusedException eklendi.
  - src/Host/Experience/Billing/BillingSplitApplication.cs ve
    BillingSplitStore.cs (V1-RMD-103 sahipliğinde) —
    ApplyDiscountAsync artık bill durumunu (BillDiscountUnsupportedBillStateException)
    ve eşzamanlı indirimleri (bill satırında FOR UPDATE kilidi) kontrol
    ediyor.
  - src/Modules/Billing/Adjustments/IBillAdjustmentRepository.cs ve
    PostgresBillAdjustmentRepository.cs (V1-BIL-003 sahipliğinde) —
    AddAsync'in connection/transaction alan bir overload'ı eklendi (yukarıdaki
    kilit sırasında ayrı bağlantıdan kaynaklanan kilitlenmeyi önlemek için;
    FK referansı aynı satır için beklemede kalıyordu).
  - src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs
    (V1-IAM-025 sahipliğinde) — uzlaştırılan bütçe ve her aksiyonun
    requester'ı artık kimliği doğrulanmış çağıranla eşleştiriliyor
    (OfflineReconciliationIdentityMismatchException, 403).
  - src/Clients/PosTerminal/src/strings.ts (V1-IAM-020 sahipliğinde) ve
    src/Clients/PosTerminal/src/features/billing/models.ts,
    BillSplitWorkspace.tsx (V1-RMD-052 sahipliğinde) — ham İngilizce
    `billStatus` artık `billStatusLabels` çeviri haritasından geçiyor.
  - ALKAROS.slnx (V1-RMD-036 sahipliğinde) — yeni TableDraft test
    projesinin girişi eklendi.
  - tests/Modules/Identity/Authorization/Grants/AuthorizationGrantServiceTests.cs,
    tests/Host/Experience/Billing/BillingSplitHttpTests.cs,
    tests/Host/Experience/OfflineReconciliation/OfflineReconciliationHttpTests.cs
    (ilgili görevlerin test sahipliğinde) — yukarıdaki düzeltmeler için
    regresyon testleri eklendi.

## In scope

1. **Critical** — `OrderManagementStore.CreateOrUpdateTableDraftAsync`:
   aynı masaya ikinci bir sipariş gönderildiğinde ilk siparişin
   kalemleri sessizce siliniyordu (bağımsız denetim, 2026-09-06).
   Artık mevcut Draft siparişin kalemleri yükleniyor ve yenileriyle
   birleştiriliyor.
2. **Critical** — Ne Cashier ne de WaiterPwa istemcisi `submit-draft`
   endpoint'ini hiç çağırmıyordu; sipariş mutfağa asla gönderilmiyordu
   (kullanıcı "gönderildi" mesajı görse de). İkisi de artık
   table-draft'ın ardından submit-draft'ı çağırıyor.
3. **Critical** — `AuthorizationGrantService.RequestAsync`'in
   idempotency-key tekrar kontrolü izin kodu/subject/requester
   eşleşmesine bakmıyordu; bir anahtar farklı bir komut için tekrar
   kullanılırsa eski çözüm sonucu (escalation atlanarak) döndürülüyordu.
   Artık eşleşmeyen bir tekrar `IdempotencyKeyReusedException` fırlatıyor.
4. **High** — Offline reconciliation endpoint'i kimliği doğrulanmış
   çağıranı atıyordu; bir kasiyer başka bir çalışanın budgetId'sini
   bilirse onun adına sahte grant kaydı enjekte edebilirdi. Artık
   bütçe sahibi ve her aksiyonun requester'ı çağıranla eşleşmiyorsa
   403 dönüyor.
5. **High** — `BillingSplitStore.ApplyDiscountAsync` bill durumunu
   veya eşzamanlılığı kontrol etmiyordu; ödenmiş bir hesaba indirim
   uygulanabiliyordu ve eşzamanlı iki indirim toplamı ödenecek tutarı
   aşabiliyordu. Artık durum kontrolü ve bill satırında FOR UPDATE
   kilidi var.
6. **Medium** — Cashier/WaiterPwa istemcilerinde ham HTTP durum kodları
   kullanıcıya gösteriliyordu; artık yerel Türkçe çeviri fonksiyonundan
   geçiyor.
7. **Medium** — PosTerminal `BillSplitWorkspace`'te ham İngilizce
   `billStatus` enum değeri Türkçe cümle içinde gösteriliyordu; artık
   `billStatusLabels` haritasından geçiyor.
8. **Low** — WaiterPwa girişinde `fetch()`'in kendisi (ağ hatası)
   attığında tarayıcının ham İngilizce mesajı gösteriliyordu; artık
   Türkçe bir mesaja düşüyor.

## Out of scope

- V1.1 (Inventory/Recipes) kapsamındaki bulgular — depoda eşzamanlı
  başka bir oturum bu görevleri ilerletiyor; çakışmayı önlemek için
  ayrı bir göreve bırakıldı.
- `OrderManagementStore.CreateOrUpdateTableDraftAsync`/`SaveAsync`'in
  mevcut kalemleri her çağrıda (değişmemiş olsalar bile) güncelleyip
  row_version'larını artırması — ayrı, denetimde bulunmamış bir
  gözlem, kapsam dışı.

## Dependencies

- V1-RMD-105

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`, port 55432):
  `ALKAROS.Host.Experience.Orders.TableDraft.Tests` 3/3 (yeni — masaya
  ikinci sipariş birleştirme + submit-draft geçişi),
  `ALKAROS.Identity.Authorization.Tests` 186/186 (idempotency-key
  eşleşmeme testi dahil), `ALKAROS.Host.Experience.Billing.Tests` 10/10
  (Paid hesapta indirim reddi + eşzamanlı indirim testleri dahil),
  `ALKAROS.Host.Experience.OfflineReconciliation.Tests` 5/5 (kimlik
  eşleşmeme testi dahil), `ALKAROS.Host.Experience.Orders.Comp.Tests`
  7/7, `.VoidSent.Tests` 8/8, `ALKAROS.Billing.Adjustments.Tests` 14/14,
  `ALKAROS.Orders.OrderAggregate.Tests` 102/102,
  `ALKAROS.Host.Experience.Composition.Tests` 5/5,
  `ALKAROS.Architecture.Tests` 8/8 — hepsi regresyonsuz.
- PosTerminal: `tsc --noEmit` temiz; `vitest run BillSplitWorkspace.test.tsx` 5/5.
- Kilit sırasında ayrı bağlantıdan kaynaklanan iki gerçek kilitlenme
  (`OrderManagementStore`'da ve `BillingSplitStore.ApplyDiscountAsync`'de)
  test geliştirilirken bizzat tetiklenip (30s timeout) düzeltmenin
  gerekliliği doğrulandı — düzeltme sonrası testler saniyeler içinde
  geçiyor.
- `python tools/consistency-audit/consistency_audit.py`: temiz.
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `validate-coverage`: sıfır hata.
- Semih'in elle deneyebileceği senaryo: bir masaya sipariş gir ve
  mutfağa gönder, aynı masaya ikinci bir sipariş (örn. tatlı) gönder —
  ikinci sipariş ayrı bir sipariş olarak mutfağa gider, ilk siparişin
  kalemleri kaybolmaz; her iki sipariş de artık gerçekten "Submitted"
  durumuna geçer (önceden sonsuza dek Draft'ta kalıyordu).

## Handoff

- V1-GOV-089
