# PDF Baseline

Bu planın karşılaştırıldığı tek PDF girdisi aşağıdadır. Dosya değiştirilmemiş,
kimliği hash ile sabitlenmiştir.

| Alan | Doğrulanan değer |
| --- | --- |
| Source file | `C:\Users\semih\Downloads\Telegram Desktop\restaurant_pos_master_v5.pdf` |
| File size | `851285` bytes |
| SHA-256 | `AF0E7F70174AC4006E93CC6E985C50E3F638EA6FC10E3C2EF96E745CDA780822` |
| Page count | `94` |
| Encrypted | `false` |
| PDF title metadata | `restaurant_pos_master_v5.md` |
| PDF creation metadata | `2026-07-29T18:26:11Z` |
| Plan audit date | `2026-07-29` |
| Visual audit pages | `2`, `25`, `90`, `91` |
| Visual audit result | `II.16` map defect and `I.46` 14/13 contradiction confirmed |

## Kaynak sınırı

- `PDF baseline`, PDF'de açıkça bulunan gereksinim veya kuraldır.
- Part IV'teki C1-C9 maddeleri kapatılmış varsayılmaz; görev ve kabul kanıtına
  dönüştürülür.
- PDF'de sözü edilip sözleşmesi bulunmayan provider davranışı, mevzuat sonucu,
  iş kuralı veya sayısal hedef implementation detayı olarak uydurulmaz.
- Resmî provider/mevzuat doğrulaması gereken iş, ilgili V0 validation/decision
  görevi `Done` olmadan başlatılmaz.
- Bu dosyadaki hash değişirse tüm PDF coverage denetimi geçersiz olur ve yeniden
  çalıştırılır.
- Text extraction bir doğruluk kanıtı olarak tek başına kullanılmaz. Bölüm
  haritası ve C8 gibi kritik sayfalar render edilerek görsel olarak da denetlenir.
