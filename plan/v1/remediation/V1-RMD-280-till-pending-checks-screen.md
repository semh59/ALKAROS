# V1-RMD-280 - Kasiyerin "Bekleyen hesaplar" ekranı

- Task ID: V1-RMD-280
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`V1-ORD-006` kilitli tasarımı garsonun "Hesabı kasaya gönder" eylemini ve kasanın "ödeme bekleyen
hesaplar" kuyruğunu tanımlıyordu; kuyruk için sunucu uç noktası vardı ama onu gösteren HİÇBİR istemci
yoktu ("Kasa/PosTerminal: bekleyen hesaplar kuyruğu — kendi görevinde" deyip görev açılmamıştı). Garson
gönderiyor, kasiyer göremiyordu.

PosTerminal'e "Bekleyen hesaplar" çalışma alanı (`/pending-checks`) eklendi; kasa satış ekranının üst
çubuğundan ve gezinme çubuğundan açılır, 10 saniyede bir yenilenir. Satırlar MASAYLA değil hesap
numarasıyla ayrılır ve yeni müşteri aynı masaya oturmuş olsa bile karışmaz: hesap numarası, "Masa 5 ·
14:32 · 3 kalem", ilk kalemlerin adı, tutar; kısmen ödenmişse "Kalan 150 ₺ (toplam 200 ₺)". Satıra
dokunmak hesabı açar (yoksa oluşturur; `from-order` idempotent) ve doğrudan Tahsilat sayfasına gider; hesap
zaten varsa yenisini oluşturmaz. Bu ekran masayı hiçbir noktada değiştirmez.

Sunucu kuyruğuna ilk kalemlerin adı (`itemPreview`) eklendi (`V1-RMD-279` üzerine).

**Bu görevin yakaladığı önceki hata:** `V1-RMD-265`'te kasa üst çubuğuna eklenen "Kasa oturumu" bağlantısı,
`vanilla-clients-a11y` (axe `bypass` kuralı) testini kırmıştı; o görevde bu test koşulmamıştı. Sayfaya gerçek
bir "Ürün kataloğuna geç" atlama bağlantısı eklenerek düzeltildi (erişilebilirlik kazancıyla).

## Owned surface

- `plan/v1/remediation/V1-RMD-280-till-pending-checks-screen.md`
- `src/Clients/PosTerminal/src/features/pending-checks/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/14-till-queue-of-sent-checks.spec.js
  (V1-CUI-011 sahipliğindeki Cashier E2E paketine eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
  (PosTerminal sahibi görevlerde kalır — yalnız yeni rota, gezinme öğesi ve başlıklar)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx
  (aynı sahiplikte — yalnız üst çubuğa bağlantı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
  (yalnız gezinme etiketi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/index.html
  (Cashier sahibi görevlerde kalır — yalnız atlama bağlantısı ve katalog bölgesinin kimliği)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.css
  (yalnız atlama bağlantısı stili)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/CashierHandoffStore.cs
  (V1-ORD-006 sahipliğinde kalır — yalnız `itemPreview` sütunu)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementContracts.cs
  (aynı sahiplikte — yalnız `ItemPreview` alanı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs
  (aynı sahiplikte — önizleme doğrulaması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/lib/paymentHelpers.js
  (V13-RMD-003 ile eklenen yardımcı — yalnız `sendCheckToCashier`)

## In scope

1. Ekran, API istemcisi, rota ve gezinme, birim testleri.
2. Gerçek tarayıcıda uçtan uca: garson gönderir, kasiyer görür, iki müşteri ayırt edilir, biri tahsil edilir,
   yalnız o kuyruktan çıkar.

## Out of scope

- Garsonun "gönderdiğim hesapların ödenip ödenmediğini görmesi".
- Bekleme süresi uyarısı / bildirim.
- Masa durumu: ödeme masaya dokunmaz (kilitli tasarım).
- Sipariş yaşam döngüsü (`Served`/`Completed`).

## Dependencies

- V1-ORD-006
- V1-RMD-279
- V1-RMD-265
- V1-RMD-276

## Acceptance evidence

- PosTerminal `tsc --noEmit` temiz; vitest 199/199 (yeni 4: aynı masanın iki hesabı ayırt edilir; hesabı olmayan
  satır hesap açıp tahsile gider; hesabı olan yenisini açmaz; boş/hata iletisi Türkçe). Vanilla istemci testleri 24/24.
- Cashier E2E (gerçek Host + Chromium): 31/31. Yeni senaryo: aynı masada iki müşterinin hesabı kuyrukta iki ayrı
  satır (₺200 / ₺100); ilkine dokunmak Tahsilat sayfasını açar; 50 ₺ tahsilat sonrası satır "Kalan ₺150,00 (toplam
  ₺200,00)"; kalanı ödeyince `billClosed = true` ve YALNIZ o satır kuyruktan çıkar, ikinci müşterinin satırı yerinde.
- Host.Experience.Orders.TableDraft: `itemPreview` doğrulandı.
- `consistency_audit.py`, `plan_audit_tool.py validate` temiz.

## Handoff

- None
