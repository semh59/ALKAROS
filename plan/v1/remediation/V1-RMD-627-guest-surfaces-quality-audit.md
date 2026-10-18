# V1-RMD-627 - Misafir yüzleri bağımsız test ve kalite denetimi

- Task ID: V1-RMD-627
- Status: Planned
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Misafir yüzleri (modül ekranları dışındaki platform katmanı) bağımsız denetçi ajanlarla eksiksiz denetlenir; bulgular karşı denetçiyle doğrulanır. Ekranların kendisi ilgili modül görevlerinde denetlenmiş olur. Bu görev kod değiştirmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-627-guest-surfaces-quality-audit.md`
- `evidence/V1-RMD-627/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Denetim dilimi (yalnız okunur): `src/Apps/CustomerWeb/**`, NFC paketi (`NfcOrder.tsx`, `nfc-entry.tsx`, `nfc.html`, `vite.nfc.config.ts`), oturumsuz QR ve NFC uçları. Mevcut otomatik kanıt: yalnız sunucu testleri.
- Özel dikkat: XSS ve IDOR; yaş kısıtı; ekran okuyucu bildirimleri; NFC yenilemede çift sipariş.
- Tam koşacak ajanlar: `qa-frontend`, `qa-ux-tasarim`, `qa-guvenlik-yetki`, `qa-test-kalitesi`, `qa-performans-dayaniklilik`, `qa-dokuman-bagimlilik`; `qa-operasyon-gozlemlenebilirlik` ve `qa-entegrasyon-cihaz` dar kapsamlı. Yedi `ui-*` ajanı (akış, durum ve hata, eksik avcısı, erişilebilirlik, görsel tutarlılık, pazar, Türkçe metin) ana oturumun verdiği yol listesi ve çıktı şablonuyla koşar. Ardından `qa-bulgu-dogrulayici` her bulguyu doğrular.
- İzinli komutlar: ana oturum başlangıçta çözümü bir kez derler ve dilimin test projelerini `ALKAROS_TEST_PG_PORT=55432` ile bir kez koşar; ajanlar bu kayıtları okur. Yalnız `qa-test-kalitesi` testi yeniden koşabilir; mutasyon yalnız repo dışındaki `git worktree` kopyasında ve aynı anda tek koşu olarak yapılır.

## Out of scope

- Kod, test, göç veya yapılandırma değişikliği: doğrulanan her bulgu ayrı düzeltme görevi olur.
- Canlı yığına (8090, 5080, 5433) veri yazan her işlem, başka projenin konteynerleri, `kvkk-retention --apply`, `audit-disposal` ve gerçek dış servislere istek.

## Dependencies

- V1-RMD-626

## Acceptance evidence

1. `evidence/V1-RMD-627/baseline/` altında sabitlenmiş commit, dilim dosya listesi (`scope-files.txt`), sayım tablosu ve onu üreten komutlar, derleme ve test kayıtları gerçek exit code ile bulunur; kırmızı test bulgu sayılır.
2. Her ajanın ham raporu `agents/` altındadır; ajanın kontrol ettiğini söylediği kalem sayısı başlangıç sayımıyla karşılaştırılmış, sapma açıklanmış veya ajan yeniden koşturulmuştur.
3. `qa-bulgu-dogrulayici` çıktısı aynı commit üzerindedir; her bulgu DOĞRULANDI, KISMEN, ÇÜRÜTÜLDÜ veya DOĞRULANAMADI olarak kanıtıyla işaretlidir; Kritik ve Yüksek bulgular ana oturumca ayrıca kodda yeniden açılmıştır.
4. `README.md` ham, tekil, doğrulanan, çürütülen ve doğrulanamayan sayıları, ajan başına çürütme oranını, temiz alanları, doğrulanamayanları, Fırsat tablosunu ve her bulgunun bağlandığı düzeltme görevini içerir.
5. Her ana durum için ekran görüntüsü (E2E düzeneğinden) ve Türkçe sızıntı taraması çıktısı eklenir. Semih senaryosu: Telefondan QR ve NFC ile sipariş verilir, hesap görülür.
6. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-628
