# Dalga 1 — kapsam kesinleştirme, V2

## Son doğrulanmış durum

- Aktif görev: V1-RMD-739, assignee hermes-session-20261028_015834_6948d340.
- HEAD: 8c03e79bdc5263f5a5a26a524693403012d871d2.
- Bu devam adımının yazma allowlist'i: yalnız evidence/V1-RMD-739/classification/wave1/**. Önceden var olan değişiklikler korunur.
- 1.376 özgün kaynak kaydının 54'ü ilk dalgada; 1.322'si bekliyor. Bekleyenlerin tam ID listesi KAPSAM_TEMEL_V2.json içindedir.
- 75 kusur/teyit başlığı ve 62 görev adayı yeniden sayıldı. PAY-012, parent kaydı olmayan yeni adaydır; özgün kaynak sayısına gizlice eklenmedi.
- Kaynak hash değişikliği yok. Kaynak satırları, aday dosya yolları, kusur→aday tam eşleşmesi ve dependency döngüsüzlüğü kontrol edildi; bu semantik inceleme veya uygulama testi değildir.

## Açık kalan kapsam sorunları

1. STK-024, STK-025 ve STK-028 içindeki F0031 tarihsel kapanış kopyası ana kayıttan farklı. Ana kayıttaki 16 tam / 29 kısmi kapanış geçerli; kopyalar tek bir güncel durum gibi sunulamaz.
2. GYZ-011 açık statüsü ile türü belirtilmemiş karar engeli çelişiyor. Teknik mimari/sahiplik engeli ürün onayından ayrılacak.
3. GYZ-024 için eski known-limitation notu yeni Semih onayı yerine geçmez. Önceki rapordaki kesin “görev açılmayacak” ifadesi yeterince kanıtlı değildir; kapsam/karar incelemesi açık kalır.
4. Production/test referans listesi yazma izni değildir. Her adayda write_paths ve read_only_paths ayrılacak; yalnız gerçekten değişecek interface test fake'leri dahil edilecek.
5. Yeni migration adı ve sıra numarası Task ID verilmeden uydurulmayacak. Arayüz/şema değişikliği gereken aday kesin kapsam oluşana kadar scope-blocked kalır.

## Bağımsız işler

- deleg_7beb144e / sa-0-6d096885: bütün RG-STK-* adaylarının kesin dosya kapsamı, test fake'leri ve erişilebilirlik bağımlılıkları.
- deleg_7beb144e / sa-1-6c0ed558: kalan yetki/ödeme adaylarının kesin kapsamı, teknik/ürün karar ayrımı.
- İki sonuç alındı ve kaynak/hash/yol/grupID/bağımlılık kontrolünden sonra KESIN_YUZEY_V2.json olarak birleştirildi. Sonuç: 19 scope-reviewed, 21 decision-blocked, 10 scope-blocked, 6 verification-only, 6 historical-regression-only. Kapsamın tamamı kapanmadı; güncel ayrıntı KAPSAM_SONUC_V2.md.
- Aktif uygulama görevi yok; başka Task ID başlatılmadı; üretim/test kodu değiştirilmedi.

## Çalıştırılmış kontroller

- Ana kayıt doğrulayıcısı: exit 0 (scope-v2-ledger.log).
- Plan denetimi: exit 0; 0 hata, 0 uyarı (scope-v2-plan.log).
- Tutarlılık: exit 0, clean (scope-v2-consistency.log).
- V1-RMD-739 görev kapsamı: exit 0 (scope-v2-scope.log).
- validate_scope_v2.py: mevcut 54/1322 temel ayrımı doğrulandı; kesin kapsam incelemesi henüz pending.
- Yeni kapsam doğrulayıcısı için 1 geçerli + 6 kasıtlı hatalı sentetik girdi kontrolü koşuldu, exit 0. Bunlar uygulama testleri veya gerçek inceleme çıktısı değildir (scope-v2-validator-checks.json).

## Sonraki adım

İki bağımsız çıktı gelince grupID kapsamını, dosya varlığını, read/write ayrımını ve bağımlılıkları doğrula; eski kararların kanıtsız dışlamaya dönüşmesini engelle. Kapanmayan kapsam sorunu scope-blocked kalır. Bu adımın ardından yeni görev sözleşmeleri hazırlanabilir; uygulama kodlaması ayrı Task ID ve kapı koşullarına bağlıdır.
