# V1-RMD-577 - Hata kategorileri için Türkçe etiketler

- Task ID: V1-RMD-577
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Hatalar ekranında sunucunun ürettiği her hata kategorisi Türkçe etiketle görünür (V1-RMD-540 bulgusu: `db.slow_query_plan`, `db.n_plus_one` ve başka kategoriler "Bilinmiyor" görünüyordu).

## Owned surface

- `plan/v1/remediation/V1-RMD-577-error-category-labels.md`
- `evidence/V1-RMD-577/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-error-events/models.ts - yalnız kategori etiket sözlüğü
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-error-events/ErrorEventsSection.test.tsx - yalnız etiket testi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Sunucu kodunun hata olayı olarak ürettiği kategoriler (api, db, background, backup, integration, kitchen, payment, printer, browser) için etiket.
- Her etiketli kategori için test.

## Out of scope

- İzleme sisteminin kendi sorgularını zaman çizelgesinden süzme.

## Dependencies

- V1-RMD-540
- V1-RMD-575

## Acceptance evidence

1. Üretilen her kategori için etiket testi yeşil; mutasyon kanıtı `evidence/V1-RMD-577/` altındadır.
2. PosTerminal testleri, `tsc --noEmit` ve `oxlint` yeşil.

## Handoff

- None
