# V1-RMD-379 - WaiterPwa Tur 2 denetimi: arama debounce'u + iki belgelenmiş/ertelenen gözlem

- Task ID: V1-RMD-379
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 4: WaiterPwa. Bu
istemci Tur 1'de zaten en olgun çıkan istemciydi; Tur 2'de de aynı sonuç doğrulandı — çoğu
gerçek rakip özelliği (modifikatör, kişi sayısı, kurs yönetimi, koltuk ataması, yardım çağrısı,
masa devri, iptal/ikram) zaten var, masa-başına taslak kalıcılığı (`alkaros_waiter_drafts_by_table`)
Cashier'ın Tur 2'de eklediği tek-taslak kalıcılığından daha ileri düzeyde.

Tek gerçek, DÜZELTİLEBİLİR bulgu: arama kutusunun debounce'u yoktu (T8). İki gerçek ama daha
büyük kapsamlı gözlem bulundu ve BİLİNÇLİ olarak bu görevin dışında bırakıldı (aşağıda
gerekçesiyle).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/waiter-app.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/specs/11-search-debounce.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-379-waiterpwa-round2-audit.md`

## In scope

1. **[T8, Orta — mobil/performans] `renderProducts()` tam listeyi HER tuş vuruşunda yeniden
   oluşturuyordu, debounce yoktu.** Cashier vanilla'nın Tur 2'de kapattığı aynı sınıftan bulgu
   (V1-RMD-376) — büyük bir kataloğu mütevazı bir telefon CPU'sunda her karakterde yeniden
   render etmek gerçek bir kekemelik riski. Aynı 120ms debounce eklendi.

## Out of scope

Aşağıdaki iki gözlem GERÇEK ama daha büyük kapsamlı — bilinçli olarak Semih'in kararına
bırakıldı:

- **[P1, rakip karşılaştırması] "Sık kullanılanlar/son eklenenler" hızlı-erişim listesi yok.**
  Toast/Square'in çoğu el terminali, en çok/en son eklenen ürünleri üstte sabitler. Menü zaten
  kategori grupları + hızlı arama sunduğu için etkisi sınırlı, ama gerçek bir gözlem.
- **[T7, rol-arası haberleşme — daha büyük kapsamlı] Yardım çağrısı tek yönlü, "gördüm/geliyorum"
  geri bildirimi yok.** Garson `/help-requests`'e POST atar, "Yardım çağrınız yöneticiye
  iletildi." toast'ı SUNUCUYA ULAŞTIĞINI doğrular ama bir yöneticinin GERÇEKTEN görüp/yanıtlayıp
  yanıtladığını asla bildirmez — garson ikinci bir çağrı yapmalı mı bilemez. PosTerminal'in
  Cashier.tsx'indeki "×" düğmesi de yalnızca YEREL bir gizleme, sunucuya bir onay göndermiyor.
  Gerçek bir çözüm yeni bir backend uç noktası + hub yayını + garson tarafında bir "onaylandı"
  göstergesi gerektiriyor — bu, Modül 3'te (split-payment) ertelenen "çapraz-rol keşif" sorunuyla
  aynı sınıftan, bu görevin kapsamının ötesinde bir backend özelliği.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/WaiterPwa` tam paketi (48 test, 11 numaralı yeni dosya dahil): tam sonuç aşağıda.
- Mutation-check: `waiter-app.js`'teki debounce `git stash` ile geri alındı, yeni test
  GERÇEKTEN kırmızı oldu (ilk assertion başarısız — arama anında filtreliyordu, debounce yoktu).
  `git stash pop` ile geri yüklendi, paket tekrar yeşile döndü.
- `node --check` ile dosya sözdizimi doğrulandı.

## Handoff

- None
