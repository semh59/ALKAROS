# V1-RMD-015 - Kitchen and operations production API

- Task ID: V1-RMD-015
- Status: Done
- Assignee: /root
- Work type: integration
- Surface state: Planned

## Goal

Mevcut kitchen, printing, backup-health ve audit read sözleşmelerini role-specific data minimization, capability-based
authorization ve fail-closed recovery davranışıyla production HTTP yüzeyine açmak.

## Owned surface

- PO:2026-08-31 kararıyla src/Host/Experience/KitchenOperations/** ve tests/Host/Experience/KitchenOperations/** yüzeyleri V1-RMD-065'e devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-015/**`

## Dependencies

- V1-GOV-010
- V1-RMD-009

## Acceptance evidence

- Station ticket query/transition, printer ve route query/update, print queue/Unknown recovery, authorized
  reason-required reprint, latest health/recent backup read ve permissioned backup command endpoint'leri versioned DTO
  ve gerçek HTTP contract testleriyle geçer.
- Missing/expired session `401`, authenticated fakat yetkisiz principal `403`, stale version `409` verir; Unknown print
  delivery otomatik reprint başlatmaz ve reprint actor/reason kaydı olmadan kabul edilmez.
- Kitchen DTO'ları gereksiz customer, personnel, token, secret, payment veya internal note verisi taşımaz; failed veya
  unknown health/backup durumu hiçbir koşulda success/healthy gösterilmez.
- `dotnet build ALKAROS.slnx -c Release` ve ilgili kitchen/operations API testleri exit code `0` verir. Mevcut migration
  değiştirilmez; yeni şema ihtiyacı exact path'li ayrı migration task'ına blocker olur.
- Semih, gerçek Host ve PostgreSQL üzerinde bir siparişten kitchen ticket üretir, item state ilerletir, Unknown print
  delivery oluşturur, supervisor reason ile reprint yapar ve failed health/backup durumunun açıkça başarısız kaldığını
  gözlemler.

## Handoff

- V1-RMD-019
