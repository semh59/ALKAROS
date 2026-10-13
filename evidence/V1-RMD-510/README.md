# V1-RMD-510 kanıtı

Sonuç: PosTerminal hesap bölme ekranının altında "İptal ve iade" paneli var: gerekçeli hesap iptali, ödeme başına iade isteme, bu hesabın iade isteklerini listeleme, onaylama/reddetme ve elle iadeyi fiş numarasıyla tamamlama.

## Davranış
- Panel yetkiye göre açılır: `bills.cancel`, `payments.refund.request`, `payments.refund.approve`; hiçbiri yoksa hiç görünmez.
- Hesap iptali yalnız ödeme dağıtımı olmayan hesapta gösterilir; başarılı olunca "Hesap iptal edildi." mesajı.
- İade isteği yalnız "Approved" ödemede ve açık isteği (Onay bekliyor / Elle iade bekliyor) olmayan ödemede sunulur.
- Onay hesabı kapatırsa iptal mesajı gösterilir.
- Hata metni sabit Türkçe cümledir (durum/kod eşlemesi); ham sunucu metni veya HTTP kodu gösterilmez; 409'da "güncel veriyi alıp yeniden deneyin".

## Kanıt
- `test-runs.log`: tsc, oxlint, vite build exit 0; vitest 66 dosya / 518 test (+ 6 yeni refund testi sonrası billing 24/24).
- `mutations.log`: 21 mutant, hepsi killed (ilk turda 5 hayatta kalan → test eklendi, ölü kod `canComplete` kaldırıldı).

## Açık
- Gerçek tarayıcıda gerçek Host ile elle deneme yapılmadı; testler jsdom + enjekte fetch üzerinde. Sunucu tarafı V1-RMD-506/508'de gerçek Host + Postgres ile doğrulandı.
- Yalnız tam iade; cari hesaba yazılan ödeme iade edilemez (sunucu 409).
