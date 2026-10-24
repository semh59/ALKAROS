# V1-RMD-725 - Orders düşük önemli kalan bulgular (B031 kod kalemleri)

**Sonuç:** B031'in kalemlerinden 16'sı gerçek çıktı ve düzeltildi; 7'si tasarım gereği ya da zaten doğru olduğu için kodla kapatıldı; 5'i bu görevde yapılmadı (gerekçeleri aşağıda).

## Düzeltilen (hepsi testli, mutasyon 14/14 KILLED, `mutations.log`)
| Kalem | Düzeltme |
|---|---|
| Satır kurs/koltuk değişimi kaydedilmiyordu | ItemSnapshot artık SeatId ve CourseNumber karşılaştırır (tekrar gönderilen turda yalnız kurs değişince UPDATE atlanıyordu) |
| Durum geçmişi sıralaması | `ORDER BY changed_at, order_status_history_id` |
| CancelItem updated_at | kalem güncellemesi `updated_at = now()` yazar |
| Bekleyen liste LIMIT yok | `LIMIT 200` |
| İstemci kopması 500 | OperationCanceled + RequestAborted: 499, Error logu yok |
| Kolona sığmayan metin 503 | StringDataRightTruncation 400 |
| Commit sonrası hub hatası 500 | `CustomerDisplayNotifier` hatayı loglar, istek başarılı döner |
| Devir notu hedef doğrulamadan önce yazılıyordu | `EnsureTransferTargetAsync` not yazımından önce |
| Deadlock yeniden deneme logu | SubmitOrderHandler uyarı loglar |
| Satır başına modifier sınırı | 50 üstü 400 |
| Çağrı "Gördüm" hatası sessizdi | hata gösterilir, çağrı ekranda kalır |
| Bekleyen hesaplar yenileme yarışı | yalnız en yeni okuma listeyi yazar |
| NfcOrder seçenek penceresi | odak içeri, Tab tuzağı, Esc kapatır, odak geri döner |
| Kasa başlığında İngilizce metin | "(Cashier POS)" kaldırıldı |
| maxSelections = 0 | Garson istemcisi 0'ı sınırsız sayıyordu; sunucu gibi "seçim yok" |
| Çift dispatcher kaydı yorumu | NFC yorumu gerçeği söyler (Host'ta DualScreen kaydı çözülür) |

## Kodla kapatılan (hata değil)
- SubmitOrderRequestHash.ChangedBy: aynı anahtarı farklı kullanıcı kullanırsa 409 tasarım gereği; istemci 718 sonrası bunu hatalı tur olarak listeler.
- CHECK_NOT_RECALLABLE: mesaj sabit Türkçe metin, ham istisna değil.
- Yapılandırma InvalidOperation 409: V1-RMD-187'den beri Warning ile loglanıyor.
- Ham error.message gösterimi: ekrana basılan metinler sunucunun Türkçe API kataloğundan geliyor.
- Üç denetim yazma yolu, SaveAsync gidiş-dönüş sayısı: tasarım/performans tercihi; ölçülmüş sorun yok.
- 36/097 NOT VALID ve 151 tam tarama: uygulanmış migration'lar düzenlenmez; yalnız büyük tabloda yükseltme süresi.
- Kaldır için geri alma: taslak satırı kolayca yeniden eklenir; onay hızlı satışı yavaşlatır, bilinçli yapılmadı.

## Yapılmayan
- Kullanılmayan parametreler ve 22 parametreli kurucu kopyası: bulgu kaynağında dosya/satır yok, doğrulanamadı.
- Sabit hex renkler ve metin glifleri (NfcOrder): renkler :root belirteçleri, yalnız birkaç `#fff`; taşındı (tasarım sistemi turu).
- Hata bandı konumu: ekranda yeniden üretilemedi.
- Operasyon runbook kalemleri (geri yükleme sonrası Orders uzlaştırma, kayıp ölü olay türü, kuyruk yaşı alarmı): kaynak metni kayıtlı değil; sonraki operasyon denetiminde (V1-RMD-598 sonrası zincir) ele alınacak.
- Kasa'da sabit "Kasiyer" etiketi: V1-RMD-678 (ekrana kullanıcı adları).
