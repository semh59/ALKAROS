# V1-RMD-498 - KVKK anonimleştirme eşzamanlılığı ve tedarikçi saklama tabanı

## Kurallar

- Her anonimleştirme adımı kendi işleminde müşterinin hesap kilidini (`account-charge-customer:<id>`, hesaba yazmanın aldığı anahtarın aynısı) alır, adımın önceden bitip bitmediğini yeniden okur ve engeli (açık bakiye) kilit altında yeniden değerlendirir. Eskiden engel yalnız iş başında bir kez bakılıyor, ardından borç yazılan müşterinin profili silinebiliyordu.
- Adım başka bir işçi tarafından bitirilmişse atlanır; aynı kuyruğu iki işçi aynı anda çalıştırınca ikincisi kayıt çakışmasıyla çökmez.
- Tedarikçi saklama süresi taban tarihi artık son alış faturasını (tedarikçi kimliği veya vergi numarasıyla) ve son mal kabulünü de sayar; yalnız faturayla çalışılan tedarikçi aktifken anonimleştirilmez.

## Kanıt

- `tests-anonymization.log`: 12 test (yeni: adımlar arasında engellenen kayıt sonraki adımı yazmaz, hesap kilidi bekleme, iki işçi aynı kuyrukta). `tests-retention.log`: 12 test (yeni: fatura/mal kabulü olan tedarikçi vadesi gelmiş sayılmaz).
- `mutation.log`: 5 mutant, hepsi en az bir testi kırdı; dosyalar hash ile geri yüklendi. İlk taslaktaki ayrı iş kilidi gereksiz çıktı (hesap kilidi aynı özneyi zaten sıraya koyuyor) ve kaldırıldı.
