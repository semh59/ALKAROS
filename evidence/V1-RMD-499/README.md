# V1-RMD-499 - Arayüzde ham hata metni sızıntısının kapatılması

## Kurallar

- Yeni ortak `userMessage(reason, fallback)` (`src/Clients/PosTerminal/src/userMessage.ts`): yalnız HTTP durumu taşıyan, yani sunucu cevabından üretilmiş hatanın metnini gösterir; ağ, ayrıştırma veya programlama hatasının ham `message` metni yerine önceden yazılmış Türkçe cümle verir.
- Bekleyen hesaplarda "Hesap açılamadı", mutfak rota formunda "Rota kaydedilemedi.", fatura işletme bilgilerinde "Fatura bilgileri okunamadı/kaydedilemedi." gösterilir. Eskiden `Unexpected token <` veya `Failed to fetch` gibi tarayıcı metni ekrana çıkabiliyordu.
- Başarısız yedeklemede sunucunun serbest hata metni (`errorMessage`) yerine sabit Türkçe cümle gösterilir.

## Kanıt

- `tests-client.log`: tüm istemci 63 dosya / 470 test geçti (yeni: yardımcı 3, bekleyen hesap, rota formu, fatura bilgisi ayrıştırma hatası, yedekleme metni). `typecheck.log`, `oxlint.log`, `build.log`: exit 0.
- `mutation.log`: 5 mutant, hepsi en az bir testi kırdı; dosyalar yedekten geri yüklendi.
- Gerçek-Host denemesi yok: değişiklik yalnız istemci görüntüleme katmanıdır, sunucu ucu veya veri yazımı değişmedi.

## Açık kalan

- Sunucunun Türkçe sözleşmeli `error.message` metinleri olduğu gibi gösterilmeye devam eder (`management/http.ts`, `catalogApi.ts`, `tableApi.ts`); sunucu tarafında İngilizce bir istisna metni sızarsa burada görünür. Bu ayrı bir sunucu taraması işidir.
- `SystemSection.tsx` uyarı başlık/mesaj alanları sunucu kaydıdır, değiştirilmedi.
