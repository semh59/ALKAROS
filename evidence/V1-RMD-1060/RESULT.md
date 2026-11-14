# V1-RMD-1060 RESULT

Satis ekrani: siparis mutfaga gonderilince kendiliginden kapanan bildirim acilmaz (panel ve alt cubuk dugmesi); kalici "✓ Sipariş gönderildi" bandi (role=status) kalir ve hizli satista "Yeni satış açabilirsiniz." ipucunu tasir; hata bildirimi eskisi gibi acilir. Siparis yokken alttaki sabit "Aktif sipariş yok / Yeni satış" cubugu cizilmez; siparis varken cubuk "Toplam", gonderildikten sonra "Gönderildi · Toplam" yazar.
Kanit: red.log (3 kirmizi), green-file.log, green-all.log PosTerminal tum suite, client-build-gate.log exit 0, mutations.txt M1-M12 killed.
Elle senaryo: hizli satista urun ekle, Mutfaga gonder: yalniz panelde Siparis gonderildi bandi ve "Yeni satis acabilirsiniz" gorunur, altta ayrica bildirim cikmaz. 768px'te siparis yokken alt cubuk yok.
Dogrulama gerekli (ekranda): dar ekranda sayfa altindaki 78px bosluk (cashier-shell padding) cubuk yokken kalir; cubuk-panel gorunurlugu.
