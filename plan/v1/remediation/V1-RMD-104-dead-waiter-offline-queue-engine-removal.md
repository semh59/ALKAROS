# V1-RMD-104 - Remove dead WaiterOfflineQueueEngine module (H4)

- Task ID: V1-RMD-104
- Status: Done
- Assignee: claude-session-01Dhks7X2RG1fxScJpZRzZiL
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla (2026-09-05), `docs/engineering/v1-independent-audit.md`'nin
**H4 [MED]** bulgusu (`WaiterOfflineQueueEngine`'in `serverDispatcher`'ı
`bool` döndürdüğü için kalıcı bir hatayı geçici bir hatadan ayırt
edemediği, kalıcı hata kuyruğu sonsuza kadar tıkadığı) araştırılırken,
tüm `src/Clients/WaiterPwa/SessionQueue/**` modülünün (kendi test
dosyasından başka) hiçbir yerden hiç referans edilmediği doğrulandı —
gerçek üretim istemcisi (`waiter-app.js`) tamamen ayrı, kendi
`flushOfflineQueue`/`queueOrderAction` mantığını kullanıyor ve zaten
kalıcı/geçici hata ayrımını doğru yapıyor (`isClientError` → kalıcı,
`failedOrders`'a taşınır; değilse → kuyrukta kalıp tekrar dener). Semih'in
kararıyla ("ne iş yapıyor ki, gereksiz ve ölü") düzeltmek yerine tamamen
kaldırıldı.

## Owned surface

- `plan/v1/remediation/V1-RMD-104-dead-waiter-offline-queue-engine-removal.md`
- Sınırlı ek: `plan/v1/remediation/V1-RMD-002-deep-audit-remediation.md`
  (sahiplik orada kalır) — kaldırılan yolların artık diskte olmadığını
  kaydeden bir not eklendi.
- `ALKAROS.slnx` (`V1-RMD-036` sahipliğinde kalır) — kaldırılan test
  projesinin girişi silindi.

## In scope

- `src/Clients/WaiterPwa/SessionQueue/**` (4 dosya: `WaiterOfflineQueueEngine.cs`,
  `OfflineQueueModels.cs`, `OfflineQueueStore.cs`, `WaiterSessionState.cs`)
  ve `tests/Clients/WaiterPwa/SessionQueue/**` (test projesi ve tek test
  dosyası, 9 test) tamamen silindi.
- `git grep`/`dotnet build` ile hiçbir yerden (başka bir `src/`/`tests/`
  dosyasından, herhangi bir `.csproj`'dan) hâlâ referans edilmediği
  doğrulandı.

## Out of scope

- Aynı sınıftaki diğer aday (`OrderEntryEngine.BeginSubmission()`,
  `src/Clients/Cashier/OrderEntry/**` ve `src/Clients/WaiterPwa/OrderEntry/**`)
  — V1-RMD-102'nin Bulgu 4 notunda ayrıca belirtildi, bu görevde
  dokunulmadı; Semih ayrıca karar verirse ele alınır.

## Dependencies

- V1-RMD-103

## Acceptance evidence

- `git grep -rn "SessionQueue\|WaiterOfflineQueueEngine"` (obj/bin hariç)
  sıfır sonuç döner.
- `dotnet build ALKAROS.slnx -c Release`: 0 uyarı / 0 hata (kaldırılan test
  projesi olmadan).
- `python tools/consistency-audit/consistency_audit.py`: temiz.
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `validate-coverage`: sıfır hata.

## Handoff

- V1-GOV-086
