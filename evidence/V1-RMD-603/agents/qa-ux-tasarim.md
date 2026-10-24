# qa-ux-tasarim - V1-RMD-603 Billing (SHA feb38368)

Kontrol: 12/21 istemci dosyası tam okundu (split-payment.js/.css/index.html, cash-session.js, customer-accounts.js, BillSplitWorkspace.tsx, RefundPanel.tsx, billing.css, billingApi.ts, refundApi.ts, models.ts kısmen, bill.js); 3 yalnız grep; 6 okunmadı (index.ts, customer-accounts/index.html, 4 PosTerminal test). Gerçek tarayıcı: Playwright, Cashier E2E düzeneği, KASA-1; 4 koşu / 6 senaryo; betik ve ekran görüntüleri tmp/qa/V1-RMD-603/qa-ux-tasarim/ (specs/ux.spec.mjs, run*.log, shots/01..10). Düzenek gitignore'lu dosyalar yazdı; git status yalnız evidence/V1-RMD-603/. İngilizce sızıntı taraması (UI_STYLE_GUIDE §4): temiz (9 satır, hepsi kod adı/ID/API kodu). Ç = çalıştırıldı, K = kod okuması.

| No | Önem | Kanıt | Sorun | Etki |
|---|---|---|---|---|
| U1 | Yüksek | Ç specs/ux.spec.mjs 3. koşu; split-payment.js:412-467 | Yüzde alanına 100 yazıp "İndirim uygula" onaysız "Hesap Ödendi" ekranı çıkarıyor; sunucuda payable 0, allocations 0, hesap Open, ödeme yok. Geri alma yolu yok (istemci/HTTP ucu yok; RemoveAsync yalnız arayüz). | Para kaybı riski, ekran-sunucu tutarsızlığı |
| U2 | Yüksek | Ç 3 koşu; split-payment.js:313-316,649 | Kart payı seçilince ödeme formu kayboluyor, yalnız "Yetkili müdür: kart çekildi mi?" kalıyor (#submit-tender = 0); diğer kişiler nakit bile ödeyemiyor; çıkış iki ayrı müdür; uyarıda tutar (₺100) ve "müdürü çağırın" yönlendirmesi yok. Bilinen kısım: dış bağımlılık Token/Beko (remaining-gaps-register, PendingBankCardTerminalIntegrationHandler) – ekran davranışı kayıtta yok. | Dört kişilik masada kart seçimi kasayı tıkar |
| U3 | Orta | Ç 2. koşu (3 kişi, ₺100); :313-316,649,300-311 | İlk ödemeden sonra tutar alanı yeniden hesaplanmıyor; 33,33x2 sonrası kalan 33,34 iken alan 33,33'te; son kişi ₺0,01'de takılıyor. | 3 ve 6 kişide sık |
| U4 | Orta | Ç 3. koşu ikinci müdür oturumu; :252,458-460 | İkinci müdür onayı sonrası alan 200.00, kalan 100 -> "Tutar kalan tutarı aşamaz"; aynı oturumda Nakit yok (:252) -> EFT/Havale'ye zorlanıyor. | Karma ödeme son adım hata |
| U5 | Orta | Ç 1440x900 ve 768x1024, #submit-tender y=1286; :1455-1486 | "Ödemeyi Ekle" en altta; indirim, bahşiş, iptal, iade blokları ondan önce; her ödemede kaydırma; iptal/iade ödemeyle yan yana. | Yoğun saatte +1 kaydırma, yanlış basma |
| U6 | Orta | Ç .sp-method-chip 67x39 px; split-payment.css:139-144 (--touch-target 48px satır 20); waiter-app.css:1203-1205 "Güncelle" min-height 30px | Yöntem çipleri 48 px altı. | Yanlış yöntem seçimi |
| U7 | Orta | Ç allocations alanları [allocationId,paymentId,amount,paymentStatus]; :757,1063 | Uygulanan tahsilatlar/iade listesinde yalnız tutar; yöntem yok; dört ₺90 satır ayırt edilemez. | Nakit mi kart mı iade belirsiz |
| U8 | Orta | K BillSplitWorkspace.tsx:97-108,121-139; workspace.tsx:439-449; grep split-design split-payment.js = 0 | Ürün bazlı bölme tasarımı yalnız kaydediyor; tahsilat sayfası dağılımı okumuyor, kişi başı tutarlar elle. Bilinen: remaining-gaps-register "Kalem bazlı bölme" (kayıt bayat). | Kişi x ürün emeği boşa gider |
| U9 | Orta | K split-payment.js:1243,652,1415; cash-session.js:211,243,276,296,329,386; customer-accounts.js:176,225,241 | "Sunucuya ulaşılamadı." 11 yerde, ne yapılacağı yok; tahsilat hatası "yenileyip kontrol edin" diyor ama yenile düğmesi yalnız renderLoadError'da. | Ödeme gerçekleşti mi bilinemez, çifte tahsilat riski |
| U10 | Orta | K :677-678,231-235; Ç 06-missing-bill.png | billId yok/404 aynı "Hesap Bulunamadı... (billId)" – geri bağlantısı yok, jargon; oturum düşünce "Giriş ekranına dön" hesaptan koparıyor. | Çıkmaz sokak |
| U11 | Orta | K BillSplitWorkspace.tsx:73,77,149,153; workspace.tsx:383-385,445 | Hücre silinince Number("")=0 kaydı engelliyor; owners boşsa (catch -> []) ekran sessizce kullanılamaz; "Dağıtımı sıfırla" onaysız. | Hata nedeni anlaşılmaz |
| U12 | Orta | K split-payment.js:112,396,441; cash-session.js:80,353; customer-accounts.js:75; billingApi.ts:50; BillSplitWorkspace.tsx:106; BillingSplitApplication.cs:177 | Doküman sapması: UI_STYLE_GUIDE §3 sunucu gövdesi gösterilmez diyor, 5 yerde body.error.message basılıyor; tek İngilizce mesaj ezilmiş; customer-accounts.js'de 409 dalı yok. | Kural ihlali, sızıntı riski |
| U13 | Orta (doğrulanamadı) | K :1329 RECEIPT_METHODS (Cash/Account/Card/Manual) vs :16 METHOD_LABELS (Cash/BankCard/Eft/Account) | Fiş yöntem sözlüğü ödeme ekranıyla farklı; gerçek /receipt yükü yakalanmadı. | Fişte "Diğer" riski |
| U14 | Düşük | K :801,805 | Ham enum fallback (|| item.adjustmentType / item.reason). | Ham İngilizce kod görünür |
| U15 | Düşük | K split-payment.css:105,117,205-207; billing.css:1-12; split-payment.js:689,824,1469,1475 | --color-text-dim #8a97a1 ~3:1 (gözle); billing.css sabit hex; satır içi stiller; sp-*/cs-* kopya stil. | Üç stil dili, kontrast |
| U16 | Düşük | K BillSplitWorkspace.tsx:122-158 (81,145) | Metinler strings.ts'te değil; jargon ("deterministik", "Sunucu bu dağıtım modunu yetkilendirmiyor"). | Anlaşılmaz |
| U17 | Düşük | K split-payment.js:16,1329,772,785; cash-session.js:684; customer-accounts.js:219; billingApi.ts:88 | Terim tutarsızlığı: kart, müdür/süpervizör/yönetici/yetkili, adisyon/hesap. | Eğitim yükü |
| U18 | Düşük | K cash-session.js:687-688 | "Kasayı Kapat" onaysız. | Yanlışlıkla vardiya kapanışı |
| U19 | Düşük | K split-payment.js:26,1459-1463 | Eşit bölme kişi sayısı sabit 2 + ayrı "Hesapla"; partySize taşınmıyor. | +2 dokunuş |
| U20 | Düşük | K :1470,1475 | "(isteğe bağlı)" ve "(opsiyonel)" karışık. | Küçük tutarsızlık |
| U21 | Düşük | K :773-781,785-791 | Yönetici-yalnız alanlar her role gösteriliyor; kasiyer 403 alıyor. | Karmaşa |

Dokunuş sayımı (₺400, dört kişi, %10, nakit x2 + kart + nakit): ~20+ dokunuş, 2 müdür, 1 oturum değişimi; kilitsiz nakit/EFT akışı 8-10 dokunuş.

Temiz: İngilizce sızıntı yok; yıkıcı işlemler (iptal/iade) iki adımlı; yükleme/boş/hata durumları ayrı ekranlar; idempotency anahtarı korunuyor; aria kapsamlı; Intl tr-TR; açık kasa yoksa Nakit pasif + para üstü önizlemesi; kupür sayımı otomatik; 409 için Türkçe mesaj.

Doğrulanamayanlar: PosTerminal /billing ve RefundPanel gerçek tarayıcıda görülmedi (dist 2026-10-14, src 2026-10-24; derleme salt-okunur kuralı gereği yapılmadı); indirim izni olmayan rol (202 Pending) ve garson-yalnız rol tohumda yok; ikinci ekran; fiş çıktısı/@media print; kontrast hesaplanmadı; U1,U3,U5,U6,U9 için plan kaydı aranmadı. Önceki koşu: tsc exit 0, static-apps exit 0, oxlint exit 1 (Billing dışı).

Fırsatlar: F1 son kişiye kalan otomatik + "2/4 ödendi"; F2 varsayılan kişi sayısı partySize; F3 ürün bazlı bölmeyi tahsilata bağlama; F4 indirimde önizleme + %10/%15/%20 düğmeleri; F5 ödeme satırında yöntem + çalışan adı; F6 nakitte kupür kısayolları; F7 kart kilidinde müdür bekleyen onaylar ekranı; F8 birincil eylem sabit alt çubuk, iade/iptal ayrı sekme.
