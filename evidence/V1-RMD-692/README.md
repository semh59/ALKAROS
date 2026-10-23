# V1-RMD-692 - KVKK ve saklama: bildirim tabloları, anonimleştirme, PIN özeti, vergi kimliği izi

## Ne değişti

- **Temizlik (B007, B014, B115, B113):** `housekeeping` artık ikinci ekran oturumlarını ve eşleştirme isteklerini, devir notlarını, kullanılmayan veya pasif kullanıcıya ait push aboneliklerini ve eski yardım çağrılarını da siliyor. Silme 5000'lik parçalarla, her parça kendi işleminde; yarıda kalırsa yeniden çalıştırmak yeter. Süreler argümanla ayarlanır: `--notification-days` (varsayılan 90), `--help-request-days` (365), `--grace-days` (7). Bu süreler Semih'in S1–S3 yanıtları gelene kadar önerilen varsayılandır.
- **Anonimleştirme (B007, B138):** personel anonimleştirilince `pin_hash` temizleniyor, o kişinin gönderdiği/aldığı devir notları `[anonymized]` oluyor, push abonelikleri siliniyor. Başka kişilerin notları ve abonelikleri dokunulmadan kalıyor.
- **Vergi kimliği izi (B006):** müşterinin vergi kimliği değişince `customer.tax_identity_changed` denetim olayı yazılıyor (kim, ne zaman, önceki/sonraki tür; numara asla yazılmıyor). Başarısız/geçersiz istek olay yazmıyor.
- **Devir notu tekilliği (B109):** göç 200: eski çiftler kapatılıyor, alıcı başına tek bekleyen not benzersiz indeksle zorlanıyor; eşzamanlı iki devir hata vermeden eskisini kapatıyor (tetikleyici + kilit).
- **Yardım çağrısı kaydı (B106):** `help_requests` yabancı anahtarları `CASCADE`'ten `RESTRICT`'e alındı; masa veya kullanıcı silinince kayıt sessizce yok olmuyor.
- **Belgeler (B138, B043 kısmen):** `docs/compliance/kvkk-retention-runbook.md` (PIN özeti, bildirim tabloları, vergi kimliği izi, var olmayan compose servisine atıf düzeltildi), `docs/operations/data-housekeeping.md`, göç prova profili.

## Kanıt

- `mutate.mjs` / `mutations.log`: 27 mutant. İlk geçişte H5 (yardım çağrısı süresi yanlış argümanı) ve A5 (denetim olayında aktör kimliği) sağ kaldı; testler güçlendirildi (200 günlük çağrı, aktör kimliği eşitliği), ikinci geçişte ikisi de KILLED. Denetim yazımı mimari kuralı (Host/Experience/CustomerAccounts, Audit'e bağımlı olamaz) yüzünden mağazadan uç noktaya taşındı; A1–A6 bu son yerleşimle yeniden koşuldu, hepsi KILLED. Kalan mutantlar ilk geçişte KILLED; kaynak her seferinde geri yüklendi.
- `logs/`: tam `Host.Tests`, mimari testler, müşteri hesapları testleri.
- Yeni testler: `HousekeepingTests` (temizlik ve devir notu/yabancı anahtar şema testleri dahil, 18 test), `KvkkRetentionTests` (PIN, not, abonelik), `CustomerAccountsHttpTests` (denetim). Şema testleri, yeni test dosyası görev yüzeyi dışında kaldığı için `HousekeepingTests` içine alındı; D1–D7 bu yerleşimle yeniden koşuldu, hepsi KILLED.

## Yapılmayanlar / doğrulanamadı

- **B043:** `kvkk-retention` için zamanlanmış compose servisi eklenmedi (`compose*.yaml` bu görevin yüzeyinde değil); runbook'ta yalnız yanlış atıf düzeltildi.
- **B006'nın "çözümleme" tarafı:** faturalama (`PostgresInvoiceGenerationService`) ve cari hesap tahsilatı (`AccountChargeHandler`) müşteri profilini yönetici rolüyle okuyor; bu okumalar bu görevin yüzeyi dışında ve iz bırakmıyor. Yalnız değişiklik izlendi.
- **Silme süreleri:** `help_requests` için 365 gün, devir notu ve push için 90 gün PO yanıtı olmadan önerilen değerlerdir. Anonimleştirmede `help_requests` satırı yalnız kullanıcı kimliği (artık kimseyi göstermeyen) taşıdığı için dokunulmadı.
- `identity.users.pin_hash` kalıntı sorgusu (`OR pin_hash IS NOT NULL`) tek başına bir testle öldürülemiyor: güncelleme PIN'i temizlemeyi atlarsa ilk test zaten kırılıyor.
- Yedekten dönen verinin silme taleplerine uygunluğu (S7) kapsam dışı, ayrı görev.
- Eski veritabanlarında 200 göçü `help_requests` yabancı anahtar adlarının PostgreSQL varsayılanı olduğunu varsayıyor (`..._table_id_fkey`); bu depoda göç 101 böyle oluşturuyor, canlı bir veritabanında adlar elle değiştirilmişse DROP sessizce atlanır ve eski kural kalır.
