# V1-RMD-1024 - Dar yan şerit stil testi bir görevin sahipliğinde

- Task ID: V1-RMD-1024
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1023 görevinin eklediği shellStyle.test.ts dosyası görev sahipliğinde kayıtlı değildi (plan denetimi UNOWNED_PRODUCTION_FILE verir). Dosya bu görevin sahipliğine alınır; içerik değişmez.

## Owned surface

- `evidence/V1-RMD-1024/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tools/plan-audit/plan_audit_tool.py
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1023

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
