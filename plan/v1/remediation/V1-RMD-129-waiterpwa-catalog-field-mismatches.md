# V1-RMD-129 - Independent audit: WaiterPwa catalog field mismatches

- Task ID: V1-RMD-129
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09) bulguları: `waiter-app.js`, ürün ve
kategori verisini gerçek backend sözleşmelerinden (`CatalogProductDto`:
`productId/sku/name/categoryCode/categoryName/unitPrice/taxRate`) tamamen
farklı, hiç var olmayan alan adlarıyla okuyordu:

1. Fiyat: `p.currentPrice || p.price || 0` — ikisi de gerçek DTO'da yok
   (gerçek alan `unitPrice`), bu yüzden ürün kartları ve sepet her zaman
   `0,00₺` gösteriyordu.
2. Kategori filtresi: `p.categoryId === state.activeCategory` — gerçek
   DTO'da `categoryId` değil `categoryCode` var; ayrıca kategoriler ayrı
   bir `/catalog?category=all` çağrısıyla, `/catalog`'un hiç döndürmediği
   bir `{ categories: [...] }` şekli beklenerek çekiliyordu (gerçek şekil
   `CatalogPage`: `{ items, nextCursor }` — aynı, tek ürün uç noktasının
   kendisi). Sonuç: `state.categories` her zaman boştu, filtre çubuğu hiç
   görünmüyordu, ve görünse bile hiçbir üründe eşleşme olmuyordu.
3. Dosyanın kendi başlık yorumu `/catalog-management/categories`'i
   "yetkili uç nokta" olarak listeliyordu — ama o uç nokta
   `CatalogManagerEndpointFilter` ile yönetici çerezi gerektiriyor; sıradan
   bir garson oturumunun bu çerezi hiç yok, yani bu yol hiçbir zaman
   çalışamazdı bile.

## Owned surface

- `plan/v1/remediation/V1-RMD-129-waiterpwa-catalog-field-mismatches.md`
  (yeni)
- Sınırlı ek — aşağıdaki yol ilgili görevin sahipliğinde kalır (yol
  geri-tik olmadan yazıldı ki denetleyici bunu sahiplik iddiası olarak
  parse etmesin):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-008/V1-RMD-051/
    V1-RMD-066 sahipliğinde) — `loadInitialData`'daki ürün/kategori
    çekme ve eşleme mantığı, `renderProducts`'taki kategori filtresi
    ve dosyanın başlık yorumu düzeltildi. `loadTables`, `renderZones`,
    sepet/sipariş gönderme akışları dahil geri kalan her şey incelendi
    ve değiştirilmedi (gerçek alanlarla ya doğrudan ya da zaten var olan
    bir fallback zinciriyle çalıştıkları doğrulandı).

## In scope

1. Ayrı, yanlış-şekilli `/catalog?category=all` çağrısı tamamen
   kaldırıldı. Kategoriler artık tek `/catalog` (ürün) yanıtının kendi
   `categoryCode`/`categoryName` alanlarından, tekrarsız olarak türetiliyor
   — ikinci bir uç noktaya, gerçek sözleşmeden bağımsız olarak kayabilecek
   bir bağımlılık yok.
2. Ürün eşlemesi gerçek `CatalogProductDto` alanlarını kullanıyor:
   `price: p.unitPrice || 0` (önceden hep 0), dahili `categoryCode` alanı
   gerçek `categoryCode`'dan geliyor (önceden hiç var olmayan
   `categoryId`'den).
3. `renderProducts`'taki kategori eşleştirme koşulu `p.categoryCode ===
   state.activeCategory` oldu (önceden asla eşleşemeyen `p.categoryId`).
4. Dosya başlığındaki "Authoritative Endpoints" listesi güncellendi;
   `/catalog-management/categories`'in neden hiç uygun olmadığı
   (yönetici-özel yetkilendirme) not edildi.
5. Doğrulama: bu dosya için hiçbir JS test altyapısı yok (WaiterPwa
   düz statik dosyalar, `package.json`/test çalıştırıcısı yok) — yeni bir
   test altyapısı kurmak bu görevin kapsamı dışında. Bunun yerine: (a)
   `node --check` ile söz dizimi doğrulandı, (b) değişen eşleme mantığının
   birebir kopyası, gerçek `CatalogProductDto` şeklini taklit eden bir
   JSON ile ayrı, commit edilmeyen bir Node betiğinde çalıştırılıp
   doğrulandı (fiyatın `unitPrice`'tan geldiği, kategorilerin ürün
   listesinden tekrarsız türetildiği, filtrenin gerçekten daralttığı, ve
   eski alan adlarının gerçekten her zaman `0`'a düştüğü — ayrıca kanıt
   olarak).

## Out of scope

- **Masa tutarı (`t.currentAmount || t.amount || 0`, `loadTables`).**
  Gerçek `TableDto`'da (`TableManagementContracts.cs`) ne `currentAmount`
  ne de `amount` diye bir alan var — masa satırı yalnız `CurrentOrderId`
  taşıyor, tutarın kendisini değil. Bu, bir alan-adı yazım hatası değil:
  düzeltmek, `TableDto`'ya yeni bir alan eklemeyi (ör. o anki siparişin
  toplamı), bunu nasıl hesaplayacağını (hangi tabloya karşı, `Order.Total`
  ile aynı iş kuralını nasıl kopyalamadan/kaymadan koruyacağı) ve
  performans etkisini (masa listesi başına ek sorgu/join) gerektiriyor —
  ürün sahibiyle ayrıca görüşülmesi gereken bir tasarım kararı. Şimdilik
  UI zaten güvenli tarafta hata yapıyor ("Boş" gösteriyor, hayali bir
  tutar uydurmuyor).
- Audit'in "entirely disconnected, needs a business decision" mimari
  bulguları (#1 Kitchen fiziksel yazdırma hattı, #2 Menu modülü, #6
  Production/Purchasing, #7 BuildingBlocks ölü temel kütüphaneleri) — ayrı,
  kullanıcıyla görüşülecek kararlar.

## Dependencies

- V1-WTR-008
- V1-RMD-051

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js`: söz dizimi
  hatası yok.
- Commit edilmeyen, atılabilir bir Node betiğinde (`verify_v1rmd129.js`,
  scratchpad'te) değişen eşleme mantığının birebir kopyası gerçek
  `CatalogProductDto` şeklini taklit eden veriyle çalıştırıldı: fiyat
  `unitPrice`'tan geliyor (60, 280 — eskiden her ikisi de `0`'a
  düşüyordu), kategoriler ürün listesinden tekrarsız türetiliyor
  (`SOUP`→`Çorbalar`, `MAIN`→`Ana Yemek`), kategori filtresi listeyi
  gerçekten daraltıyor (3 üründen 2'si `SOUP` için), ve eski alan adlarının
  (`currentPrice`/`price`) gerçek şekilde her zaman `0`'a düştüğü ayrıca
  doğrulandı — hepsi geçti.
- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata (bu dosya derlemeye
  dahil değil, tam çözüm sağlamlığı için çalıştırıldı).
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 13 önceden var
  olan ihlal (değişmedi), yeni ihlal yok.

## Handoff

- None
