# V1-RMD-1073 - Bekleyen indirim testi yük altında zamanlamaya bağlı kırılmaz

- Task ID: V1-RMD-1073
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

tests/Clients/StaticApps/split-payment-discount-pending.test.js tüm StaticApps koşusunda yük altında bir kez kırıldı. Neden: sabit 20 ms ve 150 ms gerçek bekleme süreleri (yanıtın işlendiğini varsayma) ve 100 ms gecikmeli yanıtla "önceki kontrol yanıtsızken ikinci kontrol gönderilmez" testi (yük altında yanıt tikler arasında gelebilir). Sabit beklemeler, elle açılan bir kapı (yanıt) ve arayüz durumuna bakan beklemelerle değiştirilir; test silinmez, gevşetilmez.

## Owned surface

- `evidence/V1-RMD-1073/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/split-payment-discount-pending.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. tests/Clients/StaticApps tam koşusu

## Out of scope

- Listede olmayan ekranlar ve dosyalar; üretim kodu

## Dependencies

- V1-RMD-1072

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
