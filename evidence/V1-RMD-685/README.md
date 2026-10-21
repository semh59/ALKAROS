# V1-RMD-685 — İşletme logosu sağlamlaştırma

## Sonuç
- Logo yükleme/kaldırma denetim kaydı düşer (olay adı + yapan yönetici + tür/bayt).
- Dosya türü büyük/küçük harf ve parametreden bağımsız normalize edilir ("IMAGE/PNG; charset=binary" -> image/png).
- Yükleme tek kopyayla okunur (MemoryStream çifte kopyası kalktı).
- /branding ve /logo (304) görsel baytlarını okumaz; yalnız `GetMetaAsync` (tür + tarih) okunur.

## Kanıt
- store-tests.log: BusinessIdentity testleri 25/25 başarılı.
- host-tests.log: Settings host testleri 56/56 başarılı.
- mutations.log: 17/17 mutant KILLED, her seferinde kaynak geri yüklendi (mutate.mjs).

## Açık / doğrulanamadı
- /branding ve /logo'nun "bayt okumadığı" HTTP seviyesinde test edilmedi (QrOrderingHttpTests bu görevin sahipliğinde değil); davranış değişmez, yalnız maliyet düşer.
- Tarayıcıda elle deneme yapılmadı.
- Migration 136 CHECK sertleştirmesi V1-RMD-687'dedir.
