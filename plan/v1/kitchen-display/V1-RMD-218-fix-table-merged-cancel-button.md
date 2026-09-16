# V1-RMD-218 - Masaya birleşen kartlarda tek iptal butonu yalnız ilk ticket'ı iptal ediyordu

- Task ID: V1-RMD-218
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **HIGH** bulgu:
V1-KDS-007'nin masaya göre birleştirdiği kartlarda (aynı masaya giden
iki farklı istasyonun ticket'ları) tek bir "Sorun bildir / iptal et"
butonu vardı, ama bu buton her zaman yalnızca `tickets[0]`'ı iptal
ediyordu. Personel tek buton gördüğü için tüm masa siparişinin iptal
edildiğini sanıyordu; oysa gruptaki diğer istasyonun kalemleri
Queued/Preparing durumda hazırlanmaya devam ediyordu.

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
  (ilgili modülün sahipliğinde)
- src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx
  (aynı modül)

## In scope

1. `cancelTarget` artık tek bir ticket değil, grubun tamamı
   (`readonly KitchenTicket[]`); `openCancelPrompt`/`OrderGroupCard`'ın
   `onCancel` prop'u da aynı şekilde güncellendi.
2. `submitCancel`: gruptaki her hâlâ-iptal-edilebilir (`status !==
   "Cancelled"`) ticket için ayrı ayrı `onTransitionTicket` çağrısı
   (her ticket'ın kendi `rowVersion`'ı olduğu için tek bir toplu
   çağrı mümkün değil) — kısmi başarısızlıkta, başarısız olanları
   isimleriyle bildiriyor, geri kalanı yine de iptal edilmiş oluyor.
3. Onay diyaloğu artık gruptaki TÜM ticket numaralarını gösteriyor,
   yalnız ilkini değil.
4. Yeni test: iki istasyona giden bir masa kartında iptal, her iki
   ticket için de `onTransitionTicket` çağrısı yapıyor.

## Out of scope

- Backend'de toplu (tek istekte çoklu ticket) iptal uç noktası — her
  ticket'ın kendi optimistic-concurrency alanı olduğu için bu, ayrı
  bir tasarım kararı gerektirir, bu görevin kapsamında değil.

## Dependencies

- V1-KDS-007

## Acceptance evidence

- `npx tsc --noEmit` (PosTerminal) → 0 hata.
- `npx vitest run` (PosTerminal, tüm proje) → 171/171 yeşil, yeni test
  dahil; revert-and-confirm ile testin gerçekten düzeltmeye bağlı
  olduğu kanıtlanır.

## Handoff

- None
