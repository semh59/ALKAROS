# V0-GOV-064 - Override the PDF's Hugin T300 device lock with Token/Beko (C98)

- Task ID: V0-GOV-064
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: plan-change
- Surface state: Existing

## Source basis

- CORR:C98

## Goal

PDF `I.6.1`'in ("Cihaz seçimi yeniden açılmayacaktır. Hedef cihaz Hugin
T300'dür.") kilidini, Semih'in açık kararıyla, bağımsız bir pazar
araştırmasına ve `V0-HUG-001`'in kendi kayıtlı blocker'ına dayanarak
override etmek: yeni hedef fiziksel platform **Token/Beko (300 TR veya
X30 TR)**. `V0-HUG-001`/`V13-HUG-001..004`/`V20-INT-001` task ID'lerini
DEĞİŞTİRMEDEN (cross-reference kırılmasın diye) içeriklerini Token'a
retarget etmek; `V13-FSC-004`'ün hedef cihazını güncellemek; kaynak
kayıtlarını (OFFICIAL_SOURCE_REGISTER.md) yeni platformun gerçek
kaynaklarıyla genişletmek.

## Owned surface

- `plan/v0/hugin-t300/V0-HUG-001-integration-contract.md`
- `plan/v1.3/hugin-t300/V13-HUG-001-payment-request-path.md`
- `plan/v1.3/hugin-t300/V13-HUG-002-unknown-reconciliation.md`
- `plan/v1.3/hugin-t300/V13-HUG-003-refund-and-cancel.md`
- `plan/v1.3/hugin-t300/V13-HUG-004-terminal-totals-reconciliation.md`
- `plan/v1.3/fiscal/V13-FSC-004-t300-adisyon-adapter.md`
- `plan/v2.0/integration-certification/V20-INT-001-hugin-certification.md`
- `plan/OFFICIAL_SOURCE_REGISTER.md`
- `plan/GATES.md`
- `plan/TRACEABILITY.md`
- `plan/v0/README.md`
- `evidence/V0-GOV-064/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/domain/lifecycle-transition-contracts.md
  (V0-DOM-001 sahipliğinde kalır) — yalnız "per the verified Hugin
  contract" cümlesindeki ürün adı güncellenir; `V13-FSC-*`/`V13-HUG-*` task
  ID referansları DEĞİŞMEDİĞİ için başka bir düzenleme yapılmaz.

## In scope

- `V0-HUG-001`'in Goal/In scope/Blocker/Deliverables/Acceptance
  evidence/Handoff metnini Token/Beko'ya retarget etmek; Source basis'teki
  Hugin-özel `EXT:` kaynaklarını Token kaynaklarıyla değiştirmek. Task
  Status `Blocked` KALIR — bu, gerçek bir imzalı sözleşme/cihaz erişimi
  değil, pazar araştırmasıdır; `V0-HUG-001`'in kendi kabul kriteri
  (imzalı model/firmware/protokol matrisi + gerçek test cihazı transcript'i)
  hâlâ karşılanmıyor.
- `V13-HUG-001..004`'ün Goal/Owned surface (klasör adı `HuginTransport/**`
  gibi Hugin'e özel yol adları varsa Token'a uyacak şekilde) metnini
  retarget etmek; task ID ve Dependencies/Handoff referansları DEĞİŞMEZ.
- `V13-FSC-004`'ün başlığını/Goal'ını/Owned surface yolunu
  (`HuginT300/**` → `TokenBeko/**`) ve Source basis'indeki `EXT:GIB-HUGIN-T300`
  referansını güncellemek.
- `V20-INT-001`'in başlığını/Source basis'ini/Owned surface'ini
  (`release/evidence/integrations/hugin/**` → `.../token/**`) Token'a
  retarget etmek.
- `OFFICIAL_SOURCE_REGISTER.md`'ye yeni `TOKEN-DEVELOPER-PORTAL` genel
  kaynağını ve `PRIVATE-TOKEN-CONTRACT` eksik-kanıt satırını eklemek; eski
  Hugin satırlarını SİLMEMEK (tarihi kayıt — artık hangi task'ların onları
  tükettiği güncellenir).
- `GATES.md`'nin `V0-HUG-001` reopen-tablosu satırındaki açıklamayı
  ("Gerçek Hugin provider contract/erişim" → "Gerçek Token/Beko provider
  contract/erişim") güncellemek.
- `plan/v0/README.md`'nin modül listesindeki "hugin-t300" ibaresini
  değiştirmemek (klasör adı task ID'lerle birlikte kasıtlı olarak
  korunuyor — bkz. Out of scope), yalnız ilgili cümledeki ürün adını not
  düşmek.

## Out of scope

- `plan/v0/hugin-t300/`, `plan/v1.3/hugin-t300/` klasörlerini veya
  `V0-HUG-001`/`V13-HUG-001..004`/`V20-INT-001` task ID'lerini yeniden
  adlandırmak. Bunlar, GATES.md/REMAINING_WORK_PLAN.md/PDF_COVERAGE.md/
  V13-ALC-004/V13-PAY-004/V13-REC-001/V14-ACC-004..006 içindeki ONLARCA
  cross-reference'ı kırmadan yapılamaz; ID'ler sabit kalınca hiçbir
  referans bozulmaz. Klasör/ID adındaki "Hugin"/"T300" artık yalnız
  tarihi bir kimlik etiketi; içerik doğru kaynaktır.
- `plan/PDF_COVERAGE.md`'deki PDF alıntılarını (`Content` sütunu) veya
  hash'lerini değiştirmek — onlar PDF'in o an ne dediğinin değişmez
  kaydı; task ID'ler değişmediği için bu tablonun `covered_by` sütunları
  da hâlâ doğru.
- `plan/AUDIT_REPORT.md`'yi değiştirmek — geçmiş bir denetimin donmuş
  kaydı, bugünkü kararla ilgisi yok.
- `V0-HUG-001`'i `Blocked`'tan çıkarmak, gerçek bir sözleşme/cihaz
  transcript'i üretmek veya `V13-HUG-*`/`V13-FSC-004` production kodu
  yazmak.
- `V13-MCD-*` görevlerine Multinet'in mobil'e kayma riskini işlemek —
  ayrı, gelecekte açılacak bir görev/not (bu görev yalnız bunu
  TRACEABILITY.md'ye "takip gerekir" olarak kaydeder).
- Şu altı dosyadaki betimleyici "Hugin" kelimesi düzeltilmedi (bu
  görevin Owned surface'ında değiller, henüz Planned/kod yazılmamış):
  `V13-ALC-004`, `V13-PAY-004`, `V13-REC-001`, `V14-ACC-004`,
  `V14-ACC-005`, `V14-ACC-006`. Bu tamamen kozmetik bir gecikme —
  task ID'ler değişmediği için hiçbir dependency/cross-reference kırılmadı;
  her biri kendi görevi başladığında (o an okunacak `V0-HUG-001`/
  `V13-HUG-*`'ın kendi retarget notlarıyla) düzeltilecektir.

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-17. Karar akışı: (1)
Semih pazar araştırması belgesini paylaştı, (2) derin analiz istendi ve
sunuldu (PDF'in I.6.1'inin cihaz seçimini "yeniden açılmayacak" diye
kilitlediği, ve V0-HUG-001'in kendi blocker'ının bunu bağımsız olarak
doğruladığı açıkça belirtildi), (3) "PDF'in kilidini bilerek override et"
seçeneği onaylandı, (4) platform seçenekleri arasından "Token/Beko"
seçildi. Gerekçe: Token'ın `IntegrationHub.dll`'i gerçek bir C#/.NET
SDK — ALKAROS'un kendi backend'iyle teknik olarak en doğal uyum; 6 yemek
kartı operatörünü (TokenFlex native + Edenred/Multinet/Setcard/Sodexo/
Metropol) destekliyor. Hugin T300'ün ise hem PC Link desteği
belgelenmemiş (yalnız S1/`FU` modelinde var) hem de gerçek yemek kartı
kapsamı dar (yalnız Multinet doğrulanmış).

## Deliverables

- `V0-HUG-001`, `V13-HUG-001..004`, `V13-FSC-004`, `V20-INT-001` içerik
  retargeting'i (ID'ler sabit).
- `OFFICIAL_SOURCE_REGISTER.md`'de yeni Token kaynak satırları.
- `GATES.md` reopen-tablosu güncellemesi.
- `TRACEABILITY.md` C98 kaydı (bu görevle birlikte, aynı diff'te
  yazıldı).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı (task ID'ler değişmediği için mevcut 1960 dependency edge'in
  hiçbiri kırılmaz).
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `git diff` ile doğrulanabilir: `plan/PDF_COVERAGE.md` ve
  `plan/AUDIT_REPORT.md`'de HİÇBİR satır değişmedi.
- `V0-HUG-001`, `V13-HUG-001..004`, `V20-INT-001`, `V13-FSC-004`
  dosyalarının her biri artık "Hugin"/"T300 (Hugin)" yerine "Token"/"Beko
  300 TR / X30 TR" adını taşıyor; Status alanları değişmedi (`Blocked`/
  `Planned` — gerçek sözleşme/cihaz kanıtı hâlâ yok).

## Handoff

- V0-HUG-001
