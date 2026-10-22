# V1-RMD-689 — Yardım çağrısı kalıcı, geri bildirimli ve kasada görünür

Sonuç: tamam. Kapanan bulgular: B002, B067, B073, B082, N001.

- B002: çağrı kayıtta kalır; yönetici/süpervizör "açık çağrılar" listesinden yeniden yükler (migration 198, `GET …/help-requests/open`). Yanıt, o an bağlı yönetici sayısını (`connectedRecipients`) taşır; 0 ise garsona "iletildi" denmez, "kaydedildi" denir. Yönetici "Gördüm" deyince (`POST …/acknowledge`) çağrıyı açan garsona `HelpRequestSeen` gider. Aynı masanın yanıtlanmamış çağrısı tekrarı engeller, yanıtlanmışsa yeni çağrı açılabilir.
- B067: kasada yeni çağrı sesle duyurulur; çağrı yalnız sunucu "görüldü" kaydını yaptıktan sonra ekrandan kalkar; bağlantı geri geldiğinde açık liste yeniden yüklenir.
- B073: 429 yanıtı `Retry-After` başlığı ve Türkçe kalan süre taşır.
- B082: boş masa kimliği 400 döner.
- N001: bilinmeyen çağrı türü kasada "Yardım çağrısı" olarak gösterilir, ham İngilizce kod basılmaz.

Kanıt:
- `help-tests.log`: HelpRequests 22/22 yeşil. `pos-tests.log`: PosTerminal 13/13, `tsc --noEmit` temiz.
- `api-conventions.log` 5/5, `module-boundaries.log` 10/10, `migration-composition.log` 196/196.
- Mutasyon: `mutations-run1.log`, `-run2.log`, `mutations.log`; 12 mutantın 12'si KILLED (ilk koşuda M10–M12 hayatta kaldı, test eklendi; M1/M4/M6 derlenmediği için yeniden yazıldı). Her biri geri yüklendi.
- Doğrulanamadı: WaiterPwa tarayıcı uçtan uca testi (Playwright) bu oturumda koşulmadı; `test_waiter_pwa_frontend.py` içindeki 3 eski test bu değişiklikten önce de başarısızdı, yeni test geçiyor.
- Ertelendi: yardım çağrısı saklama süresi V1-RMD-692'de.
