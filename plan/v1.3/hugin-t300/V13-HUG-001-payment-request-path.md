# V13-HUG-001 - Implement Token/Beko payment request path

- Task ID: V13-HUG-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.16
- PDF:II.3.12
- PDF:II.5.4
- PDF:III.19
- CORR:C98

## Goal

**Retarget notu (2026-09-17, `V0-GOV-064`/CORR:C98):** hedef cihaz Hugin
T300 değil, Token/Beko (300 TR / X30 TR) — `IntegrationHub.dll`
(`sendBasket`, `type`/`operatorId`). Task ID değişmedi.

Doğrulanmış Token/Beko contract'ına karşı onaylanmış ve reddedilen kart payment akışlarını uygulayın.

**Taslak notu (2026-09-18, `V13-GOV-006`):** `V0-HUG-001` hâlâ `Blocked`
olduğu için bu görev henüz `InProgress` alınamıyor. Semih'in onayıyla,
gerçek credential'dan önce yalnız dokümana dayalı, doğrulanmamış bir
taslak `evidence/V13-GOV-006/token-adapter-draft/` altında yazıldı (bu
görevin Owned surface'ının DIŞINDA, ayrı/standalone bir proje olarak).
Bu görev gerçekten başladığında o taslak referans alınabilir, ama
Acceptance evidence yine gerçek sandbox/cihaz transkripti gerektiriyor.

## Owned surface

- `src/Modules/Payments/Token/PaymentRequest/**`, `tests/Modules/Payments/Token/PaymentRequest/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Request mapping, correlation, Approved/Declined normalizasyonu ve arındırılmış provider evidence üretimi.

## Out of scope

- Payment/allocation mutasyonu, timeout/bilinmeyen kurtarma ve geri ödeme/iptal.

## Dependencies

- V13-PAY-001
- V13-PAY-002
- V0-HUG-001
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Payments/Token/PaymentRequest/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Contract testleri artı gerçek sandbox/cihaz transkripti, eşleşen referanslarla birlikte bir onaylanmış ve bir
  reddedilmiş isteği gösterir.
- Bu adapter PaymentAllocation veya Bill status değiştirmez; durable finalization sahibi `V13-PAY-004`dür.

## Handoff

- V13-HUG-002
- V13-PAY-003
- V13-PAY-004
- V13-FSC-002
- V14-ACC-006
