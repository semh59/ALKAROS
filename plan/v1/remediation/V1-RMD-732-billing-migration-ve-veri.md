# V1-RMD-732 - Billing migration ve veri bütünlüğü (603 B021, B022, B031)

- Task ID: V1-RMD-732
- Status: Done
- Assignee: claude-code-session_019auoHryukF9ryWZPtnjznR
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B021, B022, B031: 180 down çakışan bill_number varken çöküyor; 036 ve 108/123 korumasız ADD CONSTRAINT; 032/075/108/123/180 down testsiz; order.json 123 tabloları eksik; bills tutar CHECK'leri, cancelled_by FK, kalem silme CASCADE, tekrar eden indeks; masa işaretçisi yazımında yutulan hata.

## Owned surface

- `plan/v1/remediation/V1-RMD-732-billing-migration-ve-veri.md`
- `evidence/V1-RMD-732/**`
- `database/migrations/V1/V1-RMD-732/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitStore.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Yeni geri alınabilir migration (207 sonrası numara) ile kısıtlar; down testleri; masa işaretçisi yazımında uyarı günlüğü. Geçmiş migration dosyaları değiştirilmez (bayt hash).

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-732/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
