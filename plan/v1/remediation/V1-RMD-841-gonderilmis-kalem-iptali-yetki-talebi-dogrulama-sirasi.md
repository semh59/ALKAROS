# V1-RMD-841 - Gönderilmiş kalem iptalinde yetki talebi, kalem doğrulamasından önce açılıyor

- Task ID: V1-RMD-841
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesi G11 grubunun V1-RMD-836 ikram düzeltmesi sırasında bulunan kardeş kusuru: `POST .../items/{itemId}/void-sent` ucu `bills.void` yetkisi olmayan kullanıcı için önce yetki talebini (`grants.RequestAsync`) açıyor, kalemin iptal edilebilirliğini (bayat satır sürümü, kalem durumu, mutfağa gönderilmemiş veya servis edilmiş kalem, kapanmış hesap) ancak sonra, `SentItemVoidStore.VoidAsync` içinde denetliyor. Geçersiz bir istek yine de bekleyen bir yetki kaydı bırakıyor (yönetici onay kuyruğunda hayalet talep) ve yetki sayaçlarını (limit, sayı penceresi) tüketebiliyor.

Düzeltme: mağaza, doğrulamayı tek bir özel yönteme çıkarır (`VoidAsync` aynı yöntemi kullanır, davranış değişmez) ve uç, `bills.void` yetkisi olmayan dalda yetki talebinden önce `EnsureVoidableAsync` ile aynı doğrulamayı çağırır. Hata kodları değişmez.

## Owned surface

- `evidence/V1-RMD-841/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs (V1-RMD-101) — ortak doğrulama yöntemi.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-BIL-005) — doğrulama sırası.
  - tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentHttpTests.cs (V1-RMD-168) — yeni testler.
  - tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentTestDatabase.cs (V1-RMD-101) — yetki kaydı sayım yardımcısı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test (gerçek Postgres): `bills.void` yetkisi olmayan kullanıcı mutfağa gönderilmemiş kalem, servis edilmiş kalem, bayat satır sürümü ve ödenmiş hesaplı kalem için iptal dener; cevap mevcut hata kodu (409/400) olur ve yetki kaydı oluşmaz. Geçerli kalemde bekleyen kayıt oluşmaya devam eder.
2. Düzeltme, yeşil, mutasyon turu (`cp` + `cmp` ile geri alınır).
3. İngilizce kod ve test adları.

## Out of scope

- Ucun diğer davranışları, ikram ucu (V1-RMD-836'da bitti).

## Dependencies

- V1-RMD-836

## Acceptance evidence

1. VoidSent HTTP test projesi tam yeşil; kırmızı ve yeşil çıktı `evidence/V1-RMD-841/` altında.
2. Mutasyon: ön doğrulama kaldırılınca yeni testler kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
