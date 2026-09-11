# V1-WTR-020 - Gönüllü bahşiş kaydı (servis ücreti DEĞİL)

- Task ID: V1-WTR-020
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının "Yeni fikirler" bölümünden
8. madde (Katman C — "servis ücreti + bahşiş havuzu") — ama başlamadan önce
yapılan araştırma, maddenin orijinal çerçevesini geçersiz kıldı.

**Araştırma bulgusu (Semih'in talimatıyla, 2026-09-11):** 30 Ocak 2026'da
yürürlüğe giren bir yönetmelik değişikliği (Resmi Gazete 33153),
yiyecek-içecek işletmelerinin "servis, masa, kuver veya benzeri adlar
altında" adisyona ek ücret eklemesini YASAKLADI — ihlal başına 3.973 TL
(2026) ceza, adisyon adı menüde geçse bile. Yalnızca müşterinin kendi
isteğiyle verdiği, ödeme ekranında ÖNCEDEN DOLDURULMAMIŞ/seçili gelmeyen,
personel tarafından talep edilmeyen bir bahşiş yasal.

Bu yüzden bu görev fikrin yalnızca yasal yarısını kapsıyor: gönüllü bir
bahşiş tutarını kaydetmek. Bir "servis ücreti" özelliği bilinçli olarak
YAPILMADI ve bir daha yapılmamalı.

**Domain zaten tamamdı, yalnızca ulaşılamıyordu:** `BillAdjustment.CreateTip`
(VAT muaf, V0-CMP-004) V1-BIL-003'ten beri vardı ama hiçbir HTTP uç
noktasına bağlanmamıştı — tıpkı `bills.discount`'ın V1-RMD-103'e kadar
yaşadığı boşluk gibi. `ApplyDiscountAsync`'in FOR UPDATE kilit +
idempotency-key tekrar koruması deseni birebir taklit edildi.

**Önemli bir yan bulgu:** Aynı dosyada `CreateServiceFee` (ServiceFee/Kuver
adjustment type'ları) da domain-complete ama hiçbir uç noktaya bağlı değil
— yani zaten "ölü kod". Yeni yönetmelik bunu kalıcı olarak ölü tutmayı
gerektiriyor; factory metoduna bunu açıkça belirten bir uyarı yorumu
eklendi (bir sonraki oturumun bunu yanlışlıkla bağlamaması için).

**İstemci UI'ı bilinçli olarak yapılmadı:** Hiçbir ALKAROS istemcisinde
(Cashier, PosTerminal, WaiterPwa) gerçek bir "ödeme al / hesabı kapat"
ekranı yok — Cash modu V1.2'ye ertelendi (bkz. bellek notu). Discount/comp
uç noktaları da aynı sebeple hâlâ istemcisiz. Bir ödeme ekranı olmadan
bahşiş girişi için sahte bir UI icat etmek yanlış olurdu.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-020-voluntary-tip.md` (yeni)
- Sınırlı ek:
  - src/Modules/Billing/Adjustments/BillAdjustment.cs (Billing sahipliğinde)
    — `CreateTip`'e `idempotencyKey` parametresi, `CreateServiceFee`'ye
    uyarı yorumu.
  - src/Host/Experience/Billing/BillingSplitContracts.cs,
    BillingSplitStore.cs, BillingSplitApplication.cs (Host sahipliğinde) —
    `ApplyBillTipRequestV1`/`ApplyBillTipResultV1`, `ApplyTipAsync`,
    `POST /{billId}/tip`.
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs (Billing test
    sahipliğinde) — 3 yeni test.

## Out of scope

- Servis ücreti / kuver — artık yalnızca kapsam dışı değil, yasal olarak
  yasak. Bkz. yukarıdaki araştırma bulgusu.
- Bahşiş havuzu dağıtımı — V1-WTR-021'in kendi kapsamı (eşit bölüşüm,
  Semih'in kararı).
- Gerçek bir ödeme/hesap kapama ekranı — V1.2'ye ertelenen Cash modunun
  kendi kapsamı; bu görev yalnızca zaten var olan Billing altyapısına
  eksik bir uç nokta ekliyor. İleride böyle bir ekran yapılırsa: bahşiş
  tutarı alanı varsayılan olarak boş gelmeli, hiçbir yüzde önerisi/ön-seçim
  gösterilmemeli, personel tarafından sözlü olarak talep edilmemeli —
  yönetmeliğin "gönüllülük" şartı budur.

## Dependencies

- V1-BIL-003
- V1-RMD-103

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test
  tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj`
  → 17/17 (3 yeni test: bills.split'e sahip bir rol doğrudan bahşiş
  kaydeder; aynı idempotency key ile tekrar aynı satırı döner, ikinci bir
  satır eklemez; bills.split'i olmayan bir rol 403 alır).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None
