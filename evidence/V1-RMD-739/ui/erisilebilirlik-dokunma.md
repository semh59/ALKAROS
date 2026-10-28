# Tasarımcı gözü: erişilebilirlik ve dokunma (kod okuma; axe testleri ve çalıştırma yok)

Orta
- Cashier cashier-app.js:1095-1096,css:732 - Sepet satırı Sil onaysız, "+" düğmesinin 6 px yanında; yağlı elde yanlış dokunmada satır kaybolur.
- cashier-app.js:604 - Sil düğmesi aria-label yalnız "Sil" (azalt/artır etiketleri ürün adı içeriyor).
- cashier-app.css:192-195 - .search-input:focus outline:none, yerine yalnız kenarlık rengi (#00CFFF; kontrast hesaplanmadı).
- cashier-app.css:306 - .pos-ticket-panel min 380 px, dosyada hiç @media yok; dar ekranda taşma riski.

Düşük
- cashier-app.css:466 .dispatch-waiter-select 36 px; :708 .item-note-input ~28 px ve 0.8rem yazı; :234 prefers-reduced-motion yok (WaiterPwa'da var, waiter-app.css:1439).
- WaiterPwa waiter-app.css:1205 .line-price-changed button min 30 px.

Kapatılan şüpheler: PosTerminal input odağı (styles.css:43-45 3 px halka var); WaiterPwa .cartbar-count süs rozeti; PosTerminal'de 48 px altı etkileşimli hedef bulunmadı (business-identity-settings.css:27-28 renk örneği label içinde).
İyi: --target-min 48 / --target-primary 56 (WaiterPwa); role=alert/status; aria-modal; viewport zoom engeli yok.
Doğrulanamadı: modal odak tuzağı davranışı, PosTerminal 15"/mutfak yerleşimi, axe test kapsamı.
