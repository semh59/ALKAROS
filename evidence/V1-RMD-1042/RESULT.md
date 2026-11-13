# V1-RMD-1042 RESULT

Karar (Semih 2026-11-13): baska kasanin (terminalin) acik hesabi olan masayi tasimayi sunucu da engellesin.
Duzeltme: POST table-management/transfers, kaynak masanin hesabi baska terminale aitse ve cagiran cash.session.override yetkisine sahip degilse 409 BILL_OTHER_TERMINAL + "Bu hesap baska bir kasada acik; yalnizca o kasa veya bir yonetici islem yapabilir." (Turkce, Billing ile ayni ileti sabiti) doner; masa tasinmaz. Kural masa listesindeki BillOnOtherTerminal bayragi ile ayni (ayni store sorgusu, ayni yetki).
Ekran: yeni kod yok; TableWorkspace sunucunun Turkce iletisini pencerede oldugu gibi gosterir (CONCURRENT_MODIFICATION disinda genel metne dusmez); TableWorkspace.test.tsx it.each ile BILL_OTHER_TERMINAL iletisini kilitler.
Kanit: red.log (Conflict beklenirken OK), green.log Tables.Tests 48/48, green-client.log 53/53, mutations.txt M1-M5 killed, client-build-gate exit 0.
Acik (kapsam disi / karar): (1) POST /merges ve /tables/{id}/status ayni kurali uygulamiyor (qa-guvenlik-yetki, qa-backend); (2) garson uygulamasi ayni /transfers ucunu kullanir: hesabi bir kasaya ait masayi garson artik tasiyamaz, toast'ta iletiyi gorur, dugme onceden pasiflesmez; (3) durum kodu Billing'de 403, burada 409 (TableOperationBlockedException eslemesi); (4) kontrol ile tasima arasinda islem yok, sahipsiz hesabin o an baska kasada sahiplenilmesi dar penceredir.
Elle senaryo: iki kasada ayni masa icin hesap ac; ikinci kasadan Masa tasi > Onayla: pencerede Turkce ileti, masa yerinde kalir; yonetici hesabiyla tasinir.
