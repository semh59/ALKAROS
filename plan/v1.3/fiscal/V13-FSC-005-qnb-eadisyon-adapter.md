# V13-FSC-005 - Implement selected QNB e-Adisyon adapter

- Task ID: V13-FSC-005
- Status: NotApplicable
- Assignee: Semih (product owner)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.16
- PDF:II.5.4
- EXT:GIB-YNOKC-GUIDE
- EXT:QNB-API-PUBLIC
- CORR:C25

## Goal

Yalnız `V0-CMP-001` QNB e-Adisyon lifecycle'ını seçip `V0-QNB-001` exact private/public contract'ı doğruladığında
open/update/close mapping'ini uygulamak.

## Owned surface

- `src/Modules/Fiscal/AdisyonStrategy/QnbEAdisyon/**`, `tests/Modules/Fiscal/AdisyonStrategy/QnbEAdisyon/**`
- Bu görev, QNB invoice adapter veya ortak composition surface'ini değiştiremez.

## In scope

- Yalnız doğrulanmış endpoint/schema mapping, document correlation, retry/idempotency, sanitized evidence ve typed
  provider failure.

## Out of scope

- Kamuya açık kaynakta bulunmayan capability uydurmak, applicability kararı, T300 adapter ve invoice issuance.

## Dependencies

- GATE-V13-FSC-STRATEGY
- V13-FSC-001
- V0-QNB-001

## Onay

NotApplicable — 2026-09-18, Semih (Founder/Product Owner) onaylı ön karar.
Gerekçe: (1) QNB'nin kamuya açık dokümantasyonu ("e-Adisyon" ürün
sayfası, `evidence/v0/integrations/V0-QNB-001/qnb-esolutions-code-library-raw.txt`)
"adisyon" kelimesini hiç içermiyor — teknik olarak doğrulanabilir hiçbir
sözleşme yüzeyi yok. (2) `V13-FSC-004`'ün kendi Goal metni Token/Beko'yu
zaten "gerçek bir YN ÖKC/e-Adisyon cihazı" olarak tanımlıyor — donanım
zaten bu yeteneğe sahip, ayrı bir QNB entegrasyonuna ihtiyaç yaratmıyor.
(3) Semih, iki alternatifi (Token/Beko dalı vs QNB dalı) değerlendirip
Token/Beko dalını (`V13-FSC-004`) tercih etti.

**Bu, `GATE-V13-FSC-STRATEGY`'nin resmi olarak beklediği `V0-CMP-001`
(mali müşavir onaylı fiscal strateji) kararının YERİNE GEÇMEZ** — o karar
hâlâ `Blocked` ve ayrı bir profesyonel onay gerektiriyor. Bu, Semih'in
ürün/mühendislik tarafından yaptığı bir ÖN teknik tercih; `V0-CMP-001`
nihai sign-off'unda QNB dalı gerekli görülürse bu karar yeniden açılır.
`V0-GOV-064`'ün Hugin→Token retarget kararıyla aynı desende: bağımsız
araştırmaya dayalı, açıkça onaylı, gerekçeli ve revize edilebilir.

## Deliverables

- QNB e-Adisyon adapter production code'u ve exact approved contract/sandbox transcript'e bağlı automated tests.

## Acceptance evidence

- QNB branch seçildiyse her command exact approved endpoint/schema ve gerçek sandbox transcript'iyle kanıtlanır; public
  veya private kaynakta bulunmayan davranış uygulanmaz.
- QNB seçilmediyse veya applicability `NotApplicable` ise görev aynı tarihli/onaylı kararla `NotApplicable` olur;
  adapter/stub oluşturulmaz.

## Handoff

- V13-FSC-003
- V20-CMP-001
- V20-INT-002
