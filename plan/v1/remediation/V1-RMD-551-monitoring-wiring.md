# V1-RMD-551 - İzleme bağlama görevi (ortak dosyalar)

- Task ID: V1-RMD-551
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

İzleme görevlerinin dokunması gereken ortak dosyalar tek sahipli bu görevde bağlanır; diğer görevler yalnız kendi klasörlerine yazar.

## Owned surface

- `plan/v1/remediation/V1-RMD-551-monitoring-wiring.md`
- `evidence/V1-RMD-551/**`
- `src/Host/Observability/Wiring/**`
- `tests/Host/Observability/Wiring/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Observability/ObservabilityModule.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/sections.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/consistency_audit.py
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Modül ve test projesi kayıtları, `src/Host/Observability/**` için tutarlılık denetimi eşlemesi, yönetim menüsü girişi, izleme için ayrı küçük veritabanı bağlantı havuzu (ana havuzdan bağımsız, üst sınırlı).
- Sentetik veri işareti ve izleme yazımlarının yavaş-sorgu dedektöründen ayıklanması için ortak bayrak.

## Out of scope

- Dedektör ve olay mantığı.

## Dependencies

- V1-RMD-550

## Acceptance evidence

1. Ayrı havuz üst sınırı 4 bağlantıdır; izleme yazımları ana havuzu sayaçla ölçülen şekilde tüketmez.
2. Tutarlılık denetimi ve mimari testler sıfır hata.
3. Çıktılar `evidence/V1-RMD-551/` altındadır.

## Handoff

- None
