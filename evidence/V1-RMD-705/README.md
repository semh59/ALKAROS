# V1-RMD-705 Modifikatör grubunu ürüne bağlama ekranı

Kapanan bulgu: V1-RMD-600-B004 (Yüksek).

- Katalog istemcisi `/product-modifier-assignments` atamalarını yükler (7 kaynak).
- Ürün ayrıntısında "BAĞLI MODİFİKATÖR GRUPLARI" listesi ve yönetici için "Grup ata" seçici + "Grubu ata" düğmesi; yetkisiz kullanıcıda kontrol görünmez.
- Atama sunucudaki mevcut uca yazılır; sunucu değişmedi.
- Testler: katalog 24/24, tüm PosTerminal vitest 759/759, tsc exit 0 (logs/). oxlint yalnız önceden var olan CustomerDisplay.tsx:244 hatası ve uyarıları gösterir.
- Mutasyon: 6/6 KILLED, kaynak geri yüklendi (mutations.log).

Açık: atamayı kaldırma ucu sunucuda yok; modifikatör formunda ürün alanı yok (kapsam dışı). Gerçek tarayıcıda doğrulanamadı.
