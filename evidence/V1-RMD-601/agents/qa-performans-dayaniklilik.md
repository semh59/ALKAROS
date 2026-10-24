# qa-performans-dayaniklilik - V1-RMD-601 (SHA c794e951)

Kapsam: 123/123 (≈30 dosya tek tek okundu, 123'ü borç kalıplarına tarandı: TODO/NotImplemented/Skip/pragma/boş catch/Fake = 0). Koşu kayıtları yeşil. Dinamik doğrulama yok; tüm bulgular kod okuması.
Semih senaryosu hükmü: PosTerminal'den tamamlanamaz (B1); API'de yalnız dar koşulda doğru (her masada tek sipariş, ana masanın kendi siparişi var, arada düzenleme yok); ana masa boşsa / katılımcıda çok sipariş varsa / arada transfer varsa ürünler yanlış masada kalabilir (B2, B3, B5, B6).

| No | Önem | Kanıt | Sorun / etki / öneri |
|---|---|---|---|
| A1 | Yüksek | `workspace.tsx:205,241`, `TableWorkspace.tsx:333`, `FloorPlanWorkspace.tsx:203-204` | 8 sn yoklama her seferinde "loading" yapar; FloorPlanWorkspace sökülür; kat planı taslağı ve açık modal ~8 sn'de kaybolur; yorum `workspace.tsx:235-238` yanlış. |
| A2 | Orta | `workspace.tsx:241` | Hata sonrası yoklama kalkar; kesinti bitince ekran kendiliğinden toparlanmaz. |
| A3 | Orta | `ReservationStation.tsx:144`, `workspace.tsx:299`, `TableWorkspace.tsx:289` | canManage={false}: "Onayla" sessizce hiçbir şey yapmaz; test yalnız başlığı kontrol eder. |
| A4 | Düşük | `ReservationStation.tsx:43` | İki dal aynı; ağ hatası da giriş formuna düşer. |
| A5 | Orta | `tables.js:28`, `waiter-app.js:879`, `transfer.js:56-59` | PWA masa listesi yoklamasız; hata listeyi boş diziyle ezer; transfer 409'da liste yenilenmez. |
| A6 | Orta | `TableWorkspace.tsx:168,291`, `tableApi.ts:52` | Hedef listesi tüm aktif masaları gösterir; zorunlu serbest metin gerekçe; zaman aşımında "tekrar deneyin" (işlem yapılmış olabilir). |
| A7 | Düşük | `TableWorkspace.tsx:497,505,506,508` | v{rowVersion}, "Satır sürümü", ham GUID çalışana gösterilir. |
| B1 | Yüksek (bilinen RMD032-F004/V1-RMD-117) | `TableManagementContracts.cs:288`, `FloorPlanWorkspace.tsx:437` | Sunucu "Unmerge" yayınlamaz; birleşim arayüzden ayrılamaz. |
| B2 | Yüksek | `Unmerge.cs:216-218,140-154` | Unmerge işaretçisi ORDER BY'sız LIMIT 1, geri verilecek siparişleri dışlamaz; ana masa boşsa katılımcı siparişini gösterir; test sıra şansıyla geçer. |
| B3 | Yüksek | `Merge.cs:239-240,252-253`, `PostgresOrderRepository.cs:207` | Tüm açık sipariş/hesap taşınır, yalnız biri saklanır; fazlası ayırmada ana masada kalır. |
| B4 | Yüksek | `Transfer.cs:290-291,304-305`, `Merge.cs:242-246`, `DualScreenStore.Orders.cs:114-115` | Olaylar yalnız masa kimliği taşır; kaynak masa hemen Available; gecikmeli olay yeni oturan müşterinin siparişini hedefe taşır; Merge.cs yorumu yanlış. |
| B5 | Yüksek | transfer/status/PUT'ta `table_merges` 0 | Transfer/SetAvailable/düzenleme aktif birleşimi bilmez; ana masa taşınırsa ayırma etkisiz kalır. |
| B6 | Orta | `OutboxStore.cs:354-355`, `RetryPolicy.cs:13` | Olay sırası yalnız created_at; başarısız TableMerged yeniden denemede TableUnmerged'dan sonra işlenebilir; 3 denemeden sonra ölü mektup (yönetici yeniden kuyruklama var). |
| B7 | Orta | `TableEventOrderConsumer.cs:14-17` | "Idempotent by construction" yalnız kaynak boşluğuna dayanır; olay kimliği dedupe'u yok. |
| B8 | Orta | `FloorPlanRepository.cs:37,145,170`, `Unmerge.cs:26-98`, `Application.cs:579-580` | Kilit sırası ters (kat planı vs ayırma); 40P01/40001 "DATABASE_UNAVAILABLE" 503. |
| B9 | Orta | `024-table-merges.up.sql:25-28` | Aktif birleşim/rezervasyon için kısmi benzersiz indeks yok; primary başka birleşimin primary'si engellenmez. |
| B10 | Düşük | `Unmerge.cs:163-164` | Kapanmış siparişe bağlı hayalet "Dolu" masa. |
| B11 | Orta | `Application.cs:549`, idempotency_allowlist | Tekrar gönderimde ayırma "bulunamadı", birleştirme/transfer "çakışma" (işlem başarılı olmuş olabilir). Bilinen V1-IAM-024. |
| B12 | Düşük | `TableMergeRequest.cs:20` | Katılımcı sayısı sınırsız. |
| B13 | Orta | Rebuild çağıranı yok; projektör DESC vs transfer ASC; `Projector.cs:242-286` 5N+1 | Onarım işi yok; kayma kendiliğinden düzelmez. |
| C1 | Orta | `Application.cs:325`, `tableApi.ts:82` | Otomatik rezervasyon süre dolumu yok; UI expiresAt göndermez; expire süre kontrolsüz; ad/saat/telefon yok. |
| C2 | Düşük | `Application.cs:132` | Reserved masa başına ayrı sorgu (N+1). |
| D1 | Düşük | `ZoneConcurrencyStore.cs:20,72` | xmin sürüm olarak; dondurma sonrası davranış doğrulanamadı. |
| D2 | Düşük | `workspace.tsx:196-198`, `FloorPlanWorkspace.tsx:252-253` | Kat planı hatası yutulur; ApiError dışı sınıf; bilinen V1-RMD-114/367. |

Temiz: kilit sırası (merge/transfer), ödeme topolojisi danışma kilidi (`bill-settlement:` 10 dosya), kısmen ödenmiş hesap taşıma E2E 11, depo listeleri MaxUnpagedRows=5000, indeksler, borç taraması, Türkçe metinler, garson transferi (iyi örnek), sunucu hata eşlemeleri.
Doğrulanamayan: hiçbir bulgu çalıştırılmadı; B6 sıklığı; D1; Tables uçları için performans bütçesi/ölçümü yok; PWA'da birleşik katılımcıda sipariş açma; `terminal-read` hız sınırı değeri.
