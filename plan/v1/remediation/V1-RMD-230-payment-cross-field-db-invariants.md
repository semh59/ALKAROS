# V1-RMD-230 - Payment tablosuna eksik çapraz-alan DB kısıtları ve status index'i

- Task ID: V1-RMD-230
- Status: Done
- Assignee: claude-code-session_01Xsqh6z1RYhmFapKkHKoBmk
- Work type: implementation
- Surface state: Existing

## Goal

`payments.payments` tablosu (`database/migrations/V13/V13-PAY-001/
120-payments.up.sql`) yalnız tekil-alan CHECK kısıtları taşıyor
(`requested_amount > 0` vb.); `Payment.cs`'in kendi constructor'ında
zorunlu kıldığı ÇAPRAZ-ALAN invariant'ların (status↔tendered_amount null
eşleşmesi, status↔approved_amount null eşleşmesi, `approved_amount ≤
tendered_amount`, `change_amount == tendered_amount - approved_amount`)
HİÇBİRİ veritabanı seviyesinde yok — yalnız uygulama kodu koruyor. İKİ
bağımsız denetim ajanı (Payments modülü + veritabanı şeması, 2026-09-17
Kasa modülü kapsamlı denetimi) birbirinden habersiz aynı boşluğu buldu.
Ayrıca `status` alanında hiç index yok (operasyonel "bekleyen/
reconciliation gereken ödemeler" sorguları full-table-scan yapar) ve
`PostgresPaymentTests.GetByBillIdReturnsEveryPaymentAgainstThatBillMultiPaymentCap`
testinin adı/yorumu bir "bill payable üst sınırı" uygulandığını ima
ediyor ama böyle bir mekanizma kodda yok — test yalnız elle verilen
değerleri kanıtlıyor, cap'i kendisi taklit ediyor.

## Owned surface

- `database/migrations/V1/V1-RMD-230/**` (yeni migration — yalnız EKLE
  edici (additive) CHECK kısıtları ve index; mevcut 120 migration dosyasını
  değiştirmez).
- `evidence/V1-RMD-230/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Payments/PaymentAggregate/PostgresPaymentTests.cs,
  tests/Modules/Payments/PaymentAggregate/ALKAROS.Payments.PaymentAggregate.Tests.csproj
  (V13-PAY-001 sahipliğinde kalır) — yalnız yeni CHECK kısıtlarını
  doğrulayan testler eklenir, yanıltıcı test adı/yorumu netleştirilir ve
  csproj'a yeni 121 migration fixture dosyası eklenir; mevcut testler/
  fixture zinciri değişmez.
- database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs (paylaşılan) —
  migration 121 kaydı (V11-INV-009 emsaliyle aynı desen; yalnız yeni
  pozisyonun eklenmesi, mevcut 001-120 kayıtları değişmez).

## In scope

1. Yeni bir migration (sıradaki numara) ile şu CHECK kısıtlarını eklemek:
   - `(status = 'Initiated') = (tendered_amount IS NULL)`
   - `(status = 'Approved') = (approved_amount IS NOT NULL)`
   - `approved_amount IS NULL OR approved_amount <= tendered_amount`
   - `approved_amount IS NULL OR change_amount = tendered_amount - approved_amount`
2. `CREATE INDEX ix_payments_status ON payments.payments (status)`.
3. `PostgresPaymentTests`'teki yanıltıcı testin adını/yorumunu netleştirmek
   (gerçekte bir bill-payable cap'i test etmediğini açıkça belirtmek) VEYA
   gerçek bir cap mekanizması varsa (V13-ALC-001'in kapsamına giriyorsa)
   ona referans vermek — bu görev yalnız netlik sağlar, yeni bir cap
   mekanizması İCAT ETMEZ (o V13-ALC-001'in işi).
4. Yeni CHECK kısıtlarının gerçekten reddettiğini kanıtlayan testler
   (doğrudan SQL ile ihlal deneyerek, `PostgresPaymentTests`'teki
   `DatabaseRejectsAZeroRequestedAmountEvenIfSomeFutureCallerBypassesTheAggregate`
   deseniyle aynı).

## Out of scope

- `V13-ALC-001` (PaymentAllocation, gerçek bill-payable cap mekanizması) —
  bu görev onu icat etmiyor, yalnız test adının yanıltıcılığını düzeltiyor.
- `Payment.cs`'in kendi C# invariant'ları — zaten doğru, bu görev yalnız
  DB'ye aynı korumayı ekliyor (defense-in-depth).

## Dependencies

- V13-PAY-001

## Acceptance evidence

- Yeni migration, mevcut `payments.payments` verisini bozmadan uygulanır
  (tablo zaten boş/gerçek veri yok, ama migration additive olmalı).
- Doğrudan SQL ile her yeni CHECK kısıtının ihlalini deneyen testler →
  `PostgresException` ile reddedildiğini kanıtlar.
- `ManifestTests` → migration sayacı/id listesi güncellenmiş, regresyonsuz
  geçer.
- `ALKAROS.Payments.PaymentAggregate.Tests` (tamamı) → regresyonsuz geçer.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
