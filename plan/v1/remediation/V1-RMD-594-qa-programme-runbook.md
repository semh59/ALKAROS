# V1-RMD-594 - Test ve kalite programı yürütme kılavuzu

- Task ID: V1-RMD-594
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: documentation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Modül modül bağımsız test ve kalite programının yürütme kılavuzu yazılır: dilim tablosu, ajan istem şablonu, kanıt düzeni, doğrulama kuralı, düzeltme politikası ve Semih raporu şablonu. Program 595-638 numaralı görevlerden oluşur.

## Owned surface

- `plan/v1/remediation/V1-RMD-594-qa-programme-runbook.md`
- `evidence/V1-RMD-594/**`
- `docs/qa/qa-programme-runbook.md`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Kılavuz içeriği: her görevin dilim dosya listesi kuralı (modül klasörü, Host dosyaları, göçler, testler, ekranlar); ajan matrisi; her ajanın görev metninden alacağı yol listesi; kanıt klasör düzeni (`baseline/`, `runs/`, `agents/`, `verifier/`, `checks/`, `mutation/`, `closing/`); bulgu kimliği biçimi `<Görev>-B<nnn>` ve fırsat kimliği `<Görev>-F<nnn>`; denetim görevinde kırmızı testin bulgu sayılıp görevin Done olmasını engellemediği yorumu; önem yalnız düzeltme sırasını belirler, doğrulanan her bulgu düzeltilir; Kritik ve Yüksek bulgular bir sonraki denetimden önce düzeltilir.
- Sıra: pilot Audit (595), temel (596-599), para zinciri (600-610), hukuk ve güvenlik (611-615), kanallar (616-618), stok ve maliyet (619-623), istemci platformları (624-627), akışlar (628-637), kapanış (638).
- Pilot sonunda gerçek token ve süre ölçümü kılavuza işlenir.

## Out of scope

- Kod, test, göç veya yapılandırma değişikliği: doğrulanan her bulgu ayrı düzeltme görevi olur.
- Canlı yığına (8090, 5080, 5433) veri yazan her işlem, başka projenin konteynerleri, `kvkk-retention --apply`, `audit-disposal` ve gerçek dış servislere istek.
- Program görevlerinin kendisinin yürütülmesi.

## Dependencies

- V1-RMD-593

## Acceptance evidence

1. `evidence/V1-RMD-594/baseline/` altında sabitlenmiş commit, dilim dosya listesi (`scope-files.txt`), sayım tablosu ve onu üreten komutlar, derleme ve test kayıtları gerçek exit code ile bulunur; kırmızı test bulgu sayılır.
2. Her ajanın ham raporu `agents/` altındadır; ajanın kontrol ettiğini söylediği kalem sayısı başlangıç sayımıyla karşılaştırılmış, sapma açıklanmış veya ajan yeniden koşturulmuştur.
3. `qa-bulgu-dogrulayici` çıktısı aynı commit üzerindedir; her bulgu DOĞRULANDI, KISMEN, ÇÜRÜTÜLDÜ veya DOĞRULANAMADI olarak kanıtıyla işaretlidir; Kritik ve Yüksek bulgular ana oturumca ayrıca kodda yeniden açılmıştır.
4. `README.md` ham, tekil, doğrulanan, çürütülen ve doğrulanamayan sayıları, ajan başına çürütme oranını, temiz alanları, doğrulanamayanları, Fırsat tablosunu ve her bulgunun bağlandığı düzeltme görevini içerir.
5. Kılavuz belgesi mevcuttur, 595 pilot koşusunda kullanılmış ve eksikleri düzeltilmiştir.
6. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-595
