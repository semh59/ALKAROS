# Tasarımcı gözü: görsel tutarlılık (kod okuma, ekran görüntüsü yok; PosTerminal ~286 sabit hex, WaiterPwa 35, Cashier 59)

Yüksek
- Cashier: her CSS kendi :root token kopyasını taşıyor (cashier-app.css:10-33, split-payment.css:6-23, cash-session.css:7-24, customer-accounts.css); ortak --ds-* token dosyası kullanılmıyor.
- Üç istemcide üç yazı tipi: Cashier system-ui (cashier-app.css:24), WaiterPwa Inter (waiter-app.css:40), PosTerminal Manrope (tokens.css:11).
- Yarıçaplar: token iki değer (8/12px); Cashier 6/10/14px, WaiterPwa 9/12/16px ve pill.
- PosTerminal online-operations.css:4-36, online-menu.css:3-18: buton/alan/durum renkleri sabit hex; primitives.tsx kullanılmıyor.
- PosTerminal billing.css:1-74 (24 hex): eski lacivert/turuncu palet ve kendi --bill-* paleti kalıntısı.
- Odak halkası 5 farklı renk ve 2/3px kalınlık (primitives.css:23, split-payment.css:77,104,112,201, cashier-app.css:788, billing.css:74, styles.css:44,184).

Orta
- Cashier --color-text-dim #8a97a1 (beyazda ~3:1, ölçülmedi); WaiterPwa --color-muted #55636f ve Cashier #4d5c68 aynı rol iki gri; --color-text #222 (ink #0B2135 değil).
- WaiterPwa waiter-app.css:534 disabled yazı #78858e (token disabled-ink #52606d); :1009,:1372,:150,:1358 token dışı renkler.
- PosTerminal styles.css:143-146 alert renkleri sabit; styles.css:116-121,169 durum noktası yalnız renkle (metin/ikon eşlik etmiyor); styles.css:187 16px yarıçap ve özel gölge, billing.css:14,34 farklı yarıçaplar.
- WaiterPwa ve Cashier'da ayrı toast/uyarı stili (ortak bileşen yok).

Düşük: Cashier skip-link sabit değerler ve theme-color üç sayfada tutarsız; PosTerminal styles.css:141,164,166 token dışı griler; boşluk 4px ızgarası dışı (.55rem, 10px 14px); WaiterPwa üç özel gölge (--shadow-dock/sheet/toast).

Tutarlı (dokunma): para biçimi tr-TR TRY, marka/durum renk değerleri, 48px hedef, PosTerminal --ds-* alias geçişi, tarih biçimi İstanbul.
Bakılmadı: koyu tema, yüksek kontrast, ikon seti, uzun metin kırpma.
