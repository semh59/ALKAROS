# V1-RMD-730 - KDV, fiş ve bölme doğruluğu (603 B012, B013, B025)

- Task ID: V1-RMD-730
- Status: Done
- Assignee: claude-code-session_019auoHryukF9ryWZPtnjznR
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B012, B013, B025: indirim KDV'si karışık oranda tek ağırlıklı oranla hesaplanıyor; bilgi fişinde KDV satırı yok ve ara toplam kalemlerle uyuşmuyor; bölmede son hedefin KDV'si brütü aşabiliyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-730-kdv-fis-ve-bolme-dogrulugu.md`
- `evidence/V1-RMD-730/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Receipt.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
- `database/migrations/V1/V1-RMD-730/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- İndirimin kalem payına orantılı KDV'si; fişte KDV satırı ve satır indirimi; ara toplam tutarlılığı; bölme son hedef KDV'sinin sınırlanması; karma oranlı ve sıfır olmayan KDV'li testler.

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-730/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
