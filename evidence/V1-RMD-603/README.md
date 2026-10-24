# V1-RMD-603 - Billing denetimi (SHA feb38368)

**Sonuç:** Semih senaryosu ("dört kişilik masa ürün bazında bölünür, birine %10 indirim, biri nakit biri kart, kalan sıfırda hesap kapanır, fişte KDV doğru") bugün uçtan uca SAĞLANAMIYOR. Ürün bazlı bölme tasarımı tahsilat ekranına bağlı değil ve indirimli hesapta ürün bazlı bölme reddediliyor; kart payı olan hesapta müdür onayı alınana kadar kimse ödeyemiyor (Token/Beko bağımlılığı, bilinen); bilgi fişinde KDV satırı yok ve ara toplam kalemlerle uyuşmuyor. En ağır iki sorun: tüm tutarı kapatan indirim "Ödendi" gösterip hesabı açık bırakıyor (B001); aynı iade isteği ağ hatasından sonra tekrar gönderilirse ikinci nakit iade çıkıyor (B002). Düzeltme: V1-RMD-726…738 (604'ten önce).

## Sayılar
- Ham: 174 rapor kalemi (11 ajan, test-kalitesi 59 mutant ayrıca). Tekil kök: 40 (B001–B040) + doğrulayıcının 2 yeni bulgusu (N1, N2).
- Karşı denetim: DOĞRULANDI 19, KISMEN 18, ÇÜRÜTÜLDÜ 3 (B003 üretim kusuru değil test fikstürü; B005 ve B006 davranış doğru, yalnız test boşluğu), yeni 2, DOĞRULANAMADI 0 (alt iddialar: B021 FK dokümantasyonu, B022 fikstür kapsamı, B034 409 kurtarma, B039 alt kalemleri).
- Önem (doğrulayıcı sonrası): Yüksek 2 (B001, B002), Orta ~22, Düşük ~16. Ajanların 7 Yüksek adayından 5'i indirildi/çürütüldü.
- Kritik/Yüksek ana oturumca kodda yeniden açıldı: B001 (`BillPaymentClosureCalculator.cs:71` `adjustedPayable > 0 &&`; `AdjustmentCalculator.cs:58` yalnız payable'ı aşan indirimi reddeder) ve B002 (`PaymentRefunds.cs:174-175` bütçe yolunda aynı işlemde Completed; `183-refund-requests.up.sql:30-32` benzersiz dizin yalnız açık istekleri kapsar).
- Taban koşu (kırmızı test = bulgu): modül projeleri yeşil (17+44+17+29), StaticApps 120/120, tsc 0; PaymentTender 52/53 (B003: test fikstürü, `runs/PaymentTender.log`); Host.Experience.Billing 70/71 (B038: allocation sırası, ajan koşusunda yeşil, `runs/Billing.log`); oxlint exit 1 (dilimde BillSplitWorkspace.tsx:145 uyarı). Ayrıntı `runs/RED-TESTS.md`.
- Mutasyon (test-kalitesi): 59 denendi, 26 KILLED, 32 SURVIVED (ilgili boşluklar B037 → V1-RMD-737).
- `checks/path-line-check.txt`: findings-raw.md'deki 19 dosya:satır referansının 19'u SHA'da var ve aralıkta (varlık/aralık kontrolü; alıntı karşılaştırması yok). Ajan raporlarındaki satırlar ayrıca taranmadı.

## Ajan başına çürütme oranı
Tam çürütme: qa-backend B-02 (B003) ve qa-test-kalitesi T8/T10 (B005, B006) üretim kusuru olarak yanlış sınıflandı (test boşluğu doğru, davranış doğru). Abartılı önem: B004 (503 değil 400), B007, B010, B016, B021, B022, B026, B029. Ayrıntı: `verifier/verdicts.tsv`.

## Temiz alanlar
KDV formülü (100 TL → 9,09; 9,99 TL → 0,91); servis ücreti oluşturan uç yok, bahşiş KDV'siz ve öneri yok; iptalde zorunlu gerekçe, ödenmiş hesap iptal edilemez, aynı işlemde audit; isteyen kişi kendi iadesini onaylayamaz; indirim tahsil edilenin altına inemiyor (V1-RMD-410), indirimli tavan (298), anahtarlı tek yol (112) gerilememiş; kilit sırası kodda döngü üretmiyor; sorgular parametreli; lisanslar temiz; SQL interpolasyonu yalnız sabit tablo adı; modül kenarları ve Host onaylı kenarlar dokümanla uyumlu.

## Doğrulanamayanlar
Kapsam: güvenlik 74/120, performans ~46/120, backend 57/120, ux 12/21 dosya derin okundu. PosTerminal billing vitest ve Playwright E2E 04/18 koşulmadı; PosTerminal ekranları tarayıcıda görülmedi; fiş yazdırma çıktısı; indirim izni olmayan rolün 202 akışı ve garson-yalnız rol; ikinci ekran; kilit sırası çalışma zamanında doğrulanmadı; Host mutantlarının tam proje sonucu; eşzamanlı iptal+indirim yarışı yeniden üretilmedi. Dilim dışı: ShiftSummaryStore iş günü UTC gece yarısı ve bankacı yuvarlaması (PO B014 ile birleşir).

## Fırsat (çalışanın işini azaltan / hızlandıran)
| Fırsat | Etki |
|---|---|
| Son kişiye kalan tutarın otomatik yazılması ve "2/4 ödendi" göstergesi | 3 ve 6 kişide elle düzeltme biter (734) |
| Eşit bölmede kişi sayısı siparişin kişi sayısından gelsin | −2 dokunuş (734) |
| Ürün bazlı bölmenin tahsilata bağlanması: kişi seç, tutarı hazır | Dört kişilik masada yeniden yazma biter (735) |
| İndirimde önizleme ve %10/%15/%20 hazır düğmeleri | Yazım hatası (10 yerine 100) olmaz (726) |
| Ödeme satırında yöntem ve çalışan adı | Nakit/kart iadesi karıştırılmaz (734) |
| Nakitte kupür kısayolları (₺50/100/200) | Alınan para girişi hızlanır (734) |
| Kart kilidinde "müdür bekleyen onaylar" ekranı | Kasa tıkanıklığı azalır (734, PO onayıyla) |
| Ödemeyi Ekle sabit alt çubuğa, iade/iptal ayrı sekme | Her ödemede kaydırma biter (734) |

## Bulgu → düzeltme görevi
| Görev | Bulgular |
|---|---|
| V1-RMD-726 | B001 (Yüksek) |
| V1-RMD-727 | B002 (Yüksek), B016, B017, B018 |
| V1-RMD-728 | B003, B038, N1, N2 |
| V1-RMD-729 | B006, B008, B009, B010, B011, B014, B024 |
| V1-RMD-730 | B012, B013, B025 |
| V1-RMD-731 | B004 |
| V1-RMD-732 | B021, B022, B031 |
| V1-RMD-733 | B023, B029 |
| V1-RMD-734 | B007, B032, B033, B036 |
| V1-RMD-735 | B034, B035 |
| V1-RMD-736 | B019, B020, B030, B040 |
| V1-RMD-737 | B005, B037 |
| V1-RMD-738 | B027, B039 |
| PO kararı | B015 (bahşiş kart ile tahsil edilebilir mi), B026 (zorunlu servis ücreti/kuver kısıtı; V0-CMP-004 ile V1-WTR-020 çelişkisi) |
| Dış bağımlılık | B028 (iade KDV/mali belge bağlantısı: fiscal) |

## Semih raporu
Faturalamada iki gerçek para riski var. Birincisi: kasiyer tüm tutarı kapatan bir indirim yazarsa ekran "Hesap Ödendi" diyor ama hesap açık kalıyor ve sipariş hiç kapanmıyor (726). İkincisi: iade isteği ağ kopması sonrası tekrar gönderilirse kasadan ikinci kez nakit çıkabiliyor (727). Dört kişilik masada ürün bazlı bölme + indirim + karma ödeme + KDV'li fiş akışı bugün tam çalışmıyor; kart payı olan hesap müdür onayına kadar kilitli (Token/Beko bağımlılığı). Ödemeler sayfasındaki bir kırmızı test üretim hatası değil, eski test verisi (728). PO kararı bekleyen iki konu: bahşişin kartla alınıp alınmayacağı, zorunlu servis ücreti/kuver kuralı.
