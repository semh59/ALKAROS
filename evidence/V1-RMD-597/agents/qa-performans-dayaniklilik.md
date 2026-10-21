# qa-performans-dayaniklilik — V1-RMD-597 ham rapor özeti

231/231 (≈55 derin okuma, kalanı mekanik tarama). Ölçüm yapılmadı (tests/Performance kapsam dışı); tüm bulgular Kod okuması. Kritik yok.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| P1 | Yüksek | kiosk-lock.js:185-189,221-223,15 | Ağ yok/503'te kilit açma "PIN hatalı" der ve PIN'i siler; PIN yalnız sunucuda doğrulanır, çevrimdışı kilit açılamaz. |
| P2 | Orta | offline-queue.js:371,374; OfflineGrantReconciler.cs:149 | 5xx/429'da bile gönderilen çevrimdışı eylemler kalıcı silinir. |
| P3 | Orta | PostgresOfflineReplayLedger.cs:28-29 | İleri saatli tek eylem ArgumentException ile tüm partiyi düşürür; öncekiler yazılmış kalır. |
| P4 | Orta | OfflineReconciliationEndpoints.cs:53,90; OfflineGrantReconciler.cs:51 | Uzlaştırma ucunda hız sınırı ve Actions üst sınırı yok; eylem başına ~6 sıralı sorgu. |
| P5 | Orta | OfflineGrantReconciler.cs:113-114,149 | Sayma+yazma arası kilit yok; paralel istekle MaxCount aşılır (çevrimiçi yolda kilit var). |
| P6 | Orta | DualScreenApplication.Endpoints.cs:80-127 | Giriş ~10 ayrı işlemle çalışır, işlemsel değil. |
| P7 | Orta | Program.cs:297; PostgresDenialEventSink.cs; 045,047,048 | denial_events, grants, offline bütçeleri, tightenings sınırsız büyür; housekeeping elle ve yalnız device_sessions. |
| P8 | Orta | RoleManagementEndpoints.cs:53,146; AuthorizationDecisionEndpoints.cs | Yönetim gruplarında hız sınırı yok; her ret bir denial_events satırı. |
| P9 | Düşük | ManagementSessionLookup.cs:22,35 | Her yönetim isteği last_seen_at yazar, kimse okumaz; karar ekranı 3 paralel istek aynı satıra. |
| P10 | Düşük | AuthorizationService.cs:23,36 | Her yetki kararı önbelleksiz 2 sorgu. |
| P11 | Düşük | DualScreenApplication.cs:377-380 | terminalId sorguda olan rotalarda hız sınırı IP başına tek kova. |
| P12 | Düşük | AuthenticationService.cs:122-125 | PIN için paralel test yok; kilit kurulana kadar CPU maliyeti. |
| P13 | Düşük | AuthenticationService.cs:156 | /auth/pin yanlış mevcut parolayı saymaz. |
| P14 | Düşük | OfflineAuthorityBudgetPolicy.cs:16; Endpoints.cs:127 | Bütçe 4 saat, oturum 8 saat; yenileme yok (vardiya ikinci yarısında çevrimdışı yetki yok). |
| P15 | Düşük | 050; PostgresOfflineAuthorityBudgetRepository.cs:77-79; 047,008,045 | session_id indeksi yok (ölü yol); 3 indeks fazlalık/kullanılmıyor. |
| P16 | Düşük | ReconnectAsync, GetProcessedOperationIdsAsync | Ölü kod; 5000'de sert hata. |
| P17 | Düşük | MaxUnpagedRows=5000 11 yerde; AuthorizationDecisionsWorkspace.tsx:105 | Sayfalama yok; 5000'de hata. |
| P18 | Düşük (Çıkarım) | PostgresAuthorizationGrantRepository.cs:196; postgresql.tuned.conf:92 | Danışma kilidi bağlantı tutar. |
| P19 | Düşük | 107 FK | NOT VALID yok (bir kerelik). |
| P20 | Doküman | load-baseline-v1.md:100 | "12-hour sessions" vs kod 8 saat. |

Temiz: indeks kapsamı, atomik sayaç (12 paralel testli), giriş zamanlaması, bellekte sunucu durumu yok, 503 eşleme, 8 sn istemci zaman aşımı, kuyruk geri çekilme, uzlaştırma idempotent.
Doğrulanamayan: giriş/PIN gecikmesi ölçülmedi; Docker kaynak IP (X-Forwarded-For); housekeeping zamanlaması; canlı DB kesintisi.
