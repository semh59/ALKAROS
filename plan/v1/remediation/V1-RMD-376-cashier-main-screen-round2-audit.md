# V1-RMD-376 - Cashier ana ekran Tur 2 denetimi: modifikatör desteği + barkod/arama gerçekliği

- Task ID: V1-RMD-376
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 1: Cashier vanilla
ana ekran (`src/Clients/Cashier/wwwroot/**`). Tur 1'de bu beş boyut yüzeysel geçilmişti; bu
görev, gerçek kod/backend incelemesiyle desteklenen somut bulgular üretti.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/index.html
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.css
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/lib/seed.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/26-modifier-picker.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/27-barcode-scan-auto-add.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-376-cashier-main-screen-round2-audit.md`

## In scope

1. **[P1, Yüksek — rakip karşılaştırması; aynı zamanda T6 veri bütünlüğü] Ekran, ürünün
   `modifierGroups` alanını hiç okumuyordu.** `CatalogModifierGroupDto`'nun kendi doc yorumu:
   "the selection bounds are reported so a client can require a mandatory group, but the server
   does not enforce them yet" — yani zorunlu grup uygulaması BİLİNÇLİ olarak istemciye
   bırakılmış, ve bu ekran hiç katılmıyordu. Sonuç: zorunlu bir seçenek grubu olan bir ürün
   (örn. "Boy") hiçbir uyarı olmadan eklenip gönderilebiliyordu — mutfak hangi boyu
   hazırlayacağını asla öğrenemiyor, fiyat farkı (varsa) hiç yansımıyordu. Her gerçek rakip
   register (Toast/Square/Lightspeed) ve bu kod tabanının kendi WaiterPwa'sı bu anı satış
   noktasında yakalıyor. Yeni bir seçenek modalı (`#modifierModal`, WaiterPwa'nın
   product-sheet.js'iyle aynı mantık, bu ekranın tek-miktar/koltuksuz kapsamına indirgenmiş)
   eklendi: zorunlu grup karşılanmadan "Adisyona Ekle" kapalı kalıyor; seçilen modifikatörler
   ayrı bir satır olarak (birleştirilmeden) adisyona düşüyor ve dispatch payload'ına
   `modifiers: [{modifierId}]` olarak gidiyor (fiyat/isim sunucuda katalogdan otoriter olarak
   yeniden hesaplanıyor, `OrderItemModifierDto`'nun kendi doc yorumu).
2. **[P2, Yüksek — saha gerçekliği] Barkod taraması hiçbir şeyi otomatik yapmıyordu.** Bir
   barkod okuyucu odaklı alana karakterleri yazıp durur; hiçbir şey bu "tarama bitti" anına
   tepki vermiyordu, bu yüzden kasiyer HER taramadan sonra ekrana bakıp eşleşen kartı elle
   tıklamak zorundaydı — gerçek bir POS register'ın (Toast/Square) tam SKU eşleşmesinde
   otomatik eklediği, alanı temizlediği anı. Şimdi arama giriş debounce'u (120ms) tam bir SKU
   eşleşmesi bulduğunda ürünü otomatik ekliyor (zorunlu modifikatörü varsa modalı açıyor) ve
   arama kutusunu temizliyor.
3. **[P2, Orta — saha gerçekliği, ayrıca gerçek bir hata] Kod araması büyük/küçük harfe
   duyarlıydı ama yalnızca YARISI normalize edilmişti.** `query` zaten küçük harfe
   çevriliyordu, ama `p.code` hiç çevrilmiyordu — bu katalogdaki SKU'lar geleneksel olarak
   BÜYÜK harfli olduğu için (`E2E-KASA-DUSUK`), küçük harfle yazılan/taranan HİÇBİR kod araması
   asla eşleşmiyordu, sessizce. Düzeltildi.

## Out of scope

- P4 (öğrenme eşiği): yeni modifikatör modalı WaiterPwa'nın zaten kurulu desenini birebir takip
  ediyor (net "zorunlu" etiketleri, aynı buton yerleşimi) — ek bir öğrenme yükü getirmiyor.
- T7 (rol-arası haberleşme): "Servis eden" ataması (assignedWaiterUserId) yalnızca satış/bahşiş
  atfı için — garsonun fiziksel bir eylem yapması gerekmiyor (KASA-1 zaten kasiyerin kendisi
  tarafından doğrudan mutfağa gönderiliyor), bu yüzden gerçek zamanlı bir bildirim eksikliği bir
  bulgu değil, bilinçli bir tasarım.
- T8 (mobil/performans): bu ekran sabit bir kiosk terminali, mobil kullanım hedeflemiyor;
  gerçekçi bir restoran kataloğu ölçeğinde (~50-300 ürün) performans sorunu görülmedi.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/Cashier` tam paketi (64 test, 26 ve 27 numaralı yeni dosyalar dahil — toplam 7 yeni
  test): 64/64 geçti, regresyon yok.
- Mutation-check: modifikatör özelliği için `cashier-app.js`/`index.html`/`cashier-app.css`
  `git stash` ile geri alındı, 26 numaralı spesifikasyonun 3 testi de GERÇEKTEN kırmızı oldu;
  geri yüklenip tekrar 60/60 yeşile döndü. Barkod otomatik-ekleme + büyük/küçük harf
  düzeltmeleri ayrı ayrı, elle geri alınıp 27 numaralı spesifikasyonun 4 testinin de GERÇEKTEN
  kırmızı olduğu doğrulandı; geri yüklenip tekrar tam paket (64/64) yeşile döndü.
- `node --check` ile dosya sözdizimi doğrulandı; PosTerminal `corepack pnpm build` sıfır hata
  (Cashier E2E'nin `global-setup.js`'i her çalıştırmada vanilla dosyaları doğrudan kaynaktan
  kopyaladığı için ayrı bir build şart değildi, yine de doğrulandı).

## Handoff

- None
