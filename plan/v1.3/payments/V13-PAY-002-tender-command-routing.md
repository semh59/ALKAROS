# V13-PAY-002 - Implement tender command routing

- Task ID: V13-PAY-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.26-I.29
- PDF:II.2.6
- PDF:II.3.4-II.3.5
- PDF:II.5.3
- PDF:III.8

## Goal

Tender request/handler contract'ını tanımlamak; kayıtlı olmayan yöntemleri ve CustomerAccount yöntemini V1.3'e kadar
typed version-not-enabled sonucu ile reddetmek.

## Owned surface

- `src/Modules/Payments/TenderRouting/**`, `tests/Modules/Payments/TenderRouting/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx — yeni
  `ALKAROS.Payments.TenderRouting.Tests.csproj` girdisi eklenir (diğer test
  projeleriyle aynı desen). Yeni bir modül/DI kaydı veya migration
  gerekmedi — bu modülün kendi kalıcılığı yok (Owned surface `src/Modules/
  Payments/**` altına başka bir `.csproj` eklemedi, mevcut
  `ALKAROS.Payments.csproj`'un içinde yeni bir alt klasör/namespace).

## In scope

- Typed tender request/handler contract'ları, unknown method rejection ve V0-ARC-004 uyumlu version-not-enabled sonucu.

## Out of scope

- Handler registry composition, tender-specific logic, allocation persistence ve CustomerAccount handler implementation.

## Dependencies

- V13-PAY-001
- V0-DAT-002
- V0-ARC-004

## Deliverables

- `src/Modules/Payments/TenderRouting/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- `TenderMethod` kapalı kümesi tam olarak `Cash`/`BankCard`/`MealCard`/
  `CustomerAccount`'tan oluşuyor; `SplitPayment` kasıtlı olarak üye DEĞİL
  (V0-DOM-001'in "SplitPayment ne bir Payment durumu ne de bir Payment
  yöntemidir" invariant'ı — docs/domain/lifecycle-transition-contracts.md).
- `TenderMethodCatalog.TryParse` hiçbir zaman fırlatmıyor: "SplitPayment",
  rastgele metin, boş/null, ve sayısal ordinal string'ler ("0","99") hepsi
  `false` döndürüyor; yalnız dört kanonik ad kabul ediliyor (test:
  `TryParseRejectsSplitPaymentAndUnknownText`/`TryParseAcceptsEveryCanonicalMethodName`).
- `TenderRouter.RouteAsync`: `CustomerAccount` için — kayıt defterinde bir
  handler olsa BİLE — koşulsuz `TenderVersionNotEnabled` döner (kod:
  `TENDER_VERSION_NOT_ENABLED`, Türkçe mesaj); Cash/BankCard/MealCard için
  kayıtlı handler yoksa `TenderMethodNotRegistered` döner (kod:
  `TENDER_METHOD_NOT_REGISTERED`) — hiçbir durumda fabrikasyon bir başarı
  üretmiyor (veri değişmiyor, çünkü bu modülün kalıcılığı yok).
- Kayıtlı bir handler varsa router onu çağırıp sonucunu
  `TenderRoutingHandled` içine sarıyor (test:
  `RouteInvokesTheRegisteredHandlerAndWrapsItsResult`).
- `TenderRequest.Validate()` sıfır/negatif tutarı ve boş PaymentId'yi
  registry'ye danışmadan ÖNCE reddediyor.
- `TenderHandlerRegistry` aynı yönteme iki kayıt denemesini fail-closed
  reddediyor (startup'ta sessiz belirsizlik yaratmaz).
- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata.
- `dotnet test ALKAROS.Payments.TenderRouting.Tests` → 23/23 geçti (saf
  domain, DB gerekmiyor — bu modülün kalıcılığı yok).
- `ALKAROS.Payments.PaymentAggregate.Tests` (V13-PAY-001) → hâlâ 37/37,
  regresyon yok.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID.
- `git status --short` → yalnız Owned surface + deklare edilen tek Sınırlı
  ek satırında (ALKAROS.slnx) değişiklik var.

## Handoff

- V13-HUG-001
- V13-CSH-001
- V13-MCD-001
- V13-PAY-003
- V14-ACC-003
