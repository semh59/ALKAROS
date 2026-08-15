# Version Gates

## Global kurallar

- `DECISION_REVALIDATION.md` içindeki her kayıt `Blocked` sayılır; eski artifact
  veya task başlığındaki `Done` bu merkezi invalidation kaydını geçersiz kılamaz.
- `V0-GOV-001` ve kullanıcı tarafından 2026-08-02'de onaylanan `V0-GOV-002`,
  product behavior üretmeyen task-scope enforcement remediation görevleridir;
  `GATE-V0-EXIT` öncesinde yalnız bu amaçla başlatılabilir.
- 2026-08-02 kullanıcı onaylı aşağıdaki makinece ayrıştırılan exception
  tablosundaki yalnız exact Task ID'ler, kanıtlanmış bulguyu düzeltmek için
  `GATE-V0-EXIT` açıkken başlatılabilir. Bu istisna V0/V1 gate kapanış kanıtı
  değildir ve yeni product behavior üretemez.
- `v0` kapanmadan production uygulama geliştirmesi başlamaz.
- Mevcut Git geçmişi ve application ağacı yalnız candidate evidence'dır; V0 altında
  `Blocked` görev varken `implementation` veya `integration` türündeki V1+ görevi
  `InProgress` yapmak `APPLICATION_STARTED_BEFORE_V0_EXIT` ile reddedilir.
- Bir sürümün açık finansal, stok veya mevzuat kararı sonraki sürüme borç olarak
  taşınmaz.
- Dış entegrasyon sözleşmesi gerçek erişim olmadan tamamlanmış sayılmaz.
- Her migration boş PostgreSQL 18 veritabanında ileri ve geri doğrulanır.
- Her cached projection için source-of-truth, atomik güncelleme ve rebuild yolu
  belgelenmeden ilgili modül tamamlanmaz.

## Sürüm zinciri

`V0 -> V1 -> V11 -> V12 -> V13 -> V14 -> V15 -> V20`

## Sabit gate kimlikleri

| Gate | Kapanma koşulu |
| --- | --- |
| `GATE-V0-ENTRY` | PDF hash, başlangıç envanteri ve kaynak kayıtları doğrulanır. |
| `GATE-V0-EXIT` | Tüm V0 karar, güvenlik, recovery ve dış-sözleşme görevleri gerçek kanıtla `Done` veya tarihli/onaylı `NotApplicable` olur; açık `Blocked` görev kalmaz. 2026-08-03 kullanıcı onaylı devir listesindeki (aşağıda) 11 görev ve 2026-08-13 kullanıcı onaylı `V0-REV-001..030`, `V0-GOV-041`, `V0-GOV-042` bu kapanma koşulundan muaftır; kanıt koşuluyla ilgili aşamada kapanır. **2026-08-04 kullanıcı onayıyla kapatıldı (`TRACEABILITY.md` C41).** |
| `GATE-V1-ENTRY` | `GATE-V0-EXIT` kapanır. |
| `GATE-V1-EXIT` | V1 görevleri, task-scope enforcement ve otomatik kanıtları tamamlanır. |
| `GATE-V11-ENTRY` | `GATE-V1-EXIT` kapanır. |
| `GATE-V11-EXIT` | V1.1 görevleri ve stok/reçete invariant kanıtları tamamlanır. |
| `GATE-V12-ENTRY` | `GATE-V11-EXIT` kapanır. |
| `GATE-V12-MEAL-CARD-ADAPTERS` | V0-MCD approved provider listesi ve her provider için generated adapter Done olur; liste boşsa downstream task'lar tarihli NotApplicable olur. |
| `GATE-V12-FSC-STRATEGY` | V0-CMP-001 strategy kararı ve yalnız seçilen Hugin veya QNB contract kanıtı Done olur; uygulanmayan branch tarihli NotApplicable olur. |
| `GATE-V12-EXIT` | V1.2 ödeme, fiscal ve cash görevlerinin uygulanabilir kapsamı tamamlanır. |
| `GATE-V13-ENTRY` | `GATE-V12-EXIT` kapanır. |
| `GATE-V13-EXIT` | V1.3 hesap ve invoicing görevlerinin uygulanabilir kapsamı tamamlanır. |
| `GATE-V14-ENTRY` | `GATE-V13-EXIT` kapanır. |
| `GATE-V14-EXIT` | V1.4 public channel, stock race ve reconciliation kanıtları tamamlanır. |
| `GATE-V15-ENTRY` | `GATE-V14-EXIT` kapanır. |
| `GATE-V15-EXIT` | V1.5 hardening, recovery ve runbook doğrulamaları tamamlanır. |
| `GATE-V20-ENTRY` | `GATE-V15-EXIT` kapanır. |
| `GATE-V20-EXIT` | `V20-REL-003` signed Approve; `V20-REL-004` ve `V20-REL-005` kanıtla `Done` olur. |

