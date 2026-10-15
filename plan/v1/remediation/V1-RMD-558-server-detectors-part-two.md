# V1-RMD-558 - Sunucu dedektörleri ikinci parça

- Task ID: V1-RMD-558
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Bağlantı havuzu doluluğu, kaçan arka plan işi, N+1 sorgu, yavaş sorgu ve sorgu planı hata olayı olarak yakalanır; izlemenin kendi yazımları ayıklanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-558-server-detectors-part-two.md`
- `evidence/V1-RMD-558/**`
- `src/Host/Observability/ServerDetectorsDeep/**`
- `tests/Host/Observability/ServerDetectorsDeep/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Havuz > %85, kaçan zamanlanmış iş (nabız), istek başına 20 sorgu (N+1, son 8 sorgu örneği), yavaş sorgu eşiği (>500 ms Warning, >2000 ms Error).
- QR tüneli (bağlayıcı süreci) kopma sinyali hata olayı olur.
- Sorgu planı (EXPLAIN) yalnız SELECT için ve sınırlı sürede alınır; izlemenin kendi yazımları yavaş-sorgu olayından ayıklanır; dış çağrı adresleri temizlenir.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-534

## Acceptance evidence

1. Havuz %90 doluyken 1 Warning olay; düşünce olay çözülür.
2. 20'den fazla sorgu yapan tek istek 1 N+1 olayı üretir ve son 8 sorguyu örnek olarak taşır.
3. İzleme yazımları yavaş-sorgu olayı üretmez (öz kirlenme testi).
4. Çıktılar `evidence/V1-RMD-558/` altındadır.

## Handoff

- None
