# V12-TGO-003 - Uber Eats Trendyol Go'ya sipariş durumlarını bildir

- Task ID: V12-TGO-003
- Status: Planned
- Assignee: Unassigned
- Work type: integration
- Surface state: Planned

## Goal

Restoran eylemlerini Trendyol Go durumlarına çevirmek: kabul (hazırlık süresiyle `picked`), hazır
(`invoiced`), restoran iptali (`UnSupplied`, 621–627 nedenleri), kendi kuryesi olan restoran için yola çıktı ve
teslim edildi. Sağlayıcı davranışı yalnız herkese açık Uber Eats Trendyol Go geliştirici belgesine dayanan doğrulanmamış taslaktır; gerçek kanıt V12-TGO-001'e aittir.

## Owned surface

- `src/Modules/OnlineOrdering/Providers/TrendyolGo/StatusSync/**`
- `tests/Modules/OnlineOrdering/Providers/TrendyolGo/StatusSync/**`
- `evidence/V12-TGO-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Host/Experience/OnlineOrdering/ (V12-ONL-003) — eylemlerin adaptöre yönlendirilmesi.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — kayıt ve deneme profili.

## In scope

1. Durum çağrıları outbox üzerinden, sağlayıcı deneme profiliyle gider; 401'de kimlik yenilenir.
2. Zorunlu başlıklar (`User-Agent`, `x-agentname`, `x-executor-user`) ve 50/10 sn hız sınırı.
3. İç iptal nedenlerinin 621–627 kodlarına eşlenmesi; eşlenemeyen neden gönderilmez, mutabakata düşer.

## Out of scope

- Fatura bağlantısı gönderimi (e-Arşiv hazır olunca ayrı görev).

## Dependencies

- V12-TGO-002

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-TGO-003/` altında.
- `task_scope_tool.py --task-id V12-TGO-003 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
