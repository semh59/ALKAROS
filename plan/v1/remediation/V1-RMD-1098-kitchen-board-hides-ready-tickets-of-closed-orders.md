# V1-RMD-1098 - Mutfak panosu kapanmış siparişin servis bekleyen hazır biletlerini göstermez

- Task ID: V1-RMD-1098
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasa kullanıcı denemesi 11: mutfak canlı eşitleme açıkken servis edilmemiş Hazır biletler panoda kalır; garsonlar kalemi servis edildi işaretlemediği için (V1-RMD-282) ödenip kapanmış siparişlerin biletleri 11 Kasım'dan beri duruyor ve masa kartı yüzlerce piksele uzuyor. Pano sorgusu (GetActiveTicketsAsync), siparişi Completed, Cancelled veya Rejected olan Hazır biletleri panodan çıkarır; Hazır biletler servis edilene kadar ayrı bölümde kalır kuralı ve canlı eşitleme ayarı açık siparişler için aynen sürer. Hazırlanmakta olan bilet, ödeme önce alınmış olsa bile mutfağın işi olduğu için görünmeye devam eder. Bilet verisi silinmez, yalnız pano süzgeci değişir.

## Owned surface

- `evidence/V1-RMD-1098/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
  - tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi


## Out of scope

- Hazırlanmakta veya sırada olan biletlerin süzgeci; masa kartı yüksekliği ve istemci kodu; bilet saklama süresi

## Dependencies

- V1-RMD-1097

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi

2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
