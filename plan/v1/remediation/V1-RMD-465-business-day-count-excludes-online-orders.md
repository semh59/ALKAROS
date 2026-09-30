# V1-RMD-465 - Gün sonu sipariş sayısından online siparişleri çıkarmak

- Task ID: V1-RMD-465
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Semih 2026-09-30'da online (Yemeksepeti, Trendyol Go) satışların normal ciroya yazılmamasına, ayrı bir online raporda
görünmesine karar verdi. Gün kapatma bugün ciroyu yalnız `payments.payments` içindeki onaylı ödemelerden, sipariş sayısını
ise `orders.orders` içinden okuyor; online siparişin ödeme kaydı olmadığı için ciroya eklenmiyor ama sayıyı şişiriyor.
Bu görev gün sonunda saklanan sipariş sayısından `source = 'Online'` siparişleri çıkarır; ciro ve diğer kanallar
(masa, QR) değişmez. Online siparişler ayrı kanal raporundadır.

## Owned surface

- `plan/v1/remediation/V1-RMD-465-business-day-count-excludes-online-orders.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/BusinessDayTotals/BusinessDayTotalsReader.cs - yalnız sipariş sayısı sorgusu ve açıklaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reporting/EndOfDayHttpTests.cs - yalnız yeni davranışın testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reporting/EndOfDayTestDatabase.cs - yalnız siparişin kaynağını seçen isteğe bağlı parametre

## In scope

- Sipariş sayısı sorgusuna `source <> 'Online'` süzgeci; kaynak açıklaması ve `RecordedBusinessDayTotals` belge yorumu güncellenir.
- Test: aynı gün içindeki bir online sipariş sayıyı değiştirmez, masa ve QR siparişi sayılmaya devam eder.

## Out of scope

- Online satışın ciroya eklenmesi (karar: eklenmeyecek); kanal raporu ekranı (`V1-RMD-466`).

## Dependencies

- None

## Acceptance evidence

- Testler ve gerçek Host denemesi (online sipariş bulunan bir günde gün sonu sayısı yalnız masa siparişlerini sayar);
  çıktılar `evidence/V1-RMD-465/` altındadır.

## Handoff

- None
