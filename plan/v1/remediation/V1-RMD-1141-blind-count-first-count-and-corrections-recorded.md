# V1-RMD-1141 - Kör sayım bütünlüğü: ilk sayım ve düzeltme sayısı kapanış kaydında ve kapanış özetinde

- Task ID: V1-RMD-1141
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu "kör sayım bütünlüğü"): sayım ekranı beklenen tutarı göstermez, ama kasiyer fark ekranını gördükten sonra "Sayımı düzelt" ile rakamı ayarlayabilir. Her sayım `cash.cash_counts` tablosuna değişmez satır olarak yazılıyor (kod doğrulandı: `InsertCountAsync`), ama kapanış denetim kaydı (`cash.session.closed`) ve kapanış özeti yalnız SON sayımı taşır; ilk sayım ve düzeltme sayısı yöneticinin okuyacağı yerde yok. Düzeltme engellenmez (yanlış sayımı düzeltmek meşrudur), izlenir.

1. Kapanış işlemi içinde, oturum kilidi altında `cash.cash_counts` okunur ve `cash.session.closed` kaydının `afterStateJson` alanına `firstCountedAmount` (ilk sayımın tutarı) ve `countEntries` (kaydedilen sayım satırı sayısı; düzeltme sayısı = `countEntries - 1`) yazılır. 30 sn içinde aynı sayaç, tutar ve notla tekrarlanan istek yeni satır olmadığı için sayıya girmez.
2. Kapanış yanıtı `count` alanını taşır: `firstCountedAmount`, `correctionCount`. "Kasa Kapatıldı" özeti (Cashier) düzeltme varsa "İlk sayım: ₺X; sayım N kez düzeltildi" satırını gösterir; düzeltme yoksa satır çıkmaz. Metin sabit Türkçedir, suçlayıcı değildir.
3. Sayım ekranı davranışı (beklenen gizli, "Sayımı düzelt") değişmez.

## Owned surface

- `evidence/V1-RMD-1141/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CashCloseStore.cs
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Host/Experience/CashSession/CashCloseSafetyHttpTests.cs
  - tests/Clients/StaticApps/cash-session-closed-summary.test.js
  - docs/domain/cash-session-design.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. CashSession HTTP testleri, ApiConventions mimari testi ve istemci testleri (tests/Clients/StaticApps)

## Out of scope

- Yönetim ekranı tablosu ve Reporting modülü; sayım ekranını ya da düzeltme hakkını kısıtlamak; `cash.cash_counts` şeması ve migration; sayım başına ayrı denetim olayı

## Dependencies

- V1-RMD-1140

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None
