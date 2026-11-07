# V1-RMD-869 incelemeler

1. qa-test-kalitesi: kritik/orta yok; düşük: yalnız "silinen first, kasaya gönderilen second" yönü, 404 metni ve masa işaretçisi dolaylı doğrulanıyor, bekleme sorgusu hangi ifadenin beklediğine bakmıyor (ön okumalar düz SELECT olduğundan pratikte FOR UPDATE). Kabul edildi, aynı koşuldan geçen varyantlar değer katmıyor.
2. qa-backend: kritik/orta yok; kontrol sırası doğru (rows, MIXED_SENT'ten önce). Düşük: silinen + kapanmış sipariş karışımında 409 (kapanmış) 404'ten önce gelir; sipariş döngüde okunduğu için savunulabilir, davranış değişmedi.
3. Doğrulama turu: MergeOrdersHttpTests 15/15, mutant (satır sayısı kontrolü kapalı) öldü.
