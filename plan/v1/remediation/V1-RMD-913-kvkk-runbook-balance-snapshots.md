# V1-RMD-913 - KVKK saklama tablosuna günlük bakiye kayıtları eklenir

- Task ID: V1-RMD-913
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: documentation
- Surface state: Existing

## Goal

V1-RMD-896/910 açığı (koordinatör kararı: ertelenmez). Günlük bakiye kayıtları (customer_account.balance_snapshots) 730 gün sonra ve son işlemi 730 günden eski müşteriler için siliniyor; ama KVKK saklama el kitabı bu kuralı hiçbir yerde yazmıyor, bu yüzden denetçi bu veri sınıfını göremiyor. Karar: kvkk-retention-runbook.md'ye bakiye kaydı sınıfı eklenir: tablo, pencere (730 gün ve son işlemden 730 gün), eylem (silme; anonimleştirme değil, türetilmiş veri), çalıştıran iş (balance-snapshot bakım işi, 6 saatte bir) ve defterin/güncel bakiyenin dokunulmadığı. Rakamlar koddaki sabitlerle (RetentionDays, aralık) birebir doğrulanır.

## Owned surface

- `evidence/V1-RMD-913/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/compliance/kvkk-retention-runbook.md
  - evidence/V1-RMD-908/RESULT.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Eksik bölümün önce yokluğunu gösteren kanıt, sonra eklenen bölüm ve koddaki sabitlerle karşılaştırma
2. V1-RMD-908 RESULT notuna fiş sıfırlama sınırının (2) ürün kararı değil varsayım olduğu yazılır (koordinatör talimatı; sınır sabit kalır, yönetici ayarı yapılmaz)
3. İki bağımsız qa incelemesi

## Out of scope

- Onaylı V0-CMP-003 envanteri ve privacy.retention_rules ilkesi değişmez; bakiye kaydı o ilkeye eklenmez (anonimleştirme değil, silme ve ayrı iş)

## Dependencies

- V1-RMD-912

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
