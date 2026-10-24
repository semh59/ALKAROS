# V1-RMD-602 - Orders denetimi (SHA 54c6455c)

**Sonuç:** Semih senaryosu ("ağ kopup yeniden denenince sipariş ikilenmez, mutfakta tek fiş çıkar; gönderilmiş kalem iptalinde onay istenir ve stok geri döner") KISMEN karşılanıyor. Sunucu tarafında iki fazlı gönderim yeniden denemesi, stok ve denetim aynı işlemde; kalem iptalinde stok iadesi çalışıyor. Ama garson çevrimdışı kuyruğu kalıcı hataları (stok yetersiz gibi) geçici sayıp arkasındaki tüm turları tıkıyor (Yüksek, B001); Kasa hızlı siparişi yanıt kaybında ikinci siparişi 409'a düşürüyor veya sahipsiz Draft bırakıyor; gönderilmiş kalem iptali üç ayrı commit ve yeniden denemesi "çakışma" gösteriyor. Düzeltme: V1-RMD-718 (603'ten önce), 719–724.

## Sayılar
- Ham: 151 rapor satırı (12 ajan; test-kalitesi 9 + 36 mutant). Tekil kök: 32 (B001–B032).
- Karşı denetim (B001–B031) + ana oturum mutant kanıtı (B032): DOĞRULANDI 18, KISMEN 13, ÇÜRÜTÜLDÜ 1 (B029), DOĞRULANAMADI 0. Alt iddia çürütmeleri: B002 (ikinci sipariş açılmaz, PK engeller), B004 (ekran profilsiz ürün açamaz), B005, B007, B008, B015, B017, B018, B031.
- Önem: Yüksek 1 (B001), Orta 16, Düşük 14, çürütülen 1. B003 kırmızı test (10/93) üretimde sorun değil, test düzeneği; Orta.
- Kritik/Yüksek ana oturumca kodda yeniden açıldı: B001 (`offline-queue.js:220` 409'u geçici sayar, `:251-254` `break`) ve B003 (TableDraft csproj'da 205 yok; üretim `order.json` 205'i içerir). B004 ham önemi Yüksek'ti, doğrulayıcı Orta'ya indirdi (bilinen V1-RMD-702 / PO S1).
- `checks/path-line-check.txt`: bulgulardaki 107 dosya:satır referansının 107'si SHA'da var ve satır sınırı içinde (varlık/aralık kontrolü; alıntı metni karşılaştırması yapılmadı).

## Ajan başına çürütme oranı
Tam çürütme: yalnız qa-sinir-mimari 1 (B029, Held fişi yazdırılıyor). Diğer ajanlarda 0. Abartılı önem: B002, B004, B005, B010, B014, B017, B029. Ayrıntı: `verifier/verdicts.tsv`.

## Temiz alanlar
İki fazlı gönderim yeniden denemesi (istemci tek payload.id, submit-draft operationId, hash'te satır sürümü yok); eşzamanlı aynı anahtarlı istekler; stok+fiş+durum+anahtar aynı işlemde; gönderilmiş kalem iptalinde sebep, `bills.void` izni ve stok iadesi aynı işlemde; NFC çift sipariş sunucuda engelli ve yaş kısıtı sunucuda zorlanıyor; kuruş yuvarlama; Held kalem fişi ve iptali tutarlı; Kasa sepetinde tekrar gönderime karşı `order_item_id` PK koruması; üretim migration listesi 205'i içeriyor; paket/lisans (GPL/AGPL yok); dosya yolu atıfları.

## Doğrulanamayanlar
Okuma kapsamı sınırlı: qa-backend 24/142, qa-ux 17/142, qa-güvenlik 17/142, qa-mevzuat 15/142, qa-performans 22/142 dosya derin okundu; kalanı doğrulanamadı. B024'ün submit/fire-course geçmişi, B028 rozet ve "İptal iste" ifadesi, B030'un 8 ve B031'in yaklaşık 35 alt kalemi (V1-RMD-724'te tek tek kapatılır), B007 yasal zorunluluk, B016/B017/B026 tetikleyici etkisi (Çıkarım), geçişli lisanslar, Kasa/Garson E2E (T9) koşulmadı, canlı yığında hiçbir şey çalıştırılmadı. Doğrulayıcı Çıkarım notu: masada Accepted NFC/QR siparişi varken garson tur eklerse yeni sipariş açılıp masa göstergesi ezilebilir, kasa kuyruğu yalnız Submitted gösterir: para kaybı riski, V1-RMD-723'te kodla sınanır.
Test kalitesi: 36 mutantın 28'i öldü, 8 hayatta kaldı (B032, V1-RMD-720).

## Fırsat (çalışanın işini azaltan / hızlandıran)
| Fırsat | Etki |
|---|---|
| Hatalı turu listede düzeltip yeniden gönder (Sil yerine) | Garson turu baştan girmez (718) |
| Kasa gönderiminde başarısızlıkta sepeti koru | Kasiyer yeniden yazmaz, çift fiş çıkmaz (719) |
| Her işlemde 4 yenileme yerine 1 | Yoğun saatte Kasa daha hızlı (719) |
| NFC ekranında "onay bekliyor" mesajı ve süre dolumu | Müşteri ve garson ne olduğunu bilir (721) |
| Kayıp yanıtta iptal/ikram yeniden denemesi ilk cevabı döner | Garson "çakışma" görmez (722) |

## Bulgu → düzeltme görevi
| Görev | Bulgular |
|---|---|
| V1-RMD-718 | B001, B028 |
| V1-RMD-719 | B002, B011, B027 |
| V1-RMD-720 | B003, B032 |
| V1-RMD-721 | B005, B006, B007, B018, B019, B020, B021, B026 |
| V1-RMD-722 | B009, B010, B013, B014, B024, B025 |
| V1-RMD-723 | B012, B015, B016, B017 |
| V1-RMD-724 | B008, B022, B023, B030, B031 |
| V1-RMD-702 (bilinen, PO S1) | B004 |
| Çürütüldü | B029 |

## Semih raporu
Orders'ta en büyük risk garson cihazı: stok yetmediği için reddedilen tek bir tur, o garsonun sonraki tüm turlarını sonsuza dek bekletiyor ve ekranda silme yolu yok (718, 603'ten önce yapılacak). Kasa hızlı siparişinde ağ kopması kalıcı 409 ve sahipsiz taslak bırakabiliyor (719). Gönderilmiş kalem iptali tek işlemde değil; biri düşerse mutfak pişirir ama kalem iptal görünür (722). Para kaybı yönünden en önemlisi: Vergi profili olmayan eski ürünler %0 KDV ile satılıyor; bu senin S1 kararına (varsayılan KDV) bağlı. Yaş kısıtlı NFC siparişi onaysız kalırsa masa kilitli kalıyor (721).
