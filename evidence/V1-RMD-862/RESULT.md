# V1-RMD-862 sonuç (F0915)

Önce: role=tablist ve role=radiogroup gruplarında ok tuşu yoktu (ArrowLeft/Right yalnız FloorPlanWorkspace). Şimdi sağ/aşağı sonraki, sol/yukarı önceki öğeye odak verip seçer, uçlarda sarar.
- Cashier kategori sekmeleri: cashier-app.js `moveWithinGroup` + tıklamada odak korunumu (önceden seçimden sonra odak <body>'ye düşüyordu).
- split-payment.js: ödeme yöntemi ve müşteri radiogroup'ları, devre dışı nakit atlanır.
- PosTerminal: `moveBetweenTabs` (design-system/primitives.tsx) Cashier.tsx kategori rayı, BillSplitWorkspace, OnlineFoodHub, UserJourneysSection tablist'lerine bağlı; oxlint için tablist `tabIndex={-1}` (sekme sırasını değiştirmez).
- cash-session.js'de sekme/radyo grubu yok; BusinessIdentitySettings yerel radyo girdisi (tarayıcı ok tuşunu zaten işler); FloorPlanWorkspace kendi kodunu korur.

Testler: tests/Clients/StaticApps/arrow-key-groups.test.js (2), primitives.test.tsx (1). Koşular: StaticApps 35 dosya / 388 test geçti; PosTerminal ilgili 32 dosya / 318 test, tsc ve oxlint temiz.
Mutasyon (cp+cmp geri alındı): yön ters, sarma yok, devre dışı atlama yok, click yok, odak yok, odak korunumu yok: hepsi kırıldı (cashier-app'te devre dışı filtresi ölü kod olduğu için o kopyadan çıkarıldı).
Değiştirici tuşlarla (Alt, Ctrl, Meta, Shift) ok tuşu yutulmaz (tarayıcı geri vb. korunur); üç kopyada mutasyonla kırıldı. Roving tabindex yapılmadı (planda kapsam dışı): tüm sekmeler Tab sırasında kalır. Sentetik click telemetride tıklama sayılabilir, doğrulanmadı.
Dört PosTerminal bağlantısı için ek testler (Cashier.category-tabs, BillSplitWorkspace, OnlineFoodHub, UserJourneysSection); `onKeyDown` kaldırılınca her biri kırıldı. Meta ve Shift ayrı test edilmez (aynı koşul satırı).
Açık risk: tarayıcıda elle denenmedi (jsdom); müşteri radiogroup'u yöntem grubuyla aynı işlevi paylaşır, ayrı test yok.
