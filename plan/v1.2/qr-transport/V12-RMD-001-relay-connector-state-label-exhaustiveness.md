# V12-RMD-001 - Make RelaySettings' connector-state label map TypeScript-exhaustive

- Task ID: V12-RMD-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`docs/engineering/e2e-playwright-master-test-plan.md`'nin §0 bulgu 3'ü:
`RelaySettings.tsx`'in `ConnectorStateLabels` haritası düz, tipsiz bir
`Record<string, string>` idi. Backend (`RelayConnectorState` enum'u) 4.
bir gerçek değer eklerse, bu ekran ham enum adını (`?? status.connectorState`
fallback'i üzerinden) doğrudan bir yöneticiye gösterebilirdi — bu repo'nun
`docs/UI_STYLE_GUIDE.md` §3 kuralının canlı bir ihlal riski. Aynı sınıf
sorun için bu istemcide zaten doğru çözülmüş bir emsal var:
`src/strings.ts`'nin `healthStatusLabels`/`backupStatusLabels`'ı, backend
domain modelinin kendi inline string-literal union alanını (`Model["field"]`
indexed-access) kullanarak `Record<...>`'u derleme zamanında exhaustive
kılıyor.

## Owned surface

- `plan/v1.2/qr-transport/V12-RMD-001-relay-connector-state-label-exhaustiveness.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/RelaySettings.tsx
  (V12-QRT-003 sahipliğinde) — yalnız `ConnectorStateLabels`'ın tipi ve
  render satırındaki `?? status.connectorState` fallback'i değişti; ekranın
  geri kalanı, görünen 3 Türkçe etiket dahil, aynen kaldı.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/contracts.ts
  (paylaşılan sözleşme dosyası) — yalnız `RelayCredentialStatus.connectorState`
  alanının tipi `string`'ten gerçek 3 üyeli union'a (`"NotConfigured" |
  "Running" | "Restarting"`, `ALKAROS.QrRelay.LocalConnector.RelayConnectorState`
  enum'unun gerçek üyeleriyle birebir doğrulandı) daraltıldı.

## In scope

- `RelayCredentialStatus.connectorState`'i gerçek backend enum'unun 3 üyesine
  daraltmak.
- `ConnectorStateLabels`'ı `Record<RelayCredentialStatus["connectorState"],
  string>` yapmak — `src/strings.ts`'nin zaten çalışan aynı deseni.
- Artık gereksiz olan `?? status.connectorState` fallback'ini kaldırmak
  (harita artık derleme zamanında exhaustive).

## Out of scope

- `RelaySettings.tsx`'in başka herhangi bir davranışı.
- Backend `RelayConnectorState` enum'unun kendisi (değişmedi, yalnız
  doğrulandı — 3 üye: `NotConfigured`/`Running`/`Restarting`,
  `src/Integrations/QrRelay/LocalConnector/RelayConnectorStatus.cs`).

## Dependencies

- None

## Deliverables

- `contracts.ts`: daraltılmış `connectorState` tipi.
- `RelaySettings.tsx`: exhaustive `ConnectorStateLabels`, fallback kaldırıldı.

## Acceptance evidence

- `pnpm --dir src/Clients/PosTerminal typecheck` → 0 hata (bu bir tip-seviyesi
  düzeltme olduğu için temiz typecheck doğrudan kanıt: harita artık üç
  gerçek değerin hepsini kapsamıyorsa derleme başarısız olur).
- `pnpm --dir src/Clients/PosTerminal test` → 26 dosya / 194 test, hepsi
  yeşil (mevcut `RelaySettings.test.tsx` dahil — `connectorState: "Running"`
  senaryosu hâlâ ham "Running" kelimesinin sayfada görünmediğini doğruluyor,
  bkz. test dosyasının kendi `expect(document.body.textContent).not.toContain
  ("Running")` satırı).
- Üç gerçek değerin (`NotConfigured`/`Running`/`Restarting`) etiket
  metinleri değişmedi, yalnız tip daraltıldı — elle doğrulandı (obje
  literal'i dokunulmadı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
