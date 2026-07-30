# Version Gates

## Global kurallar

- `v0` kapanmadan production uygulama geliştirmesi başlamaz.
- Bir sürümün açık finansal, stok veya mevzuat kararı sonraki sürüme borç olarak
  taşınmaz.
- Dış entegrasyon sözleşmesi gerçek erişim olmadan tamamlanmış sayılmaz.
- Her migration boş PostgreSQL 18 veritabanında ileri ve geri doğrulanır.
- Her cached projection için source-of-truth, atomik güncelleme ve rebuild yolu
  belgelenmeden ilgili modül tamamlanmaz.

## Sürüm zinciri

`V0 -> V1 -> V11 -> V12 -> V13 -> V14 -> V15 -> V20`

## Canlı veri kuralı

`V20-REL-003` tamamlanana kadar hiçbir sürüm gerçek müşteri veya gerçek para ile
çalıştırılmaz. Pilot veri de production verisi kabul edilir.

