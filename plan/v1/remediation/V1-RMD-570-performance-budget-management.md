# V1-RMD-570 - Performans bütçelerini yönetimden ayarlama

- Task ID: V1-RMD-570
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Performans bütçeleri veritabanında durur ama değiştirmenin yolu yoktur; yetkili kullanıcı için listeleyen, kaydeden ve silen uç noktalar eklenir. Yönetim paneli ekranı V1-RMD-579'a ayrıldı.

## Owned surface

- `plan/v1/remediation/V1-RMD-570-performance-budget-management.md`
- `evidence/V1-RMD-570/**`
- `src/Host/Observability/PerformanceBudgetEndpoints/**`
- `tests/Host/Experience/Observability/PerformanceBudgetHttpTests.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız yeni uç nokta kaydı (iki satır)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/idempotency_allowlist.json - yalnız yeni uç nokta girdileri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Bütçeleri listeleyen (rapor görüntüleme yetkisi), kaydeden ve silen (`observability.manage` yetkisi) uç noktalar ve her değişiklik için denetim kaydı.
- Yönetim paneli ekranı kapsam dışıdır (V1-RMD-579).

## Out of scope

- Yeni ölçüm türü eklemek.

## Dependencies

- V1-RMD-544
- V1-RMD-548

## Acceptance evidence

1. Yetkisiz rol 403 alır; değişiklik denetim kaydı bırakır.
2. Çıktılar `evidence/V1-RMD-570/` altındadır.

## Handoff

- None
