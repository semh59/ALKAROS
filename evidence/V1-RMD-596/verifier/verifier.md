# qa-bulgu-dogrulayici çıktısı — V1-RMD-596 (aynı SHA; kaynak kod baseline ile aynı)

Sonuç: 26 tekil bulgu + U26'nın 16 alt maddesi kodda açıldı. 19 DOĞRULANDI, 7 KISMEN, 0 ÇÜRÜTÜLDÜ. Test koşulmadı, DB'ye bağlanılmadı; tek çalıştırma qa-guvenlik-yetki probe.dll (exit 0).

| No | Sonuç | Doğrulanan önem | Kanıt | Gerekçe |
|---|---|---|---|---|
| U01 | DOĞRULANDI | Orta (Yüksek'ten indi) | OutboxStore.cs:140; OnlineOrderingModule.cs:68 | Sağlayıcı zaman aşımı deneme sayılmaz, parti ~5 dk takılır, hiç ölmez; sağlayıcılar UNVERIFIED DRAFT, bugün tetiklenmez; katalog yayını muaf (CatalogPublicationService.cs:186). |
| U02 | DOĞRULANDI | Yüksek | UnitConverter.cs:137; StandardUnits.cs:30; InventoryAdjustmentService.cs:83; RecipesSection.test.tsx:139 | Ekran koli→adet kaydını kabul eder, stok hesabı 1:1 yapar. Bilinen: INDEPENDENT_DEEP_AUDIT_2026-09-26.md:82 (K7); V1-RMD-319 aynı-boyut kısmını bırakmış; V1-RMD-620 kabul 5 geçemez. |
| U03 | DOĞRULANDI | Orta | UnitConverter.cs:120-127 vs 168-169 | CanConvert geçer, Convert fırlatır; hata görünür. |
| U04 | DOĞRULANDI | Yüksek (relay kurulduğunda) | compose.yaml:126, connector 215-260; PostgresRelayTunnelStore.cs:86; qr-relay-setup-guide.md:45-47 | Connector master key almaz; Docker'da çalıştırılmadı. |
| U05 | KISMEN | Düşük | QrOrderIntegrationEvents.cs:41; serializer yorumu :7 | Yorum yanlış; aynı not zaten pending_order_submissions ve order_items'ta düz metin, outbox yeni ifşa değil; saklama kısmı U06'ya katıldı. |
| U06 | DOĞRULANDI | Orta | Program.cs:286; 003 indeks | dispatched/dead süresiz kalır; bilinen V15-OBS-003 (Planned). |
| U07 | DOĞRULANDI | Orta | RetryPolicy.cs:154 | Tüketici istisnası hiçbir yere yazılmaz; OutboxStoreTests.cs:102 beklentisi güncellenmeli. |
| U08 | KISMEN | Orta | RetryPolicy.cs:13 | Tam DB kesintisinde bağlantı açılamaz, deneme sayılmaz; yalnız tüketici hatası + sağlam bağlantı durumunda tetiklenir; profilsiz tüketici 4 (8 değil). |
| U09 | KISMEN | Düşük | SecurityDetectors.cs:72,79 | Dispatcher açılıştaki ölüleri loglar; yalnız izleyici (ekran/Telegram) atlar. V1-RMD-288 Done. |
| U10 | DOĞRULANDI | Orta | OutboxStore.cs:178-180 | Zehirli mesaj ölmez; U01 ile aynı kök. |
| U11 | DOĞRULANDI | Orta | OutboxStore.cs:145,272-274 | Her restart'ta ≤50 mesaj 5 dk gecikebilir; at-least-once. |
| U12 | KISMEN | Düşük | DispatchAsync | Tüketici başına zaman aşımı yok ama mevcut bağımlılıklar sınırlı. |
| U13 | DOĞRULANDI | Düşük | OutboxDispatcherHostedService.cs:27,85 | Kesintide saniyede bir Error logu. |
| U14 | DOĞRULANDI | Orta | KitchenOperationsStore.cs:197-201 | Kabul edilmiş tasarım ama ayar okuması/Enqueue geçici hatası da olayı düşürür. |
| U15 | KISMEN | Orta (Yüksek'ten indi) | KeyFailureTests.cs:66,76-88 | Test boşlukları gerçek; "10/12 mutant" doğrulanamadı (loglar silinmiş). |
| U16 | DOĞRULANDI | Düşük | probe exit 0 | Tip sözleşmesi bozuk, fail-closed. |
| U17 | DOĞRULANDI | Düşük | SensitiveEnvelope.cs:74-83 | DB yazma yetkisi gerekir. |
| U18 | DOĞRULANDI | Orta | ModuleRegistry.cs:97 | Bağlı olmayan yetenekler; V1-SEC-002 Done. |
| U19 | DOĞRULANDI | Orta | ModuleBoundaryTests.cs:19-46 | Dört modül izlenmez; Host edge testi (V1-RMD-659) de zayıflar. |
| U20 | DOĞRULANDI | Düşük | OutboxStore.cs:225 | Sıra garantisi yok; tüketiciler dayanıklı olmalı (Kitchen remark :25-26). |
| U21 | DOĞRULANDI | Düşük | serializer :22-25; OutboxEnvelope.cs:20-37 | Yalnız şema evriminde risk. |
| U22 | KISMEN | Doküman sapması | — | Alt maddeler: inbox kısmen doğru (ProviderInbox dedupe yapıyor; yalnız "3 denemede ölü" karşılıksız), 3 idempotency hata kodu, "max 3" profilleri anmıyor, btree_gist DROP anlatımı, project-manifest 14/59 yok, OutboxEnvelope/serializer yorum çelişkisi, UnitConverter yorumları U02 ile çelişir. |
| U23 | DOĞRULANDI | Düşük | — | Ölü şema; bilinen V1-RMD-134. |
| U24 | DOĞRULANDI | Orta | UnitConversionLoaderHostedService.cs:69-72; RecipeManagementEndpoints.cs:104 | Açılışta kısa DB kesintisi → özel dönüşümler restart'a kadar yok; DB yazımı başarısızsa bellek-DB ayrışır. |
| U25 | KISMEN | Düşük | — | 4 modül Transient + 6 Host TryAdd (iddia 8); etki yok; a-b = a_b gerçek; "Relay politikasına bağlı" doğrulanamadı. |

U26 alt maddeleri: DOĞRULANDI Düşük — bellek sıfırlama yok; TimeProvider yok; MaxAge Zero; requeue gerekçesi sabit; 404 metni; log ayrıntısız; kısıt adı/IF NOT EXISTS; expires_at indeksi; Enum.Parse in_flight (bugün tetiklenmez); Npgsql lisansı izin listesinde yok; Host/Outbox testsiz. KISMEN — Enqueue(tx) doğrudan testsiz (dolaylı kapsanıyor). DOĞRULANAMADI — redaksiyonda alan adları, accessor adı mesajda, InMemory thread-safe (tüketici 0), 4 paket lisans ifadesi, flaky zaman penceresi.

Temiz: nonce gerçekten rastgele, kalıcı zarfta düz metin yok, tablo adı allow-list, lease_generation çitlemesi.
Doğrulanamayan: U15 mutant sayıları, U04 Docker koşusu, U05 sağlık verisi sınıflandırması, U25 Relay politikası bağı.
