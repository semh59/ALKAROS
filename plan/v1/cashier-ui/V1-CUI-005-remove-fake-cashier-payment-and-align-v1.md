# V1-CUI-005 - Remove fake cashier payment and align V1 contract

- Task ID: V1-CUI-005
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Goal

Cashier UI üzerindeki V1 sözleşmesine aykırı sahte para kabul etme ("Nakit Tahsilat & Fiş Kes", "Tahsilat Başarılı") iddialarını ve in-memory state yanılsamalarını kaldırmak; V1 kapsamına uygun hızlı adisyon/sipariş taslağı motoruna dönüştürmek.

## Owned surface

- `src/Clients/Cashier/wwwroot/**`
- `tests/Clients/Cashier/Frontend/**`
- `evidence/V1-CUI-005/**`

## Dependencies

- V1-RMD-037

## Acceptance evidence

- Cashier arayüzünde sahte ödeme/tahsilat veya mali başarı iddiası bulunmaz.
- Adisyon oluşturma ve mutfağa iletim V1 çekirdek sözleşmesine uygun çalışır.
- Testler güncellenir ve geçer.

## Handoff

- V1-RMD-042
