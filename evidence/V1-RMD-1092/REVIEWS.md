# Reviews
- qa-guvenlik-yetki: KABUL. Sahiplik kontrolunden kacan yazma yolu yok (tek HTTP girisi; kontrol RecordAsync ve replay aramasindan once). Dusuk: replay testi ve esik alti Out testi eksikti (Out 50 vakasi eklendi); eski bekleyen talepler risk olarak yazildi.
- qa-frontend: RED (kucuk). Ipucu metni uygulanamaz bir sey soyluyordu (tek oturum kurali); odak disabled dugmeye gidiyordu; aria-describedby yoktu; metin iki yerde. Hepsi duzeltildi.
- qa-bulgu-dogrulayici (son hal): KABUL. Dort duzeltme dogru; yeni hata yok. Dusuk: odak duzeltmesi testsiz; ApiErrorCatalog iletisi "tahsil" der; orta (kapsam disi): eski bekleyen talepler.
