# V1-RMD-475 - Faturasız kalan online sipariş için uzlaştırma vakası

- Task ID: V1-RMD-475
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Restoran, platform kullanıcısına teslimden sonra en geç 7 gün içinde e-Arşiv fatura düzenlemek zorundadır. Teslim edilmiş ama taslağı açılamamış (satıcı bilgisi eksik, hata) sipariş
süre dolmadan önce Sorunlar listesinde vaka olarak görünür: Türkçe etiket, sonraki adım ("İşletme bilgilerini girin" ya da "Faturayı elle düzenleyin"). Kaynak çifti
`NotHandedOverSourcePair` kalıbıyla yazılır; taslak açılınca vaka kendiliğinden kapanır. Yönetim ekranındaki faturasız sipariş listesi ayrıdır ve vakayı beklemez.

## Owned surface

- `plan/v1/remediation/V1-RMD-475-online-order-invoice-missing-case.md`

## In scope

- Kaynak çifti, vaka ayrıntısı, istemci etiketi ve testler; kapsam başlatılırken kesin yollarla yazılır.

## Out of scope

- Taslak açma (`V1-RMD-472`).

## Dependencies

- V1-RMD-472

## Acceptance evidence

- Testler ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-475/` altındadır.

## Handoff

- None
