# V1-RMD-286 - Kasa yardım çağrısı bağlantısı kopunca geri döner

- Task ID: V1-RMD-286
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`Cashier.tsx` içindeki `/hubs/help-requests` bağlantısı da 5 yeniden denemeden sonra kalıcı kapanıyor, `start()` hatası yutuluyor ve yeniden bağlanınca kaçırılan çağrılar için bir telafi yok. Kasa, masa yardım çağrısını hiç görmeden çalışmaya devam eder. Bu görev bağlantıyı sürekli yeniden deneyen, geri geldiğinde açık yardım çağrılarını yeniden yükleyen ve durumu Türkçe gösteren hâle getirir.

## Owned surface

- `plan/v1/remediation/V1-RMD-286-cashier-help-hub-resilience.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx
  (yalnız yardım çağrısı bağlantısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.test.tsx

## In scope

1. İlk başlatma hatasında da sonsuz yeniden deneme.
2. Yeniden bağlanınca açık yardım çağrılarının uç noktadan yeniden okunması.
3. Türkçe bağlantı durumu göstergesi.

## Out of scope

- Yardım çağrısının sunucuda hedeflenmesi (`V1-RMD-289`).

## Dependencies

- V1-WTR-011

## Acceptance evidence

- PosTerminal vitest: bağlantı kapanınca yeniden başlatma ve yeniden bağlanınca yükleme davranışı; mevcut testler geçer.
- `plan_audit_tool.py validate` temiz.

## Handoff

- None
