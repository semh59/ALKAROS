# V1-RMD-578 - Zaman çizelgesinde EXPLAIN adımı ve istisna olayında rota adı

- Task ID: V1-RMD-578
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

V1-RMD-540 bulguları: (1) yavaş sorgu planlayıcısının kendi `EXPLAIN` sorgusu isteğin zaman çizelgesinde veritabanı adımı olarak görünüyordu; (2) `api.unhandled_exception` olayı rotayı "unmatched" yazıyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-578-timeline-explain-and-exception-route.md`
- `evidence/V1-RMD-578/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/TraceCorrelation/** - yalnız adım süzgeci
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/TraceCorrelation/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/ServerDetectors/** - yalnız istisna olayındaki rota
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/ServerDetectors/** - yalnız yeni test
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `EXPLAIN` ile başlayan komutlar zaman çizelgesi adımı olmaz.
- İstisna işleyicisi, istisnanın fırlatıldığı uç noktanın rota şablonunu olaya yazar.

## Out of scope

- Diğer izleme sorgularının süzülmesi.

## Dependencies

- V1-RMD-540
- V1-RMD-575

## Acceptance evidence

1. Gerçek Postgres testleri: EXPLAIN adımı yok, diğer komutlar adım; istisna olayı rota şablonunu taşır.
2. Mutasyon kanıtı `evidence/V1-RMD-578/` altındadır.

## Handoff

- None
