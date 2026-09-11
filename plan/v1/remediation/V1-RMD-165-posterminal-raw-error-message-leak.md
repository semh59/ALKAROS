# V1-RMD-165 - PosTerminal'de ham hata mesajı sızıntısı

- Task ID: V1-RMD-165
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümündeki bir
Medium bulguyu kapatır: "sunucunun ham `error.message`'ı doğrudan ekrana
basılıyor" (`docs/UI_STYLE_GUIDE.md` ihlali).

**Bulunan kalıp:** `reason instanceof Error ? reason.message : "<Türkçe
yedek>"`. `api.ts`'in `request()`'i her ağ/sunucu hatasını zaten Türkçe
mesajlı bir `ApiError`'a (`ApiError extends Error`) çeviriyor — ama `Error`
bunun ÜST sınıfı, dolayısıyla bu kontrol yalnız gerçek `ApiError`'ları değil,
bileşenin kendi kodundaki beklenmedik bir çalışma zamanı hatasını da
("Cannot read properties of undefined" gibi ham, İngilizce, teknik bir
mesaj) geçiriyordu. Aynı kusur, aynı dosyalarda, bir kez daha önce
`Cashier.tsx`'te bulunup düzeltilmişti (V1-RMD-114, 2026-09-06 bağımsız
denetim) — o düzeltmenin kendi yorumu bu görevin tam gerekçesini zaten
yazılı bırakmış: "a raw network failure (fetch() itself throwing, e.g.
'Failed to fetch') is a native, English, browser-generated message, not a
server-mapped Turkish one. Only ApiError's message is guaranteed to come
from the backend's own Turkish-mapped exception filter." O düzeltme
`Cashier.tsx`'e uygulanmış ama aynı dosya ailesindeki dört kardeşe hiç
yayılmamıştı.

**Düzeltme:** `reason instanceof Error` → `reason instanceof ApiError`,
10 yerde, 4 dosyada — `CustomerDisplay.tsx`, `NfcOrder.tsx` (ikisi de
müşteriye görünen ekranlar — QR/NFC self-servis menü ve teşhir ekranı),
`RelaySettings.tsx`, `ReservationStation.tsx`. Artık yalnız gerçek bir
`ApiError`'ın (her zaman Türkçe) mesajı gösteriliyor; başka herhangi bir
istisna güvenli, genel Türkçe yedek metne düşüyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-165-posterminal-raw-error-message-leak.md` (yeni)
- Sınırlı ek — aşağıdaki tüm yollar ilgili görevin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx (V1-RMD-097
    sahipliğinde) — üç yerde instanceof düzeltmesi.
  - src/Clients/PosTerminal/src/routes/NfcOrder.tsx (V12-NFC-003
    sahipliğinde) — iki yerde instanceof düzeltmesi.
  - src/Clients/PosTerminal/src/routes/RelaySettings.tsx (V12-QRT-003
    sahipliğinde) — dört yerde instanceof düzeltmesi.
  - src/Clients/PosTerminal/src/routes/ReservationStation.tsx (V1-CUI-006
    sahipliğinde) — bir yerde instanceof düzeltmesi.

## Out of scope

Frontend bölümünün kalan bulguları — ayrı görev/görevler.

## Dependencies

- V1-RMD-164

## Acceptance evidence

- `npm run typecheck` (src/Clients/PosTerminal) → `tsc --noEmit` temiz, 0
  hata.
- `npm run test` (src/Clients/PosTerminal, vitest) → **22 test dosyası,
  136 test — hepsi yeşil**, bu görevden önce de sonra da (regresyon yok).
- Repo genelinde `reason instanceof Error ? reason.message` ve benzeri
  kalıplar için yeniden tarama yapıldı — PosTerminal'in geri kalanı
  (`BillSplitWorkspace.tsx`, `CatalogWorkspace.tsx`,
  `KitchenOperationsWorkspace.tsx`, `FloorPlanWorkspace.tsx`,
  `TableWorkspace.tsx`, `workspace.tsx`, `Cashier.tsx`) zaten doğru
  `instanceof ApiError` desenini kullanıyordu — bu görevin kapsamı tam
  olarak bulunan 10 yerdi, fazlası veya eksiği yok.
- **Bu değişiklik için yeni bir test eklenmedi** — bilinçli bir karar,
  gerekçesi: `api.ts`'in `request()`'i her ağ/HTTP hatasını zaten
  `ApiError`'a çeviriyor, yani bu düzeltmenin gerçekte fark yarattığı
  durum yalnızca bileşenin KENDİ kodunda (ağ çağrısının dışında) çıkan
  gerçek bir çalışma zamanı hatası — bunu kara-kutu bir UI testinde doğal
  yoldan tetiklemek yapay bir hata enjeksiyonu gerektirir. `tsc --noEmit`
  ve mevcut 136 testin tam regresyonu, artı aynı dosyalarda zaten
  kanıtlanmış (V1-RMD-114) bir düzeltme desenini birebir tekrarlamak,
  yeterli güven verdi.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
