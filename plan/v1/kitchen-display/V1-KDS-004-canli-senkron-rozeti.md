# V1-KDS-004 - Mutfak ekranında canlı senkron durumu rozeti

- Task ID: V1-KDS-004
- Status: Planned
- Assignee: Unassigned
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

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → tüm proje yeşil, yeni
  test dahil (rozetin `liveSyncEnabled: false` ile göründüğü, `true` ile
  görünmediği).
- Semih'in elle deneyebileceği senaryo: ayarı kapat, Mutfak ekranında
  rozetin göründüğünü doğrula; ayarı aç, rozetin kaybolup "Canlı"
  göstergesinin kaldığını doğrula.

## Handoff

- None
