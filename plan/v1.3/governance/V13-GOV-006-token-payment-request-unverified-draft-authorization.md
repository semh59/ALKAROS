# V13-GOV-006 - Authorize an unverified draft of the Token payment request adapter ahead of real credentials

- Task ID: V13-GOV-006
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-18
- CORR:C98

## Goal

`V13-HUG-001` (kart ödeme request path) `V0-HUG-001`'e bağımlı, ve
`V0-HUG-001` hâlâ `Blocked` (gerçek Token client-id/client-secret, test
terminali/simülatör erişimi yok — bkz. `evidence/v0/integrations/
V0-HUG-001/**`, 2026-09-18 tarihli 3 araştırma dosyası). `plan/
TASK_STANDARD.md:57` gereği bir dependency yalnız `Done` ile kapanır;
`:97` gereği bağımlılığı `Blocked` olan bir task'a `Status: InProgress`
verilemez. Bu, `V13-HUG-001`'in kendi Owned surface'ı (`src/Modules/
Payments/Token/PaymentRequest/**`) altında normal yoldan kod yazılmasını
engelliyor.

Semih'in açık isteği ve onayıyla ("Evet, onaylıyorum — taslak kodu şimdi
yaz"): gerçek credential beklerken, sadece kamuya açık/gerçek Token
dokümantasyonuna (Postman koleksiyonu + developer portal, bkz.
`evidence/v0/integrations/V0-HUG-001/**`) dayalı, **doğrulanmamış bir
taslak adapter** yazmak — `V0-GOV-064`'ün Hugin→Token retarget kararına
benzer bir istisnai plan-change. Bu taslak `V13-HUG-001`'in kendi Owned
surface'ına YAZILMAZ (dependency kilidini bypass etmemek için); bunun
yerine bu governance task'ın kendi Owned surface'ı altında, ana
solution'a (`ALKAROS.slnx`) bağlanmamış, ayrı/standalone bir referans
projesi olarak tutulur. `V13-HUG-001` gerçekten başladığında (client-id
elde edilince) bu taslağı kendi Owned surface'ına taşıyıp gerçek cihaz
kanıtıyla tamamlar — bu görev o taşımayı yapmaz, sadece taslağı üretir.

## Owned surface

- `plan/v1.3/governance/V13-GOV-006-token-payment-request-unverified-draft-authorization.md`
- `evidence/V13-GOV-006/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): `plan/v1.3/hugin-t300/V13-HUG-001-payment-request-path.md`
  (V13-HUG-001 sahipliğinde kalır) — yalnız Goal'a bu taslağın var
  olduğuna dair bir referans notu eklenir; Status/Dependencies/Owned
  surface değişmez (hâlâ `Planned`, hâlâ `V0-HUG-001`'e bağımlı).

## In scope

- `evidence/V13-GOV-006/token-adapter-draft/` altında, `ALKAROS.slnx`'e
  EKLENMEYEN, bağımsız derlenip test edilebilen bir .NET class library +
  xUnit test projesi: Token'ın gerçek (Postman koleksiyonundan doğrulanmış)
  Auth/Add Instant Basket/Get Basket Details şemasına karşı bir
  `TokenBasketClient` ve `TokenTenderHandler` (gerçek `ITenderHandler`
  imzasını ada uydurarak taklit eder, ama gerçek assembly'ye
  referans vermez — bağımsız kalması gerekiyor).
- Testler yalnızca **şema uyumluluğu** kanıtlıyor (sahte `HttpMessageHandler`
  ile Postman koleksiyonundaki gerçek örnek response'lara karşı) —
  **gerçek cihaz/sandbox kanıtı DEĞİL**. Bu ayrım README'de açıkça
  yazılır.

## Out of scope

- `V13-HUG-001`'in kendi Owned surface'ına (`src/Modules/Payments/Token/
  PaymentRequest/**`) yazmak, Status değiştirmek, veya bu taslağı gerçek
  solution'a bağlamak.
- Gerçek cihaz/sandbox testi — imkansız, credential yok.
- Refund/cancel (`V13-HUG-003`) veya unknown reconciliation
  (`V13-HUG-002`) kapsamı — yalnız `V13-HUG-001`'in kendi In scope'u
  (request mapping, Approved/Declined normalizasyonu) taklit ediliyor.

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-18. Karar akışı: (1)
Semih "sen kodlamaya başla ... ben client id bulurum o zaman" dedi, (2)
`plan/TASK_STANDARD.md:57`/`:97`'nin bunu `V13-HUG-001` altında normal
yoldan engellediği açıkça belirtildi, (3) `V0-GOV-064` emsaliyle resmi bir
plan-change ile, dependency kilidini bypass etmeden (taslağı `V13-HUG-001`
dışında bir yere yazarak) ilerleme önerildi, (4) "Evet, onaylıyorum —
taslak kodu şimdi yaz" ile onaylandı.

## Deliverables

- `evidence/V13-GOV-006/token-adapter-draft/` — standalone class library +
  test projesi, gerçek Postman şemasına karşı derlenip test edilmiş.
- `evidence/V13-GOV-006/README.md` — taslağın kapsamı, doğrulanan/
  doğrulanmayan kısımlar, `V13-HUG-001` gerçekten başladığında yapılması
  gereken taşıma adımları.

## Acceptance evidence

- `dotnet build` ve `dotnet test` bu standalone proje için 0 hata ile
  geçiyor (gerçek Postman örnek response'larına karşı schema-conformance
  testleri).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata (yeni
  proje `ALKAROS.slnx`'e eklenmediği ve `src/`/`tests/`/`database/`
  altında olmadığı için production-surface/ownership taramasına hiç
  girmiyor — bu bilinçli bir tasarım tercihi, kaçırma değil).
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V13-HUG-001
