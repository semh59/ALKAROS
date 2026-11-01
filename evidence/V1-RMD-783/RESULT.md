# V1-RMD-783 - Sonuç

Karar (Semih): (1) iptal/iadede stok geri verilince günlük menü sayacı da düşsün ("sipariş adedi = porsiyon" aynen); (2) mutfakta hazırlanmış kalemin iptali ayrıca fire sayılsın; hazırlanmamış kalemin iptali yalnız geri alma.

## Ne değişti
- Stok geri veren yollar kodda tam iki tane: `SentItemVoidStore` (gönderilmiş kalem iptali) ve `PaymentRefunds` -> `ReturnConsumedShareAsync` (RestoreStock=true iade, kısmi olabilir). İkisi de artık porsiyonu aynı işlemde geri alır. Fatura iptali, `RestoreStock=false` iade ve çevrimiçi sağlayıcı iptali stoğa dokunmadığı için sayaca da dokunmaz.
- "Hazırlanmış" tanımı koddan: `SentItemVoidStore` zaten Sent/Held iptalinde stok iade ediyor, Preparing/Ready iptalinde etmiyordu. Aynı ayrım kullanıldı (ikinci tanım yok); `PostgresKitchenItemStateProvider` da aynı durumları "başladı" sayıyor.
- Tüketilmiş anahtarlı rezervasyon satırı iki kümülatif alan kazandı: `portions_returned`, `portions_wasted` (satır `Consumed` kalır; yalnız anahtarlı satır, kardeş stok satırlarına dokunulmaz). Alanlar yalnız artar; aynı iptal ikinci kez çağrılsa satır ve olay değişmez. İade payı `adet x (önceki+iade)/toplam`, 3 basamağa yuvarlanır, `adet - fire` ile sınırlıdır.
- Yeni olay `inventory.daily-menu-portion-adjusted.v1` (kümülatif tutar) aynı işlemde outbox'a yazılır. Menu tüketicisi önce Reserved ve Consumed'ı (idempotent) uygular, sonra farkı uygular; sıra dışı/tekrar/geç teslim sayacı değiştirmez. Rezervasyon başına advisory kilit.
- Fire bir taşımadır: consumed azalır, waste artar, available değişmez (porsiyon satılmadı ama kullanıldı). Yalnız waste eklense available iki kez düşerdi. Sayaç 0'ın altına inmez (QR/online ile ayrı çift sayım yok: aynı satır, Reserved/Terminal olayları değişmedi).
- Rebuild, drift ve rapor aynı formülü kullanır: consumed = adet - geri - fire, waste = Waste satırları + fire alanı. Rebuild Returned/Wasted uygulanmış-olay kayıtlarını da kurar (rebuild sonrası teslim çift düşürmez).
- Migration 210: `event_type` kısıtına `Returned`, `Wasted`; down türetilmiş satırları siler.
- Karar: iptalde porsiyon geri alma, stoğun gerçekten geri verilmesine (`stockRestored`) bağlıdır (qa-backend bulgusu); tüketim hareketi bulunamazsa sayaç korunur.

## Açık riskler
- Mutfak canlı senkronu kapalıysa KitchenState hiç Sent'ten ilerlemez; pişmiş kalem "geri alma" sayılır (sayaç stoku izler, fire görünmez).
- Hazırlanmış kalem fire'ı yalnız menü sayacında görünür; stok "Fire" raporu (`PortionWaste`, yalnız `Waste` durumlu satırlar) bunu listelemez.
- `ReverseMovementAsync` tek başına porsiyon iadesi yapmaz (çağıran ayrıca guard'ı çağırır; tek çağıran SentItemVoidStore). Yeni çağıran eklenirse bilinmeli.
- Rebuild kilitsiz (eşzamanlı Apply) kalıtsal risk, değişmedi. Eşzamanlı iki teslim için ayrı test yok (kilit kodda, mutasyonla sınanmadı).
- Kapanış kapısı aktif görev olarak V1-RMD-604'ü gösteriyor (başka oturum/eski InProgress); plan commit'i `.git/alkaros-scope-override` ile geçildi, sonra silindi.

## Kanıt (hepsi exit 0; parola loglarda [REDACTED])
- Kırmızı: `red-voidsent` (5 test, beklenen iade/fire yok), `red-menu` (consumed 3, beklenen 2). Yeşil: `final-voidsent` 30/30, `final-menu` 32/32.
- Modüller: Inventory (BalanceProjection, CrossChannelReservation, ManualAdjustments, MovementLedger, PhysicalCounts, PortionReservations/*, ReservationBalanceProjection, StockMaster, WasteRecording, MovementReversal 23/23), Orders (ItemExceptions, OrderAggregate, SubmitOrder), Production, Reporting/MenuInventory 11/11.
- Host: TableDraft 115, Confirmation 39, Void 5, Comp 21, Billing 113, KitchenOperations 73, NfcOrdering 29, OnlineOrdering 202, Menu 7, Production 4, Inventory 20, QrOrdering 35, Composition 22, Reporting 11, InventoryReporting 10. Mimari: ApiConventions 5/5, ModuleBoundaries 17/17. MigrationComposition: Manifest + MigrationSchemaTests/FileLineEnding (filtreli; `f-migcomp*`; ilk koşuda `order.json` aralığı ve 209 geri alma testi 210'a uyarlandı).
- Çalıştırılmayan: `tests/Host/MigrationComposition` içindeki 210 ile ilgisiz testler ve tüketimle ilgisiz diğer Host Experience projeleri. Bellek baskısı yüzünden bir toplu koşu öldürüldü ve projeler tek tek yeniden koşuldu; atlanan proje yok.
- Mutasyon (hepsi kırmızı, derleme hatası 0, kaynak sha256 ile geri): m1 void dal ters, m2 fire üst sınırı yok, m3 tüketici dalı yok, m4 fire consumed'ı azaltmıyor, m5 iade payı yok sayılıyor, m6/m12 rebuild olay kaydı eksik, m7 rapor fire, m8 geç eski olay, m9 stok geri verilmeden iade, m10 taban kaldırıldı, m11 iade geri küçülebilir. `mutation-*` klasörleri.
- plan-audit.txt, consistency-audit.txt, task-scope.txt: 0 hata/0 uyarı, exit 0.

## İnceleme sonrası notlar
- qa-backend: Kritik/Yüksek yok. Orta: iade stok geri verilmeden sayılıyordu -> `stockRestored` koşulu + test (m9 ile kırılır). Düşük: ReverseMovementAsync belgesi (risk olarak yazıldı), tekrar rollback kalıbı (mevcut kalıp, değişmedi).
- qa-test-kalitesi: taban, ondalıklı pay/küçülmeme, fire için rebuild-sonrası teslim, negatif girdi testleri eklendi (m10-m12 ile kırılır). Kalan: gerçek QR iptal servisi uçtan uca çağrılmıyor, Host testi kalem adedi 1, eşzamanlı teslim testi yok, down'ın veri varken sınanması yok.
