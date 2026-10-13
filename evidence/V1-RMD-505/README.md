# V1-RMD-505 - Üretim partisi ayrıntı ekranı

## Kurallar

- Üretim bölümünde her parti satırına "Ayrıntı" eklendi: partinin tüketimleri (stok kalemi adı, konum adı, miktar ve birim, fire çarpanı), çıktıları ve partiye bağlı fire kayıtları (stok kalemi, miktar, gerekçe) tek bölümde gösterilir. Kimlikler ad listelerinden çözülür; çözülemeyen kimlik "—" olur, ham kimlik gösterilmez.
- Fire kayıtları `GET /inventory/waste?wasteSource=Production&sourceReferenceId=<parti>` ile okunur (V1-RMD-501'deki arama ucu); bölüm boşsa "Kayıt yok." yazılır.
- Okuma hatasında sunucunun Türkçe reddi gösterilir; ham durum kodu veya İngilizce metin gösterilmez.

## Kanıt

- `tests-client.log`: tüm istemci 64 dosya / 503 test geçti (yeni: 3 test — ad çözümleme ve fire, boş durum ve sunucu reddi, üç ucun yol sözleşmesi). `typecheck.log`, `oxlint.log`, `build.log`: exit 0.
- `mutation.log`: 7 mutant, hepsi en az bir testi kırdı; biri ilk turda hayatta kaldı, test sıkılaştırılıp yeniden koşuldu; dosyalar yedekten geri yüklendi.
- Gerçek-Host denemesi yapılmadı: sunucu uçları değişmedi; sözleşme yol testiyle sabitlendi.

## Açık kalan

- Partinin tarif sürümünü değiştiren uç (`PUT .../recipe-version`) ekrana bağlanmadı; ürün kararı gerektirir (başlamış partide sürüm değiştirmek maliyeti etkiler).
- Tüketim satırında ürün maliyeti gösterilmiyor (sunucu ayrıntısı yok).
