# V1-RMD-504 - Mutabakat vakası geçmişi ve not ekleme ekranı

## Kurallar

- Gün sonu ve mutabakat ekranında her vaka satırına "Geçmiş" eklendi: vakanın işlem geçmişi (işlem türü Türkçe etiketle, zaman, saklanan not veya gerekçe metni) açılır. Sunucunun JSON ayrıntısından yalnız `note` veya `reason` metni gösterilir; ham JSON veya İngilizce işlem adı gösterilmez.
- `reconciliation.manage` yetkisiyle "Not ekle": durum değiştirmeden vakaya not yazar. Not alanı boşsa sunucuya gidilmez. Not metni kırpılarak gönderilir.
- Tablo işlem sütunu artık yetkisiz oturumda da görünür, çünkü "Geçmiş" salt-okunur oturuma da açıktır; durum değiştirme ve not ekleme düğmeleri yine yetkiye bağlıdır.

## Kanıt

- `tests-client.log`: tüm istemci 64 dosya / 500 test geçti (yeni: 4 test — geçmiş etiketleri ve metin, not ekleme ve yetki, sunucu reddi, istemci yol/gövde sözleşmesi). `typecheck.log`, `oxlint.log`, `build.log`: exit 0.
- `mutation.log`: 7 mutant, hepsi en az bir testi kırdı; dosyalar yedekten geri yüklendi.
- Gerçek-Host denemesi yapılmadı: sunucu uçları değişmedi; sözleşme yol/gövde testiyle sabitlendi.

## Açık kalan

- Geçmişte işlemi yapan kullanıcı gösterilmiyor (sunucu yalnız kimlik döndürüyor, ad çözümlemesi yok).
- Vaka oluşturma uç noktası ekrana bağlanmadı: vakalar tarama ve olaylarla açılır, elle açma ürün kararı gerektirir.
