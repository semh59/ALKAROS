# V1-RMD-1027 - Baska kasanin hesabinda masa panelinde Ode onceden pasif

Duzeltme: GET /tables yanitina billOnOtherTerminal (TableManagementStore.GetBillsOwnedByOtherTerminalsAsync, tek ek sorgu; cash.session.override sahibi icin sorgu yok). Masa paneli (TableOrderBridge) bu durumda Ode, Bol, Iptal / iade dugmelerini pasif yapar; ust nottaki metin sunucu mesajiyla ayni ("Bu hesap baska bir kasada acik; ...").
Kanit: backend testi once kirmizi (Assert.True), sonra yesil; Tables.Tests 44/44; ApiConventions 17/17; istemci tests 159/159; client-build-gate exit 0. Mutasyon 5/5 killed (bir esdeger mutant, "terminal_id IS NOT NULL", gereksiz kosul olarak kaldirildi).
Gercek ekran: 8090 eski derleme; kod ve test kaniti (kullanici izniyle).
Acik: birlestirilmis masada yalniz ana masanin hesabina bakilir; Bekleyen hesaplar ekrani ayni kurali gostermez (urun karari); tek masa ucu alani doldurmaz.
