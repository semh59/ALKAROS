# V1-RMD-225 - Yedekleme durumu artık Mutfak ekranında görünüyor

- Task ID: V1-RMD-225
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **MEDIUM** bulgu:
`kitchenApi.ts`'in her `load()`'da çektiği son 20 yedekleme kaydı
(`KitchenData.backups`), `KitchenOperationsWorkspace.tsx`'te hiçbir
yerde render edilmiyordu — yalnızca üst bilgideki tek bir genel sağlık
noktasının rengini etkiliyordu. Başarısız bir yedekleme (hata mesajı
dahil) mutfak personeli/yöneticisi tarafından bu ekrandan asla
görülemiyordu. Panelin kendi CSS'i (`.kitchen-backup`,
`.kitchen-backup--failed`) zaten vardı, kullanılmıyordu — bu ekranın
daha önce bu paneli gösterdiğini ve sessizce kaldırıldığını gösteriyor.

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
  (ilgili modülün sahipliğinde)
- src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx
  (aynı modül)

## In scope

1. Yeni `HealthPanel` bileşeni — mutfak operasyon rayının (`aside`) en
   üstünde: veritabanı/disk/son-yedekleme durumu (`healthStatusLabel`
   ile) ve en son yedekleme kaydı (tür, zaman, durum; başarısızsa
   hata mesajı) — zaten var olan `.kitchen-health`/`.kitchen-backup`
   CSS'ini kullanıyor.
2. Yeni test: paylaşılan test fixture'ının zaten taşıdığı başarısız
   yedekleme kaydının artık gerçekten ekranda (hem "Başarısız" etiketi
   hem hata mesajı) göründüğünü kanıtlıyor.

## Out of scope

- Backend — `GetRecentBackupsAsync`/`/operations/backups/recent` zaten
  doğru veriyi dönüyordu, yalnızca istemci tarafı render eksikti.

## Dependencies

- V1-KIT-011

## Acceptance evidence

- `npx tsc --noEmit` (PosTerminal) → 0 hata.
- `npx vitest run` (PosTerminal, tüm proje) → 175/175 yeşil, yeni test
  dahil; revert-and-confirm ile testin gerçekten düzeltmeye bağlı
  olduğu kanıtlandı.

## Handoff

- None
