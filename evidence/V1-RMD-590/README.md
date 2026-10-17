# V1-RMD-590 kanıtı

V1-RMD-589'un eklediği 193 numaralı göç için göç listesi doğrulayıcısının üst sınırı (`PhaseBMax`) 193 yapıldı, beklenen göç listesi/sayısı/son giriş testi güncellendi ve mutfak ekranının yazıcı formunun rota düzeyinde bağlandığını gösteren iki test eklendi.

- Testler (`test-runs.log`): Host göç paketi 174/174 geçti (V1-RMD-589 ile birlikte); `routes/workspace.test.tsx` 23/23 geçti.
- Mutasyon (`mutations.log`): `PhaseBMax` 192'ye çekilince Manifest testleri düşüyor (öldü). Rota testi için mutasyon kanıtı V1-RMD-589 kaydındadır.
- 589'un kendi commit'inde Manifest testleri kırmızıydı (izin listesi nedeniyle bu görev ayrıldı); bu commit ile yeşile döner.
