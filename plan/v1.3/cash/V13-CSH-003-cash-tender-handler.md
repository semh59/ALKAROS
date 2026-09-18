# V13-CSH-003 - Implement cash tender handler

- Task ID: V13-CSH-003
- Status: Done
- Assignee: Codex
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:I.49
- PDF:II.2.7
- PDF:II.5.9
- PDF:III.9

## Goal

Cash tender için Payment, PaymentAllocation, CashTransaction ve change sonucunu tek transaction içinde oluşturmak.

## Owned surface

- `src/Modules/Cash/TenderHandler/**`, `tests/Modules/Cash/TenderHandler/**`
- Bu görev CashSession veya allocation persistence schema'sını değiştiremez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/Allocations/Persistence/IPaymentAllocationRepository.cs
  (V13-ALC-001 sahipliğinde kalır) — yalnız `AllocateAsync`/`GetByIdempotencyKeyAsync`'in
  caller'ın connection/transaction'ını kabul eden birer overload'ı eklendi
  (`IPaymentRepository.AddAsync`'in zaten aynı amaçla taşıdığı, "V13-PAY-003/
  CSH-003 compose with this" notuyla önceden işaretlenmiş overload deseninin
  aynısı); şema/mevcut davranış değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/Allocations/Persistence/PostgresPaymentAllocationRepository.cs
  (V13-ALC-001 sahipliğinde kalır) — yukarıdaki overload'ların gerçek
  implementasyonu; eski parametresiz overload'lar artık kendi bağlantısını
  açıp yeni overload'ı çağırıyor (davranış aynı, kod tekrarı kaldırıldı).
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/TransactionLedger/ICashTransactionLedgerRepository.cs
  (V13-CSH-002 sahipliğinde kalır) — yalnız `RecordAsync`'in caller'ın
  connection/transaction'ını kabul eden bir overload'ı eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/TransactionLedger/PostgresCashTransactionLedgerRepository.cs
  (V13-CSH-002 sahipliğinde kalır) — yukarıdaki overload'ın gerçek
  implementasyonu; eski parametresiz overload kendi bağlantısını açıp yeni
  overload'ı çağırıyor.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/ALKAROS.Cash.csproj
  — yalnız `ALKAROS.Payments.csproj`'a (ve onun üzerinden `ALKAROS.Billing.csproj`'a)
  bir `ProjectReference` eklendi; TenderHandler'ın Payment/Bill tiplerine
  erişimi için gerekli.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx — yalnız yeni
  `tests/Modules/Cash/TenderHandler/ALKAROS.Cash.TenderHandler.Tests.csproj`
  girişi eklendi.

## In scope

- Açık session doğrulaması, tendered amount, change, idempotency ve atomik ledger/allocation yazımı.

## Out of scope

- CashSession lifecycle, drawer hardware, bank-card ve meal-card işlemleri.

## Dependencies

- V13-PAY-002
- V13-CSH-001
- V13-CSH-002
- V13-ALC-001
- V1-FND-005

## Deliverables

- `src/Modules/Cash/TenderHandler/**` altında cash tender production code'u.
- Duplicate, closed-session, insufficient tender, rollback ve concurrent submit testleri.

## Acceptance evidence

- Başarılı komut tam olarak bir Payment, allocation ve CashTransaction üretir; change deterministik hesaplanır.
- Her failure penceresinde dört kaydın tamamı yoktur veya tamamı commit edilmiştir; kısmi cash posting oluşmaz.

## Handoff

- V13-PAY-003
- V13-FSC-002
