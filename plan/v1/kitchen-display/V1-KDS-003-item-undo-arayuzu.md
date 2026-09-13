# V1-KDS-003 - Mutfak ekranında kalem geri alma (undo) arayüzü

- Task ID: V1-KDS-003
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`V1-KIT-009`'un ürettiği kısa pencereli geri alma ucunu Mutfak ekranına
bağlar. Bir kalem ilerletildiğinde, pencere açıkken stepper'da az önce
geçilen aşama soluk ama tıklanabilir kalır (dokunulursa geri alır);
pencere kapanınca normal "geçmiş/pasif" görünümüne döner. Rakip
araştırmasının bulduğu genel KDS emsaliyle aynı desen (bkz.
`[[kitchen-redesign-requirements]]`, 2026-09-13 rakip analizi).

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/** (Sınırlı ek
  — V1-KDS-001 sahipliğinde kalan dosyalar) — stepper bileşenine geri
  alma penceresi göstergesi ve aksiyonu.

## Out of scope

- `V1-KIT-009`'un kendisi — bu görev yalnız zaten var olan uca bir
  istemci ekler.

## Dependencies

- V1-KIT-009
- V1-KDS-001

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → tüm proje yeşil.
- Semih'in elle deneyebileceği senaryo: bir kalemi ilerlet, pencere
  açıkken geri al butonunu gör ve kullan, kalemin önceki aşamaya
  döndüğünü doğrula; pencere kapandıktan sonra geri alma seçeneğinin
  kaybolduğunu doğrula.

## Handoff

- None
