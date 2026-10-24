# V1-RMD-716 - Masa işaretçisi, kat planı sürüm kuralı ve düşük bulgular

Kapanan V1-RMD-601 bulguları: B020 (sunucu kısmı), B025, B026 ve B033'ün aşağıda sayılan kalemleri. B031, olay tekilleştirme ve ölü olay yeniden oynatma V1-RMD-717'ye ayrıldı.

## Değişiklikler
- B026: kat planı kaydı artık masa satır sürümünü istemez (yalnız yerleşim ve koltuk sürümleri). Masa durumu değişince yerleşim eskimiş sayılmıyordu zaten; sürüm alanı sunucu sözleşmesinden, doğrulayıcıdan ve istemciden kalktı. Eski test "masa sürümü eskidi, 409" bekliyordu; artık masa düzenlendikten sonra kayıt başarılı olur.
- B025: işaretçi projektörü kasaya gönderilmiş hesabı (sent check) açık sipariş/hesap saymaz (transfer ve birleştirme ile aynı kural). QR siparişinin Reserved tuttuğu masa (rezervasyon satırı yok) sahte kayma sayılıp Occupied'a çevrilmez. Transfer, kaynakta işaretçi yoksa en yeni sipariş ve hesabı seçer (projektörle aynı seçim; hesap sorgusunda sıra yoktu).
- B025 çağıran: `TablePointerRepairHostedService` her 10 dakikada yalnız sipariş/hesap işaretçisi eksik, eskimiş veya hayalet olan masaları onarır. Yalnız durum farkı olan masaya (elle "dolu" işaretlenmiş) dokunmaz.
- B020 sunucu: başka bölgedeki katılımcının sürümü kat planında görünmediğinden ayırma düşüyordu; katılımcı sürümü gönderilmezse kilitli güncel sürüm kullanılır, gönderilen sürüm hâlâ korur. Boş masalar da birleştirilebilir (sunucu izin veriyordu, komut listesi vermiyordu).
- B033: ReservationStation iki aynı dal (ağ hatası artık giriş formu yerine "Sunucuya ulaşılamadı / Tekrar dene"), "atomik" ifadesi, oxlint tables uyarısı, kayıt/açıklama uzunluk sınırı 500, kişi sayısı ≤ 200, birleşim katılımcı ≤ 20, masa kapasitesi ≤ 500, yinelenen masa numarası/aktif rezervasyon/birleşim için gerçek neden mesajı, rezervasyonlu masalar tek sorguda (N+1).

## Test kanıtı (gerçek PostgreSQL, tümü exit 0)
CurrentPointers 22, TableTransfer 41, TableMerge 45, TableLifecycle 62, Reservations 34, Host Tables 32, ModuleBoundaries 17, ApiConventions 5; PosTerminal tsc + vitest. Günlükler `logs/`.

## Mutasyon (`mutate.mjs`, `mutations.log`)
20 mutantın 19'u KILLED, kaynaklar geri yüklendi ve doğrulandı. P3 ve U1 ilk koşuda derleme hatası verdi, düzeltilip yeniden koşuldu (KILLED). H3 ilk koşuda SURVIVED: test yalnız boş liste gönderiyordu; alanı hiç göndermeyen istek testi eklendi, yeniden koşuda KILLED. U2 SURVIVED ve denk mutant: eskimiş katılımcı sürümü sonraki UPDATE'in `row_version` koşulunda aynı `TableMergeConcurrencyException` ile düşüyor, davranış farkı yok.

## Doğrulanamayan / açık kalan
- B033'ten yapılmayanlar: PWA "Servis dışı" rengi (WaiterPwa yüzeyi bu görevde yok), bölge/masa formunda meşgul kilidi, X-Correlation-Id okunması, masa no "3"/"03" (ürün kararı: aynı mı değil mi), eski kanıt atıfları ve yorum yanlışları, RebuildAll N+1 (yalnız elle çağrılan yol, çağıranı yok).
- B025: QR akışındaki satırsız Reserved için ayrı bir rezervasyon satırı eklenmedi; projektör bu durumu tanıyacak şekilde düzeltildi.
- B026 için ayrı bir mutant yok; kapsam HTTP testinin (masa düzenlendikten sonra kayıt) eski davranışta 409 vermesidir.
- İstemcide ayırma yine yalnız aynı bölgedeki katılımcı sürümlerini yollar; sunucu eksiği kapatır.
