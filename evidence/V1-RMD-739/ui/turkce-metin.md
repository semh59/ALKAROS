# Tasarımcı gözü: Türkçe metin ve terminoloji (kod okuma; P=PosTerminal/src, W=WaiterPwa/wwwroot, C=Cashier/wwwroot)

Pozitif İngilizce sızıntı bulunmadı (§4 tarama deseni 0 satır); sorunlar terim, ton ve ham hata metni.

Yüksek
- C index.html:93,120,166 ve cashier-app.js:585 - "Aktif Taslak Fiş" / "Sepet boş" / "Adisyona Ekle": aynı şey için üç ad; "fiş" aynı zamanda kart slipi (split-payment.js:840).

Orta
- P AuthorizationDecisionsWorkspace.tsx:57 ve 30'dan fazla `reason.message` (NfcOrder.tsx:238, CustomerDisplay.tsx:109) - ham JS/sunucu hata metni ekrana gidiyor (UI_STYLE_GUIDE §3).
- P KitchenOperationsWorkspace.tsx:503 "ticket" (aynı dosyada :605 "bilet"); billingApi.ts:101 "Adisyon" (diğer yerler "Hesap"); strings.ts:1008 "Modifikatör" ↔ :930 "Seçenek"; strings.ts:117,118,193,449,477 hata metinleri ne yapılacağını söylemiyor (30+ yer).
- P↔W "Müsait" ↔ "Boş"; "Parola" ↔ "Şifre".
- W index.html:120,134,138 "Adisyon" ↔ :158 "Hesap"; W tables.js:254 `${ad}'den not:` ek uyumu bozuk; W util.js:57,58 409/423 mesajlarında yönlendirme yok.
- C index.html:97,125,110 "İptal" (title "Fişi Temizle"), büyük harfli uzun "SİPARİŞİ ONAYLA & MUTFAĞA İLET", "TASLAK TUTARI".
- C split-payment.js:745 "hesap kimliği (billId)" kod adı ekranda; "Sunucuya ulaşılamadı." sonrası yönlendirme yok (~12 yer); customer-accounts.js:76-81 409 eşlemesi yok.

Düşük (özet): Title Case/ton tutarsızlığı (strings.ts:487,496,497,547,449; C cash-session.js:435,463,548); "Slip no"; "sağlıksız" (→ Sorunlu); "Süpervizör/müdür" (→ Yönetici); "Etkin/Aktif/Devre dışı/Pasif"; "Tümü/Hepsi"; "opsiyonel/isteğe bağlı"; "95. yüzdelik" jargon; aria-label "Aç"/"Kapat" belirsiz (W kiosk-lock.js:177, index.html:161,188); C "Kasa Girişi" login ile karışır; hata başlığı yalnız "Hata".

Sözlük önerisi: sepet=Sepet, ödenecek kayıt=Hesap, kart slipi=Slip; Seçenek (Modifikatör değil); Boş; Şifre; Etkin/Pasif; Yönetici; isteğe bağlı; Tümü; Sorunlu.
