qa-guvenlik-yetki: transfers kapsami dogru. Orta (kapsam disi): /merges ve /status ayni kurali uygulamiyor. Orta (bilincli kural): garson PWA kirilir, dugme pasiflesmez. Dusuk: TOCTOU dar pencere; 403/409 tutarsizligi. Hepsi RESULT'ta acik birakildi.
qa-backend: kural dogru, istemci akisi uyumlu (409 masa eyleminde stale durumuna dusurmez). Dusuk: okuma/yazma tarafi mantigi iki yerde (okuma toplu, yazma tekil sorgu) -> kabul. Test eksigi (hesapsiz masa) mevcut genel transfer testiyle kapali.
Dogrulama turu: duzeltme sonrasi Tables.Tests 48/48 temiz derlemeyle, gate exit 0.
