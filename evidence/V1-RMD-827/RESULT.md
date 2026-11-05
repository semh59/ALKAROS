# V1-RMD-827 sonuç

Kapatılan tutarsızlık: iş günü artık tek kural (`format.ts`: `businessDate`, `businessDayStart`, `businessDateRange`; 06:00 İstanbul - ertesi 06:00, sunucudaki BusinessDayWindow ile aynı, sabit +03:00). Mutfak "bugün" raporu (önceden UTC günü), kanal/fatura/marj raporlarının varsayılan 7 günlük aralığı (önceden cihaz yerel günü), gün sonu/stok/menü/reçete maliyeti varsayılan tarihi (önceden takvim günü) bu kuralı kullanır. Gece 00:00-06:00 arasında hepsi önceki iş gününü gösterir.

## Doğrulama
- PosTerminal vitest 1054/1054, tsc 0, oxlint 0 hata.
- Sınır testleri (02:59:59Z / 03:00:00Z, yıl sonu, DST'siz sabit ofset) farklı cihaz saat dilimlerinde (UTC, Los Angeles, Tokyo) yeşil.
- Mutasyon: 11 mutant, hepsi öldürüldü (mutation-summary.txt).
- Mutasyon 12 (teslim tarihi UTC dilimine dönerse) ve 13 (`dayMs` 23 saat) de öldürüldü; vitest güncel koşuda 1055/1055.

## İncelemeler (qa-frontend, qa-test-kalitesi)
Giderilen: sipariş fatura ekranındaki "teslim edilmemiş" listesinde teslim günü metnin UTC gününden alınıyordu (00:00-03:00 teslimler önceki gün görünürdü), cihaz bölgesinden bağımsız `formatDate` kullanıldı ve test eklendi; aralık yardımcısı için 1 ve 30 günlük ve artık yıl (2028) testleri; testlerde cihaz saat dilimi değişkeni geri alınır.
Ayrı görev (sunucu): ürün marjı raporu ve sipariş fatura listesi sunucuda İstanbul takvim gününe göre filtreler (`(opened_at AT TIME ZONE 'Europe/Istanbul')::date`, `service_date`), 06:00 iş günü penceresini (`BusinessDayWindow`) kullanmaz; V1-RMD-832 olarak planlanacak. İstemci artık iş günü tarihi gönderir; sunucu tarafı hizalanınca sınır tek kural olur.
Not: `CatalogWorkspace.tsx` `effectiveFrom` varsayılanı cihaz yerel saatini kullanır; iş günü kuralı değil, V1-RMD-832 kapsamında gözden geçirilecek.
