# V1-RMD-597 - Identity modülünün bağımsız test ve kalite denetimi

- Task ID: V1-RMD-597
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Identity modülü ile ona bağlı Host uçları, göçler, testler ve ekranlar bağımsız denetçi ajanlarla eksiksiz denetlenir; her bulgu karşı denetçiyle doğrulanır ve doğrulanan bulgular ayrı düzeltme görevlerine bağlanır. Sistemin çalışanın işini azalttığı, hızlı olduğu ve öğrenme eşiğinin düşük olduğu da ölçülür. Bu görev kod değiştirmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-597-identity-quality-audit.md`
- `evidence/V1-RMD-597/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Denetim dilimi (yalnız okunur): `src/Modules/Identity/**`, `src/Host/Experience/{Authorization,Roles,OfflineReconciliation}/**`, `src/Host/ManagementSessionLookup.cs`, oturum rotaları (`DualScreenApplication.Endpoints.cs` içindeki 6 oturum rotası), göçler 005, 008, 009, 013, 042-052, 054, 074, 094, 099, 107 ve izin tohumu göçleri 055, 079, 089-091, 093, 109-112, 129, 132-135, 142, 163, 181, 184, `tests/Modules/Identity/**`, ilgili Host testleri, Garson ve yönetim ekranları.
- Özel dikkat: İzin kodlarının dört kaynağı (katalog, ek kod sınıfı, Program.cs yönetici izinleri, uç sabitleri), 41 göç tohumu ve istemci rota-izin haritası arasındaki tutarlılık; kendi talebini onaylama koruması; PIN kilidi ve oturum ömrü; çevrimdışı yetki bütçesi.
- Tam koşacak ajanlar: `qa-backend`, `qa-api-uclar`, `qa-veritabani`, `qa-sinir-mimari`, `qa-frontend`, `qa-ux-tasarim`, `qa-guvenlik-yetki`, `qa-test-kalitesi`, `qa-performans-dayaniklilik`, `qa-dokuman-bagimlilik`. Dar kapsamlı (yalnız ilgili yüzey) koşacak ajanlar: `qa-operasyon-gozlemlenebilirlik`, `qa-mevzuat-uyum`, `qa-entegrasyon-cihaz`. Ardından `qa-bulgu-dogrulayici` her bulguyu doğrular.
- İzinli komutlar: ana oturum başlangıçta çözümü bir kez derler ve dilimin test projelerini `ALKAROS_TEST_PG_PORT=55432` ile bir kez koşar; ajanlar bu kayıtları okur. Yalnız `qa-test-kalitesi` testi yeniden koşabilir; mutasyon yalnız repo dışındaki `git worktree` kopyasında ve aynı anda tek koşu olarak yapılır.
- Önceki denetimlerde kapatılan bu dilimdeki bulguların gerilemediğinin kodla kontrolü.

## Out of scope

- Kod, test, göç veya yapılandırma değişikliği: doğrulanan her bulgu ayrı düzeltme görevi olur.
- Canlı yığına (8090, 5080, 5433) veri yazan her işlem, başka projenin konteynerleri, `kvkk-retention --apply`, `audit-disposal` ve gerçek dış servislere istek.

## Dependencies

- V1-RMD-596

## Acceptance evidence

1. `evidence/V1-RMD-597/baseline/` altında sabitlenmiş commit, dilim dosya listesi (`scope-files.txt`), sayım tablosu ve onu üreten komutlar, derleme ve test kayıtları gerçek exit code ile bulunur; kırmızı test bulgu sayılır.
2. Her ajanın ham raporu `agents/` altındadır; ajanın kontrol ettiğini söylediği kalem sayısı başlangıç sayımıyla karşılaştırılmış, sapma açıklanmış veya ajan yeniden koşturulmuştur.
3. `qa-bulgu-dogrulayici` çıktısı aynı commit üzerindedir; her bulgu DOĞRULANDI, KISMEN, ÇÜRÜTÜLDÜ veya DOĞRULANAMADI olarak kanıtıyla işaretlidir; Kritik ve Yüksek bulgular ana oturumca ayrıca kodda yeniden açılmıştır.
4. `README.md` ham, tekil, doğrulanan, çürütülen ve doğrulanamayan sayıları, ajan başına çürütme oranını, temiz alanları, doğrulanamayanları, Fırsat tablosunu ve her bulgunun bağlandığı düzeltme görevini içerir.
5. Semih senaryosu: Garson indirim ister, yönetici onaylar; aynı kişi kendi talebini onaylayamaz; 5 yanlış PIN hesabı kilitler, yönetici kilidi açar.
6. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-667
