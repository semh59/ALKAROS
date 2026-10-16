# V1-RMD-579 - Yönetim panelinde performans bütçesi ekranı

- Task ID: V1-RMD-579
- Status: Planned
- Assignee: None
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Yetkili kullanıcı performans bütçelerini yönetim panelinden görür, değiştirir ve siler (V1-RMD-570 uç noktaları üzerinden, Türkçe metinlerle).

## Owned surface

- `plan/v1/remediation/V1-RMD-579-performance-budget-screen.md`
- `evidence/V1-RMD-579/**`
- `src/Clients/PosTerminal/src/features/management-performance-budgets/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/sections.ts - yalnız yeni sekme kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız yeni sekme adı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Bütçe listesi, ekleme/düzenleme alanı, silme; tüm metinler Türkçe, hata mesajları sözlükten.
- Yetkisiz kullanıcıya düzenleme düğmeleri gösterilmez.
- Uç nokta sözleşmesi: `GET/PUT/DELETE /api/v1/management/observability/performance-budgets/` (PUT gövdesi `{metric,name,p95Ms}`, DELETE sorgu `metric` ve `name`); geçersiz değer 400, yetkisiz değişiklik 403.

## Out of scope

- Yeni ölçüm türü eklemek.

## Dependencies

- V1-RMD-570

## Acceptance evidence

1. Bileşen testleri, `tsc --noEmit` ve `oxlint` yeşil; mutasyon kanıtı `evidence/V1-RMD-579/` altındadır.

## Handoff

- None
