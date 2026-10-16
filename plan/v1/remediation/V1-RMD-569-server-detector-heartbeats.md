# V1-RMD-569 - Sunucu dedektör koşucularının nabzı

- Task ID: V1-RMD-569
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Derin sunucu dedektör koşucusu ve kayıt temizleme işi her turda nabız verir; sessiz kalan dedektör uyarısı bu işleri de kapsar.

## Owned surface

- `plan/v1/remediation/V1-RMD-569-server-detector-heartbeats.md`
- `evidence/V1-RMD-569/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/ServerDetectorsDeep/** - yalnız nabız çağrısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/ServerDetectorsDeep/** - yalnız nabız testi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `ServerDetectorsDeepRunner` her başarılı turdan sonra `DetectorHeartbeats` üzerinden nabız verir.
- Nabız adı sessiz dedektör raporunda görünür; koşucu durursa rapor onu sessiz diye işaretler.

## Out of scope

- Yeni dedektör eklemek.

## Dependencies

- V1-RMD-558
- V1-RMD-564

## Acceptance evidence

1. Koşucu bir tur çalıştırılınca nabız kaydı oluşur; hiç çalışmayan koşucu sessiz sayılır.
2. Çıktılar `evidence/V1-RMD-569/` altındadır.

## Handoff

- None
