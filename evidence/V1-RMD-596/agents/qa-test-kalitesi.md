# qa-test-kalitesi — V1-RMD-596 ham rapor özeti

105/105 kalem. 99 test yeşil (runs/). Mutasyon (repo dışı worktree, cp+cmp ile geri alındı): 12 geçerli mutant, 2 öldü, 10 hayatta kaldı. Mutasyon logları worktree ile silindi; sonuçlar bu tablodadır.

Mutantlar: M1 retry vade koşulu kaldırıldı (hayatta) · M2 kira bırakma (öldü, yalnız zamanlamaya bağlı eşzamanlı test) · M3 ORDER BY created_at→id (hayatta) · M4 tip uzunluğu >→>= (hayatta) · M5 nonce sabit sıfır (hayatta) · M6 anahtar uzunluğu !=→> (hayatta) · M7b/M7c sıfır miktar reddi (ikisi hayatta) · M8 Trim silindi (hayatta) · M9 tersinirlik kapatıldı (hayatta) · M10 saklama sınırı >→>= (öldü) · M11 env öneki (hayatta).

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| T1 | Yüksek | OutboxStore.cs:208, M1 | Vadesi gelmemiş mesajın claim edilmediğini doğrulayan test yok (backoff bozulsa yeşil). |
| T2 | Yüksek | AesGcmEnvelopeCipher.cs:38, M5 | Nonce benzersizliği/uzunluğu/tag uzunluğu testsiz; sıfır nonce ile 23 test yeşil. |
| T3 | Yüksek | EnvelopeKeyCodec.cs:30, M6 | Anahtar uzunluğu sınırı testsiz (16/24 bayt kabul edilir). |
| T4 | Yüksek | AesGcmEnvelopeCipher.Decrypt; KeyFailureTests.cs:76-91 | Tag/nonce/kesik ciphertext/farklı geçerli anahtar ile bütünlük testleri yok. |
| T5 | Orta | SensitivePayloadProtector.cs:84-86 | "classification does not match" dalı testsiz. |
| T6 | Yüksek | OutboxStore.cs:261-290; OutboxStoreTests.cs:384-400 | Bayat nesil testi yalnız Mark yolu; RecordFailure hata yolu ve claim sonrası nesil artışı, batch'in kalanı testsiz. |
| T7 | Orta | OutboxStore.cs:171-185, M2 | Taze kira yeniden claim edilmez kuralı deterministik test edilmiyor. |
| T8 | Orta | OutboxStore.cs:208, M3 | FIFO sırası testsiz. |
| T9 | Orta | OutboxEnvelope.cs:28, M4 | 100 karakter sınırı ve aggregateType 101 testsiz. |
| T10 | Yüksek | UnitConverter.cs:111,157, M7b/c | Sıfır miktar sınırı testsiz. |
| T11 | Orta | UnitConverter.cs:125-126, M8 | Trim/büyük-küçük harf normalizasyonu testsiz. |
| T12 | Orta | UnitConverter.cs:138-141, M9 | Tersinirlik kontrolü hiç tetiklenmiyor (ölü kod veya kanıtsız). |
| T13 | Orta | probu çalıştırıldı: RegisterConversion("kasa","adet",12) → TryConvert true, Convert UnknownUnitException | Convert ve TryConvert tutarsız; tüketiciler Convert çağırıyor (InventoryAdjustmentService.cs:83 vb.); RecipeManagementEndpoints.cs:104 ve UnitConversionLoaderHostedService.cs:79 kasa kodlarını RegisterConversion ile ekliyor. |
| T14 | Orta | UnitConverter RegisterConversion 3 sözlüğe ardışık | Eşzamanlı yazma/okuma testi yok. |
| T15 | Düşük | DimensionSafeUnitTests.cs | IsKnownUnit/GetDimension/CanConvert/RegisterUnit testsiz. |
| T16 | Orta | EnvironmentVariableSecretProvider.cs:15-19, M11 | Üretimdeki tek sır sağlayıcısının kendi projesinde testi yok; '/' içeren adlar env adına olduğu gibi geçer. |
| T17 | Orta | OutboxDispatcherHostedService.cs:62-96; OutboxComposition.cs:19-32 | Döngü hatası, ölü mektup uyarısı, iptal, profil uygulaması testsiz. |
| T18 | Orta | OutboxStore.cs:96-111 | Statik transactional EnqueueAsync (rollback/commit) BuildingBlocks testlerinde yok. |
| T19 | Düşük | OutboxStore ctor | Argüman ve iptal yolları testsiz. |
| T20 | Düşük | RetryScheduleIntegrationTests.cs:586-590 | Duvar saatine bağlı pencere, flaky riski. |
| T21 | Düşük | OutboxFanoutSinkTests.cs:93-102 | IntegrationContracts: yalnız TableMerged sınanır; wire-name sabitlenmemiş. |
| T22 | Düşük | ModuleComposition | Tekrarlı/boş Id, sıra testsiz. |
| T23 | Düşük | RetrySqlIdentifierTests.cs:28-39 | Sabit localhost:5432, zayıf assert. |
| T24 | Düşük | PgTestDatabase.cs:846-852 | Yardımcıda xUnit assert. |
| T25 | Düşük | EnvelopeProtectionTests.cs:36-43 | Zayıf "düz metin yok" assert'i. |
| T26 | Düşük (doküman) | RetryPolicy.cs:155-165; RetryPolicyTests.cs:40-48 | SanitizeError bilinçli; operatör hata sınıfını göremez. |

Temiz: Secrets yetki sırası, saklama sınırı (M10), metadata bütünlüğü, yetkisiz okuma, fanout sink, dead-letter ve eşzamanlılık testleri gerçek PG'de, retry tablo adı allow-list, Skip yok.
Doğrulanamayan: T18 Host/Modules dolaylı kapsam, T13 üretimde tetiklenme, T12 erişilebilirlik, Host mimari testleri.
