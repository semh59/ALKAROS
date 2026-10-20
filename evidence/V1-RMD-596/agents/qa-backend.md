# qa-backend — V1-RMD-596 ham rapor özeti

105/105 kalem. Yeniden koşum yok; kayıtlar runs/ (99 test yeşil). Kanıt düzeyi Kod okuması (aksi belirtilmedikçe).

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| B1 | Yüksek | UnitConverter.cs Convert/TryConvert: aynı boyutlu iki bilinen birim standart tablo koluna girer, _customConversions bakılmaz; StandardUnits.cs Count birimleri (koli, paket, box, pack, porsiyon, piece, adet) FactorToBase=1; DimensionSafeUnitTests.cs:87 | koli→adet=12 kaydı sessizce yok sayılır, dönüşüm 1:1; mal kabul, stok sayımı, üretim, reçete maliyeti 12 kat yanlış (IPurchasingService:236, InventoryAdjustmentService:83, WasteRecordingService:114, ProductionStockEffectService:198/593, IRecipeCostSnapshotService:81, OrderStockConsumptionService:301). |
| B2 | Orta | TryConvert vs Convert | CanConvert true iken Convert UnknownUnitException (500); çağıranlar CanConvert sonrası Convert çağırır (InventoryAdjustmentService:77-83 vb.). |
| B3 | Yüksek | OutboxStore.cs:140 `when (ex is not OperationCanceledException)`; HttpClient Timeout=10s (OnlineOrderingModule.cs:68); YemeksepetiPartnerClient.cs:109 | Sağlayıcı zaman aşımı (TaskCanceledException) hata sayılmaz, DispatchAsync dışına çıkar; sayaç artmaz; 12 denemelik profil hiç işlemez; mesaj her ~5 dk döner, parti arkadaşları 5 dk takılır. (Zincir kodla izlendi, çalıştırılmadı.) |
| B4 | Orta | OutboxStore.cs DispatchAsync/MarkDispatchedAsync cancellationToken | Kapatmada teslim edilmiş mesaj dispatched yazılamaz → çift teslim, ≤49 mesaj 5 dk gecikir. |
| B5 | Orta | RetryPolicy.cs SanitizeError; OutboxStore.cs:140-142; ILogger yok | Hata nedeni ne DB'ye ne loga yazılır; last_error hep "handler failure"; testler yapay last_error tohumlar (SecurityAdministrationHttpTests:627). |
| B6 | Orta | kira bırakma UPDATE | attempt_count artmaz; zehirli mesaj hiç dead olmaz. |
| B7 | Orta (çıkarım) | tek kira süresi parti için | Çok süreçte parti sonu kirası bitebilir; finalize InvalidOperationException ile partiyi keser. |
| B8 | Orta | created_at sırası + backoff | Aynı toplulaştırıcı olayları için sıra garantisi yok; sözleşme anmıyor. |
| B9 | Orta | SecretStartupValidator, IRetentionPolicyHook, MaxAgeRetentionPolicyHook, IPayloadRedactor/PayloadRedactor, InMemorySecretProvider, RegisterUnit üretimde bağlı değil; V1-SEC-002 Done | Tamamlandı denen güvenlik yetenekleri üretimde yok; eksik sır başlangıçta değil ilk şifrelemede çıkar. |
| B10 | Orta | outbox DELETE yok; Program.cs:286 | dispatched/dead birikir; dead için indeks yok, her döngüde count(*). |
| B11 | Orta | SensitiveEnvelope.cs:49-62; AesGcmEnvelopeCipher.Decrypt | Eksik alanlı/yanlış boyutlu zarf ham ArgumentException fırlatır, SensitiveDataException değil. |
| B12 | Orta | IntegrationEventSerializer.cs:21-26 | Eksik alanlı JSON Guid.Empty/null ile olay üretir; zehirli yük hata vermez. |
| B13 | Düşük | QrOrderIntegrationEvents.cs; serializer doc | Serbest metin not düz JSON outbox'ta; serializer sözleşmesi çelişir. |
| B14 | Düşük (bilinen V1-RMD-134) | inbox_messages kodda yok | Ölü şema. |
| B15 | Düşük | OutboxStore.cs ReadMessage Enum.Parse("in_flight"); OutboxFanoutSinkTests InFlight | Gizli tuzak; test gerçek akışı yansıtmaz. |
| B16 | Düşük | OutboxAdministration.cs:108 | requeue last_error temizlemez (OnlineOrderReconciliationActions temizler); liste limit 500, sayfalama yok. |
| B17 | Düşük | OutboxEnvelope.cs | Guid.Empty toplulaştırıcı kimliği kabul. |
| B18 | Düşük | ISecretProvider 4 modül + 8 Host (karışık Transient/Singleton) | Çift kayıt; HostComposition Add* son kayıt kazanır. |
| B19 | Düşük | EnvironmentVariableSecretProvider | a-b ve a_b aynı env değişkeni; fixture adı '/' içerir. |
| B20 | Düşük | UnitConverter RegisterConversion kilitsiz, RegisterUnit standardı ezer, taşma, loader yeniden denemez, RecipeManagementEndpoints POST bellek-DB ayrışması | Seyrek tutarsızlıklar. |
| B21 | Düşük (doküman) | UnitConversionLoaderHostedService.cs:12-21; IUnitConverter.cs | Yorum "düzeltildi" der, kod B1'de aynı. |
| B22 | Düşük | MaxAgeRetentionPolicyHook, ModuleContext, ModuleCompositionRoot | Null girişlerde NRE. |
| B23 | Düşük | 003 in_flight için claimed_at NOT NULL kısıtı yok; idempotency_keys.expires_at indeksi yok; 033 IF NOT EXISTS yok | Küçük şema eksikleri. |
| B24 | Düşük | OutboxDispatcherHostedService testi yok | Test boşluğu. |

Temiz: kira/nesil, SKIP LOCKED, backoff formülü, ölü mektup requeue (dryRun, audit aynı işlemde), retry profil kayıt, SQL allowlist, AES-GCM/AAD, sır çözümleyici sırası, ModuleComposition, birim dönüştürücü yaşam süresi tek singleton, idempotency, UTC.
Doğrulanamayan: B11 STJ davranışı çalıştırılmadı, B3 uçtan uca, B7, SubmitOrderHandler ilk çift gönderim yarışı (Orders), tüketici idempotansı, Secrets/SensitiveData test gövdelerinin bir kısmı.
