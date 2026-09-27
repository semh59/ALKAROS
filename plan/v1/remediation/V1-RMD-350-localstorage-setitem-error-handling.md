# V1-RMD-350 - `localStorage.setItem` çağrıları artık hiçbir yerde ekranı çökertmiyor

- Task ID: V1-RMD-350
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) düşük seviye bulgusu: "`localStorage.setItem` hatalarının
yakalanmaması." Doğrulandı ve `V1-RMD-232`'nin (önceki bir oturum) `alkaros_cashier_parked` anahtarının OKUMA
tarafı için zaten düzelttiği TAM AYNI kusurun YAZMA tarafında hâlâ var olduğu, ayrıca PosTerminal'in kendi
`storage.ts`'sinin (neredeyse her rota `useState(() => savedId(...))` ile mount anında çağırıyor) HEM okuma HEM
yazma tarafının hiç korunmadığı bulundu — özel sekme, devre dışı depolama ayarı veya dolu kota bu çağrıları
senkron olarak fırlatabilir ve tüm ekranı çökertebilir.

## Owned surface

- `src/Clients/PosTerminal/src/storage.test.ts`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/storage.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
- `plan/v1/remediation/V1-RMD-350-localstorage-setitem-error-handling.md`

## In scope

1. `storage.ts`: yeni `tryGetItem`/`trySetItem` yardımcıları, `savedId`'nin HEM okuma HEM yazma tarafını
   sarmalıyor. `workspace.tsx`'in kendi üç doğrudan `localStorage` çağrısı (bir tanesi zaten `try/catch` içinde
   olsa da yanlış bir genel hataya yol açıyordu) bu yardımcılara yönlendirildi.
2. `cashier-app.js`: yeni `saveParkedTickets()` — `parkCurrentTicket`/`recallParkedTicket`'ın kendi doğrudan
   `localStorage.setItem` çağrılarını `try/catch` ile sarmalıyor, `loadParkedTickets`'ın (V1-RMD-232) zaten
   sahip olduğu korumanın yazma tarafını tamamlıyor.

## Out of scope

1. `localStorage.removeItem` çağrıları (örn. `Cashier.tsx:330`) — `removeItem` alan gerektirmediği için
   `setItem`'ın aksine kota/izin kısıtlamalarında pratikte neredeyse hiç fırlatmıyor; bu bulgu özellikle
   `setItem`'ı işaret ediyor.
2. `cash-session.js` — zaten kendi `try/catch` desenini kullanıyor (bu oturumun V1-RMD-343'ü bunu doğruladı).

## Dependencies

- None

## Acceptance evidence

- `npx vitest run src/storage.test.ts`: 5/5 yeni test geçti — `savedId`/`trySetItem`/`tryGetItem`'ın GERÇEK
  `Storage.prototype.getItem`/`setItem` fırlatmalarına karşı (gerçek `DOMException`, `vi.spyOn` ile) asla
  fırlatmadığı doğrulandı.
- Mutasyon kontrolü: `storage.ts` eski (korumasız) haline döndürüldü, 3/5 test GERÇEK bir `DOMException`
  fırlamasıyla kırmızıya döndü. Dosya geri yüklendi (`diff` ile bayt-bayt doğrulandı), paket yeniden 5/5 yeşile
  döndü.
- `npx tsc --noEmit`: sıfır hata. `npx vitest run` (PosTerminal'in tüm paketi): 32 dosya, 243 test, tümü yeşil
  (regresyon yok).
- `tests/E2E/Cashier` (specs/01, 05): 4/4 test geçti — `cashier-app.js`'in `saveParkedTickets()`
  yeniden düzenlemesi gerçek bir Chromium oturumunda regresyon yaratmadı.

## Handoff

- None
