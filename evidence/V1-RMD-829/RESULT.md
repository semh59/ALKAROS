# V1-RMD-829 sonuç

Kapatılan: yanlış girilen indirim ve bahşiş artık geri alınabilir (K18 sunucu tarafı). Uç: `POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/adjustments/{adjustmentId}/undo`; yetki bahşiş girişi ile aynı (bills.split); yalnız indirim ve bahşiş (`ADJUSTMENT_NOT_UNDOABLE` 400); hesap durumu uygulama ucuyla aynı kuralı izler (ödenmiş/iptal: 409 `UNSUPPORTED_BILL_STATE`); geri alma ödenecek tutarı toplanan tutarın altına indirecekse (bahşiş) 409 `ADJUSTMENT_UNDO_BELOW_COLLECTED`; olmayan, başka hesaba ait ya da zaten geri alınmış kayıt 404 `NOT_FOUND`. Kayıt silinir (migration yok, tüm okuyucular tutarlı kalır); `bill.adjustment.undone` denetim olayı silinen kaydı (tür, tutar, gerekçe, ekleyen, zaman) ve geri alan kullanıcıyı tutar. Hesap satır sürümü artar (eski sürümle kaydedilen bölme reddedilir), müşteri ekranına `PaymentChanged` gider.

## Doğrulama
- Gerçek Postgres HTTP testleri (Undo): indirim geri alma + denetim izi, bahşiş geri alma ve diğer düzeltmenin korunması, 404 (yok/yabancı/tekrar), geri alınamaz tür, ödenmiş hesap ve toplanan tutarın altı, oturumsuz 401 / salt okunur 403, müşteri ekranı bildirimi, sürüm artışı, depo yabancı hesabın kaydını silmez.
- Mutasyon 8 mutant (durum, tür, toplanan tutar, denetim olayı, bill_id eşleşmesi, ekran bildirimi, sürüm artışı) hepsi öldürüldü; ilk turda sürüm artışı ve bill_id eşleşmesi mutantları yaşadı, iki test eklenerek öldürüldü. Per-bill advisory kilidi tek bağlantılı testle ölçülemez, apply ucuyla birebir aynı sırada alınır (kod incelemesiyle doğrulandı).
- Ham mutasyon kaydı: mutation-raw.txt, mutation-raw2.txt.

## İncelemeler
Kabul edilen (gerekçeli): Allocated durumunda geri alma uygulama ucuyla aynı kuralı izler (bölme tasarımı sürüm artışıyla yeniden yüklenir); onaylı indirimi bills.split sahibinin geri alabilmesi kabul (ödenecek tutarı yalnız ilk haline döndürür, tek yönlü ve denetim izli); tekrar çağrı 404 (çift uygulanamaz, istemci 830'da bunu "zaten kaldırılmış, yenile" diye ele alır).
- Billing test projesi 210/210 (10 dk 17 sn), ApiConventions 17/17, ModuleBoundaries 31/31 (logları bu klasörde). İki inceleme (qa-backend, qa-api-uclar) sonrası yalnız plan metni koda göre düzeltildi (rota, hata kodu, istisna adı); kod değişmedi, doğrulama turu bu üç koşuyla yapıldı.
