qa-frontend: sozlesme dogru (kitchenState string, camelCase). Orta: panel mutfak durumunu canli izlemiyor (mutfak masa surumunu degistirmez) -> DUZELTILDI (15 sn sessiz yeniden okuma, 3 test, M6-M8). Dusuk: lineNote iki kez cagriliyor (kabul, saf ve ucuz fonksiyon).
ui-turkce-metin-uzmani: terimler mutfak ve garson ekraniyla ayni (Hazir, Servis edildi). Dusuk: metin tekrari -> DUZELTILDI (itemStatusLabels'ten turetildi). "Hazir" tek basina kabul. Gosterilmeyen durumlar bilgi kaybi degil.
Dogrulama turu: duzeltmelerden sonra tum suite 1333/1333, gate exit 0, M1-M8 killed.
