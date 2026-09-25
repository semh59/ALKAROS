# V13-GOV-007 - Authorize an unverified draft of the QNB e-Fatura adapter ahead of real credentials

- Task ID: V13-GOV-007
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-18
- EXT:QNB-API-PUBLIC

## Goal

`V14-QNB-001` (kayıtlı kullanıcı sorgusu) ve `V14-QNB-002` (giden fatura
gönderimi) `V0-QNB-001`'e bağımlı, ve `V0-QNB-001` hâlâ `Blocked` (gerçek
QNB test tenant credential'ı yok — bkz. `evidence/v0/integrations/
V0-QNB-001/**`, 2026-09-18 tarihli araştırma). `plan/TASK_STANDARD.md:57`
gereği bir dependency yalnız `Done` ile kapanır; `:97` gereği bağımlılığı
`Blocked` olan bir task'a `Status: InProgress` verilemez. Ayrıca
`V14-QNB-001/002` kendi Dependencies'lerinde `V14-CST-001` (müşteri PII
şeması) ve `V14-INV-002`/`V14-INV-003` (fatura üretim domain'i) da var —
bunlar da henüz hiç kod olarak yazılmamış v1.4 görevleri. Yani bu ikisi
`V13-HUG-001`'den bile daha erken bir aşamada: gerçek Owned surface'a
bağlanacak domain nesneleri henüz yok.

Semih'in açık isteği ve onayıyla (`V13-GOV-006`'nın Token için yaptığı
tam emsal): gerçek credential ve gerçek v1.4 domain'i beklerken, sadece
kamuya açık/gerçek QNB dokümantasyonuna (bkz. `evidence/v0/integrations/
V0-QNB-001/qnb-esolutions-code-library-raw.txt`, QNB'nin kendi
`api-docs-tr-final.html` sayfasından çıkarılmış 6114 satırlık gerçek
SOAP/C#/Java kod kütüphanesi) dayalı, **doğrulanmamış bir taslak SOAP
adapter'ı** yazmak. Bu taslak `V14-QNB-001/002`'nin kendi Owned surface'ına
YAZILMAZ (dependency kilidini bypass etmemek için); bunun yerine bu
governance task'ın kendi Owned surface'ı altında, ana solution'a
(`ALKAROS.slnx`) bağlanmamış, ayrı/standalone bir referans projesi olarak
tutulur. `V14-QNB-001/002` gerçekten başladığında (test tenant credential'ı

- v1.4 domain nesneleri hazır olunca) bu taslağı kendi Owned surface'ına
taşıyıp gerçek sandbox kanıtıyla tamamlar — bu görev o taşımayı yapmaz,
sadece taslağı üretir.

**Not (yön değişikliği):** Bu görev başta `V13-FSC-005` (QNB e-Adisyon)
için planlanmıştı, ama araştırma şunu gösterdi: e-Adisyon'un kamuya açık
hiçbir teknik dokümanı yok (6114 satırlık kod kütüphanesinde "adisyon"
kelimesi hiç geçmiyor), ve zaten seçilen Token/Beko cihazı `V13-FSC-004`'ün
kendi Goal'ında "gerçek bir YN ÖKC/e-Adisyon cihazı" olarak tanımlı — yani
`GATE-V13-FSC-STRATEGY`'nin Token/Beko dalını seçmesi çok daha olası,
`V13-FSC-005` muhtemelen `NotApplicable` olacak. Semih onayıyla yön,
gerçekten ihtiyaç duyulacak ve iyi dokümante olan **e-Fatura**
(`V14-QNB-001/002`) tarafına çevrildi.

## Owned surface

- `plan/v1.4/qnb-esolutions/V13-GOV-007-qnb-efatura-unverified-draft-authorization.md`
- `evidence/V13-GOV-007/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1.4/qnb-esolutions/V14-QNB-001-registered-user-query.md,
  plan/v1.4/qnb-esolutions/V14-QNB-002-outgoing-submission.md (V14-QNB-001/002
  sahipliğinde kalır) — yalnız Goal'a bu taslağın var olduğuna dair bir
  referans notu eklenir; Status/Dependencies/Owned surface değişmez (hâlâ
  `Planned`, hâlâ `V0-QNB-001`'e bağımlı).

## In scope

- `evidence/V13-GOV-007/qnb-efatura-adapter-draft/` altında, `ALKAROS.slnx`'e
  EKLENMEYEN, bağımsız derlenip test edilebilen bir .NET class library +
  xUnit test projesi: gerçek QNB dokümantasyonundan doğrulanmış SOAP zarf
  şemasına karşı bir `QnbSoapClient` (login/logout, `belgeGonderExt`,
  `gidenBelgeDurumSorgulaExt`, `kayitliKullaniciListeleExtended`) ve bugün
  çıkarılan tam durum kodu ağacını modelleyen bir
  `QnbDocumentStatusInterpreter`.
- Testler yalnızca **şema uyumluluğu** kanıtlıyor (sahte
  `HttpMessageHandler` ile QNB dokümantasyonundaki gerçek örnek SOAP
  response'larına karşı) — **gerçek test tenant kanıtı DEĞİL**. Bu ayrım
  README'de açıkça yazılır.

## Out of scope

- `V14-QNB-001/002`'nin kendi Owned surface'ına yazmak, Status
  değiştirmek, veya bu taslağı gerçek solution'a bağlamak.
- Gerçek test tenant testi — imkansız, credential yok.
- İptal/düzeltme (cancellation, `V14-QNB-005`) — kamuya açık dokümanda hiç
  bulunamadı, bu taslak hiç kapsamıyor.
- `V13-FSC-005` (e-Adisyon) — yön değişikliği sonrası bu görevin kapsamı
  dışında kaldı.

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-18. Karar akışı: (1)
Semih "Qnb için tüm kodları var araştırma yap" dedi, (2) araştırma
`api-docs-tr-final.html`'in gerçek içeriğini (JS-render tuzağı, gömülü
`CODE_LIBRARY` objesi) buldu, (3) Semih "Qnb kodlamak için detaylı ve
derin plan yap" dedi, (4) plan hazırlanırken `V13-FSC-005`'in (ilk hedef)
kamuya açık dokümanı olmadığı VE muhtemelen `NotApplicable` olacağı
bulundu, bu bulgu Semih'e sunuldu, (5) Semih "Bunun yerine e-Fatura/
e-İrsaliye (V14-QNB-*) için plan yap" diyerek yönü değiştirdi, (6) EnterPlanMode
ile sunulan detaylı plan (SOAP client tasarımı, durum kodu ağacı, test
stratejisi) onaylandı.

## Deliverables

- `evidence/V13-GOV-007/qnb-efatura-adapter-draft/` — standalone class
  library + test projesi, gerçek QNB SOAP şemasına karşı derlenip test
  edilmiş.
- `evidence/V13-GOV-007/README.md` — taslağın kapsamı, doğrulanan/
  doğrulanmayan kısımlar, `V14-QNB-001/002` gerçekten başladığında
  yapılması gereken taşıma adımları.

## Acceptance evidence

- `dotnet build` ve `dotnet test` bu standalone proje için 0 hata ile
  geçiyor (gerçek QNB örnek SOAP response'larına karşı schema-conformance
  testleri).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata (yeni
  proje `ALKAROS.slnx`'e eklenmediği ve `src/`/`tests/`/`database/`
  altında olmadığı için production-surface/ownership taramasına hiç
  girmiyor).
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V14-QNB-001
- V14-QNB-002
