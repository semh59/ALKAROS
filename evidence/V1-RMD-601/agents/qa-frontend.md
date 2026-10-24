# qa-frontend - V1-RMD-601 (SHA c794e951)

Kapsam: 13/13 istemci dosyası (123 dosyadan 13'ü bu role ait; sunucu dosyaları sözleşme için seçerek okundu). Çalıştırılan: oxlint (features/tables + ReservationStation.tsx): exit 0, 2 uyarı (F-17); tsc exit 0 ve vitest 37/37 ana oturum kaydı. Not: SKILL madde 3 (hata kodu → Türkçe sözlük) sapması; sunucu mesajları Türkçe, ham sızıntı bulunmadı (kusur sayılmadı).

| No | Önem | Kanıt | Sorun / etki / öneri |
|---|---|---|---|
| F-1 | Yüksek | `TableManagementContracts.cs:283-293`, `:255`; `FloorPlanWorkspace.tsx:437-438`; `grep '"Unmerge"' src --include=*.cs` yalnız `:255`; remaining-gaps-register'da Unmerge 0 | Sunucu "Unmerge" komutunu hiç yayınlamaz; ölü devre dışı "Birleşimi ayır" düğmesi (title geliştirici metni). Semih senaryosu hiçbir istemciden yapılamaz; PWA'da birleştirme/ayırma yok. |
| F-2 | Yüksek | `workspace.tsx:205`, `:241-242`; `TableWorkspace.tsx:333-334`; `FloorPlanWorkspace.tsx:203-204`; yorum `workspace.tsx:232-238` yanlış | Her load() "loading" yapar (8 sn yoklama, her mutate); çocuk FloorPlanWorkspace sökülür; kat planı düzenleme taslağı en geç 8 sn'de silinir; ekran titrer, odak/modal kaybolur. Sessiz yenileme; düzenlemede yoklamayı durdur. |
| F-3 | Yüksek | `ReservationStation.tsx:144`, `workspace.tsx:294-299`, `TableWorkspace.tsx:289`; test `ReservationStation.test.tsx:60-79` yalnız boş liste | İstasyon `canManage={false}`: onAction undefined, "Onayla" sessizce hiçbir şey yapmaz; ekranın tek amacı çalışmaz. Yetkiyi allowedCommands'a göre ayır; dolu masa + Onayla testi. |
| F-4 | Yüksek | `TableWorkspace.tsx:324-327`, `:365`; `primitives.tsx:138-139`; `workspace.tsx:250`; test `TableWorkspace.test.tsx:209` document.body okur | Eylem hatası (PAYMENT_UNSETTLED, 409) sayfa `feedback` şeridine yazılır, modal açık ve perde arkasında; içeride formErrors boş. Kullanıcı hata görmez. Hatayı formErrors'a yaz. |
| F-5 | Orta | `workspace.tsx:250`, `TableWorkspace.tsx:327`, `:472` | 409'da action() fırlatınca load() çağrılmaz ama mesaj "Güncel durum yüklendi" der; modal eski rowVersion'la açık kalır; tekrar Onayla aynı 409. |
| F-6 | Orta | `tables.js:41`, `TableManagementContracts.cs:146` (occupiedSince), `TableManagementHttpTests.cs:277`, `tableAgeBadgeHtml tables.js:151` | PWA eski alan adını (currentOrderOpenedAt) okur; masa yaşlanma rozeti (V1-WTR-019) üretimde ölü. (qa-backend B-13.) |
| F-7 | Orta | `tables.js:20-21`, `:27-28` | loadZones/loadTables hata/çevrimdışıda iyi listeyi boş listeyle ezer; garson "Bu bölgede masa yok" görür; `offline-queue.js:268` sonrası tetiklenebilir. |
| F-8 | Orta | loadTables çağıranları: `waiter-app.js:357,864`, `offline-queue.js:268,322`, `transfer.js:63,136`; `waiter-app.js:879` | PWA masa listesi yalnız kendi eylemlerinden sonra yenilenir, yoklama yok; PosTerminal 8 sn. |
| F-9 | Orta | `FloorPlanWorkspace.tsx:252-253`, `tableApi.ts:14,56`, `workspace.tsx:258-261`, test `FloorPlanWorkspace.test.tsx:158` | `TableManagementApiError` ApiError'dan türemez: `instanceof ApiError` hep yanlış; çakışma İngilizce regex ile aranır; "Taslağınız korundu" hiç çıkmaz; test uydurma ApiError ile geçer. Koda bak. |
| F-10 | Orta | `workspace.tsx:355-356,213`, `TableWorkspace.tsx:355`, `FloorPlanWorkspace.tsx:327` | lastUpdated ham ISO UTC ekranda. tr-TR/Europe/Istanbul biçimi. |
| F-11 | Orta | `TableWorkspace.tsx:209`, `:291`, `:477`; PWA `transfer.js:52` | Masa değiştir/birleştir/ayır/rezerve/iptal için zorunlu serbest metin gerekçe; PWA sabit metin kullanır. Varsayılan gerekçe. |
| F-12 | Orta | `TableWorkspace.tsx:293,299,314-318`, `tableApi.ts:82,104-108,113`, `Contracts.cs:155-162` | Rezervasyon yalnız kişi sayısı + gerekçe; ad/telefon/saat yok; reservedAt/expiresAt gönderilmez. |
| F-13 | Orta | `TableWorkspace.tsx:302-307`, `workspace.tsx:190-198`, `PostgresTableMergeRepository.Unmerge.cs:80-81` | Ayırma katılımcıları yalnız o bölgenin kat planından; bölgeler arası birleşimde sunucu "Missing expected row version" reddeder; kat planı hatası sessiz yutulur. |
| F-14 | Orta | `ReservationStation.tsx:43` (iki dal aynı "anonymous") | Ağ/500 hatası da giriş formuna düşer. |
| F-15 | Düşük | `TableWorkspace.tsx:183-221`, `:228-285` | Bölge/masa oluştur-düzenle'de meşgul kilidi yok. |
| F-16 | Düşük | `TableWorkspace.tsx:144,216,461` | Yeni masa formunda bölge seçili bölgeden alınmaz; varsayılan kapasite 2. |
| F-17 | Düşük | oxlint exit 0: `TableWorkspace.test.tsx:7:10` kullanılmayan ApiError; `ReservationStation.tsx:111:115` autoFocus | İki uyarı. |
| F-18 | Düşük | `TableWorkspace.tsx:472,495,497,506,508` | "v3 satır sürümü", "Sunucu yetkisi ve v… kaynak gerçek", tam GUID, "Sipariş #order-12" çalışana gösterilir. |
| F-19 | Düşük | `tables.js:143-155,159`, `tables.css:141,150` (.7rem) | PWA ızgara her yenilemede innerHTML; odak kaybı; küçük rozet. |
| F-20 | Düşük | `TableWorkspace.test.tsx:242`, `FloorPlanWorkspace.test.tsx:172`, `ReservationStation.test.tsx:87` | Üç axe taramasında color-contrast kapalı; modal/hata durumları taranmaz. |
| F-21 | Düşük | `tableApi.ts:41-49`; sunucu Tables'ta Idempotency-Key 0 | İstemci idempotency anahtarı yok; her denemede yeni X-Correlation-Id; yeniden deneme sürüm kontrolüne kalır (skill madde 7). |

Temiz: ham HTTP/İngilizce enum yok; sunucu mesajları Türkçe; sözleşme alan uyumu; actionBusy + modal kilidi + 8 sn zaman aşımı; PWA openTable eski yanıtı eler; kat planı erişilebilir liste/klavye/aria; 48 px hedefler; telemetri kimlikleri; tsc/vitest.
Doğrulanamayan: sunucu tarafı konular (işaretçi kayması, kısmen ödenmiş masa, olay idempotanlığı) kapsam dışı; PWA'da birleşik masa davranışı; gerçek tarayıcıda F-4 perdesi; V1-RMD-114/367 kodda çözülmüş görünüyor (`TableWorkspace.tsx:82-95`, `workspace.tsx:218-224`); V1-RMD-128 dilimde yok. oxlint çıktısı: tmp/qa/V1-RMD-601/qa-frontend/oxlint.txt.
