# V1-RMD-138 - Independent audit: NFC/QR order limits and relay hardening (O1, D2, D3)

- Task ID: V1-RMD-138
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09, QR/NFC müşteri sipariş yüzeyi turu)
Orta/Düşük öncelikli üç bulgusu:

- **O1** — `NfcOrderingStore`/`QrPendingOrderStore`'un anonim, oturumsuz
  sipariş uç noktalarında ne bir kalem başına miktar üst sınırı ne de
  bir gönderimdeki kalem sayısı üst sınırı vardı (yalnız `>0` alt sınırı
  domain seviyesinde zaten uygulanıyordu). Personelin hiç aradan
  geçmediği bir yol olduğundan, kötü niyetli/hatalı bir istemci saçma
  büyük bir sipariş üretebilirdi.
- **D2** — `RelayConnectorSupervisor`, çöken `cloudflared`'ı sabit 5
  saniyelik gecikmeyle sonsuza kadar, üstel geri çekilme ya da bir
  tavan olmadan yeniden başlatıyordu; art arda hızlı çökmeler için ayrı
  bir log sinyali de yoktu.
- **D3** — `RelayProvisioningException`'ın kendi doc yorumu "mesaj her
  zaman bir yöneticiye gösterilmesi güvenlidir (asla ham bir Cloudflare
  hatası değil)" diyordu, ama kod tam tersini yapıp
  `exception.Message`'ı (Cloudflare'in kendi, genelde İngilizce API
  hata metnini) doğrudan bu "güvenli" mesaja ekliyordu — kendi
  belgelediği sözleşmeyi ihlal eden gerçek bir kusur.

## Owned surface

- `plan/v1/remediation/V1-RMD-138-nfc-qr-order-bounds-and-relay-hardening.md`
  (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/Experience/NfcOrdering/NfcOrderingStore.cs (V12-NFC-001
    sahipliğinde) — `PlaceOrderAsync`'e kalem başına miktar (999) ve
    gönderim başına kalem sayısı (50) üst sınırı eklendi.
  - src/Modules/QrOrdering/PendingOrders/QrPendingOrderStore.cs
    (V12-QRO-001 sahipliğinde) — `SubmitAsync`'e aynı iki üst sınır
    eklendi (aynı sabitler, NFC ile senkron kalsın diye).
  - src/Integrations/QrRelay/LocalConnector/RelayConnectorSupervisor.cs
    (V12-QRT-001 sahipliğinde) — art arda hızlı çökmelerde üstel geri
    çekilme (5 dakikada tavan) ve bir eşikten sonra Error seviyeli tek
    bir log eklendi; sağlıklı bir çalışma süresi seriyi sıfırlıyor.
    Diğer tüm denetim/durum mantığı değişmedi.
  - src/Integrations/QrRelay/PublicGateway/RelayProvisioningService.cs
    (V12-QRT-001 sahipliğinde) — Cloudflare hatası artık ham haliyle
    yöneticiye gösterilmiyor; sunucu tarafında loglanıp jenerik,
    eyleme geçirilebilir bir Türkçe mesaj gösteriliyor.
  - tests/Host/Experience/NfcOrdering/NfcOrderingHttpTests.cs,
    tests/Modules/QrOrdering/PendingOrders/QrPendingOrderStoreTests.cs,
    tests/Integrations/QrRelay/LocalConnector/RelayConnectorSupervisorTests.cs,
    tests/Integrations/QrRelay/PublicGateway/RelayProvisioningServiceTests.cs
    (ilgili görevlerin sahipliğinde) — yukarıdaki değişiklikleri
    doğrulayan yeni/güncellenmiş testler.

## In scope

1. `MaxQuantityPerItem = 999` (cashier-facing
   `DualScreenStore.Orders.cs.AddItemAsync`'in zaten uyguladığı sınırla
   birebir aynı), `MaxItemsPerSubmission = 50` — hem NFC hem QR
   (henüz erişilemez olsa da) yolunda.
2. `RelayConnectorSupervisor`: art arda "hızlı" başarısızlık (3 poll
   aralığından kısa süre ayakta kalan) her seferinde geri çekilmeyi
   ikiye katlıyor (5 dakika tavan); 5. art arda hızlı başarısızlıktan
   sonra Error seviyeli tek bir "olası çökme döngüsü" logu basılıyor.
   Sağlıklı bir çalışma (3 poll aralığından uzun ayakta kalma) seriyi
   sıfırlıyor.
3. `RelayProvisioningService`: Cloudflare hatası `ILogger` ile
   loglanıyor (`LoggerMessage.Define`, CA1848 uyumlu); yöneticiye
   dönen mesaj artık her zaman jenerik ve Türkçe.

## Out of scope

- **D1** (nonce temizlik işi) — bilinçli olarak ertelendi: bugün
  `qr_ordering.relay_request_nonces` tablosuna yazan sıfır kod yolu var
  (QR'ın hiçbir HTTP yüzeyi yok, bkz. 2026-09-09 denetiminin #0
  bulgusu); tablo pratikte hiç büyümüyor. QR'ın kendi HTTP yüzeyi
  yapılırken (ve tablo gerçekten büyümeye başladığında) öncelik
  kazanmalı — bugün test edilemeyen bir temizlik zamanlayıcısı kurmak
  spekülatif olur.
- **O2** (NFC sayfasının tüm PosTerminal bundle'ıyla aynı origin'den
  servis edilmesi) — bu görevin kapsamına alınmadı: gerçek bir mimari
  karar gerektiriyor (customer-display'in kullandığı ayrı-origin/
  port deseni mi tekrarlanacak, yoksa yalnızca build-time code-splitting
  mi yeterli — ikisinin de deploy/CLI-seçenek etkileri farklı).
  Kullanıcıyla ayrıca görüşülmeli.
- Denetimin daha önce kapatılan diğer bulguları (K1 → V1-RMD-137, Y1 →
  V1-RMD-136).

## Dependencies

- V12-NFC-001
- V12-QRO-001
- V12-QRT-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Gerçek Postgresql'e karşı Docker'da, real exit code 0 ile:
  - `ALKAROS.Host.Experience.NfcOrdering.Tests`: **17/17** (2 yeni test
    dahil).
  - `ALKAROS.QrOrdering.PendingOrders.Tests`: **10/10** (2 yeni test
    dahil).
  - `ALKAROS.Host.Experience.RelaySettings.Tests`: 9/9 (regresyon yok).
- `dotnet test` (yerel, Postgres gerekmeyen saf bellek-içi testler):
  - `ALKAROS.QrRelay.PublicGateway.Tests`
    (`RelayProvisioningServiceTests`): **4/4** — güncellenen test artık
    mesajın jenerik olduğunu VE ham Cloudflare metnini asla
    içermediğini doğruluyor.
  - `ALKAROS.QrRelay.LocalConnector.Tests`: **7/7** (2 yeni zamanlama
    testi dahil, 3 tekrar çalıştırmada kararlı — art arda hızlı
    çökmelerin geri çekilmeyi belirgin şekilde artırdığını ve sağlıklı
    bir çalışmanın seriyi sıfırladığını doğruluyor).
- `python -m pytest tests/Architecture/ProjectManifest/test_project_manifest.py`:
  4/4.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var
  olan, ilgisiz ihlal (değişmedi), yeni ihlal yok.

## Handoff

- None
