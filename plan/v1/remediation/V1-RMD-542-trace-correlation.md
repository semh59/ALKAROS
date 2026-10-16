# V1-RMD-542 - İzleme kimliği zinciri

- Task ID: V1-RMD-542
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Bir düğmeye basıldığında başlayan izleme kimliği isteğe, veritabanı komutuna, olaylara ve denetim kaydına taşınır; mevcut `X-Correlation-Id` ile hizalanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-542-trace-correlation.md`
- `evidence/V1-RMD-542/**`
- `src/Host/Observability/TraceCorrelation/**`
- `tests/Host/Observability/TraceCorrelation/**`
- `src/Clients/PosTerminal/src/features/trace-correlation/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız ara katman ve servis kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/main.tsx - yalnız izleme kimliği yayılımının kurulumu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İstemci mevcut `X-Correlation-Id` başlığına W3C `traceparent` ekler; sunucu tek ara katmanla `Activity` açar ve kimliği günlüğe, hata olayına ve denetim kaydına yazar; denetim kaydı noktalarına ayrı dokunulmaz.
- Veritabanı komutları ve dış çağrılar alt aralık (span) olarak süreleriyle kaydedilir.

## Out of scope

- Dış izleme sunucusu kurulumu.

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. Bir düğme eyleminin kimliği istekte, olayda ve denetim kaydında aynıdır (uçtan uca test).
2. Alt aralık sayısı eylem başına en çok 50.
3. Çıktılar `evidence/V1-RMD-542/` altındadır.

## Handoff

- None
