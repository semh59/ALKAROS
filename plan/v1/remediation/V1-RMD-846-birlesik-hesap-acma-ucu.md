# V1-RMD-846 - Birleşik hesap açma ucu

- Task ID: V1-RMD-846
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

F0046 (PO:2026-11-06) ikinci adım: birden çok siparişi tek hesapta toplayan gerçek sunucu akışı. `POST /api/v1/terminals/{terminalId}/billing/bills/merge` gövdesindeki en az iki siparişin aktif kalemlerini `BillSourceOperations.CreateMergedBill` ile tek hesapta toplar, hesabı çağıran terminale sahiplendirir (V1-RMD-843) ve masa işaretçisini bağlar. Yetki `bills.split`; aynı `IdempotencyKey` ile tekrar gönderim aynı hesabı döndürür. Reddedilen durumlar Türkçe hata kodlarıyla döner: tek sipariş/yinelenen sipariş, bulunamayan sipariş, kapanmış/iptal/taslak sipariş, zaten hesabı olan sipariş, farklı masalar (yalnız aynı masadaki veya birleştirilmiş masa grubundaki siparişler birleştirilir).

Eşit bölmede kuruş kalıntısı son kişide kalır (`SplitEngine`, F0349 kararı); bu görev bölme kodunu değiştirmez, birleşik hesap mevcut bölme tasarımı uçlarıyla bölünür.

## Owned surface

- `evidence/V1-RMD-846/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitApplication.cs — yeni uç ve kayıt.
  - src/Host/Experience/Billing/BillingSplitStore.cs — birleşik hesap oluşturma.
  - src/Host/Experience/Billing/BillingSplitContracts.cs — istek ve sonuç sözleşmesi.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs — Türkçe hata eşlemeleri.
  - tests/Architecture/ApiConventions/idempotency_allowlist.json — yeni POST ucu.
  - tests/Host/Experience/Billing/ — uç testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı testler (gerçek Postgres): iki sipariş tek hesapta toplanır, tutar iki siparişin toplamıdır, terminal sahibi olur, masa işaretçisi bağlanır; aynı anahtarla tekrar aynı hesabı döner; yetkisiz kullanıcı 403; her ret durumu kendi kodu ve Türkçe mesajıyla; yarışta (iki terminal aynı siparişleri) tek hesap oluşur.
2. Düzeltme, yeşil, mutasyon turu.
3. Birleşik hesap ödenince V1-RMD-845'in kapatma akışıyla kaynak siparişlerin tamamlandığını gösteren uçtan uca test.

## Out of scope

- Kasa ekranı (V1-RMD-847); siparişin kalemlerini birden çok hesaba bölme akışı.

## Dependencies

- V1-RMD-845

## Acceptance evidence

1. Billing test projesi ve API sözleşme testleri tam yeşil; kırmızı/yeşil çıktı `evidence/V1-RMD-846/` altında.
2. Mutasyon: her ret kuralını ve idempotency'yi kaldırmak testleri düşürür.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
