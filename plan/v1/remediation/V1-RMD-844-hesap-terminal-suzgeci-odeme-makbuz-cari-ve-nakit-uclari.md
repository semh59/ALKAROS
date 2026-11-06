# V1-RMD-844 - Hesap terminal süzgeci: ödeme, makbuz, cari hesap ve nakit tahsilat uçları

- Task ID: V1-RMD-844
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesi bulgusu F0897'nin (PO:2026-11-06) ikinci yarısı: V1-RMD-843 hesabın terminal sahipliğini (`billing.bills.terminal_id`), `BillTerminalAccess` denetimini ve `BILL_OTHER_TERMINAL` hata eşlemesini getirdi ve Billing uçlarına bağladı. Bu görev aynı denetimi `billId` alan geri kalan uçlara bağlar: ödeme alma ve bekleyen/belirsiz ödeme çözümleme (`tenders` grubu, `paymentId` içeren iki uç dahil), manuel kart onayı kabul/ret, ekranda gösterme, makbuz okuma, cari hesaba yazma ve nakit tahsilat (`billId` gövdede).

Bir terminal başkasının hesabına ödeme alamaz, makbuzunu okuyamaz, cari hesaba yazamaz; `cash.session.override` sahibi yapabilir. Ödemenin hesabı uçlar tarafından zaten denetleniyor (`payment.BillId == billId`); böylece ödemeler hesap üzerinden kapsanır.

## Owned surface

- `evidence/V1-RMD-844/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.Payments.cs (V1-RMD-785) — filtre bağlama.
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs (V1-RMD-516) — filtre bağlama.
  - src/Host/Experience/CustomerAccounts/DualScreenApplication.CustomerAccounts.cs (V1-RMD-794) — filtre bağlama.
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs (V1-RMD-790) — nakit tahsilatta gövdedeki hesap için denetim.
  - src/Host/Experience/Billing/BillTerminalAccess.cs (V1-RMD-843) — sahiplenmeyi uç başarısından sonraya alma.
  - src/Host/Experience/Billing/BillingSplitApplication.cs (V1-RMD-843) — iade tamamlamada aynı sıra.
  - tests/Host/Experience/PaymentTender/ (V1-RMD-787) — yeni BillTerminalAccessTenderHttpTests.cs ve eski testlerin güncellenmesi.
  - tests/Host/Experience/CashSession/ (V1-RMD-790) — eski testlerin güncellenmesi.
  - tests/Host/Experience/CustomerAccounts/ (V1-RMD-794) — eski testlerin güncellenmesi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı testler (gerçek Postgres, gerçek `DualScreenApplication.Build` host'u): başka terminalin kasiyeri sahipli hesapta her uçtan `BILL_OTHER_TERMINAL` (403) alır, sahibi geçer, `cash.session.override` sahibi geçer; uç başına bir tablo satırı.
2. Düzeltme, yeşil, mutasyon turu (`cp` + `cmp` ile geri alınır): filtreyi bir uçtan kaldırmak o uçun satırını düşürür.
3. Eski testlerin yeni kurala göre güncellenmesi.
4. İngilizce kod ve test adları.

## Out of scope

- Billing uçları ve sahiplik altyapısı (V1-RMD-843); `orders/active` içindeki ödeme ilerlemesi ve işletme geneli kuyruk/gün sonu listeleri.

## Dependencies

- V1-RMD-843

## Acceptance evidence

1. PaymentTender, CashSession ve CustomerAccounts test projeleri tam yeşil; kırmızı/yeşil çıktı `evidence/V1-RMD-844/` altında.
2. Mutasyon: her uçtan denetim kaldırma testleri düşürür.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.
5. Elle deneme: iki kasa terminali açın; birinde bir hesabın ödemesini başlatın, diğerinden aynı hesaba ödeme veya makbuz isteyin ve "Bu hesap başka bir kasada açık" mesajını görün; yönetici oturumuyla aynı işlem geçer.

## Handoff

- None
