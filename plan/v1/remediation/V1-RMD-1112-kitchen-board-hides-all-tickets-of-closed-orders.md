# V1-RMD-1112 - Mutfak panosu kapanmış siparişin eski ve hazır biletlerini göstermez

- Task ID: V1-RMD-1112
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal
Kasa kullanıcı denemesi 12: V1-RMD-1098 yalnız Hazır biletleri gizledi; hesabı ödenip kapanmış siparişlerin Bekliyor ve Hazırlanıyor biletleri panoda duruyor ve masa kartı 2000 pikseli aşıyor. Pano sorgusu ve yazdırma hatası şeridi şunu uygular: siparişi Cancelled veya Rejected olan hiçbir bilet görünmez; siparişi Completed olan Hazır biletler görünmez; siparişi Completed olan Bekliyor, Kabul edildi veya Hazırlanıyor biletler yalnız son 12 saat içinde açıldıysa görünür (tezgâh satışı hemen ödenir, mutfak işi sürer; eski terk edilmiş biletler düşer). Açık siparişin biletleri, Hazır bölümü ve canlı eşitleme ayarı aynen sürer. Bilet verisi silinmez, yalnız pano süzgeci değişir. (Görev talimatı "durum ne olursa olsun gizle" idi; bağımsız qa, tezgâh satışında ödenmiş yemeğin mutfaktan kaybolacağını gösterdiği için Completed için 12 saatlik pencere seçildi.)

## Owned surface

- `evidence/V1-RMD-1112/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
  - tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi


## Out of scope

- Masa kartı yüksekliği ve istemci kodu; bilet saklama süresi; açık siparişin terk edilmiş biletleri

## Dependencies

- V1-RMD-1098

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi


## Handoff

- None
