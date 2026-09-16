# V1-RMD-223 - Rapor sekmesi artık board'un 8sn'lik pollingine yakalanmıyor

- Task ID: V1-RMD-223
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **MEDIUM** bulgu:
`KitchenRoute`'un `onLoadPerformanceReport` prop'u her render'da yeni
bir satır-içi fonksiyondu (`useCallback`/`useMemo` ile sarılmamış).
Board'un kendi 8 saniyelik otomatik yenilemesi `load()`'u çağırıyor,
`load()` da her seferinde YENİ bir `client` nesnesi kuruyor
(`createKitchenOperationsClient`) — bu da `onLoadPerformanceReport`'un
her poll turunda yeni bir fonksiyon kimliği almasına yol açıyordu.
Mutfak ekranının rapor sekmesindeki `useEffect`'i bu prop'a bağımlı
olduğundan, rapor açıkken her 8 saniyede bir yeniden "yükleniyor"
durumuna düşüp yeniden çekiliyordu — workspace'in kendi yorumunun
vaat ettiği "rapor pollinge dahil değil" davranışının tam tersi.

## Owned surface

- src/Clients/PosTerminal/src/routes/workspace.tsx (ilgili modülün
  sahipliğinde)
- src/Clients/PosTerminal/src/routes/workspace.test.tsx (aynı modül)

## In scope

1. `KitchenRoute`: `clientRef` (bir `useRef`) her `client` değiştiğinde
   güncelleniyor; `loadPerformanceReport` artık `useCallback(...,[])`
   ile SABİT kimlikli — her çağrıda o anki `clientRef.current`'ı
   kullanıyor, ama fonksiyonun kendisi asla değişmiyor.
2. `onLoadPerformanceReport={client ? loadPerformanceReport :
   undefined}` — `client` null↔dolu arasında geçiş yapmadıkça (yalnız
   gerçek yükleme hatası/başarısı sırasında) aynı fonksiyon referansını
   döndürüyor, poll'un kurduğu yeni `client` nesneleri bunu artık hiç
   etkilemiyor.
3. Yeni test: rapor sekmesi açıkken sahte zamanlayıcıyla 8sn'lik
   poll'u ilerletip rapor ucunun yalnızca BİR kez çağrıldığını
   kanıtlıyor.

## Out of scope

- `load()`'un her poll turunda yeni bir `client` nesnesi kurması —
  bu, `loadKitchenRuntimeConfiguration`'ı da her seferinde yeniden
  çağırdığından (istasyon kimliği değişmiş olabilir), kasıtlı bir
  tasarım; bu görev yalnızca rapor callback'inin KİMLİĞİNİ
  stabilize ediyor, `client`'ın kendisini yeniden kullanılabilir
  yapmıyor.

## Dependencies

- V1-KDS-009

## Acceptance evidence

- `npx tsc --noEmit` (PosTerminal) → 0 hata.
- `npx vitest run` (PosTerminal, tüm proje) → 174/174 yeşil, yeni test
  dahil; revert-and-confirm ile testin gerçekten düzeltmeye bağlı
  olduğu kanıtlandı (poll sonrası rapor çağrı sayısı 1'den 2'ye
  çıktı).

## Handoff

- None
