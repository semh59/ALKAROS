# V1-RMD-599 - Host modülünün bağımsız test ve kalite denetimi

- Task ID: V1-RMD-599
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Host modülü ile ona bağlı Host uçları, göçler, testler ve ekranlar bağımsız denetçi ajanlarla eksiksiz denetlenir; her bulgu karşı denetçiyle doğrulanır ve doğrulanan bulgular ayrı düzeltme görevlerine bağlanır. Sistemin çalışanın işini azalttığı, hızlı olduğu ve öğrenme eşiğinin düşük olduğu da ölçülür. Bu görev kod değiştirmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-599-host-quality-audit.md`
- `evidence/V1-RMD-599/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Denetim dilimi (yalnız okunur): `src/Host/**` içinde diğer görevlere atanmayan 44 dosya (`Program.cs`, `Composition/**`, kalan DualScreen dosyaları, HelpRequests, WebPush, WaiterNotifications, HostDatabaseConnection), `customer_display` ve `notifications` şemaları (038, 119; 096, 100, 101), `tests/Host/**` platform testleri ve mimari testler.
- Özel dikkat: Oturum filtresinin tüm uç dosyalarında tekdüzeliği; oturumsuz istisna listesinin gerekçeleri; hata kataloğunda ham istisna mesajı sızıntısı; hız sınırı, TLS ve göç koşucusu; hub bağlantılarında oturum kontrolü; Caddy güvenlik başlıkları.
- Tam koşacak ajanlar: `qa-backend`, `qa-api-uclar`, `qa-veritabani`, `qa-sinir-mimari`, `qa-guvenlik-yetki`, `qa-test-kalitesi`, `qa-performans-dayaniklilik`, `qa-operasyon-gozlemlenebilirlik`, `qa-entegrasyon-cihaz`, `qa-dokuman-bagimlilik`. Dar kapsamlı (yalnız ilgili yüzey) koşacak ajanlar: `qa-frontend`, `qa-ux-tasarim`, `qa-mevzuat-uyum`. Ardından `qa-bulgu-dogrulayici` her bulguyu doğrular.
- İzinli komutlar: ana oturum başlangıçta çözümü bir kez derler ve dilimin test projelerini `ALKAROS_TEST_PG_PORT=55432` ile bir kez koşar; ajanlar bu kayıtları okur. Yalnız `qa-test-kalitesi` testi yeniden koşabilir; mutasyon yalnız repo dışındaki `git worktree` kopyasında ve aynı anda tek koşu olarak yapılır.
- Önceki denetimlerde kapatılan bu dilimdeki bulguların gerilemediğinin kodla kontrolü.

## Out of scope

- Kod, test, göç veya yapılandırma değişikliği: doğrulanan her bulgu ayrı düzeltme görevi olur.
- Canlı yığına (8090, 5080, 5433) veri yazan her işlem, başka projenin konteynerleri, `kvkk-retention --apply`, `audit-disposal` ve gerçek dış servislere istek.

## Dependencies

- V1-RMD-598

## Acceptance evidence

1. `evidence/V1-RMD-599/baseline/` altında sabitlenmiş commit, dilim dosya listesi (`scope-files.txt`), sayım tablosu ve onu üreten komutlar, derleme ve test kayıtları gerçek exit code ile bulunur; kırmızı test bulgu sayılır.
2. Her ajanın ham raporu `agents/` altındadır; ajanın kontrol ettiğini söylediği kalem sayısı başlangıç sayımıyla karşılaştırılmış, sapma açıklanmış veya ajan yeniden koşturulmuştur.
3. `qa-bulgu-dogrulayici` çıktısı aynı commit üzerindedir; her bulgu DOĞRULANDI, KISMEN, ÇÜRÜTÜLDÜ veya DOĞRULANAMADI olarak kanıtıyla işaretlidir; Kritik ve Yüksek bulgular ana oturumca ayrıca kodda yeniden açılmıştır.
4. `README.md` ham, tekil, doğrulanan, çürütülen ve doğrulanamayan sayıları, ajan başına çürütme oranını, temiz alanları, doğrulanamayanları, Fırsat tablosunu ve her bulgunun bağlandığı düzeltme görevini içerir.
5. Semih senaryosu: Müşteri ekranı eşleştirilir; ürün eklenince ekranda anında görünür, eşleştirme iptal edilince ekran veri göstermez.
6. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-600
