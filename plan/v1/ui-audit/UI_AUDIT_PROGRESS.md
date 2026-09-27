# Arayüz denetimi ilerleme takibi

Modül-modül, kalıcı denetim süreci (2026-09-27 kararı). Kurallar:

- Her oturum başında bu dosya okunur; "Bekliyor" olmayan hiçbir modüle tekrar dokunulmaz.
- "Devam Ediyor" durumundaki bir modül varsa, yeni bir modüle başlamadan önce o bitirilir.
- Bir modülün TÜM bulguları (düzeltilmiş ya da bilinçli olarak ertelenmiş şekilde belgelenmiş)
  kapanmadan sıradaki modüle geçilmez.
- Denetim boyutları (her modül için): P1 Rakip karşılaştırması, P2 Saha gerçekliği,
  P3 Basitlik/bilişsel yük, P4 Öğrenme eşiği, T1 Erişilebilirlik, T2 Native tarayıcı API'leri,
  T3 Hata yönetimi, T4 Durum kalıcılığı, T5 Tutarlılık, T6 Frontend-backend uyumu,
  T7 Rol-arası haberleşme, T8 Mobil/performans.

| # | Modül | Durum | Bulgu | Düzeltilen | Tarih | Not |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Cashier vanilla (ana ekran) | Tamamlandı | 6 (+4 ürün gözlemi) | 6 | 2026-09-27 | V1-RMD-360; ürün gözlemleri (P1/P3/P4) Semih'in kararına bırakıldı |
| 2 | Cashier — Kasa Oturumu | Tamamlandı | 3 (+1 ürün gözlemi) | 3 | 2026-09-27 | V1-RMD-361 |
| 3 | Cashier — Tahsilat/Split Payment | Tamamlandı | 4 | 4 | 2026-09-27 | V1-RMD-362 |
| 4 | WaiterPwa | Tamamlandı | 1 | 1 | 2026-09-27 | V1-RMD-363; diğer modüllere göre en olgun çıktı |
| 5 | PosTerminal — Cashier.tsx | Tamamlandı | 2 | 2 | 2026-09-27 | V1-RMD-364; ayrıca axe-core test kapsama boşluğu kapatıldı |
| 6 | PosTerminal — workspace.tsx + tables | Tamamlandı | 1 | 1 | 2026-09-27 | V1-RMD-365 (ilk tarama, hatalıydı) → V1-RMD-367 ile yeniden açılıp düzeltildi (yanlış hata sınıfı) |
| 7 | PosTerminal — billing | Tamamlandı | 1 | 1 | 2026-09-27 | V1-RMD-366; yanlış hata sınıfı kontrolü (V1-RMD-114 sınıfından) |
| 8 | PosTerminal — catalog | Tamamlandı | 1 | 1 | 2026-09-27 | V1-RMD-368; aynı yanlış hata sınıfı deseni (V1-RMD-366/367 ile aynı kök neden) |
| 9 | PosTerminal — kitchen-operations | Tamamlandı | 3 | 3 | 2026-09-27 | V1-RMD-369; ARIA durumu + aynı yanlış hata sınıfı deseni |
| 10 | PosTerminal — pending-checks | Tamamlandı | 1 | 1 | 2026-09-27 | V1-RMD-370; yalnızca axe test-kapsama boşluğu |
| 11 | PosTerminal — online-* (hub/menu/ops/credentials/problems/store-status) | Tamamlandı | 1 | 1 | 2026-09-27 | V1-RMD-371; en olgun bölümlerden biri, tek CSS sınıf uyumsuzluğu |
| 12 | PosTerminal — system-health | Tamamlandı | 2 | 2 | 2026-09-27 | V1-RMD-372; axe boşluğu + Modül 9'da ertelenen hata sınıfı |
| 13 | PosTerminal — Ayarlar ekranları (Relay/Qnb/Token/Security/BusinessIdentity/Reservation/Screensaver) | Tamamlandı | 2 | 2 | 2026-09-27 | V1-RMD-373; axe boşluğu (7 ekran) + eksik test dosyası (Screensaver) |
| 14 | PosTerminal — NfcOrder + CustomerDisplay | Tamamlandı | 2 | 2 | 2026-09-27 | V1-RMD-374; axe boşluğu + CustomerDisplay'in eksik test dosyası |
| 15 | CustomerWeb — Menu | Bekliyor | - | - | - | - |
| 16 | CustomerWeb — OrderEntry | Bekliyor | - | - | - | - |
| 17 | CustomerWeb — Bill | Bekliyor | - | - | - | - |
