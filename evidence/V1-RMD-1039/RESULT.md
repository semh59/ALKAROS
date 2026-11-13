# V1-RMD-1039 RESULT

Sorun: baska kasanin acik hesabi olan masada Ode/Bol/Iptal pasifken Masa tasi etkindi.
Duzeltme: TableOrderBridge Masa tasi dugmesi ayni otherTill degiskeniyle pasif (neden title'da, panel notuyla ayni metin); salon plani denetcisindeki Masa degistir/tasi dugmesi de selectedRecord.billOnOtherTerminal ile pasif ve nedenini title'da soyler (qa-frontend: harita yolu acikti).
Kanit: red (M1/M3 mutantlari = duzeltmesiz davranis), green routes+tables 412/412, mutations.txt M1-M4 killed, client-build-gate exit 0.
Acik (yetki karari, yapilmadi): sunucu /table-management/transfers baska kasanin acik hesabini tasimayi engellemiyor (qa-guvenlik-yetki: BillTerminalAccess yok, hesap hedef masaya gecer, veri butunlugu bozulmaz). "Kasa yalniz kendi hesabini isler" kurali masa tasimaya da uygulanacak mi karari gerekli.
Elle senaryo: baska kasanin hesabi olan masayi sec: Masa tasi (panel ve salon plani) soluk, ustune gelince/aciklamada neden yazar.
