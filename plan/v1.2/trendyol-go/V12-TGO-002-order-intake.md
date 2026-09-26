# V12-TGO-002 - Uber Eats Trendyol Go siparişlerini webhook ve çekmeyle al

- Task ID: V12-TGO-002
- Status: Planned
- Assignee: Unassigned
- Work type: integration
- Surface state: Planned

## Goal

Trendyol Go `created`, `cancelled`, `unsupplied`, `shipped` ve `delivered` olaylarını ortak gelen kutusuna
alan webhook alıcısı ile sipariş listesini çeken adaptörü yazmak. Sağlayıcı davranışı yalnız herkese açık Uber Eats Trendyol Go geliştirici belgesine dayanan doğrulanmamış taslaktır; gerçek kanıt V12-TGO-001'e aittir.

## Owned surface

- `src/Modules/OnlineOrdering/Providers/TrendyolGo/OrderIntake/**`
- `tests/Modules/OnlineOrdering/Providers/TrendyolGo/OrderIntake/**`
- `src/Host/Experience/OnlineOrdering/TrendyolGoWebhookEndpoints.cs`
- `evidence/V12-TGO-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — kayıt.
  - tests/Host/Experience/OnlineOrdering/ — uçtan uca testler.

## In scope

1. Webhook alıcısı: HTTPS, entegratör tanımındaki özel başlıkla doğrulama, olay kimliğiyle tekrar ayıklama, gövdeyi okumadan önce kimlik doğrulama.
2. `created` yükünün iç modele çevrilmesi: satırlar, ek ve çıkarılan malzemeler, müşteri notu, kupon ve promosyonlar, yemek kartı ödeme bilgisi; tutarların doğrulanması.
3. Çekme adaptörü (V12-ONL-009 portu): paket listesi, hız sınırı ve 429 davranışı.

## Out of scope

- Dışarı giden durumlar (V12-TGO-003), menü (V12-TGO-004).

## Dependencies

- V12-ONL-009
- V12-ONL-010
- V12-TGO-001

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-TGO-002/` altında.
- `task_scope_tool.py --task-id V12-TGO-002 --diff-base <InProgress commit>` exit 0.
- Belgedeki örnek yüklerle testler; webhook ve çekmeyle gelen aynı sipariş tek kayıt olur.

## Handoff

- None
