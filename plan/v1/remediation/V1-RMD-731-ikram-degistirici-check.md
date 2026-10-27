# V1-RMD-731 - Değiştiricili ikramda 036 CHECK uyumsuzluğu (603 B004)

- Task ID: V1-RMD-731
- Status: InProgress
- Assignee: claude-code-session_019auoHryukF9ryWZPtnjznR
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B004: ikram kaleminin indirimi fiyatlı değiştirici dahil satır toplamı iken 036 CHECK yalnız adet*birim fiyatı kabul ediyor; fiyatlı değiştiricili ikram Orders yazımında CheckViolation (400) veriyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-731-ikram-degistirici-check.md`
- `evidence/V1-RMD-731/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Orders/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/**
- `database/migrations/V1/V1-RMD-731/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/BillFoundation/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Önce gerçek PostgreSQL testiyle ihlalin Orders ve Billing yazımında yeniden üretilmesi; CHECK'in değiştirici toplamını kapsaması (geri alınabilir migration) veya ikramın doğru tutarla yazılması.

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-731/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
