# V1-RMD-1077 RESULT
split-payment.js closedHeading(): fis status Paid ise AwaitingManualRefund icin "Hesap Odendi" basligi altinda "Iade onaylandi, fis girisi bekleniyor." yazar (Requested ile birlikte olsa da). Status Paid degilse veya okunamadiysa eski "Bu yapilana kadar hesap kapanmaz." cumlesi aynen kalir.
Test: split-payment-open-refund-closed.test.js 11 test; red.log yeni test kirmizi; green-file 11/11; green-static 51 dosya 606 test exit 0; mutations.txt 8 mutant hepsi killed, cmp ok; consistency-audit clean.
Acik risk: Havale ile iade edilen kapali hesapta da "fis girisi" yazar (havalede fis yok; metni PO belirler); kapali hesapta ikinci bekleyen istek metinde gorunmez; fis/iade listesi yarisinda gecici eski cumle (1076 ile ayni).
