# V1-WTR-001 - Implement Waiter PWA session and offline queue

- Task ID: V1-WTR-001
- Status: Done
- Assignee: Antigravity-v1-wtr-001
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7
- PDF:I.14-I.15

## Goal

Personal device session, installable shell ve izinli offline operation queue davranışını uygulamak.

## Owned surface

- `src/Clients/WaiterPwa/SessionQueue/**`, `tests/Clients/WaiterPwa/SessionQueue/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Oturum depolama, sıraya alınmış işlem ID, yeniden bağlanma yeniden oynatma, süre sonu/iptal ve desteklenmeyen
  çevrimdışı reddetme.

## Out of scope

- Order-giriş widget'ları ve genel QR davranışı.

## Dependencies

- V1-IAM-003
- V0-ARC-002
- V1-FND-002
- V0-CMP-005

## Deliverables

- `src/Clients/WaiterPwa/SessionQueue/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tarayıcının yeniden başlatılması izin verilen kuyruğu korur; iptal edilen oturum tekrar oynatılamaz; desteklenmeyen
  sonlandırma hiçbir zaman çevrimdışı başarıyı bildirmez.
- PWA session ve offline durumları `V0-CMP-005` kararındaki waiter criteria ve device/browser matrix'ini karşılar.

## Handoff

- V1-WTR-002
