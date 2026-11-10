# V1-RMD-922 - Kasiyer tahsilata ana ekrandan ve masa panelinden ulaşır

- Task ID: V1-RMD-922
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, rapor 12, bulgular 3 ve 2b): ana kasa ekranında tahsilat girişi yok; "Siparişi gönder" sonrası ekran yalnız "Sipariş gönderildi. Yeni satış açabilirsiniz." diyor, ödeme ancak "Bekleyen hesaplar" listesindeki bir kartın kendisine tıklayınca açılıyor (kartta düğme veya ok yok). Masa panelinde masanın siparişi için hesap alma yolu yok. Karar: (1) gönderilmiş siparişte ana ekranda belirgin "Tahsil et" düğmesi (hesabı açar, ödeme sayfasına götürür); (2) Bekleyen hesaplar kartında görünür "Tahsil et" işareti; (3) siparişi olan masa panelinde "Hesabı al" düğmesi (masanın hesabı varsa ona, yoksa siparişinden hesap açıp ödeme sayfasına). Üçü aynı yardımcıyı kullanır (hesap yoksa siparişten açar, ödeme sayfası adresini verir).

## Owned surface

- `evidence/V1-RMD-922/**`
- `src/Clients/PosTerminal/src/collectOrder.ts`
- `src/Clients/PosTerminal/src/collectOrder.test.ts`
- `src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx`
- `src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx`
- `src/Clients/PosTerminal/src/routes/Cashier.collect.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/pending-checks.css
  - src/Clients/PosTerminal/src/features/pending-checks/pendingChecksApi.ts
  - src/Clients/PosTerminal/src/features/pending-checks/index.ts
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/features/billing/billingApi.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı testler: yardımcı (hesap varsa onu, yoksa siparişten açar), ana ekran "Tahsil et", masa paneli "Hesabı al"
2. Düzeltmeler, mutasyon ve iki bağımsız qa

## Out of scope

- Ödeme sayfasının kendisi, "Kasaya dön" bağlantıları (V1-RMD-928)

## Dependencies

- V1-RMD-921

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
