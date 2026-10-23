# qa-ux-tasarim - V1-RMD-600 (SHA e00a61c0)

Kapsam: M=71 sayıldı, 18 kalem UX açısından incelendi (PosTerminal catalog 7 dosya, Host Experience/Catalog 4, E2E 01 ve 22 okundu; 26, 27, Waiter 01 yalnız adı/kapsamı), 53 kalem UX dışı. Liste dışı okunanlar: routes/workspace.tsx, Cashier.tsx, strings.ts, design-system/primitives.*, Cashier cashier-app.js/.css, WaiterPwa menu.js, waiter-app.js, api.js, waiter-app.css. İngilizce sızıntı taraması: görünen metinde yok. Çalıştırma/Playwright yok; hepsi kod okuması.

| No | Önem | Kanıt | Sorun / öneri |
|---|---|---|---|
| U-001 | Yüksek | CatalogManagementStore.cs:208, :232-235; DualScreenStore.cs ~215 (`current_price IS NOT NULL`) | UI'dan açılan ürüne (current_price yalnız ürün formundan, fiyat satırı yok) "yarından geçerli" fiyat eklenince bugün geçerli satır olmadığından current_price NULL olur; ürün bugün kasa/garson listesinden düşer, uyarı yok (aynı kök backend B-002). Öneri: mevcut fiyatı otomatik satıra çevir; ekranda "bugünkü fiyat korunur". |
| U-002 | Yüksek | CatalogWorkspace.tsx:96-98, :162-164; primitives.css:77-85 (backdrop z-index 100, %72 opak) | Sunucu reddi (SKU/fiyat çakışması) modal açıkken hata banner'ı modalın arkasında kalır; kullanıcı hiçbir şey görmez. Hata modal içinde (ValidationSummary) gösterilmeli. |
| U-003 | Yüksek | CatalogWorkspace.tsx ~100 (`/409\|conflict\|overlap\|duplicate/i`); ApiErrorCatalog.cs:117,121 | Regex İngilizce kelime arıyor, sunucu mesajı Türkçe → çakışma dalı hiç çalışmıyor; "güncel veriyi yenileyin" rehberliği verilmiyor; 400'de "İstek doğrulanamadı." yönlendirmiyor. status===409 ile dallan, her kod için yönlendirici metin. |
| U-004 | Yüksek | CatalogWorkspace.tsx ~212 (toCreateInput: categoryId \|\| null, taxProfileId \|\| null, displayOrder 0), ~187-190; DualScreenStore.cs:185-186 (COALESCE 'Diğer', COALESCE vat 0) | Ürün formunda kategori alanı yok (KDV seçimi de yok: bilinen) → UI'dan açılan her ürün "Diğer" sekmesine düşer, KDV 0 sayılır. Formda kategori ve KDV seçimi; varsayılan kategoriden/son üründen. |
| U-005 | Orta | waiter-app.js:268-275; menu.js ~28; api.js:29 | Garson ilk açılışta katalog alınamazsa hata/yeniden dene yok; boş ekranda "Aramanıza uyan ürün yok" yanıltıcı. Ayrı mesaj + "Yeniden dene". |
| U-006 | Orta | cashier-app.js ~549-565 | Vanilla kasada arama/kategori sonucu boşsa ızgara tamamen boş, boş durum mesajı yok (React kasada var, Cashier.tsx:700). |
| U-007 | Orta | cashier-app.js ~549 (toLowerCase) | Vanilla kasa aramasında tr-TR yok: "SICAK" "Sıcak Çikolata"yı bulmaz (WaiterPwa ve React tr-TR kullanıyor). toLocaleLowerCase('tr-TR'). |
| U-008 | Orta | CatalogWorkspace.tsx ~139 (catalogRows fiyat satırı, active:true sabit) | Fiyatlar sekmesinde geçerlilik başlangıç/bitişi listede ve ayrıntıda yok; hangi fiyat bugün geçerli, hangisi yarın başlıyor anlaşılmaz; "Aktif" sabit ve yanıltıcı. "dd.MM.yyyy'den itibaren" + "Şu an geçerli / Yarın başlıyor" rozeti. |
| U-009 | Orta | CatalogWorkspace.tsx ~167 | "yalnızca yönetici ekleyip düzenleyebilir" diyor, ürün/fiyat düzenleme yok (bilinen); alt not geliştirici dili. Sade ve doğru metin. |
| U-010 | Orta | CatalogWorkspace.tsx ~172-173 | "Menüden kaldır (86)" restoran argosu; yeni çalışan bilmez. "Satıştan kaldır (bitti)". |
| U-011 | Orta | CatalogWorkspace.tsx ~99 → workspace.tsx:477 load() → setState("loading") | Her kayıttan sonra tüm çalışma alanı tam sayfa "Katalog yükleniyor"a dönüyor (yanıp söner, kaydırma/seçim kaybolur). Sessiz arka plan yenileme. |
| U-012 | Düşük | CatalogWorkspace.tsx ~163; workspace.tsx ~467 | "Son güncelleme" ham ISO damga. toLocaleTimeString("tr-TR"). |
| U-013 | Düşük | CatalogWorkspace.tsx ~135-141; Cashier.tsx formatMoney | Katalogda "150.00 TRY", kasada ₺ virgül: tutarsız. Ortak formatMoney. |
| U-014 | Düşük | placeholder'lar SALON, VAT10, MILK, SYC-S; "Vergi profili" | İngilizce kod örnekleri; "Modifikatör" vs garson/kasada "Seçenekli". Türkçe örnek, tek terim. |
| U-015 | Düşük | CatalogWorkspace.tsx ~122-125 | Başlangıç varsayılanı UTC "şimdi" (bilinen kayma); yarın için elle tarih+saat. |
| U-016 | Düşük | waiter-app.js:255 (CATALOG_REFRESH_INTERVAL_MS 15 dk) | Fiyat/"bitti" değişikliği açık garson uygulamasına 15 dk'ya kadar yansımaz. Yenile düğmesi / kısa aralık. |
| U-017 | Düşük | catalog.css ~38 (.65rem), menu.js .cat-label .67rem, .product-meta .71rem | Aktif/Pasif rozeti ve kategori etiketi ~10-11px. (Dokunma hedefleri 48px yeterli.) |
| U-018 | Düşük | tests/E2E "Menü ve katalog"/management/catalog araması 0 | Yönetim ekranının gerçek tarayıcı testi yok. |

