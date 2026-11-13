# V1-RMD-1028 - Bölme ekranı başlığı fatura kodu yerine masayı gösterir

- Task ID: V1-RMD-1028
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Hesap bölme ekranında başlık BILL-POS-... gibi iç fatura kodunu büyük yazıyordu; kasiyer için anlamsızdı. Başlık masa varsa Masa ile numarası, yoksa Hesap olur; fatura kodu durum satırında küçük bilgi olarak kalır.

## Owned surface

- `evidence/V1-RMD-1028/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/billing/billing.css
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1027

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
