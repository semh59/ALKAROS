# qa-operasyon-gozlemlenebilirlik - V1-RMD-600 (SHA e00a61c0, DAR kapsam)

Kapsam: 14 kalemlik yüzey (CatalogPriceRecomputeHostedService, CatalogManagementEndpoints/Store, ApiErrorCatalog CatalogManagement alanı, ApiExceptionHandler; karşılaştırma LowStockAlertHostedService, src/Host/Observability/**). Build/test koşulmadı.

| No | Önem | Yer | Sorun | Öneri |
|---|---|---|---|---|
| O-001 | Orta | CatalogPriceRecomputeHostedService.cs:63-69 (catch + LogLoopFault); Catalog'a özel ILoggerProvider yok | Döngü hatası yalnız ILogger (EventId 5400); hata olay sistemine/Telegram'a gitmiyor; DB uzun erişilemezse zamanlanmış fiyat devreye girmez / süresi biten fiyat tahsil edilmeye devam eder, operatör fark etmez. | Üst üste N hatada bildirim; "fiyat yeniden hesabı bayat" dedektörü. |
| O-002 | Orta | HostedService.cs:30,49-80; Observability'de catalog/price eşleşmesi 0 | Worker için son başarılı çalışma zamanı/sağlık ucu yok; durma ile "değişiklik yok" ayrılamıyor. | Son başarılı tick zamanını sağlık göstergesi yap. |
| O-003 | Orta | CatalogManagementStore.cs:199, :217-218 | Fiyat satırı yazılıp current_price güncellemesi düşerse API 503, satır DB'de; tekrar denemede 409 OVERLAPPING_EFFECTIVE_PRICE; düzelme 60 sn worker'a kalır; ekran açıklamıyor (api A-004, db V-004). | Tek transaction; 409 mesajına "kayıt oluşmuş olabilir". |
| O-004 | Orta | CatalogManagementStore.cs:76-132,185-222; CatalogManagementEndpoints.cs:174-181 | Yazma yolunda log/metrik/iz yok; "fiyat neden/kim değiştirdi" log'da da yok (denetim kaydı eksikliği bilinen G-001). | Yapılandırılmış bilgi log'u + sayaç. |
| O-005 | Orta | CatalogManagementStore.cs:230-238; HostedService.cs:92-104,112-132; PostgresProductRepository.cs:169 | current_price yazılırken row_version artmıyor (bilinen: backend B-006, db V-003). UpdateAsync SET listesinde current_price var mı tam okunmadı. | Davranışı belgele / UpdateAsync kapsamını netleştir. |
| O-006 | Düşük | CatalogManagementEndpoints.cs:267, :208-210 | İstisna mesajları İngilizce (kullanıcıya ApiErrorCatalog üzerinden Türkçe, sızıntı yok) ama ArgumentException için sabit "İstek doğrulanamadı." alan söylemiyor. | Alan bazlı Türkçe mesaj. |
| O-007 | Düşük | HostedService.cs:30 | Aralık 60 sn sabit, yapılandırılamıyor; belgede yok. | Yapılandırma veya runbook notu. |
| O-008 | Düşük | LowStockAlertHostedService.cs:28 | EventId'ler (5400, 5810) dağınık, merkezi tablo yok. | EventId tablosu. |

Doküman eksiği: docs/recovery/** ve docs/operations/** içinde catalog/katalog 0 sonuç (katalog için yedek/geri yükleme anlatımı yok; genel pg_dump runbook şema bazlı değil, backup-restore-runbook.md:126); data-housekeeping.md'de product_prices saklama politikası yok.
Temiz: worker döngüsü iptali temiz, hata yutulmadan loglanıyor, döngü ölmüyor; SQL idempotent (IS DISTINCT FROM), tohum fiyatı silmiyor (test CatalogManagementHttpTests.cs:460); çok örnekli çalışmada kilit gerekmiyor; worker testleri var (:413,460); 5xx'te TraceIdentifier log + yanıt, 503 Retry-After; mesajlarda kişisel veri yok.
Doğrulanamayan: testler koşulmadı; O-005 son nokta; yedek geri yüklemede catalog.* dahil mi (dar kapsam); migration'larda şifreli alan taranmadı.