Bir gate, uygulanabilir görevlerde açık `Blocked`, kanıtsız `Done`, onaysız
`NotApplicable`, açık critical/high finding veya çözümlenmemiş
finansal/stok/mevzuat kararı varken kapanamaz.

- `Done` statüsündeki her görev, doğrudan ve transitive task dependency zincirinde
  yalnız `Done` statusu taşımalıdır. Bu koşul `plan_audit_tool.py validate`
  tarafından fail-closed doğrulanmadan hiçbir version gate kapanamaz.

Bir consumer, dependency'si `NotApplicable` olduğunda yalnız kendi acceptance
sözleşmesi bu sonucu açıkça ele alıyorsa başlayabilir; aksi durumda gate açık kalır.

## 2026-08-10/2026-08-11 C52/C53/C54 remediation admission

Bu tablo yalnız `CORR:C52` kaynaklı 18 görevi ve C53/C54 ile onaylanan
`V1-FND-023` kaydını kabul eder. Önceki remediation onayları tarihsel kayıttır;
mevcut `Done` görevleri bu tabloya girmez, yeniden açılamaz ve
`--candidate-remediation` ile kabul edilmez. Bu izin yalnız `V0-GOV-037`
tarafından devredilen exact yüzeydeki kanıtlanmış bulguyu düzeltmek için
uygulanır; gate kapanış kanıtı veya yeni ürün davranışı izni değildir.

<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:START -->
| Task ID | Approval date | Source basis | Purpose | Gate closure evidence | New feature behavior |
| --- | --- | --- | --- | --- | --- |
| `V1-FND-016` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-017` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-018` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-019` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-020` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-021` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-022` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-023` | `2026-08-11` | `CORR:C52;CORR:C53;CORR:C54` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-006` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-007` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-008` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-009` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-010` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-011` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-012` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-013` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-SEC-004` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-SEC-005` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-CAT-003` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:END -->

`GATE-V1-ENTRY` sonrasında yalnız `V1-FND-001` başlatılır. Ardından sırasıyla
`V1-FND-010`, `V1-FND-003`, `V1-FND-004`, `V1-FND-005`, `V1-SEC-001`, `V1-SEC-002`,
`V1-FND-002` ve `V1-FND-006` tamamlanır. Bu sekiz görev gerçek kanıtla `Done`
olmadan başka hiçbir application görevi `InProgress` yapılamaz.

2026-08-01 tarihli kullanıcı onaylı plan değişikliğiyle `V1-FND-007`
(audit remediation) bu zincirden önce başlatılır; kayıt `TRACEABILITY.md`
FIND-IA-0027 satırındadır. Zincir kuralı diğer application görevleri için
aynen geçerliliğini korur.

2026-08-01 tarihli ikinci kullanıcı onaylı plan değişikliğiyle `V1-FND-008`
(boundary audit round 2) de aynı istisna kapsamında zincirden önce
başlatılır; kayıt `TRACEABILITY.md` FIND-IA-0037 satırındadır. Zincir kuralı
diğer application görevleri için aynen geçerliliğini korur.

2026-08-01 tarihli üçüncü kullanıcı onaylı plan değişikliğiyle ("DÜZELT")
`V1-FND-009` (pushed history rewrite + force-push) de aynı istisna kapsamında
zincirden önce başlatılır; kayıt `TRACEABILITY.md` FIND-IA-0050 satırındadır.
Zincir kuralı diğer application görevleri için aynen geçerliliğini korur.

