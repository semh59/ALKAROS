# V1-RMD-926 - Vardiya kapanışı faturasız gönderilmiş (ödenmemiş) siparişleri de uyarır

- Task ID: V1-RMD-926
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, rapor 12, bulgu 6): ödenmemiş ₺10'luk satış varken vardiya kapanışı uyarısız geçti. Önceki karar: uyar, onayla kapat; sunucu bunu `CashCloseStore.UnsettledSql` ile yapıyor ama yalnız açık `billing.bills` kayıtlarını (ödenmemiş, iptal edilmemiş) ve belirsiz ödemeleri sayıyor. Kasiyerin gönderdiği ama henüz hesabı açılmamış sipariş (Bekleyen hesaplar) hiçbir hesap kaydına sahip olmadığı için sayılmıyor. Karar: "açık hesap" sayısına, durumu Gönderildi olan, çevrim içi olmayan (çevrim içi siparişler platformda ödenir) ve iptal edilmemiş hiçbir hesabı olmayan siparişler de eklenir. Uyarı metni ve onay akışı aynı kalır.

## Owned surface

- `evidence/V1-RMD-926/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CashCloseStore.cs
  - tests/Host/Experience/CashSession/CashCloseSafetyHttpTests.cs
  - tests/Host/Experience/CashSession/CashSessionHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: hesabı olmayan gönderilmiş sipariş kapanışta sayılır, uyarır, onayla kapanır; çevrim içi, taslak ve hesaplı siparişler çift veya yanlış sayılmaz
2. Düzeltme, mutasyon ve iki bağımsız qa

## Out of scope

- Uyarı ekranı metni

## Dependencies

- V1-RMD-925

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
