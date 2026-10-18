# V1-RMD-638 - Test ve kalite programı kapanış kaydı

- Task ID: V1-RMD-638
- Status: Planned
- Assignee: Unassigned
- Work type: documentation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Programın kapsamı kanıtlanır: tüm denetim görevlerinin dilim dosya listelerinin birleşimi depodaki izli kaynak, göç, test ve dağıtım dosyalarıyla karşılaştırılır; açık düzeltme görevleri ve Semih kararları tek kayıt belgesinde toplanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-638-qa-programme-closing-register.md`
- `evidence/V1-RMD-638/**`
- `docs/qa/qa-programme-2026-10-register.md`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Tüm `scope-files.txt` dosyalarının birleşimi, `git ls-files src database tests deploy compose*.yaml` listesine (kilit ve üretilmiş dosyalar hariç) eşit olmalıdır; fark açıklanır veya ilgili denetim yeniden açılır.
- Kayıt belgesi: modül başına bulgu sayıları, açık düzeltme görevleri, çürütülen iddialar ve çürütme oranları, doğrulanamayanlar, Semih karar listesi, toplam token ve süre.

## Out of scope

- Kod, test, göç veya yapılandırma değişikliği: doğrulanan her bulgu ayrı düzeltme görevi olur.
- Canlı yığına (8090, 5080, 5433) veri yazan her işlem, başka projenin konteynerleri, `kvkk-retention --apply`, `audit-disposal` ve gerçek dış servislere istek.

## Dependencies

- V1-RMD-637

## Acceptance evidence

1. `evidence/V1-RMD-638/baseline/` altında sabitlenmiş commit, dilim dosya listesi (`scope-files.txt`), sayım tablosu ve onu üreten komutlar, derleme ve test kayıtları gerçek exit code ile bulunur; kırmızı test bulgu sayılır.
2. Her ajanın ham raporu `agents/` altındadır; ajanın kontrol ettiğini söylediği kalem sayısı başlangıç sayımıyla karşılaştırılmış, sapma açıklanmış veya ajan yeniden koşturulmuştur.
3. `qa-bulgu-dogrulayici` çıktısı aynı commit üzerindedir; her bulgu DOĞRULANDI, KISMEN, ÇÜRÜTÜLDÜ veya DOĞRULANAMADI olarak kanıtıyla işaretlidir; Kritik ve Yüksek bulgular ana oturumca ayrıca kodda yeniden açılmıştır.
4. `README.md` ham, tekil, doğrulanan, çürütülen ve doğrulanamayan sayıları, ajan başına çürütme oranını, temiz alanları, doğrulanamayanları, Fırsat tablosunu ve her bulgunun bağlandığı düzeltme görevini içerir.
5. Kapsam birleşimi kanıtı ve kayıt belgesi mevcuttur; açık düzeltme görevleri listelenmiştir.
6. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- None
