# V1-KDS-004 - Mutfak ekranında canlı senkron durumu rozeti

- Task ID: V1-KDS-004
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Planned

## Goal

`V1-KIT-010`'un açtığı `liveSyncEnabled` değerini Mutfak ekranında
görünür kılar. Kapalıyken üst barda "Canlı senkron kapalı — kalem hazır
olduğunda garsona bildirim gitmiyor" tarzı bir rozet gösterilir; açıkken
mevcut "Canlı" göstergesi (`.kitchen-live-dot`, `V1-KDS-001`) korunur.

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/** (Sınırlı ek
  — V1-KDS-001 sahipliğinde kalan dosyalar).

## Dependencies

- V1-KIT-010
- V1-KDS-001

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → **0 hata.**
- `cd src/Clients/PosTerminal && npx vitest run` → **Test Files 23
  passed (23), Tests 147 passed (147)** — tüm proje, izole değil (yeni
  test: `"shows a badge when kitchen.live_sync_enabled is off, and hides
  it when on"`; `kitchenApi.test.ts`'e `liveSyncEnabled` alanının
  gerçekten `/operations/live-sync` yanıtından okunduğunu kanıtlayan iki
  test eklendi — `true` ve `false` senaryoları ayrı ayrı, bağımsız
  denetimin bulduğu "yalnız true'yu kanıtlıyor, hardcode olsa da geçerdi"
  zayıflığı kapatıldı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- Uygulanan tasarım: `kitchenApi.ts`'in `load()`'ı artık 7 paralel GET
  yapıyor (yeni `/operations/live-sync` dahil), `KitchenData.liveSyncEnabled`
  alanına yazıyor. Kapalıyken üst barda "Canlı senkron kapalı" rozeti
  (turuncu/uyarı renginde) görünüyor; açıkken hiçbir ek gösterim yok,
  mevcut "Canlı" noktası (bağlantı canlılığı, farklı bir kavram)
  değişmeden kalıyor.
- Semih'in elle deneyebileceği senaryo: `kitchen.live_sync_enabled`
  ayarını kapat, Mutfak ekranını yenile, üst barda "Canlı senkron kapalı"
  rozetinin göründüğünü doğrula; ayarı aç, yenile, rozetin kaybolup
  "Canlı" göstergesinin kaldığını doğrula.

## Handoff

- None
