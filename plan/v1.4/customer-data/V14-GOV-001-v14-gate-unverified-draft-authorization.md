# V14-GOV-001 - Authorize unverified v1.4 drafts ahead of GATE-V14-ENTRY

- Task ID: V14-GOV-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-18

## Goal

`GATE-V14-ENTRY`, `GATE-V13-EXIT`'in kapanmasını gerektiriyor, o da
v1.3'ün ödeme/fiscal/nakit zincirinin uygulanabilir, kanıtlı bir duruma
ulaşmasını gerektiriyor — şu an `V0-HUG-001`/`V0-CMP-001` yüzünden bloklu
(ikisi de hâlâ `Blocked`). `plan/TASK_STANDARD.md:57`/`:97` gereği,
`GATE-V14-ENTRY` açıkken hiçbir v1.4 görevi (`V14-CST-001`,
`V14-INV-001..004`, `V14-QNB-001..006`, `V14-PUR-001`, `V14-UI-002/003`
vb.) `Status: InProgress` alamaz.

`V13-GOV-006`/`V13-GOV-007`'nin (yalnız eksik bir dış credential yüzünden
bloklu) aksine, bu gate planın kasıtlı olarak verdiği bir SIRALAMA
kararı: v1.4'ün fatura/müşteri domain'i, v1.3'ün fiscal stratejisi
(`V0-CMP-001`) netleşmeden tasarlanırsa, değişen strateji fatura domain
varsayımlarına sıçrayabilir. Semih'e bu açıkça belirtildi ve yine de
taslak istemeyi tercih etti (Token/QNB taslaklarıyla aynı STANDALONE,
gerçek Owned surface'a değil deseni) — gate'i resmen atlamak veya beklemek
yerine.

## Owned surface

- `plan/v1.4/customer-data/V14-GOV-001-v14-gate-unverified-draft-authorization.md`
- `evidence/V14-GOV-001/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1.4/customer-data/V14-CST-001-customer-pii-schema.md
  (V14-CST-001 sahipliğinde kalır) — yalnız Goal'a bu taslağın var
  olduğuna dair bir referans notu eklenir; Status/Dependencies/Owned
  surface değişmez (hâlâ `Planned`, hâlâ `GATE-V14-ENTRY`'ye bağımlı).

## In scope

- `evidence/V14-GOV-001/customer-pii-draft/` altında, `ALKAROS.slnx`'e
  EKLENMEYEN, bağımsız derlenip test edilebilen bir .NET class library +
  xUnit test projesi: `V14-CST-001`'in kendi Goal'ında istenen minimum
  müşteri kimlik/vergi/iletişim alanlarını ve rol tabanlı alan-maskeleme
  kuralını modelleyen bir domain taslağı — hiçbir gerçek veritabanına
  veya Owned surface'a bağlanmaz.

## Out of scope

- `V14-CST-001`'in kendi Owned surface'ına yazmak, Status değiştirmek.
- Diğer v1.4 görevleri (`V14-INV-*`, `V14-QNB-001..005`, `V14-PUR-001`,
  `V14-UI-*`) — bu görev yalnız zincirin ilk halkasını taslaklıyor; geri
  kalanı ayrı, kendi task ID'leriyle ele alınır (tek-görev disiplini).
- `GATE-V14-ENTRY`'yi resmen kapatmak veya `V0-CMP-001`/`V0-HUG-001`
  kararlarını yerine geçmek.

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-18. Karar akışı: (1)
Semih "v1.4 fatura modülünü şimdi inşa edelim" dedi, (2)
`GATE-V14-ENTRY`'nin `GATE-V13-EXIT`'e (dolayısıyla `V0-HUG-001`/
`V0-CMP-001`'e) bağlı olduğu ve bunun (Token/QNB'nin aksine) bilinçli bir
sıralama kararı olduğu, atlanmasının fatura domain'inde yeniden yazım
riski taşıdığı açıkça belirtildi, (3) üç seçenek sunuldu (taslak/
standalone, gate'i bilerek atla, bekle), (4) Semih tercih belirtmedi
("[No preference]"), en düşük riskli seçenek (taslak/standalone, Token/
QNB draftlarıyla aynı desen) varsayılan olarak uygulandı.

## Deliverables

- `evidence/V14-GOV-001/customer-pii-draft/` — standalone class library +
  test projesi.
- `evidence/V14-GOV-001/README.md` — kapsam, doğrulanan/doğrulanmayan
  kısımlar, `V14-CST-001` gerçekten başladığında (gate açılınca) yapılması
  gereken taşıma adımları.

## Acceptance evidence

- `dotnet build`/`dotnet test` bu standalone proje için 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V14-CST-001
