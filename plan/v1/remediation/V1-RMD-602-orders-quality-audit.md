# V1-RMD-602 - Orders modülünün bağımsız test ve kalite denetimi

- Task ID: V1-RMD-602
- Status: Planned
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Orders modülü ile ona bağlı Host uçları, göçler, testler ve ekranlar bağımsız denetçi ajanlarla eksiksiz denetlenir; her bulgu karşı denetçiyle doğrulanır ve doğrulanan bulgular ayrı düzeltme görevlerine bağlanır. Sistemin çalışanın işini azalttığı, hızlı olduğu ve öğrenme eşiğinin düşük olduğu da ölçülür. Bu görev kod değiştirmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-602-orders-quality-audit.md`
- `evidence/V1-RMD-602/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Denetim dilimi (yalnız okunur): `src/Modules/Orders/**`, `src/Host/Experience/{Orders,NfcOrdering,PendingOrderNotifications}/**`, `src/Host/SecurityAdministration/OrderBacklogAdministration.cs`, `DualScreenStore.Orders.cs`, göçler 011, 036, 041, 056, 076, 077, 097, 102, 104, 105, 151, `tests/Modules/Orders/**`, ilgili Host testleri, Kasa E2E 02, 14, 20, Garson E2E 02, 03, 06, 08.
- Özel dikkat: İki fazlı gönderimde yeniden deneme; iptal, ikram ve gönderilmiş kalem iptali; bekleyen kurs iptalinde stok iadesi; denetim kaydının aynı işlemde yazılması; NFC yenilemede çift sipariş ve yaş kısıtının sunucuda zorlanması.
- Tam koşacak ajanlar: `qa-backend`, `qa-api-uclar`, `qa-veritabani`, `qa-sinir-mimari`, `qa-frontend`, `qa-ux-tasarim`, `qa-guvenlik-yetki`, `qa-test-kalitesi`, `qa-performans-dayaniklilik`, `qa-mevzuat-uyum`, `qa-dokuman-bagimlilik`. Dar kapsamlı (yalnız ilgili yüzey) koşacak ajanlar: `qa-operasyon-gozlemlenebilirlik`. Ardından `qa-bulgu-dogrulayici` her bulguyu doğrular.
- İzinli komutlar: ana oturum başlangıçta çözümü bir kez derler ve dilimin test projelerini `ALKAROS_TEST_PG_PORT=55432` ile bir kez koşar; ajanlar bu kayıtları okur. Yalnız `qa-test-kalitesi` testi yeniden koşabilir; mutasyon yalnız repo dışındaki `git worktree` kopyasında ve aynı anda tek koşu olarak yapılır.
- Önceki denetimlerde kapatılan bu dilimdeki bulguların gerilemediğinin kodla kontrolü.

## Out of scope

- Kod, test, göç veya yapılandırma değişikliği: doğrulanan her bulgu ayrı düzeltme görevi olur.
- Canlı yığına (8090, 5080, 5433) veri yazan her işlem, başka projenin konteynerleri, `kvkk-retention --apply`, `audit-disposal` ve gerçek dış servislere istek.

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. `evidence/V1-RMD-602/baseline/` altında sabitlenmiş commit, dilim dosya listesi (`scope-files.txt`), sayım tablosu ve onu üreten komutlar, derleme ve test kayıtları gerçek exit code ile bulunur; kırmızı test bulgu sayılır.
2. Her ajanın ham raporu `agents/` altındadır; ajanın kontrol ettiğini söylediği kalem sayısı başlangıç sayımıyla karşılaştırılmış, sapma açıklanmış veya ajan yeniden koşturulmuştur.
3. `qa-bulgu-dogrulayici` çıktısı aynı commit üzerindedir; her bulgu DOĞRULANDI, KISMEN, ÇÜRÜTÜLDÜ veya DOĞRULANAMADI olarak kanıtıyla işaretlidir; Kritik ve Yüksek bulgular ana oturumca ayrıca kodda yeniden açılmıştır.
4. `README.md` ham, tekil, doğrulanan, çürütülen ve doğrulanamayan sayıları, ajan başına çürütme oranını, temiz alanları, doğrulanamayanları, Fırsat tablosunu ve her bulgunun bağlandığı düzeltme görevini içerir.
5. Semih senaryosu: Ağ kopup yeniden denenince sipariş ikilenmez, mutfakta tek fiş çıkar; gönderilmiş kalem iptalinde onay istenir ve stok geri döner.
6. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-603
