# V1-RMD-1179 sonuc

- Kirmizi: red-*.log (tender 21 test, refund 1 test basarisiz; uc, arama ve yontem turetimi yokken).
- Yesil: final5-*.log (PaymentTender 25, refund 1, migration 19 gecti); full-*.log tam sinif kosusu (bellek siniri nedeniyle OOM kesilen testler rerun-*.log ile tek tek gecti); mimari testler (ApiConventions 17, ModuleBoundaries 31) gecti.
- Mutasyon: round1 (13 mutant), round2 (4), mutants-r3 (G21,G22 oldu; G23 derlenmedi), mutants-r4 (G23b hayatta: BillNotPayable ApiErrorCatalog'da zaten ayni kodla eslendigi icin ucta tekrar eden catch kaldirildi). Tum mutantlar cp+cmp ile geri alindi. Esdeger sayilan: kalan tutar on kontrolu (tahsisat deposu ayni istisnayi atar), anahtar kilidi (hesap kilidi zaten siralar).
- Kod dogrulamasi: yontem tabloda degil kayitlardan turetiliyor (migration gerekti); kasa oturumu kapanisi yontem kirilimi gostermez (rapor satiri V1-RMD-1180); cekmece yalniz nakit tahsilat ucundan acilir.
- Karar: slip ayni hesapta engellenir (uyari degil), farkli hesapta serbest (POS parti numarasi tekrari). Cakisma kodu TENDER_IDEMPOTENCY_KEY_REUSED.
- Bilinen sinir: kart numarasi kontrolu sezgiseldir (13-19 hane + Luhn); Luhn gecmeyen numara fis sayilir. Iade fisi ExternalCard icin ayni dogrulamadan gecer; Card iade fisi (eski) serbest metin kalir.
- Ara durum: rapor 1180 gelene kadar harici POS karti EFT satirina duser; ayni surumde cikmali.
- Plan/consistency audit 0 hata 0 uyari.
