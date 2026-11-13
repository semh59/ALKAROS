# V1-RMD-1056 RESULT

Masa plani: durum rozeti tek yerde (7 kural, docs/CASHIER_UI_RULES.md tablosunu tablesStyle.test.ts okur); kart ve panel ayni table-status--<anahtar> sinifini kullanir (panelde seftali/mor yok). Desen yalniz Hesap istedi ve 45 dk asan masada; Uzun sure acik yazisi ve erisilebilir ad. Dikkat seridi yalniz sayi (Hesap isteyen: n, Odeme bekleyen: n), tiklanmaz. Adi olmayan terminalde baslikta Terminal alani cizilmez (ham kod kalkti). tables.css yazilari >= 14px.
Kanit: red.log (8 kirmizi), green-file.log, green-all.log tum PosTerminal suite, client-build-gate.log exit 0, mutations.txt M1-M19 killed.
Elle senaryo: Masalar ekraninda odeme bekleyen masalar ayni camgobegi-mavi rozetli, uzun suredir acik masa cizgili ve "Uzun sure acik" yazili; masa secince paneldeki rozet kartla ayni renk; baslikta TERMINAL kodu yok.
Bilinen sinir / urun karari: seridin sayi olmasi masa secme yolunu kaldirdi (kart secer). floorPlan.css canli salon tuvali 11px esiginde kaldi (kapsam disi). primitives.css .ds-button--primary beyaz yazi (belge disi bulgu 1055).
