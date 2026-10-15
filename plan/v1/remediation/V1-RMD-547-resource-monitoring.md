# V1-RMD-547 - Kaynak izleme

- Task ID: V1-RMD-547
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Konteyner kaynakları (işlemci, bellek, disk), gerçek zamanlı bağlantı kopmaları ve garson çevrimdışı kuyruk uzunluğu izlenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-547-resource-monitoring.md`
- `evidence/V1-RMD-547/**`
- `src/Host/Observability/ResourceProbes/**`
- `tests/Host/Observability/ResourceProbes/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız kaynak izleme kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Konteyner içi görünür kaynaklar; ana makine (Docker Desktop/WSL2) kaynakları konteynerden görünmez, bu sınır ekranda belirtilir.
- SignalR anormal kopma sayacı (genel hub süzgeci). Garson çevrimdışı kuyruk uzunluğu istemci bildirimi gerektirir ve V1-RMD-558 kapsamına taşındı.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. Disk %90 eşiği olay üretir.
2. Bağlantı kopma sayacı gerçek bir kopmada artar.
3. Çıktılar `evidence/V1-RMD-547/` altındadır.

## Handoff

- None
