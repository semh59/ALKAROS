# qa-guvenlik-yetki - V1-RMD-603 Billing (SHA feb38368)

Kontrol: 120 kalemin 74'ü (7 tam okundu, 67 hedefli grep, 46 konu dışı atlandı). Billing testleri koşulmadı.

Yetki matrisi: bills.split (from-order, tip, split-design) BillingSplitApplication.cs:102,221,411; bills.cancel :118; bills.discount :147 (yoksa grant); payments.refund.request :281,296,340,354; payments.refund.approve :310,325,368; payments.take Receipt.cs:57. bills.void/comp billing'de kullanılmıyor. GET split-design/owners/adjustments yalnız terminale bağlı kasiyer oturumu ister. Ekranda gizlenip sunucuda korunmayan eylem yok.

| No | Önem | Kanıt (kod okuması) | Sorun |
|---|---|---|---|
| G1 | Orta | BillingSplitStore.cs:122,126,163-172; AuthorizationGrant.cs:22 | Grant ile uygulanan indirimde authorized_by ve audit actorId onaylayan değil isteyen kasiyer; grant id ve onaylayan indirim satırında/audit'te yok. |
| G2 | Orta | :147; BillAdjustment.cs:138 | bills.discount sahibi süpervizör sınırsız yüzde/tutarla (%100 dahil) ikinci onaysız indirim yapabilir; comp için bütçe var, discount için yok. |
| G3 | Orta | PaymentRefunds.cs:175-196; 184, 185 göçleri | Doküman sapması (PO: nakit iade yönetici onayı): payments.refund.approve sahibi süpervizör de hem onaylar hem vardiya allowance verir; allowance varsa kasiyer nakit iadeyi ikinci kişisiz kapatır (approval_mode='Allowance'); allowance PUT yönetici rolü şartı aramıyor, üst sınır 10.000.000. |
| G4 | Orta | PaymentRefunds.cs:105-197, 309-345 | İade isteği oluşturma ve manuel yönteme geçen onay için audit olayı yok; allowance oto-onayı yalnız Completed olunca audit yazıyor. |
| G5 | Orta | :340, :373-399 | Manuel iadeyi tamamlamak için yalnız kasiyer izni yeter; istekçi, tamamlayan ve fiş no giren aynı kişi olabilir; fiş no yalnız uzunluk kontrolü (1-50), benzersizlik yok. |
| G6 | Orta (Çıkarım) | BillingSplitSessionAuthorizer :496-510 | billId/paymentId/requestId sorgularında terminal süzgeci yok; bir terminal oturumuyla herhangi bir masanın hesabı/ödemesi/iade isteği işlenebilir. Tek işletmede tasarım gereği; çok şubede yatay erişim. |
| G7 | Düşük | :103-109,237-243 | Doğrudan indirim yolunda aynı idempotency key farklı tutarla gelirse ilk kayıt sessizce başarı döner (grant yolu reddediyor). |
| G8 | Düşük | :61-70,221-224 | Hesap durumu FOR UPDATE kilidinden önce okunuyor; araya giren iptal sonrası iptalli hesaba indirim/bahşiş yazılabilir. |
| G9 | Düşük | :211-212 | Bahşişe üst sınır yok (yalnız > 0); bahşiş havuzu şişirilebilir. |
| G10 | Düşük | PostgresBillAdjustmentRepository.cs:144-156 | RemoveAsync hesap düzeltmesini audit'siz fiziksel siler; üretimde çağıran yok, arayüzde açık. |
| G11 | Düşük | BillingSplitApplication.cs:90,266,374 | Billing grupları terminal-write oran sınırı taşımıyor (receipt'te terminal-read var). |
| G12 | Düşük | PaymentRefunds.cs:175-179 | Manuel yönteme giden allowance tamamlanmazsa serbest kalmıyor; AwaitingManualRefund iptal ucu yok. |
| G13 | Düşük | BillCancellation.cs:34; PaymentRefunds.cs:109 | İptal/iade gerekçesi serbest metin (500), audit ve ekrana düz yazılıyor; kişisel veri uyarısı yok. |
| G14 | Düşük (test) | tests/Host/Experience/Billing | Yanlış terminal cookie'si, süresi dolmuş/iptal oturum, eksik payments.refund.request için 403 testi yok (grep 0). |

Temiz: SQL interpolasyonları yalnız sabit tablo adı; split-payment.js dinamik değerleri escapeHtml ile (48 çağrı); oturum token hash + device_id + süre + aktif kullanıcı, cookie HttpOnly SameSite=Strict; isteyen kişi onaylayamaz (:317, :356, DB check decider_not_requester); hata sızıntısı yok (Türkçe genel mesaj); bill-settlement kilidi ve idempotency; grant konu uyumu doğrulanıyor; fişte müşteri adı/telefon/vergi no yok, müşteri hesabı ekranı maskeli; iptalde zorunlu gerekçe, ödemeli hesapta 409, audit aynı işlemde; iade listesi LIMIT 200.

Doğrulanamayanlar: Billing Host testleri koşulmadı (PaymentTender kaydı kesik); Secure cookie bayrağı Request.IsHttps'e bağlı (DualScreenApplication.Endpoints.cs:654, ortama bağlı); "yönetici onayı" PO kapsamı (G3); çok şubeli yol şemada aranmadı; bilinen kayıtlarla çakışma teyit edilmedi, hiçbir bulgu "bilinen" işaretlenmedi.
