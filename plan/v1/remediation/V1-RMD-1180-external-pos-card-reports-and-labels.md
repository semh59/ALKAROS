# V1-RMD-1180 - Harici POS kartı: gün kapanışı ödeme yöntemleri satırı, iade ve son satışlar etiketleri (PosTerminal)

- Task ID: V1-RMD-1180
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1179 ile `ExternalCard` yöntemli ödemeler oluşur; rapor bunları bugün "geriye kalan" kuralıyla EFT/Havale satırına düşürür. Kod doğrulaması: kasa oturumu kapanışı (`CashCloseStore`, `cash-session.js`) yöntem kırılımı göstermez, yalnız nakit sayımıdır; yöntem kırılımı yalnız gün kapanışındaki "Ödeme yöntemleri" tablosundadır (`PaymentSettlementReportService` → PosTerminal `ClosingSection`). Bu görev:

1. `PostgresPaymentSettlementReportRepository.MethodSql` `payments.external_card_receipts` kaydı olan ödemeyi `ExternalCard` olarak ayırır (EFT'den önce, kart denemesinden sonra); `MethodCount` 5 olur (LIMIT sessizce satır düşürmesin); iade tutarı yöntemin kendi satırından düşer; `ReportingModels.cs` açıklaması beşinci ayırt edici kayıt olarak güncellenir. Günlük ciro toplamı değişmez (zaten tüm onaylı ödemeler).
2. PosTerminal: gün kapanışı ödeme yöntemleri tablosu `ExternalCard` için "Harici POS kartı" satırı ve toplamı gösterir (EFT/Havale ve Banka kartı ile karışmaz); iade paneli `ExternalCard` yöntemini "Harici POS kartı" diye adlandırır ve elle iade bekleyen satırda "Kart iadesi banka POS cihazında yapılır; çıkan iade fişinin numarasını yazın." notunu gösterir (iade fiş numarası alanı kart gibi çalışır, havale gibi "gönderildi" düğmesi çıkmaz); son satışlar listesi yöntemi "Harici POS kartı" yazar. Türkçe sözlükten geçer, ham enum gösterilmez.

## Owned surface

- `evidence/V1-RMD-1180/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reporting/Payments/PostgresPaymentSettlementReportRepository.cs
  - src/Modules/Reporting/Payments/ReportingModels.cs
  - tests/Modules/Reporting/Payments/ALKAROS.Reporting.Payments.Tests.csproj
  - tests/Modules/Reporting/Payments/Fixtures/PaymentReportTestDatabase.cs
  - tests/Modules/Reporting/Payments/PaymentSettlementReportTests.cs
  - src/Clients/PosTerminal/src/features/management-closing/models.ts
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.test.tsx
  - src/Clients/PosTerminal/src/features/recent-sales/RecentSalesWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (rapor depo testi gerçek PG, PosTerminal vitest), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. Testler: harici POS kartı ödemesi kendi satırında (tutar, adet), EFT ve kart satırına karışmaz; iadesi kendi satırından düşer; yöntem sayısı sınırı; PosTerminal satır etiketi, iade notu ve düğme davranışı, son satışlar etiketi
4. `node tools/client-build-gate/clientBuildGate.js --base <başlangıç commit'i>` exit code 0 ve çıktısı `evidence/V1-RMD-1180/` altında

## Out of scope

- Ödeme alma, migration, kasiyer (Cashier) ekranı ve belgeler
- Kasa oturumu kapanış raporuna yöntem kırılımı eklemek (oturum kapanışı nakit sayımıdır; kod doğrulaması RESULT'a yazılır)

## Dependencies

- V1-RMD-1179

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. İstemci üretim derlemesi çıktısı, plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- V1-RMD-1181
