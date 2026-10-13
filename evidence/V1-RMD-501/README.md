# V1-RMD-501 - Stok eşleme ve fire geçmişi ekranı

## Kurallar

- Yönetim > Stok bölümüne (`inventory.manage` yetkisiyle) iki panel eklendi:
  - **Stok eşlemeleri:** ürün veya seçenek seçilir; bağlı stok kalemleri, kullanım çarpanı ve kalan miktar listelenir; stok kalemi + çarpan (boşsa 1, sıfır veya sayı olmayan değer reddedilir, sunucuya gidilmez) ile eşleme eklenir; eşleme kaldırılır.
  - **Fire geçmişi:** kaynak türü (Üretim, Porsiyon ayırma, Elle kayıt, Bozulma, Son kullanma tarihi, Hazırlık hasarı) ve kaynak kaydı numarası girilir; sunucunun `GET /inventory/waste` cevabı Türkçe etiketlerle listelenir. Numara geçerli bir kimlik değilse sunucuya gidilmez.
- Yalnız `reports.view` yetkisi olan oturum bu panelleri görmez ve hedef listesini sunucudan okumaz.
- Sunucu uçları değişmedi; `StockSection.test.tsx` içindeki yol testi her yolu, yöntemi ve gövdeyi doğrular.

## Kanıt

- `tests-client.log`: tüm istemci 64 dosya / 486 test geçti (stok bölümünde 6 yeni test). `typecheck.log`, `oxlint.log`, `build.log`: exit 0.
- `mutation.log`: 8 mutant, hepsi en az bir testi kırdı; biri ilk turda hayatta kaldı, test sıkılaştırılıp yeniden koşuldu.
- Gerçek-Host denemesi yapılmadı: sunucu tarafı değişmedi; istemci–sunucu sözleşmesi yol testiyle sabitlendi.

## Açık kalan

- Sunucu fire kayıtlarını yalnız kaynak türü ve kaynak numarasıyla sorguluyor; tüm fireyi tarihe göre listeleyen bir uç yok. Bu yüzden "Fire geçmişi" bir kayıt arama aracıdır (ör. bir üretim partisinin fire kayıtları), genel liste değildir. Genel liste için ayrı bir sunucu görevi gerekir.
- Elle girilen fire kaydı kaynak numarası taşımadığı için (`recordWaste` göndermiyor) bu ekranda aranamaz.
