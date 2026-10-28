# Tasarımcı gözü: durumlar ve hata (kod okuma; WA=WaiterPwa/wwwroot/js, CA=Cashier/wwwroot, PT=PosTerminal/src)

Olumlu: WaiterPwa çevrimdışı kuyruk, tek tur gönderim kilidi, masa devri 409 yenilemesi; Cashier tahsilat idempotency anahtarı ve belirsiz ağ hatası mesajı (split-payment.js:639-720); PT RefundPanel.tsx:165 onay adımı.

Yüksek
- CA cashier-app.js:761,796-797 - releaseSharedTable hatası yok sayılıyor (yalnız console.error); KASA-1 serbest kalmazsa sonraki müşteri aynı siparişe karışabilir; operationId `${orderId}:submit` (:745) ikinci turda IDEMPOTENCY_KEY_REUSED 409 verebilir; kasiyere uyarı yok.
- PT management/http.ts:24 - Her çağrıda yeni randomUUID; stockApi.ts:44 recordWaste, purchase-invoices/api.ts:21 approve/fetch-qnb, management-staff-roles/api.ts:53 aynı kök; 8 sn zaman aşımında yeniden denemede yeni anahtar (atık ikilenebilir; stok servisi doğrulanamadı).

Orta
- WA bill.js:37,234 + tables.js:221-233 - loadOrder hatasında "Bu masada henüz sipariş yok"; dolu masa boş sayılıp menüye atılıyor.
- WA void-comp.js:87-91,163-167,247-251 - 409'da yalnız toast, sipariş yenilenmiyor (transfer.js:61 doğru yapıyor).
- WA pending-orders.js:89 - Onayla/Reddet 409'da bayat kart ekranda kalıyor.
- CA cash-session.js:257,541,310,626 - setBusy ekranı yeniden çizip tutar/açıklamayı sıfırlıyor; farklı tutarla yeniden denemede sunucu ilk satırı döndürürse yeni tutar kaybolabilir (sunucu davranışı doğrulanamadı).
- CA cash-session.js:187-191 - /active 404 dışındaki her hatada (5xx dahil) girişe dönülüyor, yeniden dene yok.
- PT AuthorizationDecisionsWorkspace.tsx:131-132,156,182 - Reddet / vekâleti geri al / sıkılaştırmayı kaldır tek tıkla, onay ve gerekçe yok.
- PT workspace.tsx:551,557 - Yetki ekranı yükleme hatasında bekleyen onaylar boşaltılıyor; otomatik yenileme ve 409 eşlemesi yok.
- PT PurchaseInvoicesSection.tsx:141-142 - Fatura onay/red onay adımsız; red gerekçesi istenmiyor.

Düşük: WA bill.js:46-52 kurs ateşlemede kilit/ilerleme yok; WA pending-orders.js:86 red gerekçesi sabit "Garson reddetti"; PT workspace.tsx:104 "Yeniden dene" sayfayı yeniden yüklüyor (durum kaybı); arka plan sekmede poll sürüyor (PT dinleyici yok, CA visibilitychange yok, WA yalnız masa poll'u waiter-app.js:890).
Doğrulanamadı: customer-accounts.js, failed-orders.js, FloorPlanWorkspace, KitchenOperationsWorkspace, online-* ekranları derin okunmadı.
