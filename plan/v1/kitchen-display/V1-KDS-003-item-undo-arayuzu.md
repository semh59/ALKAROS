# V1-KDS-003 - Mutfak ekranında kalem geri alma (undo) arayüzü

- Task ID: V1-KDS-003
- Status: Done
- Assignee: Claude Sonnet 5
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

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → **0 hata.**
- `cd src/Clients/PosTerminal && npx vitest run` → **Test Files 23
  passed (23), Tests 149 passed (149)** — tüm proje, izole değil (2 yeni
  test: geri al düğmesinin taze bir geçişten hemen sonra göründüğü ve
  `kitchen.advance`-yalnız — Mutfak Personeli — bir oturumda da
  çalıştığı; pencere geçtikten sonra düğmenin hiç görünmediği).
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- Uygulanan tasarım: `KitchenOperationsClient.undoItem`, yeni
  `onUndoItem` prop'u (yalnız `canAdvance` ile kapılı — `canOperate`
  gerekmiyor, undo bir iptal değil ileri-adım düzeltmesi). Her kalem
  satırında, `item.updatedAt`'tan itibaren `UNDO_WINDOW_MS` (10sn,
  backend'in `KitchenTicketItem.UndoWindow`'ıyla eşleşiyor) içinde
  görünen bir "Geri Al · Nsn" düğmesi; `now` state'i artık 1 saniyede
  bir tikliyor (önceden 15sn — geri sayımın gerçek pencereyle senkron
  kalması için). Backend'in kendi 409'u zaten otoriter — istemci
  yalnız butonun ne zaman gösterileceğine karar veriyor, ayrı bir
  doğrulama icat etmiyor.
- Semih'in elle deneyebileceği senaryo: bir kalemi ilerlet, pencere
  açıkken geri al butonunu gör ve kullan, kalemin önceki aşamaya
  döndüğünü doğrula; "Mutfak Personeli" izinli bir oturumda da aynı
  düğmenin çalıştığını doğrula; pencere kapandıktan sonra geri alma
  seçeneğinin kaybolduğunu doğrula.

## Handoff

- None
