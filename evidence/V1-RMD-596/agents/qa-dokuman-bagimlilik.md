# qa-dokuman-bagimlilik — V1-RMD-596 ham rapor özeti

105/105 kalem; 16 Done görev tek tek açıldı; 20 paketin nuspec lisansı okundu; project_manifest_tool exit 0 VALID.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| 1 | Yüksek (doküman sapması) | idempotency-inbox-outbox.md:19-24; RetryPolicy.cs:20; inbox_messages src'de yalnız yorum | Doküman ve alkaros-boundary-event SKILL'i çalışan inbox anlatıyor; V1-RMD-134 InboxStore/IInboxHandler/Idempotency/Transactions'ı sildi. |
| 2 | Orta | 002-inbox-messages.up.sql; 033; V1-RMD-112/074 ix_inbox_messages_claimable | Kodu silinen tablo, 2 göçte sütun ve indeksle duruyor. |
| 3 | Orta (doküman) | idempotency-inbox-outbox.md:16; ApiErrorCatalog.cs:563; OrderManagementEndpoints.cs:1154; NfcOrderingEndpoints.cs:186 | Aynı durum için üç hata kodu: IDEMPOTENCY_KEY_CONFLICT (doküman), IDEMPOTENCY_CONFLICT, IDEMPOTENCY_KEY_REUSED (kod). |
| 4 | Orta (doküman) | idempotency-inbox-outbox.md:28; YemeksepetiStatusSyncModels.cs:55; OnlineOrderingModule.cs:74,80,104 | Doküman "max 3 deneme"; 3 olay türü için 12 denemelik profil var. |
| 5 | Orta (doküman) | postgresql-extension-ownership.md:39-44; 012-btree-gist-ownership.down.sql | Doküman DROP EXTENSION der, down koruma ile düşürmez. |
| 6 | Orta | build/project-manifest.json | 59 kayıtlı proje, 14'ü diskte yok; IntegrationContracts, Measurements, Messaging.Tests, Measurements.Tests eksik; doğrulayıcı bayatlığı yakalamıyor. |
| 7 | Orta | OutboxStore.EnqueueAsync(tx) testi yok; CatalogPublicationService.cs:105 kullanıyor | Atomiklik garantisi testsiz (SKILL şart koşuyor). |
| 8 | Düşük | Npgsql nuspec "PostgreSQL" lisansı; third-party-license-policy.md izin listesi | Npgsql lisansı izin listesinde adı geçmiyor; politikaya göre PO kararı gerekir. |
| 9 | Düşük | 4 paket nuspec'te lisans ifadesi yok (system.configuration.configurationmanager 4.4.0 vb.) | Makine okunur lisans yok. |
| 10 | Düşük (tarihsel) | V1-RMD-134 evidence yok | Kural sonradan geldi; kayıt. |
| 11 | Düşük | V1-FND-002/010/011/014/015/019, V0-GOV-014 Owned surface'te diskte olmayan yollar | Eski görevlerde yüzey iddiası bayat (V1-RMD-134 silmesi). |
| 12 | Düşük | V1-FND-001 glob yüzeyleri | Bilgi. |

Temiz: paket/kilit/merkezi sürüm, lisanslar (GPL/AGPL yok), FluentAssertions 6.12.0 politikayla uyumlu, olay yolu paragrafı, kabul testleri, borç kalıpları.
Doğrulanamayan: Logging.Abstractions requested 8.0.3 / resolved 8.0.2 (restore locked-mode exit 0), npm lisansları, SKILL'in diğer maddeleri.
