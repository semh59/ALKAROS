# V1-RMD-462 - Online menü sekmesinde eşlenmemiş platform kodlarını göstermek

- Task ID: V1-RMD-462
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Online menü sekmesi (`features/online-menu`), `V1-RMD-460`ın eklediği eşlenmemiş platform kodlarını gösterir: "Siparişlerde
eşlenmemiş kodlar" listesi (kod, sipariş sayısı, son görülme), koda tıklayınca ilgili ürünün eşleme alanına yazılması
için elle yazılan kod alanına öneri listesi. Platform menüsü okunabildiğinde (Trendyol Go) ürün adı seçiminin yanında
kodlar yine görünür. Tüm metinler Türkçedir.

## Owned surface

- `plan/v1/remediation/V1-RMD-462-online-menu-unmapped-codes-screen.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-menu/onlineMenuApi.ts - yalnız yeni alanın tipi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-menu/OnlineMenuTab.tsx - yalnız eşlenmemiş kod listesi ve öneri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-menu/OnlineMenuTab.test.tsx - yalnız yeni davranışın testleri

## In scope

- Liste boşken hiçbir şey gösterilmez; doluyken sayı ve kodlar; kod önerisi elle yazılan alanda `datalist` ile.

## Out of scope

- Uç nokta ve veri (`V1-RMD-460`); Yemeksepeti'ye özel ekran değişikliği.

## Dependencies

- V1-RMD-460

## Acceptance evidence

- Vitest ve typecheck exit code 0; gerçek Host denemesi; çıktılar `evidence/V1-RMD-462/` altındadır.

## Handoff

- None
