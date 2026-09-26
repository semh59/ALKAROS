# V1-RMD-315 - `alkaros.current-bill-id` çıkışta temizlenmiyordu, vardiya devrinde sızıyordu

- Task ID: V1-RMD-315
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K4 bulgusu: `workspace.tsx` (satır 288, 317, 344) `alkaros.current-bill-id` anahtarını `localStorage`'a yazıp okuyor (bir kasiyer bölünmüş ödeme ekranından ayrılıp geri döndüğünde aynı hesaba devam edebilsin diye), ama `Cashier.tsx`'teki `logout()` bu anahtara hiç dokunmuyordu. Sonuç: vardiya değişiminde bir terminalde çıkış yapılıp yeni kasiyer giriş yaptığında, önceki kasiyerin açık bıraktığı bölünmüş hesap ekranı otomatik olarak yeni kasiyere görünür/erişilir hâle gelebiliyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-315-current-bill-id-cleared-on-logout.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.logout-clears-storage.test.tsx
  (yeni dosya)

## In scope

1. `logout()` başarılı sunucu çağrısından sonra `localStorage.removeItem("alkaros.current-bill-id")` çağırır.

## Out of scope

- Kasa (vanilla Cashier) tarafındaki "beklet" (park) sepeti localStorage kalıcılığı — ayrı bir orta seviye bulgu, ayrı görev.
- `storage.ts`'in `savedId` ile yönettiği terminal kimliği — cihaza ait, kasiyer oturumuna değil; bilerek temizlenmiyor.

## Dependencies

- None

## Acceptance evidence

Yeni bir vitest testi (`Cashier.logout-clears-storage.test.tsx`, jsdom, gerçek `Cashier` bileşeni gerçek DOM'a render edilip gerçek "Oturumu kapat" düğmesine tıklanarak) `alkaros.current-bill-id`'nin çıkıştan önce set edildiğini, çıkıştan sonra `localStorage`'dan tamamen silindiğini (`null`) doğruluyor. Tüm PosTerminal vitest paketi çalıştırıldı: 29/29 dosya, 217/217 test yeşil (regresyon yok).

Mutasyon kontrolü: `logout()`'a eklenen `localStorage.removeItem(...)` satırı geçici olarak kaldırıldı — yeni test gerçekten kırmızı oldu (`expected '...' to be null`, `received` hâlâ eski değeri döndürdü); dosya `diff` ile birebir orijinaline geri getirildi, test tekrar yeşil.

## Handoff

- None
