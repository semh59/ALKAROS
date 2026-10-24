# V1-RMD-738 - Billing doküman sapmaları ve KVKK runbook (603 B027, B039)

- Task ID: V1-RMD-738
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B027, B039: api-contract-standard Örnek 2, void-complimentary-discount-policy, remaining-gaps-register, migration-dependency-graph, module-dependency-rules BillStateChanged satırı, KVKK runbook known gap (billing serbest metinleri), bayat yorumlar ve çift DI kaydı, Billing Npgsql paket başvurusu.

## Owned surface

- `plan/v1/remediation/V1-RMD-738-billing-dokuman-ve-kvkk.md`
- `evidence/V1-RMD-738/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/ALKAROS.Billing.csproj
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/BillingModule.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/PaymentClosure/BillPaymentClosureModule.cs
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Doküman koda hizalanır; kod yorumlarında görev kimliği ve tarih temizliği yalnız dokunulan dosyalarda; PO kararı gereken kalemler (B015, B026, B028) belgelenir ve değiştirilmez.

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-738/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
