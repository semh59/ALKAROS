# V1-RMD-847 - Kasa ekranı: siparişleri tek hesapta birleştir

- Task ID: V1-RMD-847
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

F0046 (PO:2026-11-06) üçüncü adım: kasa ekranında (PosTerminal "Bekleyen hesaplar") aynı masanın birden çok siparişini seçip "Hesapları birleştir" ile V1-RMD-846'nın ucunu çağırmak ve açılan birleşik hesabı mevcut ödeme/bölme sayfasında açmak. Tüm metinler Türkçe, hata kodları Türkçe sözlükten geçer (ham kod veya İngilizce enum gösterilmez). Cashier ödeme sayfası (`split-payment.js`) hesabı `billId` ile açtığı için değişmez.

## Owned surface

- `evidence/V1-RMD-847/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx — çoklu seçim ve birleştir eylemi.
  - src/Clients/PosTerminal/src/features/pending-checks/pendingChecksApi.ts — birleştirme çağrısı.
  - src/Clients/PosTerminal/src/features/pending-checks/pending-checks.css — seçim durumu.
  - src/Clients/PosTerminal/src/features/billing/billingApi.ts — birleştirme istemcisi ve hata sözlüğü.
  - src/Clients/PosTerminal/src/strings.ts — Türkçe metinler.
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx — ekran testleri.
  - src/Clients/PosTerminal/src/features/billing/billingApi.test.ts — istemci testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı ekran testleri: yalnız aynı masanın iki+ siparişi seçilince "Hesapları birleştir" etkin olur; başarıda ödeme sayfası birleşik hesapla açılır; her sunucu hata kodu Türkçe mesaj gösterir; çift tıklama tek istek gönderir; aynı seçimin yeniden denemesi aynı anahtarı taşır, seçim değişince yeni anahtar; yetki yanıtı (FORBIDDEN) Türkçe gösterilir (kuyruk uç noktası yetki bilgisi taşımadığından eylem gizlenemez, V1-RMD-846 notu).
2. Uygulama, yeşil, mutasyon turu; İngilizce sızıntı taraması (`docs/UI_STYLE_GUIDE.md` §4) temiz.

## Out of scope

- Sunucu ucu (V1-RMD-846), Cashier vanilla ödeme sayfası.

## Dependencies

- V1-RMD-846

## Acceptance evidence

1. PosTerminal birim testleri, tip denetimi ve yapı exit 0; çıktı `evidence/V1-RMD-847/` altında.
2. Mutasyon: seçim kuralı ve hata çevirisi kaldırılınca testler düşer.
3. Elle deneme: aynı masada iki sipariş verin, bekleyen hesaplardan ikisini seçip birleştirin, tek hesap olarak ödeyin; iki sipariş de tamamlanır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
