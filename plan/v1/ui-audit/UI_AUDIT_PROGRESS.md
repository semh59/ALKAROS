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
| 15 | CustomerWeb — Menu | Tamamlandı | 0 | 0 | 2026-09-27 | V1-RMD-375; gerçek bulgu yok (canlı güncelleme yok) |
| 16 | CustomerWeb — OrderEntry | Tamamlandı | 1 | 1 | 2026-09-27 | V1-RMD-375; sipariş durumu sessizce güncelleniyordu |
| 17 | CustomerWeb — Bill | Tamamlandı | 1 | 1 | 2026-09-27 | V1-RMD-375; "canlı güncellenir" dediği halde hiç duyurulmuyordu |

## Tur 2 (2026-09-27 başladı): P1/P2/P4/T7/T8 derin geçişi

Semih'in geri bildirimi: Tur 1'de T1/T3/T6 (erişilebilirlik, hata yönetimi, backend uyumu)
gerçek derinlikte işlendi, ama P1 (rakip karşılaştırması), P2 (saha gerçekliği), P4 (öğrenme
eşiği), T7 (rol-arası haberleşme), T8 (mobil/performans) yüzeysel geçildi — "gözlem yok"
denilip atlandı, gerçek bir inceleme yapılmadı. Bu tur, aynı 17 modülü, yalnızca bu 5 boyut
üzerinden, gerçek derinlikte yeniden ele alıyor. Aynı kurallar geçerli: modül sırayla, bir
modülün TÜM bulguları kapanmadan diğerine geçilmez, her gerçek bulgu için gerçek düzeltme +
gerçek test + mutation-check + görev dosyası + dört gate + commit/push.

| # | Modül | Durum | Bulgu | Düzeltilen | Tarih | Not |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Cashier vanilla (ana ekran) | Tamamlandı | 3 | 3 | 2026-09-27 | V1-RMD-376; modifikatör desteği yok (P1), barkod/kod arama gerçekliği (P2) |
| 2 | Cashier — Kasa Oturumu | Tamamlandı | 1 | 1 | 2026-09-27 | V1-RMD-377; kupür bazlı sayım (P1) |
| 3 | Cashier — Tahsilat/Split Payment | Tamamlandı | 1 | 1 | 2026-09-27 | V1-RMD-378; onay bekleyen kart tahsilatı sessizce bayatlıyordu (T7) |
| 4 | WaiterPwa | Tamamlandı | 1 (+2 ertelenen) | 1 | 2026-09-27 | V1-RMD-379; arama debounce'u (T8); favoriler (P1) ve yardım-onay geri bildirimi (T7) Semih'e bırakıldı |
| 5 | PosTerminal — Cashier.tsx | Bekliyor | - | - | - | - |
| 6 | PosTerminal — workspace.tsx + tables | Bekliyor | - | - | - | - |
| 7 | PosTerminal — billing | Bekliyor | - | - | - | - |
| 8 | PosTerminal — catalog | Bekliyor | - | - | - | - |
| 9 | PosTerminal — kitchen-operations | Bekliyor | - | - | - | - |
| 10 | PosTerminal — pending-checks | Bekliyor | - | - | - | - |
| 11 | PosTerminal — online-* (hub/menu/ops/credentials/problems/store-status) | Bekliyor | - | - | - | - |
| 12 | PosTerminal — system-health | Bekliyor | - | - | - | - |
| 13 | PosTerminal — Ayarlar ekranları (Relay/Qnb/Token/Security/BusinessIdentity/Reservation/Screensaver) | Bekliyor | - | - | - | - |
| 14 | PosTerminal — NfcOrder + CustomerDisplay | Bekliyor | - | - | - | - |
| 15 | CustomerWeb — Menu | Bekliyor | - | - | - | - |
| 16 | CustomerWeb — OrderEntry | Bekliyor | - | - | - | - |
| 17 | CustomerWeb — Bill | Bekliyor | - | - | - | - |
