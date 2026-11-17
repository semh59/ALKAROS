# V1-RMD-1134 - Fiziksel kasa çekmecesi: nakit ödemede kendiliğinden açılma, "Satış yok" ucu, denetim izi, kapanış sayacı

- Task ID: V1-RMD-1134
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

PO kararı (2026-11-17): (1) nakit ödeme alınınca çekmece kendiliğinden açılsın (kart, havale, cari değil); (2) "Satış yok" = para işlemi olmadan elle açma, gerekçe zorunlu, her açma denetim izine yazılır, kasa kapanış kaydında sayaç olur, çok açma uyarısı verilir; (3) özellik AYARLA açılıp kapatılır (varsayılan KAPALI). Zemin V1-RMD-1132 (yetki `cash.drawer.kick`, ayarlar `cash.drawer_kick_enabled` ve `cash.drawer_printer_address`) ve V1-RMD-1133 (`ICashDrawerKicker`) görevlerindedir.

1. `CashDrawerService` (Host, kasa oturumu tarafı): ayarlardan "etkin mi" (açık ve adres geçerli) kararını okur; etkin değilse hiçbir şey göndermez. `ICashDrawerKicker` (V1-RMD-1133, gönderici) `DualScreenApplication.cs` içinde `TcpCashDrawerKicker` olarak kaydedilir (modül kaydı yoktur; erişilebilirlik denetimi bu görevde gerçek çağrıyla sağlanır). Gönderim her zaman kendi 2 saniyelik zaman aşımı belirteciyle yapılır (isteğin iptal belirteci geçirilmez); hata ödemeyi veya uç yanıtını bozmaz, kaydedilir.
2. Nakit ödeme kancası (`cash-tender` ucu): yalnız ödeme BAŞARILI ve tekrar (`WasReplayed`) DEĞİL ise ve ayar etkinse çekmece açılır. Kart, havale, cari uçlarına kanca konmaz. Ödeme yanıtı en çok 2 saniye geciken tek istisna yazıcıya ulaşılamaması durumudur (bilinen sınır, raporlanır). Başarılı gönderim `cash.drawer.opened` (`kind = Sale`, hesap, terminal), başarısız gönderim `cash.drawer.kick-failed` denetim olayı olarak ödemeden SONRA ve ayrı işlemde en iyi çabayla yazılır; yazılamazsa günlüğe düşer.
3. "Satış yok": `POST /api/v1/terminals/{terminalId}/cash-sessions/{cashSessionId}/cash-drawer/no-sale`, gövde `IdempotencyKey`, `ReasonCode` (`ChangeCoins` = Bozuk para, `Count` = Sayım, `Other` = Diğer) ve `Note`. Kurallar: `cash.drawer` + `cash.drawer.kick` yetkisi, kasa oturumu terminale ve çağırana ait (nakit ödemeyle aynı sahiplik kuralı); gerekçe kodu zorunlu ve listedekilerden biri; `Other` için not zorunlu (en çok 200 karakter), diğerlerinde not isteğe bağlı; özellik etkin değilse 409 `CASH_DRAWER_DISABLED` ("Para çekmecesi bağlantısı kapalı; yönetici Ayarlar ekranından açabilir."); gerekçe geçersizse 400 `CASH_DRAWER_REASON_INVALID`. Denetim kaydı darbeden ÖNCE yazılır (kayıt yazılamazsa çekmece açılmaz) ve tekrar (aynı oturum + aynı anahtar) ikinci kayıt ve ikinci darbe üretmez: kontrol ve kayıt tek işlemde danışma kilidi altında yapılır; tekrar `Replayed = true` döner. Darbe başarısız olursa kayıt kalır, ayrıca `cash.drawer.kick-failed` yazılır ve yanıt `Delivered = false` olur (kasiyere "çekmece açılamadı, yazıcı bağlantısını kontrol edin" gösterilir; para hareketi yoktur). Para defterine (cash.cash_transactions) SATIR YAZILMAZ: beklenen nakit değişmez.
4. Çok açma uyarısı: aynı kasa oturumunda satışsız açma sayısı 5 ve üstüne ulaşınca yanıtta `Warning` Türkçe metni döner ("Bu vardiyada N kez satışsız çekmece açıldı."). Eşik koddaki sabittir (5): bir vardiyada bozuk para ve sayım için birkaç açma olağandır, 5 ve üstü yönetici dikkatine değer; ayar yapılmadı (kapsam). Uyarı denetim kaydında `warned` bayrağıyla da durur.
5. Kapanış sayacı: kasa kapanış denetim olayı (`cash.session.closed`) `afterStateJson` alanına, aynı işlemde sayılarak, `drawerSaleOpenCount`, `drawerNoSaleCount` ve `drawerKickFailedCount` yazar. Sayım `audit.audit_events` içinden oturum kimliğine göre yapılır.
6. Durum ucu: `GET .../cash-sessions/{cashSessionId}/cash-drawer` -> `Enabled`, `CanKick` (çağıranın `cash.drawer.kick` yetkisi), `NoSaleCount`, `WarningCount`, gerekçe listesi (kod ve Türkçe ad); kasa ekranı bunu düğmeyi göstermek için okur (V1-RMD-1135).
7. Türkçe hata cümleleri `ApiErrorCatalog` içindedir; ham istisna metni sızmaz. Alan adları ve kodlar İngilizce, kullanıcı metni Türkçe.

## Owned surface

- `evidence/V1-RMD-1134/**`
- `src/Host/DualScreen/CashDrawerService.cs`
- `src/Host/DualScreen/DualScreenApplication.CashDrawer.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/CashSession/CashDrawerHttpTests.cs (V13-CSH-004) — yeni dosya: çekmece HTTP testleri.
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - src/Host/DualScreen/DualScreenApplication.cs
  - src/Host/DualScreen/CashCloseStore.cs
  - src/Host/Composition/Errors/ApiErrorCatalog.cs
  - tests/Host/Errors/ApiErrorCatalogTests.cs
  - tests/Architecture/ApiConventions/api_convention_allowlist.json
  - tests/Architecture/ApiConventions/idempotency_allowlist.json
  - tests/Host/Experience/CashSession/CashSessionHttpTests.cs
  - tests/Host/Experience/CashSession/CashCloseSafetyHttpTests.cs
  - docs/domain/cash-session-design.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Kasa oturumu HTTP testleri (loopback `TcpListener` ile gerçek bayt doğrulaması), ApiErrorCatalog ve ApiConventions mimari testleri

## Out of scope

- Kasa ekranı düğmesi (V1-RMD-1135); ayar ekranı ve kılavuz (V1-RMD-1136); kart, havale, cari ve iade ödemelerinde açma; terminal başına çekmece; eşik ayarı; para defterine satır; kuyruk tablosu; yazıcı profiline göre pim farkı

## Dependencies

- V1-RMD-1132
- V1-RMD-1133

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. ApiConventions mimari testi exit code 0

## Handoff

- V1-RMD-1135