2026-08-05 tarihli kullanıcı onaylı plan değişikliği (`TRACEABILITY.md` C45):
`V1-FND-009` kapsamı push edilmiş geçmişin tamamına (125 commit) genişletildi.
19 commit kanıtlı `Task:`/`Gate:` footer'ı alır; sahiplik kanıtlanamayan
11 commit (`1991abb`, `ed3d97d`, `f87c7dc`, `849bcaa`, `d6f7438`, `4374a3b`,
`f87468b`, `17c9080`, `792fa2c`, `630e324`, `6f5278c`) ve `fc5ae22` kök
baseline kurgusal atıf almaz (kayıtlı istisna). Force-push güncel
`origin/master` HEAD'i üzerine uygulanır; zincir kuralı diğer application
görevleri için aynen geçerliliğini korur.

2026-08-04 tarihli kullanıcı onaylı bağımsız-denetim remediasyon planı
(`TRACEABILITY.md` C42): `V1-IAM-005` (login timing sözleşmesi + kararlı
test), `V1-FND-013` (host DI constructability), `V1-FND-014` (retry SQL
identifier), `V1-FND-015` (inbox idempotency sözleşmesi) ve `V0-GOV-030`
(gate evidence sayım refresh) yalnız kanıtlanmış bulguyu (FIND-IA-0056..0061)
düzeltir, gate kapanış kanıtı üretmez ve yeni product behavior başlatmaz;
Aşama 3 kabul zinciri sırası değişmez.

2026-08-04 tarihli kullanıcı onaylı plan değişikliği (`TRACEABILITY.md` C43):
`V0-GOV-031` (C42 remediasyon entry-gate onayı) task-scope aracının onay
setini ve `TASK_SCOPE_REMEDIATION_EXCEPTIONS` tablosunu `V1-IAM-005`,
`V1-FND-013`, `V1-FND-014`, `V1-FND-015` kimlikleriyle genişletir; istisna
yalnız kanıtlanmış bulgu remediasyonu içindir, gate kapanış kanıtı üretmez ve
yeni product behavior başlatmaz; Aşama 3 kabul zinciri sırası değişmez.

2026-08-04 tarihli kullanıcı onaylı plan değişikliği (`TRACEABILITY.md` C44):
`V0-GOV-032` (devirli entry-gate tanıma) task-scope aracının `GATE-V0-EXIT`
türetilmiş entry-gate kontrolünü, aşağıdaki `V0_DEFERRED_TASKS` tablosundaki
kullanıcı onaylı 11 devir kimliğini kapanma koşulundan muaf sayacak şekilde
genişletir; böylece 2026-08-04 kullanıcı onaylı kapanış (C41) makinece
doğrulanabilir ve V1 görevleri remediasyon istisnası olmadan başlayabilir.
İstisna yalnız gate türetimidir, devir kanıtı üretmez ve yeni product
behavior başlatmaz; Aşama 3 kabul zinciri sırası değişmez.

## 2026-08-03 user-approved V0 deferrals

