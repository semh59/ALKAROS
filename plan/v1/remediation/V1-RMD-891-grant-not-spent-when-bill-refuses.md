# V1-RMD-891 - Hesap kabul etmeyecek ikram/indirim için hibe harcanmaz (tahsil edilenin altı kontrolü hibeden önce)

- Task ID: V1-RMD-891
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer/garson ikram veya indirimi önce hibe motorundan geçer (`grants.RequestAsync`; otomatik onayda `granted` satırı yazılır, rol limitini ve hesap toplam sınırını tüketir). "Tahsil edilenin altına iniyor" kontrolü ise hibeden sonra, `ApplyDiscountAsync` içinde yapılır; 409 `DISCOUNT_BELOW_COLLECTED` verirse hibe boşa harcanmış kalır (limit sayacı ve hesap toplamı dolar, yönetici onayı verilmişse boşa gider). `EnsureDiscountRequestableAsync` (hibeden önce çalışan kontrol) tahsil edilenin altı kontrolünü de yapar: aday indirim/ikram ile hesaplanan ödenecek tutar tahsil edilenin altındaysa hibe açılmadan 409 verilir. Aynı anahtarla daha önce uygulanmış bir isteğin tekrarı (replay) bu ön kontrollerden muaf tutulur ve eskisi gibi cevaplanır. Hibe sonrası kontrol (yarış durumu için kilitli son kontrol) aynen kalır.

## Owned surface

- `evidence/V1-RMD-891/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitStore.cs — ön kontrol ve ortak aday/tahsilat okuması
  - src/Host/Experience/Billing/BillingSplitApplication.cs — ön kontrole kullanıcı kimliği
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs — hibe harcanmaz testleri
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: tahsil edilenin altına inecek ikram, sabit indirim ve yüzde indirim isteği 409 verir ve `identity.authorization_grants` tablosunda satır bırakmaz
2. Test: kısmen uygulanmış ikramın aynı anahtarla tekrarı 200 ile aynı sonucu döner (ön kontrol tekrarı bozmaz)
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Yönetici onayından sonra, tekrar gönderimden önce başka bir ödemenin gelmesi (yarış): hibe onaylı kalır, uygulama 409 verir; bu görevde değişmez ve RESULT.md'de raporlanır

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
