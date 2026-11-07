# V1-RMD-873 sonuç

Beş sekme listesinde (React: Cashier kategori rayı, BillSplitWorkspace modları, UserJourneysSection görünümleri, OnlineFoodHub bölümleri; vanilla: cashier-app.js kategori sekmeleri) bütün sekmeler Tab sırasındaydı. Artık yalnız seçili sekme tabindex=0, diğerleri -1; ok tuşuyla seçim değişince durak yeni sekmeye geçer (moveBetweenTabs zaten odak+seçim yapıyordu). Kasa kategori rayında seçili kategori katalogdan kaybolursa etkin kategori "Tümü"ye döner (ürün listesi, başlık, aria-selected ve durak birlikte), vanilla'da hiçbiri seçili değilse ilk sekmeye düşer: liste klavyeyle erişilemez kalmaz.
Kırmızı: red.log (4 başarısız). Yeşil: PosTerminal routes/billing/online-hub/user-journeys/design-system/telemetry 353/353 (36 dosya), StaticApps cashier-app+arrow-key-groups, tsc ve oxlint temiz.
Mutasyon (cp+cmp geri alındı): beş listede "hepsi tabindex 0" ve Cashier "Tümü" hiç durak değil: altısı da öldü.
Sınır: Cashier kaybolan kategori yedeği (activeCategory türetmesi) ve vanilla "hiçbiri seçili değil" yedeği için ayrı test yok; tarayıcıda elle denenmedi (jsdom).
