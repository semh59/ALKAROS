# V1-RMD-674 kanıtı

- Göç 196: garson × indirim `auto_within` 100 TL, en çok 10, 8 saat; var olan satıra dokunmaz; ikram/iptal için satır yok.
- Devir: devreden pasifse FindCovering sonuç vermez.
- Testler: test-authorization.log (244 yeşil), test-manifest.log (16), test-billing.log (71, Billing düzeneği göçleri elle seçtiği için tohumdan etkilenmez).
- Mutasyon: mutations.log, 5/5 yakalandı.
- Doğrulanamadı: tam Host.Tests (tüm göç zinciriyle) bu görevde çalıştırılmadı; indirim isteyen akışların tam zincirdeki davranışı elle doğrulanmadı. Garsonun 100 TL altı indirimi artık yönetici onayı beklemez (Ürün Sahibi kararı).
