# V1-RMD-877 - Kasada ikram (F0223)

## Sonuç
Kasiyer ödeme ekranında (Tahsilat > "İkram") hesabın bir tutarını veya kalanını ikram edebilir. Yeni paralel akış yok: mevcut yetki motoru (bills.comp izni, authorization_policies limiti, Yetki Kararları ekranı) kullanıldı. Tek yeni sunucu parçası `POST /billing/bills/{id}/comp`; indirimle aynı işleyiciyi paylaşır (izin kodu ve gerekçe kataloğu farklı).
- Limit içi: anında uygulanır (200). Limit üstü / sayı aşımı / politika satırı yok: yönetici onayına gider (202), Yetki Kararları ekranında "İkram" olarak görünür. Onay sonrası kasiyer "Onay durumunu kontrol et" ile aynı anahtarla yeniden gönderir, uygulanır. Ret: 403 "İkram isteği reddedildi.".
- Yetkisi olan rol (müdür/süpervizör, bills.comp) doğrudan uygular; hesap sıfırlanırsa hesap kapanır.
- Sınır ayarı: Yönetim > Roller ve yetkiler > Onay sınırları (İkram / Kasiyer satırı; ekran ve sunucu bu çifti zaten destekliyordu, React değişikliği gerekmedi).

## Karar boşlukları (benim belirlediğim)
1. Kapsam: hesap düzeyinde tutar. Kalem düzeyi ikram sipariş tarafında zaten var; kasa ekranı kalemlerin sipariş sürümünü bilmez.
2. Varsayılan sınır (migration 222): kasiyer, tek ikram en çok 50 TL, 8 saatte en çok 5; yönetici değiştirebilir. Mevcut satıra dokunmaz.
3. Etki: aynı satır kalem ikramını (`/orders/.../items/{id}/comp`) da yönetir; kasiyerin 50 TL altı kalem ikramı artık onaysız uygulanır (eskiden her zaman onaya giderdi).
4. İkram, hesapta `DiscountAmount` düzeltmesi olarak saklanır; gerekçe kodu (Müşteri memnuniyeti, Yönetici ikramı, Özel misafir, Hizmet özrü) ikramı indirimden ayırır. Denetim olayı: `bill.comp.applied`. Gerekçe büyük/küçük harf duyarlı.
5. Geri al: ikram da diğer düzeltmeler gibi bills.split ile geri alınır (yalnız tutarı geri artırır).

## Kanıt
- Gerçek Postgres: Billing HTTP 68/68 (öncesi), ikram testleri 17/17 (green6); Orders/Comp 30/30; Roles 28/28 (iki test tohum satırına uyarlandı); Identity politika 69/69; Manifest + migration 222 ileri/geri/yeniden uygulama testleri geçti (green2).
- StaticApps vitest: 406/406 (yeni split-payment-comp.test.js 8 test).
- Mutasyon (mutations.txt): M1-M9 sunucu/migration, J1-J5 ekran; hepsi öldü (J2/J3/J5 ikinci turda; J5 için test güçlendirildi).
- Not: `DeployFilesTests.TheBuildContextExcludesTheEnvironmentFileAndGitHistory` ".git" bulamıyor; V1-RMD-874'te de kayıtlı, bu görevle ilgisiz mevcut ortam sorunu.

## Açık risk / ürün kararı (REVIEWS.md'de ayrıntı)
- Garson kasa oturumuyla `/discount` (limit 100 TL, onaysız) ikramdan gevşek; ikram sınırını delmek için indirim kullanılabilir. Mevcut V1-RMD-674 kararı; Semih'e: indirim ve ikram sınırları birlikte mi düşünülmeli?
- Onaylanmış izin süresiz geçerli; ikram geri alınırsa aynı anahtarla yeniden uygulanabilir (indirimde de aynı, mevcut tasarım).
- Bekleyen indirim (202) için istemci anahtarı siliyor; onaylanan indirim kasadan uygulanamıyor (ikramda düzeltildi, indirimde duruyor).
- Hesap başına toplam ikram sınırı yok (5x50 TL parça parça); bekleyen/reddedilen ikram talebi denetim olayı yazmaz (hibe tablosunda iz var).
- Tam ikramla kapanan hesapta ekran "Hesap Ödendi" der (ikram olduğunu söylemez).
- Sonraki ürün görevleri 879'dan başlar (878 kullanılmadı; bölünmedi).
