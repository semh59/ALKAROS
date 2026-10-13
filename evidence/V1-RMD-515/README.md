# V1-RMD-515 kanıtı

Sonuç: nakit tahsilatta isteğe bağlı "Alınan para" alanı ve para üstü gösterimi eklendi.

## Davranış
- Yalnız Nakit yönteminde "Alınan para (isteğe bağlı)" alanı; yazdıkça "Para üstü: ₺X" önizlemesi.
- Boş bırakılırsa akış eskisi gibi (TenderedAmount = tutar); girilen tutardan azsa sunucuya gitmeden "Alınan para tutardan az olamaz." uyarısı.
- Başarıdan sonra sunucunun döndürdüğü para üstü büyük yazıyla gösterilir; hesap ödenmiş ekranında da kalır; sonraki tahsilat gönderilince silinir.
- İstenmemiş (alınan para girilmemiş) para üstü gösterilmez; başka yönteme geçince eski alınan para uygulanmaz.

## Kanıt
- `test-runs.log`, `mutations.log`: 13 mutant, hepsi killed (ilk turda 7 hayatta kalan → test eklendi).

## Açık
- Banknot kısayol düğmeleri yok; sunucu sözleşmesi değişmedi (TenderedAmount, changeAmount zaten vardı).
- Gerçek tarayıcıda elle deneme yapılmadı.
