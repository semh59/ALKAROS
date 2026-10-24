# V1-RMD-603 - Billing modülünün bağımsız test ve kalite denetimi

- Task ID: V1-RMD-603
- Status: Done
- Assignee: claude-code-session_01XYCVWVTbShZKMmSzdAGR6K
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Billing modülü ile ona bağlı Host uçları, göçler, testler ve ekranlar bağımsız denetçi ajanlarla eksiksiz denetlenir; her bulgu karşı denetçiyle doğrulanır ve doğrulanan bulgular ayrı düzeltme görevlerine bağlanır. Sistemin çalışanın işini azalttığı, hızlı olduğu ve öğrenme eşiğinin düşük olduğu da ölçülür. Bu görev kod değiştirmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-603-billing-quality-audit.md`
- `evidence/V1-RMD-603/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Denetim dilimi (yalnız okunur): `src/Modules/Billing/**`, `src/Host/Experience/Billing/**`, `DualScreenApplication.Receipt.cs`, göçler 019, 020, 021, 032, 036, 075, 108, 123, 180, 181, `tests/Modules/Billing/**`, `tests/Host/Experience/Billing/**`, `tests/Host/Experience/PaymentTender/**`, Kasa E2E 04, 18, hesap ve bölme ekranları.
- Özel dikkat: Hesap matematiği, yuvarlama ve KDV; indirim ve bahşiş farkında tahsilat tavanı; idempotency istisna listesindeki mutasyon uçları; iptal gerekçesi; bahşiş ve servis ücreti kuralı.
- Tam koşacak ajanlar: `qa-backend`, `qa-api-uclar`, `qa-veritabani`, `qa-sinir-mimari`, `qa-frontend`, `qa-ux-tasarim`, `qa-guvenlik-yetki`, `qa-test-kalitesi`, `qa-performans-dayaniklilik`, `qa-mevzuat-uyum`, `qa-dokuman-bagimlilik`. Ardından `qa-bulgu-dogrulayici` her bulguyu doğrular.
- İzinli komutlar: ana oturum başlangıçta çözümü bir kez derler ve dilimin test projelerini `ALKAROS_TEST_PG_PORT=55432` ile bir kez koşar; ajanlar bu kayıtları okur. Yalnız `qa-test-kalitesi` testi yeniden koşabilir; mutasyon yalnız repo dışındaki `git worktree` kopyasında ve aynı anda tek koşu olarak yapılır.
- Önceki denetimlerde kapatılan bu dilimdeki bulguların gerilemediğinin kodla kontrolü.

## Out of scope

- Kod, test, göç veya yapılandırma değişikliği: doğrulanan her bulgu ayrı düzeltme görevi olur.
- Canlı yığına (8090, 5080, 5433) veri yazan her işlem, başka projenin konteynerleri, `kvkk-retention --apply`, `audit-disposal` ve gerçek dış servislere istek.

## Dependencies

- V1-RMD-602
- V1-RMD-725

## Acceptance evidence

1. `evidence/V1-RMD-603/baseline/` altında sabitlenmiş commit, dilim dosya listesi (`scope-files.txt`), sayım tablosu ve onu üreten komutlar, derleme ve test kayıtları gerçek exit code ile bulunur; kırmızı test bulgu sayılır.
2. Her ajanın ham raporu `agents/` altındadır; ajanın kontrol ettiğini söylediği kalem sayısı başlangıç sayımıyla karşılaştırılmış, sapma açıklanmış veya ajan yeniden koşturulmuştur.
3. `qa-bulgu-dogrulayici` çıktısı aynı commit üzerindedir; her bulgu DOĞRULANDI, KISMEN, ÇÜRÜTÜLDÜ veya DOĞRULANAMADI olarak kanıtıyla işaretlidir; Kritik ve Yüksek bulgular ana oturumca ayrıca kodda yeniden açılmıştır.
4. `README.md` ham, tekil, doğrulanan, çürütülen ve doğrulanamayan sayıları, ajan başına çürütme oranını, temiz alanları, doğrulanamayanları, Fırsat tablosunu ve her bulgunun bağlandığı düzeltme görevini içerir.
5. Semih senaryosu: Dört kişilik masada hesap ürün bazında bölünür, birine yüzde on indirim uygulanır, biri nakit biri kartla öder; kalan tutar sıfır olunca hesap kapanır ve fişte KDV doğrudur.
6. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
