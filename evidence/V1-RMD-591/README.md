# V1-RMD-591 kanıtı

`vite.config.ts` içine test başına ve kanca başına 30 saniyelik sınır eklendi (varsayılan 5 sn). Ekran testleri jsdom'da tüm rotayı çizer (tek başına ~4,5 sn) ve paralel koşunca 5 sn'yi aşıp rastgele düşüyordu.

- Üç ardışık tam koşu: 77 dosya, 665 test, hepsi geçti (`test-runs.log`); `tsc --noEmit` exit 0.
- Ayar yokken aynı paket her koşuda farklı 6-10 testle düşüyordu (`mutations.log`).
- Testlerin kendisi hızlanmadı: 4,5 sn'lik ekran testleri hâlâ yavaştır; bu sınır yalnız yük altındaki zaman aşımını kaldırır.
