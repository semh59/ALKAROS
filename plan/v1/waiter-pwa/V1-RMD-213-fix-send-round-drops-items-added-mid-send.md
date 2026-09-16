# V1-RMD-213 - Gönderim sırasında eklenen kalem sessizce kayboluyordu

- Task ID: V1-RMD-213
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16, Garson+Mutfak derin denetimi)
bulduğu **CRITICAL** bulgu: `offline-queue.js`'in `sendDraft()`'ı
`targetItems = state.draft` ile canlı bir referans tutuyordu, kopya
değil. `postOrder()`'ın `await`'i sürerken (ağ gecikmesi 1-2 sn) garson
menüden yeni bir ürüne dokunursa `addToDraft()` bu yeni satırı doğrudan
`state.draft`'a (== `targetItems`, aynı dizi) ekliyor; `await` dönünce
`removeSentDraftLines(targetItems)` artık bu yeni satırın id'sini de
"gönderildi" sanıp `state.draft`'tan siliyor — o kalem sunucuya/mutfağa
hiç gitmeden, hiçbir hata/toast olmadan tamamen kayboluyor.

## Owned surface

- src/Clients/WaiterPwa/wwwroot/js/offline-queue.js (ilgili modülün
  sahipliğinde)

Sınırlı ek (yollar geri-tik olmadan):

- tests/Clients/StaticApps/support/fetchRouter.js (paylaşılan test
  desteği) — routes artık isteğe bağlı `delayMs` taşıyabiliyor, gönderim
  sırasında gerçek bir "hâlâ beklemede" penceresi açmak için.
- tests/Clients/StaticApps/waiter-app.test.js (ilgili görev sahipliğinde)
  — yarış durumunu kanıtlayan yeni test.

## In scope

1. `sendDraft()`: `targetItems = state.draft` yerine
   `targetItems = state.draft.slice()` — gönderim anındaki satırların
   dondurulmuş bir kopyası. `removeSentDraftLines` artık yalnızca o anda
   gerçekten gönderilmiş id'leri siler; `await` sırasında eklenen hiçbir
   satır bu kopyada yer almadığı için dokunulmadan kalır.
2. `fetchRouter.js`: bir route artık `delayMs` ile kendi cevabını
   erteleyebiliyor — testin `await postOrder(...)` süresi içinde gerçek
   bir eylem (ikinci bir ürüne dokunma) yapabilmesi için.
3. Yeni test: gönderim beklerken ikinci bir ürüne dokunulursa, gönderim
   bitince o ikinci ürün sepette kalmaya devam ediyor (kaybolmuyor).

## Out of scope

- `queueOrder`/`flushQueue` yolundaki aynı desen zaten `payload`'ı
  `await`'ten önce senkron olarak dondurduğu ve `removeSentDraftLines`'ı
  aynı `targetItems` ile çağırdığı için bu tek düzeltme onu da kapsıyor
  — ayrı bir görev gerekmiyor.

## Dependencies

- V1-WTR-053

## Acceptance evidence

- `node --check offline-queue.js` → temiz.
- `tests/Clients/StaticApps` (`npx vitest run`) → tüm testler yeşil,
  yeni yarış-durumu testi dahil; revert-and-confirm ile testin gerçekten
  düzeltmeye bağlı olduğu kanıtlanır.

## Handoff

- None
