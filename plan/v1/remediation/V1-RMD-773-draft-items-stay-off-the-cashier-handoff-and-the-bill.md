# V1-RMD-773 - Ateşlenmemiş Draft sipariş kasaya gitmesin, Draft kalem faturaya girmesin

- Task ID: V1-RMD-773
- Status: Done
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu (V1-RMD-719, Yüksek). V1-RMD-602 B011 "ateşlenmemiş Draft kalemler faturaya girer; Draft sipariş kasa kuyruğundan düşer, masa Cleaning olur" der ve düzeltmeyi V1-RMD-719'a verir. V1-RMD-719 yalnız istemci tarafını (Kasa gönderimi) yaptı; sunucu yarısı yapılmadı: `CashierHandoffStore.SendCheckToCashierAsync` siparişin durumuna bakmaz, `Bill.FromOrder` yalnız iptal edilmiş kalemleri dışlar.

Sonuç bugün: durumu `Draft` olan (mutfağa hiç gönderilmemiş) sipariş kasaya gönderilebilir; masa `Cleaning` olur ve sipariş `Submitted` olmadığı için kasa kuyruğunda hiç görünmez (sipariş hiçbir ekranda yok). Ayrıca mutfağa gönderilmemiş `Draft` kalemler, açık bir siparişte bile, faturaya girer ve müşteriden mutfağın hiç yapmadığı ürünün parası alınır.

Garson istemcisinin mevcut kuralı bununla aynı yöndedir: "Hesabı kasaya gönder" düğmesi yalnız sunucuda gönderilmiş (Active) kalemi olan, yerel gönderilmemiş turu bulunmayan hesapta görünür (`bill.js`). Sunucu bu kuralı kendisi de uygular.

Çözüm:

1. `SendCheckToCashierAsync` durumu `Draft` olan siparişi reddeder: yeni `CheckNotYetOrderedException`; filtre bunu 409 `ORDER_NOT_SUBMITTED` + Türkçe mesaja çevirir. Masa ve sipariş olduğu gibi kalır.
2. `Bill.FromOrder` iptal edilmiş kalemlerin yanında `Draft` kalemleri de dışlar. Mutfağa gönderilmiş (Active) kalemler aynen faturaya girer.
3. `CreateBillFromOrderAsync` durumu `Draft` olan siparişten fatura açmaz (mevcut `UNSUPPORTED_ORDER_STATE` 409); yoksa kalemsiz 0 TL'lik fatura oluşur ve sipariş sonradan ateşlense de boş kalır.

## Owned surface

- `plan/v1/remediation/V1-RMD-773-draft-items-stay-off-the-cashier-handoff-and-the-bill.md`
- `evidence/V1-RMD-773/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/CashierHandoffStore.cs (V1-WTR-033) — Draft sipariş reddi ve yeni istisna.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-ORD-005) — filtre eşlemesi (yeni hata kodu).
  - src/Modules/Billing/BillFoundation/Bill.cs (V1-RMD-035) — Draft kalem dışlama.
  - tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs (V1-RMD-160) — kasaya gönderme testi.
  - src/Host/Experience/Billing/BillingSplitStore.cs (V1-RMD-057) — Draft sipariş için fatura açma reddi.
  - tests/Modules/Billing/BillFoundation/PostgresBillTests.cs (V1-RMD-002) — fatura kalem testi ve Active tohum.
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs (V1-RMD-040) — tohum siparişlerin kalemleri Active.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Durumu Draft olan sipariş `send-to-cashier` ile reddedilir (409 `ORDER_NOT_SUBMITTED`); masa `Cleaning` olmaz, `table_sent_checks` satırı yazılmaz.
2. `Bill.FromOrder` Draft kalemleri faturaya almaz.
3. Draft kalem taşıyan eski veri için tespit sorguları ve etki değerlendirmesi `evidence/V1-RMD-773/RESULT.md` içinde.

## Out of scope

- Eski faturaların/siparişlerin düzeltilmesi, iade veya temizlik (para kararı, Semih'e kalır).
- `BillSourceOperations` (üretimde çağıran yok), kasa kuyruğu toplamı, ateşlemeden sonra mevcut faturaya kalem ekleme.

## Dependencies

- V1-RMD-719

## Acceptance evidence

1. Gerçek PostgreSQL testi: Draft sipariş send-to-cashier 409 döner, masa pointer'ı ve durumu değişmez; düzeltmeden önce kırmızı (200, masa Cleaning), sonra yeşil.
2. Gerçek PostgreSQL testi: Active + Draft kalemli siparişten `Bill.FromOrder` yalnız Active kalemi alır; düzeltmeden önce kırmızı, sonra yeşil.
3. Kural başına mutasyon: mutant testi kırar; kaynak `cp`+`cmp` ile geri alınır.
4. Billing, Billing Host, TableDraft ve ApiConventions test projeleri yeşil; plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.

## Handoff

- None
