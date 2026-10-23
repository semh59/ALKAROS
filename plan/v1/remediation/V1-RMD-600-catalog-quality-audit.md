# V1-RMD-600 - Catalog modülünün bağımsız test ve kalite denetimi

- Task ID: V1-RMD-600
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Catalog modülü ile ona bağlı Host uçları, göçler, testler ve ekranlar bağımsız denetçi ajanlarla eksiksiz denetlenir; her bulgu karşı denetçiyle doğrulanır ve doğrulanan bulgular ayrı düzeltme görevlerine bağlanır. Sistemin çalışanın işini azalttığı, hızlı olduğu ve öğrenme eşiğinin düşük olduğu da ölçülür. Bu görev kod değiştirmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-600-catalog-quality-audit.md`
- `evidence/V1-RMD-600/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-701-terminal-binding.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Denetim dilimi (yalnız okunur): `src/Modules/Catalog/**`, `src/Host/Experience/Catalog/**`, terminal katalog rotası, göçler 006, 007, 022, 040, 053, 084, 097, 103, `tests/Modules/Catalog/**`, Kasa E2E 01, 22, 26, 27, katalog ve ürün ızgarası ekranları.
- Özel dikkat: KDV profilleri ve oran değişikliği geçerlilik tarihi; güncel fiyat yeniden hesabı; satır sürümü; fiyat değişikliğinin açık siparişe etkisi; katalog yönetim iznin katalog dışında tanımlı olması.
- Tam koşacak ajanlar: `qa-backend`, `qa-api-uclar`, `qa-veritabani`, `qa-sinir-mimari`, `qa-frontend`, `qa-ux-tasarim`, `qa-guvenlik-yetki`, `qa-test-kalitesi`, `qa-mevzuat-uyum`, `qa-dokuman-bagimlilik`. Dar kapsamlı (yalnız ilgili yüzey) koşacak ajanlar: `qa-performans-dayaniklilik`, `qa-operasyon-gozlemlenebilirlik`. Ardından `qa-bulgu-dogrulayici` her bulguyu doğrular.
- İzinli komutlar: ana oturum başlangıçta çözümü bir kez derler ve dilimin test projelerini `ALKAROS_TEST_PG_PORT=55432` ile bir kez koşar; ajanlar bu kayıtları okur. Yalnız `qa-test-kalitesi` testi yeniden koşabilir; mutasyon yalnız repo dışındaki `git worktree` kopyasında ve aynı anda tek koşu olarak yapılır.
- Önceki denetimlerde kapatılan bu dilimdeki bulguların gerilemediğinin kodla kontrolü.

## Out of scope

- Kod, test, göç veya yapılandırma değişikliği: doğrulanan her bulgu ayrı düzeltme görevi olur.
- Canlı yığına (8090, 5080, 5433) veri yazan her işlem, başka projenin konteynerleri, `kvkk-retention --apply`, `audit-disposal` ve gerçek dış servislere istek.
- Düzeltme görevleri V1-RMD-702 ve sonrası olarak ayrıca planlanır; profilsiz mevcut ürünlerin dönüşümü PO kararı bekler.
- Fiyat ekranında bitiş tarihi ve fiyat düzeltme uçları V1-RMD-703 kapsamı dışında kalır.
- Modifikatör atamasını kaldırma ve modifikatör formuna ürün alanı V1-RMD-705 kapsamı dışındadır.

## Dependencies

- V1-RMD-599

## Acceptance evidence

1. `evidence/V1-RMD-600/baseline/` altında sabitlenmiş commit, dilim dosya listesi (`scope-files.txt`), sayım tablosu ve onu üreten komutlar, derleme ve test kayıtları gerçek exit code ile bulunur; kırmızı test bulgu sayılır.
2. Her ajanın ham raporu `agents/` altındadır; ajanın kontrol ettiğini söylediği kalem sayısı başlangıç sayımıyla karşılaştırılmış, sapma açıklanmış veya ajan yeniden koşturulmuştur.
3. `qa-bulgu-dogrulayici` çıktısı aynı commit üzerindedir; her bulgu DOĞRULANDI, KISMEN, ÇÜRÜTÜLDÜ veya DOĞRULANAMADI olarak kanıtıyla işaretlidir; Kritik ve Yüksek bulgular ana oturumca ayrıca kodda yeniden açılmıştır.
4. `README.md` ham, tekil, doğrulanan, çürütülen ve doğrulanamayan sayıları, ajan başına çürütme oranını, temiz alanları, doğrulanamayanları, Fırsat tablosunu ve her bulgunun bağlandığı düzeltme görevini içerir.
5. Semih senaryosu: Fiyat yarından geçerli olacak şekilde değiştirilir; bugün eski, yarın yeni fiyat çıkar; KDV ürün kartında ve fişte aynıdır.
6. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-601
