# V1-RMD-727 - İade akışı sertleştirme (603 B002, B016, B017, B018)

- Task ID: V1-RMD-727
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B002, B016, B017, B018: bütçe ile tamamlanan iade tekrar gönderilirse ikinci iade oluşuyor; iade isteği idempotency anahtarı taşımıyor. İstek ve onay audit'siz, manuel iade tamamlamada isteyen=tamamlayan olabilir ve fiş numarası benzersiz değil, tamamlanmayan manuel iadede bütçe serbest kalmıyor, iade listesi 200 ile kesiliyor, ürün bazlı iadede son kalem kuruş farkı yüzünden reddediliyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-727-iade-sertlestirme.md`
- `evidence/V1-RMD-727/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- `database/migrations/V1/V1-RMD-727/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Payments/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/idempotency_allowlist.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- İade isteğine IdempotencyKey ve benzersiz dizin; istek ve onay audit'i; manuel iade tamamlayanın isteyenden farklı olması ve fiş numarası benzersizliği; bütçe serbest bırakma yolu; iade listesine billId, status ve sayfalama; son kalemde kalan tutar kuralı. Yeni migration numarası 207.

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-727/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
