# V1-RMD-890 - MergedBillOrderLinkTests bayat kasa kuyruğu beklentisi (birleşik hesap tek satır)

- Task ID: V1-RMD-890
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

`MergedBillOrderLinkTests.TheTillQueueShowsTheMergedBillOnItsOrdersAndDropsThemOncePaid` kırmızı: iki siparişten birleştirilmiş hesap için kasa kuyruğunda iki satır bekliyor, oysa birleşik hesap kasada tek çek olarak görünür (kuyruk en eski siparişi temsilci yapar, kalem sayısı ve toplam birleşir). Beklenti, doğru davranışı doğrulayacak biçimde düzeltilir: açıkken tek satır (iki siparişin kalemleri ve toplamı birleşik), ödendikten sonra kuyrukta hiçbiri.

## Owned surface

- `evidence/V1-RMD-890/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Billing/MergedBillOrderLinkTests.cs — bayat beklenti
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Testin tek satır, birleşik kalem sayısı ve toplam beklentisine çevrilmesi
2. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Kasa kuyruğu üretim kodu (`CashierHandoffStore`)

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
