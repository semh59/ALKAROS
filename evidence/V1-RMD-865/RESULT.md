# V1-RMD-865 sonuç (F0243, F0244, F0734, F0735, F1009)

F0243 (gerçek): reconciliation.case_actions üzerinde değişmezlik koruması yoktu. Migration 220 satır tetikleyicisi (UPDATE/DELETE) ve toplu boşaltma tetikleyicisi ekler; ikisi de integrity_constraint_violation verir. Down betiği ikisini ve işlevi kaldırır; geri alıp yeniden uygulama test edildi.
F0244 (gerçek): GET vaka listesi limiti artık depoda 1..500 arasına kısılır (negatif değer Postgres hatası, büyük değer sınırsız okumaydı).
F0734, F0735, F1009 (kusur değil, kanıtla kapatıldı): aynı anahtar için Resolved/Dismissed vakadan sonra yeni vaka açılmaması PostgresReconciliationRepository.CreateOrDeduplicateCaseAsync içinde V1-RMD-323 kararıyla bilinçli kuraldır; kısmi unique index (uq_reconciliation_cases_active_dedup) ve advisory kilit yerinde; V1-RMD-864'te eklenen "kapatılmış vaka tekrar okunmaz" testi bu kuralı da sınar. Davranış değişmedi.
Testler: CaseFoundation 10/10 (green-casefoundation.log); Host MigrationSchemaTests + ManifestTests 52/52 (green-migrations.log, 7 dk 39 sn); yeni koruma testi tek başına geçti (final-case-history.log).
Mutasyon (cp+cmp geri alındı, yeniden derlenerek): kelepçe alt sınırı, üst sınırı, UPDATE korumasız, DELETE korumasız, boşaltma tetikleyicisi yanlış tabloda: beşi de öldü (mutations.txt).
Not: order.json içinde phaseBRange.max de 220 yapıldı (manifest testi bunu denetler).
