# V12-ONL-001 - Implement Yemeksepeti webhook inbox

- Task ID: V12-ONL-001
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Eşzamansız işlemden önce her provider event'nin kimliğini bir kez doğrulayın ve kalıcı hale getirin.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/**`,
  `database/migrations/V12/V12-ONL-001/**`
- `src/Host/Experience/OnlineOrdering/YemeksepetiWebhookEndpoints.cs` — sağlayıcının çağıracağı HTTP ucu; modül
  HTTP barındıramadığı için Host'ta, bu görevle oluşturulan yeni dosya. Doğrulanmamış taslak olarak işaretlidir.
- `tests/Host/Experience/OnlineOrdering/**` — bu ucun gerçek HTTP ve PostgreSQL testleri.
- `evidence/V12-ONL-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-25 "Sınırlı ek + yol
  notu" kararı):
  - src/Modules/OnlineOrdering/ALKAROS.OnlineOrdering.csproj ve OnlineOrderingModule.cs (V12-MAP-001 sahipliğinde)
    — Secrets/SensitiveData yapı taşı referansları ve inbox kaydı.
  - src/Host/DualScreen/DualScreenApplication.cs — ucun kaydı, eşlenmesi ve IP'ye göre `yemeksepeti-webhook` hız
    sınırı politikası.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 145 numaralı migration konumu.
  - ALKAROS.slnx ve `dotnet restore --force-evaluate`'in mekanik olarak güncellediği packages.lock.json dosyaları.
- Doğrulanmamış taslak notu: sağlayıcıya dönük her kural yalnız herkese açık Partner API v2.0.2 belgesine ve POS
  partner-picking SSS sayfasına dayanır. Bu kurallar şunlar: Partner Portal'da tanımlanan secret'ın `Authorization`
  başlığında gelmesi, 10 saniyelik zaman aşımı, en fazla 5 tekrar, yükteki `order_id`, `status` ve `sys.updated_at`
  alanları. Gerçek bir teslimat hiç alınmadı (V0-YSP-001 `Blocked`, `V12-GOV-004` waiver'ı). Secret
  (`ALKAROS_SECRET_YEMEKSEPETI_WEBHOOK_SECRET`) tanımlı değilse uç 503 döner ve hiçbir şey okumaz; yani kanal
  operatör bilinçli olarak açana kadar kapalıdır.

## In scope

- Webhook belirteç/imza politikası, harici event benzersizliği, ham yük koruması ve retry-güvenli onay.

## Out of scope

- Harici order normalleştirme ve ürün eşleme.

## Dependencies

- V0-YSP-001
- V1-FND-002
- V1-FND-006
- V0-CMP-003
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Provider retry, durable insert sonrasında başarılı replay alır; duplicate event tek Inbox
  kaydı üretir; geçersiz kimlik doğrulama hiçbir şeyi saklamaz.
- Kapanış kanıtı (2026-09-25, gerçek PostgreSQL 18 UTF8, port 56433): modül testleri 21/21, Host HTTP testleri 5/5
  yeşil. Kalıcı kayıttan sonra gelen tekrar aynı inbox kimliğiyle başarılı yanıt alır (HTTP 200 `duplicate`). On
  eşzamanlı aynı teslimat tek kayıt bırakır. Aynı siparişin farklı durumları, aynı güncelleme zamanını taşısalar bile,
  ayrı olaylardır. Yanlış, eksik, kısaltılmış veya uzatılmış secret (HTTP 401), yapılandırılmamış kanal (HTTP 503),
  bozuk gövde (HTTP 400) ve sınırı aşan gövde (HTTP 413) hiçbir satır yazmaz. Ham gövde yalnız AES-256-GCM zarfı
  olarak saklanır; veritabanı baytlarında telefon ve soyad düz metin olarak yoktur ve başka bir erişimci zarfı
  açamaz. Migration 145 geri alınıp yeniden uygulanır.
- Mutasyon kontrolü (geri alındı, dosya birebir eşleşti): kimlik doğrulama atlanınca 5, çakışma yönetimi kaldırılınca
  3, olay anahtarından durum çıkarılınca 1, gövde şifresiz saklanınca 1 test kırmızıya döndü. Olay anahtarı
  mutasyonu ilk denemede yakalanmadı; bunun üzerine aynı güncelleme zamanlı farklı durum testi eklendi.
- Kanıt: `evidence/V12-ONL-001/`.

## Handoff

- V12-ONL-002
