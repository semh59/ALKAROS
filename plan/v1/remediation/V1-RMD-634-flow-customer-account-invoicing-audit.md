# V1-RMD-634 - Cari hesap ve dönem faturası akışının uçtan uca test ve kalite denetimi

- Task ID: V1-RMD-634
- Status: Planned
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Cari hesap ve dönem faturası akışı halka halka izlenir: her halkada veri gerçekten aktarılıyor mu, tutar, miktar ve durum tutarlı mı, kısmi hata ve geri alma yolunda veri bozuluyor mu. Sistemin çalışanın işini azalttığı, hızlı olduğu ve öğrenme eşiğinin düşük olduğu da ölçülür; fırsatlar ayrı tabloda raporlanır. Bu görev kod değiştirmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-634-flow-customer-account-invoicing-audit.md`
- `evidence/V1-RMD-634/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Akış halkaları: CustomerData, veresiye ve tahsilat, kasa, kaynak seçimi, taslak, izlenebilirlik, QNB sınırı.
- Mevcut otomatik kanıt: Kasa E2E 30.
- Koşacak ajanlar: `qa-uctan-uca-akis`, `qa-mevzuat-uyum`; `qa-guvenlik-yetki`, `qa-entegrasyon-cihaz`, `qa-test-kalitesi` dar kapsamlı. Ardından `qa-bulgu-dogrulayici` her bulguyu doğrular.
- Veri yazan adımlar yalnız E2E düzeneğinde denenir (kendi veritabanı ve Host, port 55432, aynı anda tek koşu); canlı yığına yalnız oturumsuz okuma isteği atılır.
- İzinli komutlar: ana oturum başlangıçta çözümü bir kez derler ve dilimin test projelerini `ALKAROS_TEST_PG_PORT=55432` ile bir kez koşar; ajanlar bu kayıtları okur. Yalnız `qa-test-kalitesi` testi yeniden koşabilir; mutasyon yalnız repo dışındaki `git worktree` kopyasında ve aynı anda tek koşu olarak yapılır.

## Out of scope

- Kod, test, göç veya yapılandırma değişikliği: doğrulanan her bulgu ayrı düzeltme görevi olur.
- Canlı yığına (8090, 5080, 5433) veri yazan her işlem, başka projenin konteynerleri, `kvkk-retention --apply`, `audit-disposal` ve gerçek dış servislere istek.

## Dependencies

- V1-RMD-633

## Acceptance evidence

1. `evidence/V1-RMD-634/baseline/` altında sabitlenmiş commit, dilim dosya listesi (`scope-files.txt`), sayım tablosu ve onu üreten komutlar, derleme ve test kayıtları gerçek exit code ile bulunur; kırmızı test bulgu sayılır.
2. Her ajanın ham raporu `agents/` altındadır; ajanın kontrol ettiğini söylediği kalem sayısı başlangıç sayımıyla karşılaştırılmış, sapma açıklanmış veya ajan yeniden koşturulmuştur.
3. `qa-bulgu-dogrulayici` çıktısı aynı commit üzerindedir; her bulgu DOĞRULANDI, KISMEN, ÇÜRÜTÜLDÜ veya DOĞRULANAMADI olarak kanıtıyla işaretlidir; Kritik ve Yüksek bulgular ana oturumca ayrıca kodda yeniden açılmıştır.
4. `README.md` ham, tekil, doğrulanan, çürütülen ve doğrulanamayan sayıları, ajan başına çürütme oranını, temiz alanları, doğrulanamayanları, Fırsat tablosunu ve her bulgunun bağlandığı düzeltme görevini içerir.
5. Adım, modül, aktarılan veri ve değişmez tablosu hazırdır; her halkada tutar, KDV ve miktar takibi yapılmıştır; E2E koşu kaydı eklidir.
6. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-635
