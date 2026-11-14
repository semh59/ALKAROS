# V1-RMD-1066 RESULT

floorPlan.css: masa dugumu satirlari disindaki butun yazilar (arac cubugu, eyebrow, kuzey etiketi, inspektor, inceleme, yogun liste, koltuk duzenleyici), masa numarasi ve koltuk numarasi 14px (.875rem) oldu; koltuk dairesi 1.5rem, dugum ic bosluğu .2rem (14px numara 58px'lik varsayilan dugumde sigsin diye).
Degerlendirme (kural belgesiyle fark): dugum ici uc satir (durum, baglam, gecen sure) ve dugum rozeti 11,2px kaldi. Dugum, tuvalde yuzdeyle cizilir (varsayilan 144x88 birim, 1000x600 tuvalde ~58px yukseklik, ic alan ~44px); numara + iki satir + rozet 14px'te sigmaz. docs/CASHIER_UI_RULES.md 2 istisna tanimlamaz; kural gevsetilmedi, istisna yalniz testte adi konmus (alt sinir 11px, 14px'e cikan istisnadan cikmali). Urun/tasarim karari: dugum ici ikincil satirlari 14px yapmak icin dugum boyutu buyutulmeli ya da satirlar inspektore tasinmali.
Kanit: red.log, green-file.log, green-all.log (PosTerminal tum suite), client-build-gate.log exit 0, mutations.txt (8 killed).
Olculemedi: 14px'in gercek ekranda gorsel sigmasi (inspektor 17-21rem, ozellikle "Rezervasyon" etiketi) dogrulanmadi.
