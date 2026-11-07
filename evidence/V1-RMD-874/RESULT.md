# V1-RMD-874 sonuç

payments.manual_card_confirmations'ta yalnız Pending'i kapsayan kısmi indeks vardı; ödeme yöntemi çözümlemesi (PaymentMethodLookup.Sql: payment_id = X AND status = 'Approved'; iade isteği ve toplu çözümleme) Approved'i arar, indeks kullanılamaz, FK de otomatik indeks üretmez. Migration 221: ix_manual_card_confirmations_payment_status (payment_id, status), down betiği düşürür. Manifest, order.json ve sayım testleri 221'e taşındı.
Test (katalogdan: kolon sırası, benzersiz değil, kısmi değil): 221 geri alınınca kalkar; yeniden uygulanır (MigrationSchemaTests). Hedefli koşu 19/19 (ManifestTests, vaka geçmişi koruma testi, yeni test; green-1.log).
Mutasyon (cp+cmp geri alındı): yalnız payment_id kolonu, kısmi indeks, down betiği indeksi bırakıyor, UNIQUE, ters kolon sırası: beşi de öldü.
İndeksin gerekçesi: PaymentMethodLookup.cs:33 ve PostgresPaymentSettlementReportRepository.cs:33 yalnız Approved arar; Pending kısmi indeksi onları karşılamaz ve benzersizlik kuralını taşıdığı için kalır (yazma maliyeti tablo küçük olduğundan ihmal edilebilir).
Not: tüm tests/Host/MigrationComposition projesi tek koşuda ~20 dakikayı aşıp yarıda kesildi (full-run-aborted.log); orada DeployFilesTests.TheBuildContextExcludesTheEnvironmentFileAndGitHistory ".git" bulunamadı diyerek düşüyor (bu görevle ilgisiz, mevcut ortam sorunu: sürüm kontrol dizini görünmüyor).
Sınır: indeksin planlayıcı tarafından seçildiği EXPLAIN ile ölçülmedi (tablo küçük); sorgu için gerekçe kod okumasıdır.
