# V12-TGO-005 - Uber Eats geçiş modelindeki sipariş alanlarını destekle

- Task ID: V12-TGO-005
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: integration
- Surface state: Planned

## Goal

Uber Eats geçişi (ilk aşamada Bilecik) sipariş modelini değiştiriyor: sipariş kodu 3'ten 5 karaktere çıkıyor,
adres alanları maskeleniyor, siparişler otomatik `Invoiced` oluyor, sesli arama için dinamik numara ve PIN
geliyor. Adaptör iki modeli de kaldırır. Sağlayıcı davranışı yalnız herkese açık Uber Eats Trendyol Go geliştirici belgesine dayanan doğrulanmamış taslaktır; gerçek kanıt V12-TGO-001'e aittir.

## Owned surface

- `evidence/V12-TGO-005/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/Providers/TrendyolGo/ (V12-TGO-002, V12-TGO-003) — model alanları.
  - tests/Modules/OnlineOrdering/Providers/TrendyolGo/ — testler.
  - src/Modules/OnlineOrdering/Providers/Contracts/ (V12-ONL-007) — müşteri notu ve arama bilgisinin platformdan okunması.
  - src/Modules/OnlineOrdering/Yemeksepeti/Provider/ (V12-ONL-007) — Yemeksepeti notunun aynı noktadan okunması.
  - src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/ (V12-ONL-001) — not okumanın yükten ayrılması.
  - src/Host/Experience/OnlineOrdering/ (V12-OUI-001) — not uç noktasının arama bilgisini taşıması.
  - src/Clients/PosTerminal/src/features/online-operations/ (V12-OUI-001) — arama bilgisinin Türkçe gösterimi.
  - tests/Host/Experience/OnlineOrdering/ — testler.

## In scope

1. 5 karakterli sipariş kodu ve maskeli adres alanları hatasız işlenir.
2. Otomatik `Invoiced` geçişinde restoranın ayrıca `invoiced` göndermesi hata üretmez.
3. Dinamik telefon ve PIN operasyon ekranına Türkçe etiketle taşınır.

## Out of scope

- Diğer şehirlerin geçiş takvimi (Trendyol Go henüz açıklamadı).

## Dependencies

- V12-TGO-003

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-TGO-005/` altında.
- `task_scope_tool.py --task-id V12-TGO-005 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
