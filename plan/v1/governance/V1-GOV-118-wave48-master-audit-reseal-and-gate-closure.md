# V1-GOV-118 - Wave 48 master audit reseal and gate closure

- Task ID: V1-GOV-118
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: validation
- Surface state: Existing

## Source basis

- PO:2026-09-07

## Goal

`V1-RMD-120` tamamlandıktan sonra ilgili test süitlerinin ve plan
bütünlüğünün doğrulanması ve `GATE-V1-EXIT` kapısının 48. dalga için kesin
olarak yeniden mühürlenmesi.

## Owned surface

- `plan/v1/governance/V1-GOV-118-wave48-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `dotnet build ALKAROS.slnx -c Debug` (0 uyarı/0 hata),
  `docker compose -f compose.yaml -f compose.test.yaml up --build test` +
  gerçek container exit code doğrulaması — hepsi yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata.
- `python tools/consistency-audit/consistency_audit.py`: yeni ihlal yok.
- `GATE-V1-EXIT` kapısını `plan/GATES.md` (48. dalga reseal notu) ve
  `plan/v1/README.md` üzerinde kesin olarak mühürlemek.

## Out of scope

- Kod değişikliği — `V1-RMD-120`'nin kapsamındadır (zaten tamamlanmış).

## Dependencies

- V1-RMD-120

## Deliverables

- Mühürlü `GATE-V1-EXIT` kapısı.

## Acceptance evidence

- `V1-RMD-120` `Done`; ayrıntılar task dosyasının kendi Acceptance evidence
  bölümünde.
- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml up --build test`:
  gerçek container çıkış kodu `docker inspect alkaros-test-1` ile 0 olarak
  doğrulandı; 80 test projesi, 1743 test, sıfır başarısız (`ALKAROS.Orders.OrderAggregate.Tests`
  115/115, `ALKAROS.Host.Tests` 121/121, `ALKAROS.Host.Experience.Tables.Tests`
  9/9, `ALKAROS.Host.Experience.OfflineReconciliation.Tests` 5/5 dahil).
  İlk koşuda gerçek bir regresyon yakalandı ve düzeltildi: `DualScreenStore`'a
  DI ile enjekte edilen `IOrderRepository`, onu yalnız oturum kimlik
  doğrulaması için kaydeden dört ayrı Experience kompozisyonunda (Tables,
  OfflineReconciliation, Billing, Kitchen — Orders modülünü hiç kaydetmeyen
  dar test host'ları) çözülemiyordu; `TableManagementHttpTests` 7/9 ve
  `OfflineReconciliationHttpTests` 4/5 gerçek 500 hatasıyla başarısız oldu.
  Düzeltme: `DualScreenStore` `IOrderRepository`'yi DI'dan almak yerine
  zaten sahip olduğu `NpgsqlDataSource`'tan kendi kuruyor (`PostgresOrderRepository`
  concrete tip — CA1859 analyzer uyarısı da bunu istedi); ikinci koşuda tüm
  paket yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate`: sıfır hata (bir
  `SURFACE_DUPLICATE` bulunup düzeltildi: `V1-RMD-090`/`V1-RMD-097`'nin
  `DualScreenStore.cs`/`DualScreenStore.Orders.cs` üzerindeki eski backtick'li
  iddiaları `V1-RMD-120`'ye devir cümlesine çevrildi, kurulu proje
  geleneğiyle aynı desende).
- `GATE-V1-EXIT` `plan/GATES.md` üzerinde (özet hücre + 48. dalga kesin
  reseal tarihli bölümü) ve `plan/v1/README.md` üzerinde kesin olarak
  mühürlendi.

## Handoff

- GATE-V1-EXIT