Bilinen (kodla doğrulandı): KDV profili seçimi yok; fiyat formunda bitiş tarihi yok; ürün/fiyat düzenleme-pasife alma yok; liste 100 kayıt (catalogApi.ts ~31); yönetim ekranında "KDV dahil" yok (React kasada var Cashier.tsx:696); datetime-local UTC/yerel kayması. UI'dan fiyat zammı planlamak neredeyse hiçbir durumda güvenle yapılamıyor.

## Fırsat tablosu
| No | Fırsat | Gerekçe |
|---|---|---|
| V1-RMD-600-F001 | Fiyat zammı sihirbazı: ürün satırında "Fiyatı değiştir" → yeni fiyat + "Yarından itibaren / Şimdi / Tarih seç"; eski satırı sistem kapatır. | Şu an en az 5 adım, 3 alan, eski fiyat kapatılamaz; hedef 2-3 dokunuş. |
| V1-RMD-600-F002 | Başlangıç için yalnız gün (00:00 yerel) + "yarın" kısayolu. | Şu an UTC "şimdi", saat elle. |
| V1-RMD-600-F003 | Ürün formunda kategori ve KDV seçimi; varsayılanı kategori/son üründen. | U-004. |
| V1-RMD-600-F004 | Kod/SKU adı addan otomatik; kategori sırası otomatik. | Şu an elle. |
| V1-RMD-600-F005 | Ürün satırından hızlı "Fiyatı düzenle"/"Pasife al"; kayıttan sonra satırda "Yarın 45,00 ₺" etiketi. | Tek ekran. |
| V1-RMD-600-F006 | Kayıt sonrası "Kasada şöyle görünecek" önizlemesi. | Eğitim gerekmesin. |
| V1-RMD-600-F007 | Boş katalogda kılavuzlu ilk kurulum (Kategori → KDV → Ürün). | Şu an varsayılan Ürünler sekmesi, kategorisiz form. |
| V1-RMD-600-F008 | Kasa/garsonda "yeni fiyat bugün geçerli" bildirimi (15 dk gecikme yerine olay). | U-016. |

Temiz: Türkçe metin/etiket/aria-label, enum sözlükleri (productTypeLabels vb., sözlükte yoksa "Tanımsız"), ApiErrorCatalog Türkçe; durum kapsamı (yükleme, yetkisiz, çevrimdışı, hata, eski veri, çakışma, boş) ayrı; modal odak tuzağı/Escape/odak geri verme (primitives.tsx:123-148); axe testi (CatalogWorkspace.test.tsx:185); dokunma hedefleri 48px; tasarım sistemi bileşenleri, Button varsayılan type=button; Cashier/Waiter ızgara akışı; menüden kaldırma "Menüye geri al" ile tersine çevrilebilir.
Doğrulanamayan: U-002, U-011, U-005 gerçek tarayıcıda yeniden üretilmedi; U-001 koşturulmadı; E2E 26/27/Waiter 01 ayrıntılı okunmadı; küçük/ikinci ekran görsel doğrulama yok; kontrast ölçülmedi.
