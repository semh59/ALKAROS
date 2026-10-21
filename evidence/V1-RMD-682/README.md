# V1-RMD-682 — Masa sınırı doğru sayar ve masa açarken Türkçe uyarı verir

Sonuç: Garson başına masa sınırı artık gerçek açık masayı sayıyor ve yeni masa açılırken uyarıyla durduruyor. Bulgu N1 ve R1 (V1-RMD-598) kapandı.

Davranış:
- Açık masa = ödenmemiş, iptal/ret olmayan siparişi olan farklı masa (servis edilmiş ama ödenmemiş masa da sayılır; masasız sipariş tek başına sayılır).
- Sınır 3, garson 4. masayı açmaya çalışırsa 409 `TABLE_LIMIT_REACHED`: "Bu garson en fazla 3 açık masaya bakabilir. Başka bir garson seçin veya bir masayı kapatın." Masa açılmaz.
- Zaten açık siparişi olan masaya ek tur hiçbir zaman reddedilmez. Sınır 0 (varsayılan) = sınırsız, hiçbir şey değişmez.
- "Önerilen garson" seçimi de aynı sayaçla süzülüyor (önceden sipariş sayıyordu, servis edileni dışlıyordu).
- Karar: vardiya devri (`/transfer-server`) sınırlanmadı; devir yarıda kesilmemeli. Aynı anda iki masa açılırsa sınır en çok 1 aşılabilir (kilitsiz denetim), kabul edildi.

Kanıt:
- `test-tabledraft.log`: TableDraft projesi 93/93 geçti (4 yeni test: 4. masa reddi + Türkçe metin, aynı masaya ek tur, servis edilmiş masa sayılır, sınır yokken sınırsız).
- `test-pending-notifications.log`: 12/12 geçti (2 yeni test: bir masadaki birden çok sipariş tek masa sayılır; yeni masa açma denetimi).
- `mutations.log`: 7 mutant; ilk koşuda L6 ve L7 sağ kaldı (testler ayırt etmiyordu), testler güçlendirilip yeniden koşuldu → 7/7 KILLED.

Doğrulanamadı: tarayıcıda/garson uygulamasında elle deneme; istemcinin uyarıyı kullanıcıya gösterme biçimi (ayrı iş, V1-RMD-684 ekranı).
