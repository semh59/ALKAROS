# V1-RMD-856 - Kendi onayı ekran süzgeci: iade istekleri ve kart çekildi onayı yetkisiz düğmeyi göstermez

- Task ID: V1-RMD-856
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (kendi onayı ayarı): sunucu 'approvals.manager_self_approval_enabled' kapalıyken kişinin kendi isteğini onaylamasını/reddetmesini reddediyor (RefundSelfDecisionException), fakat ekranlar bunu bilmiyor. (1) PosTerminal RefundPanel her 'Requested' satırda Onayla/Reddet gösteriyor, iade listesi ayar ya da kullanıcı bilgisi taşımıyor; kendi talebinde düğme basılır, sunucu reddeder. (2) Kasiyer ekranı (split-payment.js) iade satırlarında aynı; kart çekildi onayında ise tersi: kendi bildirimi her zaman onaylanamaz sayılıyor, ayar açıkken sunucu izin verdiği halde düğme gizli. Kasa hareketi ekranı (cash-session.js) bu süzgeci zaten uyguluyor (sunucu selfApprovalEnabled döndürüyor). Karar tek yerde, sunucuda verilir: iade isteği satırına ve bekleyen kart onayına `mayDecide` (isteği yapan kişi çağıran değilse ya da ayar açıksa true) eklenir; ekranlar yalnız bu alana bakar, kullanıcı kimliği ya da ayar bilgisi istemciye taşınmaz.

## Owned surface

- `evidence/V1-RMD-856/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/PaymentRefunds.cs — RefundRequestV1'e MayDecide alanı.
  - src/Host/Experience/Billing/BillingSplitApplication.cs — iade listesi uç noktası MayDecide'ı çağırana göre doldurur.
  - src/Host/DualScreen/DualScreenApplication.Payments.cs — bekleyen kart onayına MayDecide.
  - src/Clients/PosTerminal/src/features/billing/refundApi.ts — RefundRequestRow.mayDecide.
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx — mayDecide false ise Onayla/Reddet/limit düğmeleri yerine açıklama.
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.test.tsx — davranış testi.
  - src/Clients/PosTerminal/src/strings.ts — Türkçe açıklama metni (gerekirse).
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js — iade satırı ve kart onayı mayDecide'a bakar.
  - tests/Clients/StaticApps/split-payment-refunds.test.js — iade satırı davranış testi.
  - tests/Clients/StaticApps/split-payment-hierarchy.test.js — kart çekildi onayı davranış testi.
  - tests/Host/Experience/Billing/RefundSelfApprovalHttpTests.cs — liste mayDecide sunucu testi.
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs — tender özeti bekleyen onay mayDecide sunucu testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı testler: sunucu (ayar kapalı: kendi isteği mayDecide=false, başkasınınki true; ayar açık: ikisi de true) ve iki ekran.
2. Mutasyon: sunucuda koşul tersine çevrilir ve alan hep true döner; ekranlarda mayDecide denetimi kaldırılır (cp+cmp ile geri alınır).
3. Yeni Türkçe metin docs/UI_STYLE_GUIDE.md kurallarına uyar; İngilizce sızıntı taraması geçer.

## Out of scope

- Üretim sunucu kararları (EnsureMayDecide) değişmez; kasa hareketi ve kasa mutabakatı ekranları (zaten süzgeçli).
- Ayarın kendisinin ekranı (SettingsHistorySection) değişmez.

## Dependencies

- None

## Acceptance evidence

1. Sunucu testleri gerçek Postgres (alkaros-test-pg, 55432) ile, istemci testleri vitest ile exit 0; çıktılar `evidence/V1-RMD-856/`.
2. tsc --noEmit ve oxlint temiz.
3. Mutasyon kanıtı cp+cmp ile geri alınmış.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
