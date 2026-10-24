# V1-RMD-601 - Tables denetimi (SHA c794e951)

**Sonuç:** Semih senaryosu (3 ve 4 birleştirilir, ayrılır, ürünler doğru masaya döner) KARŞILANMIYOR. Arayüzden ayırmak mümkün değil; birleştirme çoğu zaman FK hatasıyla geri alınıyor; uç doğrudan çağrılsa bile işaretçi ve fazla siparişler yanlış masada kalabiliyor; ödeme sonrası birleşim Aktif kalıyor. Düzeltme: V1-RMD-710 (602'den önce).

## Sayılar
- Ham: ≥157 rapor satırı (api 26, ux 23, frontend 21, backend 24, güvenlik 16, performans 24, doküman 16, sınır 7; test-kalitesi ve veritabanı raporları özet kayıtlı, sayıya katılmadı). Tekil kök: 33 (+ N1) ham listede, + 5 sınır/mimari kalemi (S2, S4–S7; S1=B005, S3=B005).
- Karşı denetim (33+N1): DOĞRULANDI 25, KISMEN 8, YENİ 1, ÇÜRÜTÜLDÜ 0, DOĞRULANAMADI 0 (alt iddia: 5 kısmen çürütüldü; B021 "bölge silme açık masaları denetlemez" çürütüldü: FK engelliyor).
- Yeniden belirlenen önem: Yüksek 6 (B001, B002, B003, B004, B007, B009), Orta 18, Düşük 8, doküman sapması 1. Sınır ajanı: Yüksek S2, Orta S4/S5, Düşük S6/S7 (karşı denetçiden geçmedi, kod okuması).
- Kritik/Yüksek bulgular ana oturumca kodda yeniden açıldı: B002 (`Merge.cs:239-240`, `Guid? ?? Guid` → Guid.Empty) ve B004 (`Unmerge.cs:215-219`, ORDER BY'sız LIMIT 1) kodda görüldü; geri kalanı doğrulayıcı ve ajanlar dosya:satırla kanıtladı, B002/B003 ayrıca qa-test-kalitesi probe'uyla çalıştırıldı.

## Ajan başına çürütme oranı
Hepsi 0 tam çürütme. Abartılı önem: B005, B006, B010, B011, B015, B017, B022, B023, B031 (9); eksik önem: B007 (Orta→Yüksek). Alt iddia çürütmeleri: B009 (modal), B011 (satır sürümü), B021, B024, B027. Ayrıntı: `verifier/verdicts.tsv`.

## Temiz alanlar
Kilit sırası (merge/transfer), ödeme topolojisi danışma kilidi (11/11), kısmen ödenmiş hesap taşıma E2E, depo liste sınırı/indeksler, SQL enjeksiyonu/XSS/CSRF, rate limit, Türkçe metinler, borç taraması (TODO/Skip/boş catch 0), sürümlü olay adları ve outbox yazımı, bağımlılık sürümleri ve lisanslar (GPL/AGPL yok), Tables'tan başka modüle yazma yok (yalnız SELECT; taşımayı tüketiciler yapar).

## Doğrulanamayanlar
Hiçbir bulgu canlı yığında çalıştırılmadı (B002/B003 hariç probe); B005/S1 sıra bozulması ve gerçek yeniden teslim sıklığı; kontrast 1,85:1 ölçümü; PWA'da birleşik katılımcıda sipariş açma; KASA-1'in istemcide görünmesi; xmin dondurma; Tables uçları için performans bütçesi yok; gerçek tarayıcı görselleri.

## Fırsat (çalışanın işini azaltan / hızlandıran)
| Fırsat | Etki |
|---|---|
| Hedef masa listesini duruma göre süz, gerekçeyi hazır seçenekle getir | Birleştirme/transfer dokunuşu ve klavye ihtiyacı azalır (V1-RMD-713) |
| Kat planı taslağını ve modalı yoklamada koru | Yönetici düzenleme kaybı biter (713) |
| Rezervasyona ad, telefon, saat ve otomatik süre dolumu | Gelmeyen misafirin masası kendiliğinden açılır (712) |
| Masa kartında tutar ve süre | Garson/kasiyer ekranı gezmeden karar verir (713) |
| Ölü olay için uyarı + yeniden oynatma | Sessiz veri kaybı görünür olur (711) |

## Bulgu → düzeltme görevi
| Görev | Bulgular |
|---|---|
| V1-RMD-710 | B001, B002, B003, B004, B007 (kapanış), B008, B030, N1 |
| V1-RMD-711 | B005 (S1, S3), S2, B006, B007 (transfer/durum), B021, B022, B024, B025 |
| V1-RMD-712 | B013, B014, B015, B016, B026, B029 |
| V1-RMD-713 | B009, B010, B011, B012, B017, B018, B019, B020, B028 |
| V1-RMD-714 | B023, B027, B031, B032, B033, S4, S5, S6, S7 |

Bilinen kayıtlar: RMD032-F004/V1-RMD-117 (Unmerge), V1-IAM-024 (idempotency), V1-RMD-114/367 (ham hata mesajı, büyük oranda giderilmiş).

## Semih'e metin
Masa birleştirme ve ayırma bugün güvenilir değil: ayırma düğmesi çalışmıyor, birleştirme sık hata veriyor ve ürünler yanlış masada kalabiliyor. Düzeltme görevleri açıldı; önce bunlar kapanacak, sonra sıradaki denetime geçilecek.
