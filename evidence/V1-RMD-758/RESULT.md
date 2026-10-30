# V1-RMD-758 sonuç

Bulgu (V1-RMD-756 denetimi): kasa hareketi anahtar imzası `Notes` içeriyordu; sunucu yalnız yön ve tutarı karşılaştırır. Yalnız açıklama düzeltilince yeni anahtar üretilip hareket iki kez yazılabiliyordu.

## Kanıt
- Kırmızı (`red/`): yeni adım (yalnız not değişimi aynı anahtar) 1 failed, 3 passed; `spec:160 expect(keys[3]).toBe(keys[2])` satırında kırıldı.
- Yeşil (`green/`): `05-cash-session-lifecycle.spec.js` gerçek Chromium + Host + PostgreSQL: 4 passed, exit 0.
- Mutasyon (`mutation-notes-in-signature/`): imzaya gövdenin tamamı geri konunca aynı satırda 1 failed. Koşu sonrası kaynak `cmp` ile geri yüklendi (sha256 1cde371e...); `cmp` çıktısı kanıt dosyasına kaydedilmedi, yalnız oturumda gözlendi.
- Kapsam: `scope-vs-e75961bf.json` valid=true.
- Bağımsız inceleme: kod incelemesi ve test incelemesi, engelleyici bulgu yok.

## Sınırlar
- Kanıt koşularından sonra yalnız iki yorum satırı değişti (kapanış kapısı yorumda görev ID'sini yasaklar); çalışan kod aynıdır, hash ile ayrıca kanıtlanmadı. `git diff e75961bf 94c4b9ae -- src` 2 ekleme, 1 silme gösterir.
- Ağ hatası `route.abort` ile üretilir; isteğin sunucuya ulaşıp yanıtın kaybolması sunucu kaydıyla doğrulanmaz.
- Sunucu yalnız ilk açıklamayı saklar; aynı anahtarla gelen düzeltilmiş açıklama kaydedilmez (çift hareket riskine tercih edildi).
- Yön (Giriş/Çıkış) değişiminin yeni anahtar üretmesi test edilmez; imzadan `Direction` çıkaran mutant hayatta kalır.
