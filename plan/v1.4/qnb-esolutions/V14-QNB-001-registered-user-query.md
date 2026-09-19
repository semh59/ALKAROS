# V14-QNB-001 - Implement QNB registered-user query

- Task ID: V14-QNB-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.17
- PDF:II.5.11
- PDF:III.20

## Goal

Doğrulanmış QNB contract'yi kullanarak zaman sınırlı e-Fatura kaydını status sorgulayın ve önbelleğe alın.

**Taslak notu (2026-09-18, `V13-GOV-007`):** `V0-QNB-001` hâlâ `Blocked`
olduğu için bu görev henüz `InProgress` alınamıyor. Semih'in onayıyla,
gerçek credential'dan önce yalnız dokümana dayalı, doğrulanmamış bir
taslak `evidence/V13-GOV-007/qnb-efatura-adapter-draft/` altında yazıldı
(bu görevin Owned surface'ının DIŞINDA, ayrı/standalone bir proje olarak).
Bu görev gerçekten başladığında o taslak referans alınabilir, ama
Acceptance evidence yine gerçek test tenant transcript'i gerektiriyor.

## Owned surface

- `src/Modules/Invoicing/Qnb/RegisteredUser/**`, `tests/Modules/Invoicing/Qnb/RegisteredUser/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kimlik doğrulama, sorgu eşleme, önbellek süresinin dolması, provider hatası ve eski önbellek ilkesi.

## Out of scope

- Invoice gönderimi ve gelen invoice alımı.

## Dependencies

- V0-QNB-001
- V14-CST-001
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Invoicing/Qnb/RegisteredUser/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Contract testleri kayıtlı/kayıtsız/hatayı kapsar; süresi dolmuş önbellek belge türünü sessizce seçmez.

## Handoff

- V14-QNB-002
