# V1-RMD-1157 - Yönetici kasa oturumları tablosu ilk sayımı, düzeltme sayısını ve çekmece sayaçlarını gösterir

- Task ID: V1-RMD-1157
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 16 (bulgu 2): "Gün sonu ve mutabakat > Kasa oturumları" tablosu yalnız Durum, Beklenen, Sayılan ve Fark gösterir; ilk sayım, düzeltme sayısı ve çekmece sayaçları yalnız kasiyerin kapanış özetinde ve denetim kaydındadır. Yönetici bunları kendi ekranında göremez.

1. Ödeme mutabakat raporunun `cashSessions` satırları, oturumun `cash.session.closed` denetim kaydından (kapanışta sunucunun gördüğü sayılar, tek kaynak) `firstCountedAmount`, `correctionCount` (`countEntries - 1`), `saleOpenCount`, `noSaleCount`, `noSaleKickFailedCount` ve `saleKickFailedCount` alanlarını taşır; kapanmamış oturumda ve bu alanlar yazılmadan önce kapanmış kayıtlarda `null` gelir (sıfır uydurulmaz).
2. Yönetim kapanış ekranı tablosu "İlk sayım", "Düzeltme", "Satışsız açma", "Açılamayan satışsız deneme" ve "Ödemede açılamayan darbe" sütunlarını gösterir; `null` "—" olur. Metinler Türkçe, 14 px altında yazı ve 48 px altında dokunma hedefi yoktur; tablo dar ekranda yatay kayar.
3. Sayaç tanımları V1-RMD-1156 ile aynıdır; Reporting yeni bir sayım sorgusu yazmaz, denetim kaydındaki değeri okur (`aggregate_type = 'CashSession'` ve oturum kimliğiyle, `ix_audit_events_aggregate` kullanılır). Alan adları Host ile Reporting arasındaki sözleşmedir: gerçek bir kapanış sonrası Reporting deposunu okuyan bir Host testi kilitler.

## Owned surface

- `evidence/V1-RMD-1157/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reporting/Payments/ReportingModels.cs
  - src/Modules/Reporting/Payments/PostgresPaymentSettlementReportRepository.cs
  - src/Clients/PosTerminal/src/features/management-closing/models.ts
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.tsx
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.test.tsx
  - src/Clients/PosTerminal/src/features/management-closing/management-closing.css
  - src/Clients/PosTerminal/src/strings.ts
  - tests/Host/Experience/CashSession/CashDrawerHttpTests.cs
  - tests/Modules/Reporting/Payments/ALKAROS.Reporting.Payments.Tests.csproj
  - tests/Modules/Reporting/Payments/PaymentSettlementReportTests.cs
  - tests/Modules/Reporting/Payments/Fixtures/PaymentReportTestDatabase.cs
  - docs/domain/cash-session-design.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Reporting, CashSession HTTP testleri ve PosTerminal istemci testleri

## Out of scope

- Sayaç tanımları (V1-RMD-1156); kapanış kaydının yazılması; kapanmamış oturumun canlı sayaçları; işletme günü kapanış kuralları

## Dependencies

- V1-RMD-1156

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. PosTerminal testleri, istemci üretim derlemesi ve CashSession HTTP testleri exit code 0

## Handoff

- None
