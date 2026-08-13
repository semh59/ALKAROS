# V0-GOV-058 - Replace fixed TaskScope route totals with structural parity

- Task ID: V0-GOV-058
- Status: Done
- Assignee: /root/implement_v0_gov_058
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C61

## Goal

TaskScope repository parity kontrolünü, geçmişte sabitlenen toplamı güncellemek
yerine, immutable 42-orijin bulgu seti ile canlı ve kanonik post-closure
CSV/JSON/catalog kayıtlarını fail-closed yapısal invariant olarak doğrulayacak
hale getirmek.

## Owned surface

- `evidence/V0-GOV-058/**`

CORR:C65, `tests/Architecture/TaskScope/test_task_scope.py` yüzeyi ileri
custody ile `V0-GOV-062`ye devredilmiştir; bu historical task'ın status,
assignee, acceptance evidence, commit tarihçesi ve mevcut diff'leri aynen
kalır (yalnız Owned surface daraltma kaydı).

## In scope

- Testte aşağıdaki immutable orijin setini ve `42` count'unu explicit sabit
  olarak tanımlamak: `GOV-001`, `GOV-002`, `GOV-006`, `CODE-001`, `CODE-002`,
  `CODE-004`, `CODE-006`, `CODE-013`, `CODE-016`, `GOV-003`, `GOV-004`,
  `GOV-007`, `GOV-008`, `GOV-010`, `GOV-012`, `VER-CI-001`, `VER-GOV-002`,
  `VER-GOV-003`, `CODE-003`, `CODE-005`, `CODE-007`, `CODE-008`, `CODE-009`,
  `CODE-010`, `CODE-011`, `CODE-012`, `CODE-014`, `CODE-015`, `CODE-018`,
  `CODE-019`, `GOV-005`, `GOV-009`, `GOV-011`, `GOV-013`, `GOV-014`,
  `GOV-015`, `VER-BUILD-004`, `VER-COV-007`, `VER-FMT-005`, `VER-PROV-006`,
  `CODE-017`, `VER-ACC-008`.
- Canlı `AUDIT_REMEDIATION_ROUTING.csv` ve JSON'u parse etmek; CSV ve JSON'da
  her finding ID'nin tekil olmasını, aynı kanonik ID sırasını, alan parity'sini
  ve JSON `audit_register` count'larını doğrulamak.
- Orijin satırlarının tam olarak immutable 42-ID seti olduğunu; post-closure
  satırlarının yalnız `POST-CL-###` biçiminde olduğunu ve toplamın
  `42 + canlı post-closure row count` olduğunu doğrulamak. Doğru kayıtlı
  gelecekteki post-closure satırı bu invariant için test değişikliği istemez.
- C59/C60 kanonik zorunlu satırlarını exact doğrulamak: `POST-CL-010` yalnız
  `V0-GOV-056`, `CORR:C59`, `V0-GOV-050;V0-GOV-054` ve C59 closure evidence;
  `POST-CL-011` yalnız `V0-GOV-057`, `CORR:C60`, `V0-GOV-055` ve C60 closure
  evidence taşır. Her satırın CSV/JSON alanları eş olmalıdır.
- `V0-GOV-056` ve `V0-GOV-057` catalog kayıtlarının exact task path,
  dependency ve closure-evidence kaynaklarını; `V0-GOV-058` kaydının da kendi
  path'i ve `V0-GOV-056;V0-GOV-057` dependency'leriyle eşleştiğini doğrulamak.
- Missing, malformed, non-canonical, extra veya duplicate post-closure row;
  missing/extra/duplicate orijin ID; CSV/JSON row, count, alan veya catalog
  uyuşmazlığı; C59/C60 satırı, owner, source, dependency veya closure evidence
  farkı; ve `V1-FND-023` source marker missing/extra/out-of-order durumlarını
  deterministic negatif regression ile reddetmek.

## Out of scope

- TaskScope veya PlanAudit aracını, plan/GATES/contract dokümanlarını, routing
  artifact'lerini, admission tuple'ını veya `V1-FND-023` product/test-discovery
  davranışını değiştirmek.
- `V0-GOV-056` historical closure'ını yeniden açmak ya da onun status,
  assignee, acceptance/evidence veya commit tarihçesini değiştirmek.

## Dependencies

- V0-GOV-056
- V0-GOV-057
- V0-GOV-061

## Deliverables

- Immutable 42-ID origin baseline, dynamic post-closure total ve canonical
  CSV/JSON/catalog parity için fail-closed TaskScope regression matrisi;
  V058'e ait hashli raw acceptance kanıtı.

## Acceptance evidence

- Test, immutable 42-ID origin seti ile canlı post-closure row count'unu ayrı
  doğrular ve toplamı `42 + canlı post-closure row count` olarak hesaplar;
  doğru kayıtlı yeni post-closure rota eklenmesi sabit toplam veya test
  güncellemesi gerektirmez.
- C59/C60 required rows ve V056/V057/V058 catalog path/dependency/closure
  contracts exact geçer; malformed/missing/extra/duplicate row, CSV/JSON veya
  catalog uyuşmazlığı ve source marker order sapması deterministic non-zero
  assertion ile reddedilir.
- `py -m pytest tests/Architecture/TaskScope -q`,
  `python -B tools/plan-audit/plan_audit_tool.py validate`, pre-Done task-scope
  ve diff check exit code `0` verir; raw transcriptler yalnız
  `evidence/V0-GOV-058/**` altındadır.

## Handoff

- V0-GOV-045
- V0-GOV-048
