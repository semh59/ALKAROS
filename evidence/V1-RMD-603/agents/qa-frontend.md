# qa-frontend - V1-RMD-603 Billing (SHA feb38368)

Kontrol: 28/28 istemci dosyası (21 kaynak + 7 test); 11 dosya satır satır, 10 hedefli grep, 7 StaticApps testi yalnız başlık/log. tsc exit=0; oxlint exit=1 (Billing dilimindeki tek bulgu BillSplitWorkspace.tsx:145); static-apps 120/120. Hepsi Kod okuması.

| No | Önem | Kanıt | Sorun | Etki |
|---|---|---|---|---|
| 1 | Orta | BillSplitWorkspace.tsx:55,97 | 409 sonrası "hesabı yenileyip karşılaştırın" diyor ama taslak kirli (dirty) olduğundan yenilenen tasarım uygulanmıyor; kayıt/sıfırlama eski billRowVersion ile gidiyor. Test kurtarmayı sınamıyor. | Çakışma sonrası her kayıt 409, çıkış yalnız sayfadan ayrılmak |
| 2 | Orta | BillSplitWorkspace.tsx:149,153,73,77 | Alanı boşaltmak Number('')=0 üretir, doğrulama 0'ı hata sayar; sahip çıkarılamaz. | Alan bir kez yazılıp silinirse kayıt bloke |
| 3 | Orta | workspace.tsx:375,398 | ?orderId= açılışta billId değişince load kimliği değişir, effect ikinci kez aynı POST'u atar (sunucu idempotent); loading titrer; yarış koruması yok. | Gereksiz istek, eski yanıt yenisini ezebilir |
| 4 | Orta | split-payment.js:300-314,264,459,505 | refreshSummary amountDraft'a dokunmuyor; kısmi tahsilattan sonra alan eski değerde; submitEqualSplit her seferinde kalan/splitCount: 100/3 -> 33,33+33,33, 0,01 açık. | Son kişide kuruş farkı, elle düzeltme |
| 5 | Orta | split-payment.js:188-189; index.html main aria-live | Onay beklerken 5 sn'de bir tüm kart innerHTML ile yeniden çiziliyor; odak düşer, ekran okuyucu tekrar okur; yoklama .catch yok. | Müdür onay notu yazarken odak kaybı (WCAG 2.4.3/4.1.3) |
| 6 | Orta | cash-session.js:532,252 | Meşgul yeniden çizim ve hata sonrası tutar/açıklama sıfırlanıyor; yön/tutar değişince idempotency anahtarı sıfırlanmıyor (tahsilat akışı yapıyor). | Kayıp girdi; aynı anahtarla farklı içerik |
| 7 | Orta | customer-accounts.js:137,145,158,187,209,230 | Hızlı iki müşteri seçiminde eski yanıt statement'ı ezer; tahsilat selectedId'ye yazılır, ekranda diğer müşterinin borcu; busy render edilmiyor. | Yanlış müşteriye tahsilat riski |
| 8 | Orta | split-payment.js:112, cash-session.js:80,353, customer-accounts.js:75 | body.error.message doğrudan basılıyor (UI_STYLE_GUIDE'a aykırı); sunucuda İngilizce mesaj: BillingSplitApplication.cs:177 "Discount request was denied."; PosTerminal RefundPanel sabit cümle. | İngilizce sızıntı, üç istemcide farklı davranış |
| 9 | Orta | split-payment.css:104, cash-session.css:123 | input:focus { outline: none } ve :focus-within yok. | Klavye odağı görünmez (WCAG 2.4.7) |
| 10 | Orta | split-payment.css:87 (24px onay kutusu), cash-session.css:146-151,218,240 | 48px altı dokunma hedefleri (kupür sayacı, tek tutar geçişi, giriş/çıkış sekmesi, EFT onay kutusu). WaiterPwa temiz. | Kasada dokunmatik hatası |
| 11 | Orta | split-payment.js:199-221, cash-session.js:163, customer-accounts.js | 500/ağ kesintisi "Oturumunuz sona erdi" giriş ekranı gösteriyor; çevrimdışı durumu yok; fetch'te zaman aşımı yok, busy sonsuza kalır. | Yanıltıcı yönlendirme, takılma |
| 12 | Orta | split-payment.js refundAction/renderRefundSection | Cashier iptal/iade/onay/limit düğmelerini herkese gösterip sunucu 403'üne bırakıyor; kendi iade isteğini onaylama düğmesi de görünür; PosTerminal capabilitySet ile tutarsız. | Boş düğme, eğitim yükü |
| 13 | Orta | grep split-design Cashier/WaiterPwa = 0 | Bölme: PosTerminal sunucu tasarımı, Cashier yerel submitEqualSplit; kaydedilen dağıtımı Cashier tahsilatı kullanmıyor; WaiterPwa'da bölme/ödeme yok. | İki ayrı yol, dağıtım elle yeniden girilir |
| 14 | Düşük | BillSplitWorkspace.tsx:145 | oxlint label-has-associated-control (bilinen listede yok, yeni). | a11y lint temiz değil |
| 15 | Düşük | BillSplitWorkspace.tsx:124,126,131 | tablist'te ok tuşu/aria-controls/tabpanel yok; bileşen içi main ikinci landmark olabilir; Cashier radiogroup/tablist ok tuşu yok. | Klavye/okuyucu tutarsızlığı |
| 16 | Düşük | BillSplitWorkspace.tsx:86; RefundPanel itemsInvalid | owners.find(...)! kaybolan anahtarda hata; itemsInvalid iken düğme sessizce pasif, satır içi mesaj yok. | Neden pasif bilinmiyor |
| 17 | Düşük | refundApi.ts describeRefundFailure; BillingSplitApplication.cs:581-585 | BILL_HAS_PAYMENT, UNSUPPORTED_BILL_STATE genel 409 cümlesine düşüyor. | Yönlendirme kayboluyor |
| 18 | Düşük | RefundPanel money TRY sabit; toLocaleTimeString('tr-TR'); split-payment.js:1345; cash-session.js:495,711,712 | timeZone Europe/Istanbul verilmiyor. | Yanlış cihaz saatinde yanlış saat |
| 19 | Düşük | RefundPanel load/run | Eşzamanlı load sıralanmıyor; düğmeler yalnız render sonrası pasifleşiyor, ref koruması ve idempotency anahtarı yok. | Çift tıkta çift istek (sunucu reddeder) |
| 20 | Düşük | RefundPanel itemsReady (5 alan) | Ürün iadesinde adet, ödeme, stok, gerekçe elle; tek seçenekse otomatik seçilebilirdi. | Ürün ilkesi (Fırsat) |
| 21 | Düşük | bill.js aria-label Azalt/Artır | Ürün adı yok; fireCourse çift dokunuş koruması yok, state.table.id null olabilir. | Ekran okuyucuda belirsiz |
| 22 | Düşük | customer-accounts.js:207 | Boş limit 0 olarak gönderilir, vade NaN olabilir. | Yönetici akışında limit sıfırlanır |
| 23 | Orta | grep cash-session/customer-accounts tests = yalnız telemetry + E2E 05; axe StaticApps = 0 | Cashier cash-session.js ve customer-accounts.js için birim/axe testi yok; split-payment.js için axe yok; WaiterPwa bill.js testsiz. | 5, 6, 7, 9, 10 yakalanmaz |

Temiz: tsc exit=0; static-apps 120/120; tenderIdempotencyKey ağ hatasında korunuyor/değişimde sıfırlanıyor; yıkıcı eylemler iki adımlı; RefundPanel sunucu gövdesi taşımıyor; para biçimi Intl tr-TR TRY; escapeHtml tutarlı; WaiterPwa bill.js sunucu toplamları, yarış korumalı loadOrder, 48px; sunucu sözleşmesi (yollar, expectedBillRowVersion, IdempotencyKey) uyumlu.

Doğrulanamayanlar: cash-movements ucunun IDEMPOTENCY_KEY_REUSED döndürüp döndürmediği; yalnız payments.refund.approve sahibinin liste/tamamlama erişimi; Cashier statik sayfalarında data-telemetry-id; PosTerminal vitest billing koşusu runs/ içinde yok; bilinen kayıtlarla eşleştirme yapılmadı.
