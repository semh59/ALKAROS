# V1-RMD-832 - Ürün marjı raporu iş günü penceresini kullanır; sipariş fatura listesi varsayılan aralığı takvim günüdür

- Task ID: V1-RMD-832
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-827 incelemesinde iki sunucu/istemci uyumsuzluğu çıktı. (1) Ürün marjı raporu satışı `opened_at` İstanbul takvim gününe göre gruplar; kanal raporu ve günlük toplamlar ise 06:00 iş gününe (`BusinessDayWindow`) göre. Gece 00:00-06:00 satışları marj raporunda ertesi güne düşer. Düzeltme: marj raporunun satış günü ve alış maliyeti günü aynı 06:00 kaydırmasını kullanır (kanal raporundaki kalıp). (2) Sipariş fatura listesi sunucuda vergi `service_date` takvim gününe göre süzer (tasarım gereği iş gününden ayrı kavram, `BusinessDayWindow` açıklaması); V1-RMD-827 bu ekranın varsayılan aralığını iş gününe çekmişti, gece 00:00-06:00 arası bugünün faturalarını dışarıda bırakır. Düzeltme: fatura listesi varsayılan aralığı İstanbul takvim gününe döner (`venueDateRange`), marj ve kanal raporu iş gününde kalır.

Katalog fiyat başlangıcı varsayılanı (cihaz saati) ayrı görevdir: dosya başka bir görevin çalışma ağacında.

## Owned surface

- `evidence/V1-RMD-832/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reporting/ProductMargin/PostgresProductMarginReportService.cs (V1-RMD-488) — iş günü kaydırması.
  - tests/Modules/Reporting/ProductMargin/ProductMarginReportTests.cs (V1-RMD-488) — sınır testleri.
  - src/Clients/PosTerminal/src/format.ts (V1-RMD-784) — `venueDateRange`.
  - src/Clients/PosTerminal/src/format.test.ts (V1-RMD-784) — sınır testi.
  - src/Clients/PosTerminal/src/features/management-order-invoices/OrderInvoicesSection.tsx (V1-RMD-473) — varsayılan aralık.
  - src/Clients/PosTerminal/src/features/management-order-invoices/OrderInvoicesSection.test.tsx (V1-RMD-473) — aralık testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları.

## Out of scope

- Sunucuda fatura listesi (takvim günü kalır); katalog varsayılan saati.

## Dependencies

- V1-RMD-827

## Acceptance evidence

1. ProductMargin test projesi yeşil: satış 05:59:59 yerel önceki gün, 06:00 yeni gün; 05:00 yerel alış önceki iş gününün maliyetine girer. PosTerminal vitest tam paket, tsc yeşil: fatura listesi varsayılanı gece 02:00'de bugünün takvim gününü içerir.
2. Mutasyon: kaydırma kalkarsa ve takvim günü iş gününe dönerse test kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
