# V1-IAM-003 - Implement device session lifecycle

- Task ID: V1-IAM-003
- Status: Done
- Assignee: opencode-v1-iam-003
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.1
- PDF:III.3

## Goal

Cashier ve waiter client'ları için device-bound session creation, expiry ve revocation davranışını uygulamak.

## Owned surface

- `src/Modules/Identity/DeviceSessions/**`, `tests/Modules/Identity/DeviceSessions/**`,
  `database/migrations/V1/V1-IAM-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- V0-ARC-002 tarafından izin verilen karma oturum belirteçleri, cihaz kimliği, süre sonu, iptal ve yeniden bağlanma
  davranışı.

## Out of scope

- Çevrimdışı order kuyruğu ve genel QR belirteçleri.

## Dependencies

- V1-IAM-001
- V0-ARC-002

## Deliverables

- `src/Modules/Identity/DeviceSessions/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- İptal edilen/süresi dolan oturumlar gönderilemez; ham belirteçler hiçbir zaman kalıcı olmaz; yeniden bağlanma yalnızca
  izin verilen sıraya alınmış işlemleri korur.

## Handoff

- V1-ORD-002
