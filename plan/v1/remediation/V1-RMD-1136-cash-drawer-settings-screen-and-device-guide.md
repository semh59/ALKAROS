# V1-RMD-1136 - Çekmece ayarlarının yönetim ekranında düzenlenmesi ve gerçek cihaz deneme kılavuzu

- Task ID: V1-RMD-1136
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Yönetici, çekmeceyi yönetim Ayarlar ekranından açıp kapatabilsin ve kasa yazıcısı adresini girebilsin; PO gerçek yazıcı ve çekmecede deneyebilsin.

1. Ayar düzenleyicisi bugün yalnız açma/kapama ve sayı kuralı bilir. "Metin" kuralı eklenir: `cash.drawer_printer_address` düzenlenebilir, ipucu "Kasa yazıcısının adresi ve portu, örn. 192.168.1.50:9100; boş bırakırsanız çekmece açılmaz". İstemci biçimi sunucuyla aynı kuralla sınar (adres:port, port 1-65535; boş serbest), hatayı Türkçe cümleyle gösterir; gerekçe zorunluluğu ve çakışma davranışı mevcut düzenleyiciyle aynıdır. `cash.drawer_kick_enabled` ayarının açıklaması "Çekmeceyi gerçek cihazda denemeden açmayın; varsayılan kapalı" uyarısını taşır.
2. `docs/qa/device-browser-test-plan.md`'ye "Kasa çekmecesi (gerçek cihaz)" bölümü: Semih'in izleyeceği adımlar (çekmece kablosu yazıcının çekmece portuna, yazıcının ağ adresi ve portu, Ayarlar'da adresi girme, ayarı açma, nakit deneme ödemesi, "Satış yok" üç gerekçeyle, kapanışta sayaç, ayarı kapatınca hiçbir şey açılmaması), her adımın beklenen sonucu ve sorun giderme (çekmece açılmıyor: kablo/port, yazıcı ESC p desteği ve pim, sunucunun yazıcı ağına erişimi, Docker ağı; iki kez açılıyor; "çekmece açılamadı" iletisi). Bilinen sınırlar yazılır: tek işletme düzeyi yazıcı, Star ve Xprinter ESC p davranışı doğrulanmadı, satışsız açmanın mevzuat yorumu mali müşavire sorulmalı.
3. Üretimde ayar donanım denemesi öncesi AÇILMAZ; kılavuz bunu ilk satır olarak yazar.

## Owned surface

- `evidence/V1-RMD-1136/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/management-settings-history/models.ts
  - src/Clients/PosTerminal/src/features/management-settings-history/SettingEditor.tsx
  - src/Clients/PosTerminal/src/features/management-settings-history/SettingsHistorySection.test.tsx
  - src/Clients/PosTerminal/src/features/management-settings-history/management-settings-history.css
  - src/Clients/PosTerminal/src/strings.ts
  - docs/qa/device-browser-test-plan.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. PosTerminal istemci testleri ve üretim derlemesi (`clientBuildGate.js`)

## Out of scope

- Sunucu ve ayar doğrulaması (V1-RMD-1132); gerçek cihaz denemesinin kendisi (Semih yapar); terminal başına çekmece

## Dependencies

- V1-RMD-1132

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. İstemci üretim derlemesi kanıtı (`clientBuildGate.js`)

## Handoff

- None
