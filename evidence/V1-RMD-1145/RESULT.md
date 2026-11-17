# V1-RMD-1145 sonuc
Dogrulama: bulgu 6 gercekti (kutu 48 px, ic yazi alani 30-35 px; kenara/₺'ye dokunmak odaklamiyordu).
Duzeltme: bes tutar kutusu (Tutar, Alinan para; Acilis, Nakit giris/cikis, Sayilan) <div> yerine <label>; min-height var(--touch-target); dikey ic bosluk kutudan yazi alanina (12 px; alcak ekranda 8 px) gecti, yazi alani kutuyu doldurur (align-self: stretch). aria-label ve kimlikler ayni; ₺ simgesi aria-hidden (cift etiketten "Tutar ₺" okunmasin).
Kanit: red.log (6 kirmizi), green.log 583/583 (split-payment-discount-pending ayri kosuldu 31/31: paralel yukte dalgali), mutations.txt C1-C9 killed, live-measure.txt (gercek Chromium: kutu 48/48/56 px, yazi alani kutuyu doldurur; kenara dokunus label'a gider), client-build-gate 0.
Plan degisikligi: alcak ekran kuralini sabitleyen mevcut split-payment-table-note testi sahip yuzeye ayri plan commit'iyle eklendi (590d5df72).
Acik riskler: kasa kutusu alcak ekranda 61 px kalir (yer sikintisi dogrulanmadi, kural 48 ister, ust sinir yok); testler kaynak metni denetler, yerlesimi canli olcum kanitlar (CI'da tarayici olcumu yok).
