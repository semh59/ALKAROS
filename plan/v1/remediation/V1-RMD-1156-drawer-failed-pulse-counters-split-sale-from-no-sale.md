# V1-RMD-1156 - Çekmece sayaçları: açılamayan satışsız deneme ile ödeme sırasında açılamayan darbe ayrı sayılır

- Task ID: V1-RMD-1156
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 16 (bulgu 3): "Açılamayan çekmece denemesi" sayacı `cash.drawer.kick-failed` olaylarının tamamını sayar (`CashDrawerService.CountsSql`); olayın `kind` alanı `NoSale` (kasiyerin satışsız açma denemesi) ya da `Sale` (nakit ödeme sonrası darbe) olabilir. Etiket "deneme" der ama ödeme darbeleri de girer; kasiyer ve yönetici ayrımı göremez (6 satışsız + 5 ödeme = 11).

1. `CashDrawerCounts` açılamayanları ikiye ayırır: `FailedNoSaleKicks` (`kind = NoSale`) ve `FailedSaleKicks` (`kind = Sale`); `SaleOpenings` ve `NoSaleOpenings` tanımı değişmez.
2. Kapanış yanıtı `drawer` alanı `saleOpenCount`, `noSaleCount`, `noSaleKickFailedCount`, `saleKickFailedCount` taşır; `cash.session.closed` denetim kaydı `drawerKickFailedCount` yerine `drawerNoSaleKickFailedCount` ve `drawerSaleKickFailedCount` yazar (eski toplam alan kaldırılır; toplam ikisinin toplamıdır).
3. "Kasa Kapatıldı" özeti iki ayrı satır gösterir, yalnız sayı sıfırdan büyükse: "Açılamayan satışsız çekmece denemesi" ve "Ödeme sırasında açılamayan çekmece". Çekmece hiç kullanılmadıysa hiçbir çekmece satırı çıkmaz.
4. `docs/domain/cash-session-design.md` ve `docs/qa/device-browser-test-plan.md` yeni alan adlarını ve tanımı anlatır.

## Owned surface

- `evidence/V1-RMD-1156/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CashDrawerService.cs
  - src/Host/DualScreen/CashCloseStore.cs
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Host/Experience/CashSession/CashDrawerHttpTests.cs
  - tests/Clients/StaticApps/cash-session-closed-summary.test.js
  - docs/domain/cash-session-design.md
  - docs/qa/device-browser-test-plan.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. CashSession HTTP testleri, ApiConventions mimari testi ve istemci testleri (tests/Clients/StaticApps)

## Out of scope

- Yönetim ekranı ve Reporting modülü; uyarı eşiği (5 satışsız açma); çekmece ayarları; sayımın kendisi

## Dependencies

- V1-RMD-1155

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps ve CashSession HTTP testleri exit code 0

## Handoff

- None
