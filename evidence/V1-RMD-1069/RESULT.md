# V1-RMD-1069 RESULT

Kok neden: tables.css'teki ".table-details__header > div > span:last-child { color: var(--table-muted) }" kurali basligin iki sutunundaki son span'e uygulaniyordu; ikinci sutundaki (durum rozeti) rozeti de eziyordu (ozgullugu .table-status'tan yuksek). Kural ".table-details__header > div:first-child > span:last-child" oldu (yalniz baglam satiri). Rozet artik kartla ayni --plan-text jetonunu alir.
Test: tablesStyle.test.ts - baslik kurallarindan renk koyanlarin yalniz ilk sutuna uzanmasi sart. Kanit: red.log, green-file.log, green-all.log, client-build-gate.log exit 0, mutations.txt (2 killed).
Olculemedi: renk jsdom'da hesaplanmadi (stil testi seciciye bakar); gercek ekranda rozet rengi gorsel dogrulanmadi. Panel rozetinin 768px'te iki satira sarmasi bu gorevde degistirilmedi.
