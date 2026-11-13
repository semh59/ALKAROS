# V1-RMD-1036 RESULT

Kok neden (1): TableOrderBridge kalem listesi yalniz masa surumu/tutar degisince yeniden okunuyordu; Mutfaga gonder surumu degistirmedigi icin "Mutfaga gonderilmedi" kaliyordu ("Yenile" paneli bastan kurdugu icin duzeliyordu).
Kok neden (2): Masa tasima cevabi, siparisin masa baginin outbox tuketicisi (TableEventOrderConsumer, OutboxStore bosta 1 sn) tarafindan yazilmasindan once doner; hemen yapilan tek okuma bosalan masada eski tutari okur. 8 sn'lik yoklama sonunda duzeltir ama kasiyer o sirada eski durumu gorur.
Duzeltme: workspace.tsx refreshSettled = hemen okuma + 1,5 sn sonra bir okuma (masa eylemi ve Mutfaga gonder icin tek yol; unmount ve art arda eylemde zamanlayici temizlenir); bridge gonderimden sonra kalemleri yeniden okur (sentCount).
Kanit: red.log 2 test kirmizi; green: routes+tables 411/411; mutations.txt M1-M6 hepsi killed (M1 ilk kosuda sagdi, masa tasima testi eklendi); client-build-gate exit 0 (oxlint, tsc, vite build).
Elle senaryo: masaya urun ekle, panelden Mutfaga gonder: "Mutfaga gonderilmedi" ibaresi ve gonder dugmesi Yenile'siz kaybolur; Masa tasi: bosalan masa ~1,5 sn icinde Musait ve tutarsiz olur.
Kabul edilen risk: gecikmeli okuma, 1,5 sn icinde bolge degistirilirse eski bolge kapanimiyla calisir (yalniz kat plani bolgesi; secilen bolgenin kendi okumasi sonra gelir).
Not: 6. madde (floor-plans 404) V1-RMD-1030 ile kod tarafinda zaten kapali (sayfa basina bir kez); 8090 yigini eski derleme.
