# V1-RMD-1065 RESULT

shell.css (10), styles.css (25 + yeni genel small kurali), primitives.css (2) ve billing.css (6) icindeki 14px alti yazi boyutlari .875rem (14px) oldu; musteri ekrani kurallari (.display-order-number span, .display-table-head, .privacy-note) adi konmus istisna. "Kasada eklendi" notu (<small> varsayilani 11,7px) genel small kuraliyla 14px.
Test: cashierTextSize.test.ts (dosya basina 14px alti yok, istisna listesinde olmayan kural yok, small kurali boyut tasir); shellStyle.test.ts alt siniri 11'den 14'e cekildi.
Kanit: red.log (6 kirmizi), green-file.log, green-all.log (PosTerminal 1390 test), client-build-gate.log exit 0, mutations.txt (11 killed; T2 ilk kosuda test eksigi yuzunden hayatta kaldi, test sikilastirildi, yeniden killed).
Olculemedi: 14px olunca dar menude (768 alti alt cubuk, etiket tek satir ve ... ile kesilir) ve ust basliktaki yatay sigma gorsel olarak dogrulanmadi; gercek ekranda bakilmali.
Ek (qa-ux bulgusu): 14px etiket dar rayda (76px) ve telefon alt cubugunda (nowrap + ...) kesilirdi; ray 96px'e cikarildi, alt cubuk etiketi satir sarar (shellStyle.test.ts korur). Birim kor noktasi icin her font-size'in rem/px yazildigini sinayan test eklendi. Disi: floorPlan.css (V1-RMD-1066), katalog/mutfak/yonetim ekranlari bu gorevin kapsami disindadir.
