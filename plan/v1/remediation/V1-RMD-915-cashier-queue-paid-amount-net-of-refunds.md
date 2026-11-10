# V1-RMD-915 - Kasa kuyruğunda ödenen tutar iadeleri düşerek gösterilir

- Task ID: V1-RMD-915
- Status: Planned
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-911 qa bulgusu (koordinatör kararı: ertelenmez). Kasanın ödeme bekleyen hesaplar kuyruğu 'ödenen tutarı' yalnız ödeme dağıtımları toplamı olarak gösteriyor; iade edilmiş ödeme hâlâ ödenmiş görünüyor, kasiyer kalan borcu yanlış okur. Karar: gösterilen tutar dağıtımlar eksi iadeler olur (911'deki tahsilat tanımı).

## Owned surface

- `evidence/V1-RMD-915/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/CashierHandoffStore.cs
  - tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs
  - tests/Host/Experience/Orders/TableDraft/ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Test: kısmen iade edilmiş ödemeli hesapta kuyruk, iadeden sonra kalan tutarı gösterir; iadesiz hesap değişmez
2. Sorgu değişikliği ve test projesine iade tablosu göçü
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Kuyruğun hangi hesapları listelediği değişmez

## Dependencies

- V1-RMD-912

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
