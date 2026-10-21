# V1-RMD-673 kanıtı

- Göç 195: hibede onaylayan≠talep eden CHECK (NOT VALID: yeni satırlar için), değişmez alanlara talep eden rolü ve konu alanları eklendi; devir tablosunda silme yasağı ve yalnız tek seferlik iptal alanı değişimi.
- Testler (gerçek PostgreSQL): test-authorization.log (241 yeşil, yeni 6 test dahil, aşağı göç testi dahil), test-manifest.log (16), test-host-authorization.log (10).
- Mutasyon: mutations.log, 7 mutantın 7'si yakalandı.
- Açık kalanlar (plan): hibede devir kaydı (delegation_id), kullanım anında devredenin aktifliği ve kümülatif tavan, kullanıcı/oturum yabancı anahtarları, ret ve bütçe kayıtlarının temizliği. Başlangıç limitleri tohumlanmadı: garsona indirimde otomatik onay 2026-09-04 kararıyla çelişiyor; politika ekranı yok.
- Not: tam Host.Tests ve Experience Orders/Billing paketleri bu görevde yeniden çalıştırılmadı (bellek koruması bir arka plan çalışmasını durdurdu, yeniden başlatılmadı). Göç yalnız bu tabloları etkiler ve ilgili Host Authorization testleri yeşil.
