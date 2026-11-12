# V1-RMD-1002 sonuc

- Backend: BillSplitDesignDto.TableNumber, BillReceiptV1.TableNumber + BillReceiptItemV1.AddedWithoutKitchenReason, PendingCheckSummaryV1.AddedWithoutKitchenCount (birlesik hesapta toplanir). Okuma yardimcisi BillOrderNotes (tek yer).
- Odeme ekrani (split-payment.js): masa adi ve "Kasada eklendi (mutfaga gitmedi): ..." notu (en fazla 2 kalem + "ve N kalem daha"); odendi ekraninda ve fiste Masa satiri ve kalem altinda "Kasada eklendi"; Esit bolustur ve Urune gore bol Odemeyi Ekle dugmesinin altina tasindi (DOM sirasi = gorsel sira); kisa ekranda (max-height 860) bosluklar sikilastirildi.
- Hesap bolme ekrani: Masa adi, kalem altinda not. Bekleyen hesaplar: "N kalem kasada eklendi (mutfaga gitmedi)".
- Testler: client 1276 gecti; static apps 531 gecti; .NET BillReceiptHttpTests 30, Billing (MergedBillOrderLinkTests + BillTerminalAccess) 42 gecti; oxlint/tsc/vite build 0; mutations.txt J1-J7, T1-T3, N1-N3 killed.
- Acik (olculmedi): 768x1024 ve 1366x768'de "Odemeyi Ekle" ilk ekranda mi - jsdom yerlesim olcmez; gercek ekran goruntusuyle dogrulanacak (#8 ile birlikte). Masa adi /receipt yanitiyla gec gelir (baslik ~30px uzar). Fiste "Kasada eklendi" satiri musteriye gorunur: urun karari (kaldirmak istenirse tek satir).
- qa notlari kabul: masa numarasi okuma kopyasi (3. kopya), kasa kuyrugu masasizda "—" digerleri null.
