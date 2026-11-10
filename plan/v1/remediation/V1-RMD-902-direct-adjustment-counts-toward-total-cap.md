# V1-RMD-902 - Yönetici/supervisor doğrudan ikram ve indirimi hesap toplam sınırına sayılsın, aşarsa "yönetici sınırı aştı" işareti

- Task ID: V1-RMD-902
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Ürün sahibi kararı (Semih): supervisor ve üstü rolün doğrudan (yetki motoruna girmeden) verdiği ikram/indirim de hesap başına toplam sınıra (billing.adjustment_total_cap) SAYILIR; sınırı aşarsa engellenmez, kayda "yönetici sınırı aştı" işareti düşer. Bugün kapı yalnız verilmiş hibeleri topluyor (BillAdjustmentTotalGate -> SumGrantedForSubjectAsync); doğrudan uygulananlar hibe satırı bırakmadığından sayılmıyor. Düzeltme: kapı, hesapta fiilen uygulanmış indirim ve ikram toplamını (billing tabloları, Host tarafında uygulanan IBillAdjustmentTotals) kullanır ve hibe toplama sorgusu kaldırılır; doğrudan yol uygulama sonrası toplam sınırı aştıysa denetim kaydının sonrası durumuna totalCapExceeded=true yazar. Engelleme yoktur.

## Owned surface

- `evidence/V1-RMD-902/**`
- `src/Host/Experience/Billing/BillAppliedAdjustmentTotals.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Identity/Authorization/Grants/BillAdjustmentTotalGate.cs
  - src/Modules/Identity/Authorization/Grants/IBillAdjustmentTotals.cs — yeni arayüz
  - src/Modules/Identity/Authorization/Grants/IAuthorizationGrantRepository.cs
  - src/Modules/Identity/Authorization/Grants/PostgresAuthorizationGrantRepository.cs
  - src/Host/Experience/Billing/BillingSplitApplication.cs
  - src/Host/Experience/Billing/BillingSplitStore.cs
  - tests/Modules/Identity/Authorization/Grants/BillAdjustmentTotalGateTests.cs
  - tests/Host/Experience/Billing/BillAdjustmentTotalCapHttpTests.cs
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Uygulanmış toplamın kaynağı ve kapıya bağlanması; hibe toplama sorgusunun kaldırılması; aynı hesaptaki ikram/indirim isteklerinin kapıdan uygulamaya kadar tek kilitle sıralanması (toplam artık uygulanmış düzeltmelerden okunduğu için)
2. Doğrudan yol: sayılır, aşınca işaret düşer, engellenmez; hibe yolu davranışı değişmez
3. Gerçek Postgres testleri, mutasyon, iki bağımsız qa incelemesi

## Out of scope

- Geri alınan indirimin ve garson kalem ikramının toplamdaki yeri (sonraki görev)
- Tek-istek limitleri, ayar ekranı

## Dependencies

- V1-RMD-885

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
