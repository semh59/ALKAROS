# qa-sinir-mimari — V1-RMD-596 ham rapor özeti

105/105 kalem (31 src satır satır). Çalıştırma yok; hepsi Kod okuması. Ajan commit e03090d2.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| B1 | Yüksek | compose.yaml:126 (ALKAROS_SECRET_ENVELOPE_MASTER_KEY yalnız api); connector servisi compose.yaml:215-252; ConnectorHost/Program.cs:54-60; PostgresRelayTunnelStore.cs:86; RelayConnectorSupervisor.cs:139 | Connector konteynerine ana anahtar env'i verilmemiş; tünel token çözümü SecretNotFoundException; compose ortamında QR halka açık tünel başlamaz. Docker'da çalıştırılmadı. |
| B2 | Orta | KitchenOrderItemStateSyncPublisher.cs:58; KitchenOperationsStore.cs:197,~246 | Mutfak kalem durumu commit'inden sonra olay ayrı bağlantıyla yazılır (diğer 9 yayıncı aynı işlemde); araya çökme olayı kaybeder; telafi doğrulanamadı. |
| B3 | Orta | OutboxStore.cs:140-142; RetryPolicy.cs:154 | Tüketici istisnası loglanmıyor, last_error sabit. |
| B4 | Orta | OutboxStore.cs:149,151,273 | Bir mesajın hatası batch'in kalanını ~5 dk in_flight bırakır. |
| B5 | Orta | ModuleBoundaryTests.cs:16-46,303 | ModuleAssemblies'ta Payments, QrOrdering, Invoicing, Privacy yok; iki mimari test bu modülleri izlemez. |
| B6 | Orta | tests/Architecture (BuildingBlocks eşleşmesi yalnız ModuleBoundaryTests) | "BuildingBlocks modüle bağlı olmaz" kuralı yalnız ModuleComposition için test edilir. |
| B7 | Orta | outbox DELETE yok; Program.cs:286 yalnız idempotency_keys | dispatched satırlar hiç silinmez; sınırsız büyüme. |
| B8 | Orta | QrOrderIntegrationEvents.cs:37,41; IntegrationEventSerializer.cs:8; OutboxEnvelope.cs:5 | QR siparişi notu/fiyat düz JSON; yorumlar tersini söyler. |
| B9 | Orta | ISecretProvider 4 modül + 8 Host + ConnectorHost = 13 kayıt, karışık yaşam süresi | Ortak kayıt noktası yok; QrOrderingModule.cs:29 yorumu bayat. |
| B10 | Orta | QrOrderingModule.cs:33,35; SecurityModule.cs:28; RotatingSecretResolver.cs:16 | Süreç çapında ISecretAccessPolicy yalnız Relay politikası; Security'nin IRotatingSecretResolver kaydı tüketicisiz, ilk tüketici reddedilir. |
| B11 | Düşük | SecretStartupValidator, InMemorySecretProvider, PayloadRedactor, MaxAgeRetentionPolicyHook üretimde kullanılmıyor | Fail-fast yok. |
| B12 | Düşük | OutboxStore.cs:246 Enum.Parse("in_flight") | Bugün erişilmez; gelecekte çöker. |
| B13 | Düşük | OutboxStore.cs:162-204 | Lease dolumunda attempt_count artmaz (zehirli mesaj). |
| B14 | Düşük | ORDER BY created_at + backoff | Aynı toplam için sıra garantisi yok (tüketici doğrulanamadı). |
| B15 | Düşük | tests'te OutboxDispatcherHostedService/OutboxComposition doğrudan test yok | Test boşluğu. |
| B16 | Düşük | inbox_messages kodda kullanılmıyor | Sahipsiz şema. |
| B17 | Düşük | SecretValue.cs; AesGcmEnvelopeCipher.cs:121-130 | Baytlar sıfırlanmaz. |
| B18 | Düşük (doküman) | module-dependency-rules.md:13 olay tablosu | Belgedeki olay adları koddaki 3 olay dosyasıyla eşleşmez. |

Temiz: BuildingBlocks csproj'ları domain modülüne bağlı değil, döngü kontrolü, outbox lease/SKIP LOCKED, sır yönetimi, kod borcu kalıpları 0, migrationlar, solution kaydı.
Doğrulanamayan: B1 Docker, B2 telafi, B14 tüketici sırası, DefaultCatalog eşleşmesi.