<!-- V0_DEFERRED_TASKS:START -->
| Task ID | Approval date | Reopen stage | Required evidence | Gate closure evidence |
| --- | --- | --- | --- | --- |
| `V0-HUG-001` | `2026-08-03` | `V12` | Gerçek Hugin provider contract/erişim kanıtı | Not V0 gate closure evidence |
| `V0-QNB-001` | `2026-08-03` | `V13` | Gerçek QNB provider contract/erişim kanıtı | Not V0 gate closure evidence |
| `V0-YSP-001` | `2026-08-03` | `V12` | Gerçek Yapı Kredi provider contract/erişim kanıtı | Not V0 gate closure evidence |
| `V0-MCD-001` | `2026-08-03` | `V12` | Gerçek meal-card provider sözleşme/onay kanıtı | Not V0 gate closure evidence |
| `V0-PRN-001` | `2026-08-03` | `V14` | Gerçek yazıcı/cihaz sözleşmesi veya onay kanıtı | Not V0 gate closure evidence |
| `V0-QRG-001` | `2026-08-03` | `V14` | Gerçek QR relay public kanal onay kanıtı | Not V0 gate closure evidence |
| `V0-CMP-001` | `2026-08-03` | `V12` | Mali müşavir onaylı FSC/T300-QNB adisyon strateji kararı | Not V0 gate closure evidence |
| `V0-SEC-001` | `2026-08-03` | `V14` | Doğrulanmış güvenlik gereksinim kaynağı/standart kanıtı | Not V0 gate closure evidence |
| `V0-LIC-001` | `2026-08-03` | `V20` | Gerçek license server ve lisans sözleşmesi kanıtı | Not V0 gate closure evidence |
| `V0-BKP-001` | `2026-08-03` | `V15` | Gerçek PostgreSQL 18 ikinci instance/cihaz kanıtı | Not V0 gate closure evidence |
| `V0-BKP-002` | `2026-08-03` | `V15` | Gerçek yedekleme donanımı/cihaz kanıtı | Not V0 gate closure evidence |
| `V0-REV-001` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-002` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-003` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-004` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-005` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-006` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-007` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-008` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-009` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-010` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-011` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-012` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-013` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-014` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-015` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-016` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-017` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-018` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-019` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-020` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-021` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-022` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-023` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-024` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-025` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-026` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-027` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-028` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-029` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-REV-030` | `2026-08-13` | `V12` | Tarihli source packet + named approver (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-GOV-041` | `2026-08-13` | `V12` | Successful GitHub Actions candidate workflow URL/SHA + repository admin ruleset/required-check readback (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
| `V0-GOV-042` | `2026-08-13` | `V12` | Coverage threshold, project scope ve supported exporter için tarihli named technical approval (ad-soyad, kurum/rol, onay tarihi) | Not V0 gate closure evidence |
<!-- V0_DEFERRED_TASKS:END -->

Bu devir listesi 2026-08-03 kullanıcı onayıyla kayıtlıdır (`TRACEABILITY.md`
C40). Devredilen görev `Blocked` durumunda kalır; `GATE-V0-EXIT` bunların
kanıtı olmadan kapanabilir ve görev ilgili aşama gate'inde gerçek kanıtla
`Done` veya tarihli/onaylı `NotApplicable` olur. Devir yeni product behavior
başlatma izni vermez ve V0 karar/uygulama kapsamını daraltmaz.

`V0-REV-001..030` satırları 2026-08-13 kullanıcı onayıyla kayıtlıdır
(`TRACEABILITY.md` C65): revalidation görevleri tarihli source packet + named
approver kanıtı bekler ve bu harici kanıt V0 exit'ten önce var olamaz; aynı
C40 muafiyet hükümleri geçerlidir.

`V0-GOV-041`/`V0-GOV-042` satırları 2026-08-13 kullanıcı onayıyla kayıtlıdır
(`TRACEABILITY.md` C66): GitHub branch-policy/ruleset ve coverage named
technical approval kanıtı V0 exit'ten önce doğrulanamaz; aynı C40 muafiyet
hükümleri geçerlidir.

## Canlı veri kuralı

`V20-REL-003` signed Approve kararı üretmeden hiçbir sürüm gerçek müşteri veya
gerçek para ile çalıştırılmaz. Production yetkisi yalnız `V20-REL-004` task'ına
aittir. `V20-REL-002` yalnız sentetik ya da yetkili sanitize edilmiş veriyle
yapılan non-production pilot rehearsal görevidir.

## Aşama 2 süreç notu

`TRACEABILITY.md` C67 ile onaylanan Aşama 2 süreç katmanı sadeleştirmesi
(terminoloji ve şablon metni) gate koşullarını, `V0_DEFERRED_TASKS`,
`TASK_SCOPE_REMEDIATION_EXCEPTIONS` devir/remediation tablolarını ve yukarıdaki
gate kapanma koşullarını değiştirmez. Bu not PDF current authority değildir,
gate kapanış kanıtı üretmez ve yeni product behavior başlatmaz.
