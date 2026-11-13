# V1-RMD-1049 RESULT

Karar (Semih 2026-11-14): nakit tahsilat yalniz cagiran kasiyerin kendi acik kasa oturumuna alinir; terminalde tek oturum olur, yonetici oturumu aciksa once o kapatir.
Duzeltme: cash-tender ucu oturum sahibi cagiran degilse 409 CASH_SESSION_NOT_OWNED (Turkce) doner ve hicbir odeme yazmaz; tahsilat ekrani baskasinin oturumunu gorunce nakdi kapatir, "Once kasayi acin" bildirimi ve tek-oturum aciklamasini gosterir. Iade onayindaki "kasiyerin kendi terminal oturumu" kuraliyla ayni anahtar.
Kanit: red-http.log, red-client.log (iki test kirmizi); green-http.log CashTender 8/8 (sinif 30/30 daha once), green-tender-access.log 12/12 (yonetici gecisi 4xx), green-errors.log 232/232, green-client.log 560/560; mutations.txt M1-M8 killed (M6 gercek assertion ile dogrulandi).
Not: proje geneli CashSession test kosusu ilk denemede test ana islemi kilitlenmesiyle durdu (bellek); sinif bazli kosuldu.
Elle senaryo: yonetici kasayi acik birakir, kasiyer giris yapar, hesabi tahsilat ekranina acar: nakit secilemez, "Once kasayi acin" gorunur; yonetici kapatip kasiyer kendi oturumunu acinca nakit calisir.
Kapsam disi (urun karari olarak listelenir): cari tahsilat (account-receipts) ayni sahiplik kuralina baglanmadi; PosTerminal kasa rozeti baskasinin oturumunu hala "acik" gosterir; yoneticinin (cash.session.override) baskasinin oturumuna nakit almasi.
