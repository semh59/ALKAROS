# V1-RMD-832 sonuç

Kapatılan: (1) Ürün marjı raporu satış günü ve alış (mal kabul) günü artık 06:00 İstanbul iş gününe göre (kanal raporundaki aynı kaydırma); gece 00:00-06:00 satışları önceki iş gününde görünür. (2) Sipariş fatura listesinin varsayılan aralığı İstanbul takvim günü (`venueDateRange`): sunucu `service_date`'i teslim anının İstanbul takvim günü olarak tutar ve takvim gününe göre süzer (vergi kavramı, iş gününden ayrı); V1-RMD-827'nin bu ekranı iş gününe çekmesi gece 00:00-06:00 arası bugünün faturalarını dışarıda bırakırdı. Kanal ve marj raporları iş gününde kalır.

## Doğrulama
- ProductMargin testleri 21/21 (kırmızı: 2 yeni test önce kırıldı, kırmızı kaydı red.log, yeşil green.log). PosTerminal vitest 1067/1067, tsc 0.
- Mutasyon 4 mutant (marj satış günü kaydırması, alış günü kaydırması, takvim günü yerine UTC günü, fatura aralığı iş gününe dönerse) hepsi öldürüldü.

## İncelemeler (qa-veritabani, qa-frontend)
Giderilen: sınıf özeti "business day"; yıl sonu ve artık yıl aralık testleri; `formatDecimal` biçimi.
Ayrı görev: fatura ekranında "takvim günü" / rapor ekranlarında "iş günü" ipucu cümlesi `strings.ts` değişikliği ister; bu dosya başka bir görevin çalışma ağacında olduğu için V1-RMD-834'te (katalog fiyat başlangıcı ile birlikte) yapılacak.
