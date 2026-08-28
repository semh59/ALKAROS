# V1-GOV-009 - Production experience capability realignment

- Task ID: V1-GOV-009
- Status: Done
- Assignee: /root/gov009_product_experience
- Work type: decision
- Surface state: Planned

## Source basis

- PO:2026-08-25

## Goal

V1'in masa, sipariş, mutfak/operasyon ve katalog yeteneklerini gerçek production kullanıcı yüzeylerine eşleyen ürün
deneyimi kararını üretmek; mevcut headless motor, mock prototip ve production dikey dilim ayrımını kapatacak kesin
API/UI görev zincirini belirlemek.

## Owned surface

- `docs/product/V1_PRODUCTION_EXPERIENCE_DECISION_2026-08-25.md`
- `evidence/V1-GOV-009/**`

## Dependencies

- V1-GOV-008
- V1-RMD-009

## Acceptance evidence

- Karar kaydı V1 masa, sipariş, katalog, mutfak ve operasyon yeteneklerini `production UI`, `production headless`,
  `mock-only` veya `missing` olarak dosya ve endpoint kanıtıyla sınıflandırır.
- Production shell bilgi mimarisi; kasa, masa planı, sipariş, mutfak/operasyon ve menü/katalog yüzeylerini rol ve cihaz
  sınırlarıyla tanımlar; her kritik akış için default, loading, empty, busy, success, error, offline, stale,
  unauthorized ve conflict durumlarını belirtir.
- Resmi güncel rakip dokümantasyonu yalnız benchmark bağlamı olarak kullanılır; ALKAROS davranışları Semih'in
  `PO:2026-08-25` kararı ve repository sözleşmelerinden türetilir.
- Eksik API, production shell, masa yönetimi, menü/katalog yönetimi ve bağımsız designer kabul turu birbirinden ayrı,
  tek-sahipli görevler olarak exact owned surface, dependency ve elle denenebilir kabul senaryolarıyla tanımlanır.
- Karar; seçilen yönü, reddedilen alternatifleri, etkilenen kesin task kimliklerini ve V1-RMD-010/V1-RMD-011/
  V1-GOV-004 üzerindeki sıra etkisini açıklar.
