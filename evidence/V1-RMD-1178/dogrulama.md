# V1-RMD-1178 doğrulama listesi (metin ve yol kaynakları)

- Giriş: Kullanıcı adı / Parola / Giriş yap: src/Clients/PosTerminal/src/routes/Cashier.tsx:631-655; "Kasa sunucusu hazır": Cashier.tsx:615
- Vardiya: "Kasayı aç" (strings.ts cashSessionText.openAction), "Vardiyayı Başlat", "Açılış Tutarı", "Kasayı Aç": src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js:877-890
- Masa: "Sipariş al", "Ürün ekle", "Öde", "Hesap fişi", "Böl", "İptal / iade": routes/TableOrderBridge.tsx:171-206; "Mutfağa gönder", "Tahsil et", "Gönder ve tahsil et", "Sipariş gönderildi": routes/Cashier.tsx:1166-1180, strings.ts:1514
- Ödeme: Nakit / EFT/Havale / Hesaba yaz (cari); "Kredi/Banka Kartı" yalnız cardTerminalConfigured !== false iken: split-payment.js:21,1379-1380; "Tam tutar", "Para üstü", "Çekmece açılamadı, elle açın.", "Ödemeyi Ekle", "Hesap Ödendi", "2/3/4 kişi", "Ödeyen: Kişi N", "Ürüne veya kişiye göre böl", EFT onay kutusu: split-payment.js:2310-2630, 2354, 2859
- Hızlı satış: üst sekme "Hızlı satış" Cashier.tsx:895; kasa-review 33 madde 11 (dokunuş sırası)
- Not: itemNoteText strings.ts:1549-1557, CashierItemNote Cashier.tsx:1026
- İade: "Son satışlar" strings.ts:50,1530; "İade" bölümü, "İade iste", "İade isteği gönderildi; onay bekliyor.", "İade vardiya bütçenizden tamamlandı.": split-payment.js:1927-1935, 2147-2151
- Satış yok: Diğer menüsü NoSaleLink Cashier.tsx:857; gerekçeler Bozuk para / Sayım / Diğer, "Çekmeceyi aç": cash-session.js:1134-1158; kasa-review 29 "Doğrulananlar" (menüde yalnız çekmece açıkken)
- Sayım ve kapatma: "Kasayı Kapat: Sayıma Başla", "Evet, sayıma başla", "Sayımı Kaydet", "Sayımı düzelt", "Fark Açıklaması", "Yönetici kullanıcı adı/şifresi", "Bunları bilerek kapatıyorum", "Kasa Kapatıldı", "Çıkış yap": cash-session.js:956-964 ve sonrası
- Yardım: kasiyer ekranında yönetici çağırma düğmesi yok (Cashier.tsx ve strings.ts içinde "çağır" araması boş); gelen çağrılar "Gördüm": Cashier.tsx:929-936
- Bağlantı: "Bağlantı sorunu", "Bağlantı yok": Cashier.tsx:86-88, 907; "Tahsilat gerçekleşmiş olabilir; hesabı yenileyip kontrol edin.": split-payment.js:1319
- Yönetim: Diğer > Yönetim (reports.view) Cashier.tsx:873; sekmeler "Gün sonu ve mutabakat", "Güvenlik" (Yedekleme paneli SecuritySection.tsx:14-15), "Sistem sağlığı", "Hatalar", "Kullanıcı yolculuğu" (Düğmeler / İstekler): strings.ts:67,524,897,945,988; features/management/sections.ts
- Yedek: saatte bir, backup-scheduler profilsiz ve sürekli açık (compose.yaml:255-290); alarm adları features/management-error-events/models.ts:30; docs/operations/yedekten-geri-donus.md bölüm 1, 5, 5b
- Elle defter: docs/operations/production-go-live-checklist.md §6; kasa-review 36 Soru 3 (matbu belge, 10 gün, tarih ve numara değişmez)
- Pilot öncesi denenmemiş 5 madde: kasa-review 35 satır 14, 16, 17, 18, 19
