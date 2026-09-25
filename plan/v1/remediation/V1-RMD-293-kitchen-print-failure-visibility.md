# V1-RMD-293 - Mutfak yazıcı hataları ve belirsiz teslimler personele görünür

- Task ID: V1-RMD-293
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`KitchenPrintDispatchHostedService` yazıcı hatalarında yeniden dener ve belirsiz iletimi (`PrinterTransmissionUncertain`) kayda alır; ama `print-jobs` ve denetim uç noktalarının hiçbir istemcisi yok. Yazıcı erişilemezse ya da fiş belirsiz kalırsa mutfak personeli ve kasiyer bunu görmez. Mutfak ekranına başarısız/belirsiz yazdırma işleri şeridi, yeniden yazdırma ve 'fiş çıktı' onayı eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-293-kitchen-print-failure-visibility.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/16-kitchen-print-failures.spec.js

## In scope

1. Yazdırma işleri listesi, yeniden deneme ve belirsiz teslim onayı, Türkçe durum etiketleri, testler.

## Out of scope

- Fiziksel yazıcı sürücüsü ve gerçek cihaz doğrulaması (dış bağımlılık).

## Dependencies

- V1-RMD-220

## Acceptance evidence

- vitest ve Host testi: başarısız iş listelenir, yeniden dene işi kuyruğa alır; belirsiz teslim onaylanınca kapanır.
- `plan_audit_tool.py validate` temiz.

## Handoff

- None
