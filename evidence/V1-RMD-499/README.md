# V1-RMD-499 - Arayüzde ham hata metni sızıntısının kapatılması

## Kurallar

- Bekleyen hesaplar, mutfak rota formu ve fatura işletme bilgileri artık yalnız sunucu cevabından üretilmiş hata sınıflarının (`PendingChecksApiError`, `BillingSplitApiError`, `KitchenOperationsApiError`, `SellerProfileApiError`) metnini gösterir; ağ, ayrıştırma veya programlama hatasının ham `message` metni yerine önceden yazılmış Türkçe cümle verilir. Eskiden `Unexpected token <` veya `Failed to fetch` gibi tarayıcı metni ekrana çıkabiliyordu.
- Başarısız yedeklemede sunucunun serbest hata metni (`errorMessage`) yerine sabit Türkçe cümle gösterilir.
- Ortak bir yardımcı dosya eklenmedi; yeni dosya plan sahipliği gerektireceği ve mevcut sınıf denetimi aynı işi gördüğü için her ekran kendi hata sınıfını denetler.

## Kanıt

- `tests-client.log`: tüm istemci 62 dosya / 467 test geçti (yeni: bekleyen hesapta ayrıştırma hatası, rota formunda çalışma zamanı hatası, fatura bilgisi ayrıştırma hatası, yedekleme metni). `typecheck.log`, `oxlint.log`, `build.log`: exit 0.
- `mutation.log`: 4 mutant, hepsi en az bir testi kırdı; dosyalar yedekten geri yüklendi.
- Gerçek-Host denemesi yok: değişiklik yalnız istemci görüntüleme katmanıdır, sunucu ucu veya veri yazımı değişmedi.

## Açık kalan

- Sunucunun Türkçe sözleşmeli `error.message` metinleri olduğu gibi gösterilmeye devam eder (`management/http.ts`, `catalogApi.ts`, `tableApi.ts`); sunucu tarafında İngilizce bir istisna metni sızarsa burada görünür. Bu ayrı bir sunucu taraması işidir.
- `SystemSection.tsx` uyarı başlık/mesaj alanları sunucu kaydıdır, değiştirilmedi.
