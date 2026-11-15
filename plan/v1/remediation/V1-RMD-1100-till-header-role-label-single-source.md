# V1-RMD-1100 - Kasa satış üst çubuğundaki rol etiketi diğer ekranlarla aynı kaynaktan gelir

- Task ID: V1-RMD-1100
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasa kullanıcı denemesi 11: yönetici girişi yapmışken satış ekranının üst çubuğu "Kasiyer · Diğer" yazıyor, diğer ekranlar "Yönetici" gösteriyor. Rol etiketi tek bir işlevden (oturum yetkilerinden türetilen roleLabelFor) gelir; kabuk ve satış üst çubuğu aynı işlevi kullanır. Üst çubuk "<rol> · Diğer ▾" biçimini korur.

## Owned surface

- `evidence/V1-RMD-1100/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.header.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Rol adlarının metni; yetki kuralları; diğer ekranların üst çubuğu

## Dependencies

- V1-RMD-1099

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0

3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
