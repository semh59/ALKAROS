# V1-RMD-735 - PosTerminal bölme ekranı ve ürün bazlı bölme bağlantısı (603 B034, B035)

**Sonuç:** Bölme ekranında 409 sonrası artık çıkış var, boş hücre/boş sahip listesi/çift yükleme/etiket hatası düzeldi; kasa tahsilat sayfası POS'ta kaydedilen kişi başı tutarları tek dokunuşla öneriyor. 9/9 mutasyon KILLED, PosTerminal 804/804, StaticApps 136/136, tsc 0.

## B034 (doğru, düzeltildi)
- 409 kurtarma: hesap yenilenince taslak korunarak yeni hesap sürümü alınıyor (eskiden taslak kirliyken yenileme yok sayılıyor, her kayıt yeniden 409 veriyordu). "Hesabı yenile" ve "Taslağı at" düğmeleri eklendi.
- Ürün bölmenin indirimli hesapta reddi artık "başka yerde değişti" diye yanlış etiketlenmiyor; sunucunun gerçek mesajı görünüyor.
- Boş hücre sıfır sayılıp kaydı engellemiyor (hücre silinince kayıt kalkıyor).
- Sandalye/kişi listesi boşsa açık mesaj ve kayıt kapalı.
- Siparişten hesap açılırken `from-order` iki kez POST ediliyordu; tek POST (test: `routes/workspace.test.tsx`).
- `label` erişilebilirlik uyarısı giderildi. "Sıfırla" artık onay istiyor. "deterministik" jargonu sadeleşti.

## B035 (kısmen)
- Yapıldı: kasa sayfası kayıtlı dağılımdan kişi başı tutarı (sahip etiketi + toplam) tek dokunuşluk öneri olarak gösteriyor; POS ekranı indirimli/ek ücretli hesapta ürün bazlı bölmenin kullanılamadığını baştan söylüyor.
- **Yapılmadı (PO kararı):** indirimin ürünlere nasıl yayılacağı (V1-RMD-413'te bilinçli olarak ertelendi). Bu karar olmadan Semih senaryosu (4 kişi, ürün bölme, %10 indirim) ürün bazında uçtan uca sağlanamaz; indirimli hesap tutar/eşit modla bölünür. WaiterPwa'da ödeme yok (kapsam dışı).
- **Yapılmadı:** tüm bölme metinlerinin `strings.ts`'e taşınması (yalnız değişen metinler yerinde bırakıldı).

## Kanıt
`logs/` (posterminal-vitest, staticapps-vitest, tsc exit 0; oxlint exit 1 yalnız bu görevden önceki `CustomerDisplay.tsx` hatası, billing dosyalarında uyarı yok), `mutations.log`.
