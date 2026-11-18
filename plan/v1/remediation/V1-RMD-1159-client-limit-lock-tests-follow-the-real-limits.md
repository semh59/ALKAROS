# V1-RMD-1159 - İstemci sınır kilidi testleri gerçek sınırları izler (4 kırmızı kök neden)

- Task ID: V1-RMD-1159
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

`ClientLimitLockTests` içinde 4 test kırmızıdır (kasa ekranı not sınırı, 3 iade ekranı). Kök neden: ürün regresyonu yok; istemci sınırları sunucu kurallarıyla hâlâ aynıdır. Testin iki varsayımı eskidi: (1) kasa ekranında yalnız 500 karakterlik not vardır (sonradan çekmece gerekçesi 200, yönetici kullanıcı adı 64 ve parola 256 sınırları eklendi: V1-RMD-1135, V1-RMD-1144); (2) iade ekranı "refund ve maxLength geçen her dosya"dır (bekleyen hesaplar ve eşleştirme alanı da eşleşir).

1. Kasa ekranındaki her sınır bir sunucu kuralına bağlanır: 500 not sınırı `CashSessionPolicy.MaxExplanationLength`, 200 çekmece notu `CashDrawerService.MaxNoteLength`, 64 ve 256 yönetici kimlik bilgisi `DualScreenApplication.CashSession.cs` doğrulaması. Sunucu kaynağı bu sayıları taşımıyorsa test kırılır; ekranda sınıflanmamış sayı çıkarsa kırılır (gevşetme yok).
2. İade ekranları "refund-requests" uç noktasını çağıran istemci dosyalarıdır; sınırları 500, 50 ve `BillingSplitApplication.cs` kimlik doğrulama sınırı 64/256 ile sınıflanır; başka sayı kırar.

## Owned surface

- `evidence/V1-RMD-1159/**`
- tests/Host/Experience/CashSession/ClientLimitLockTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (mevcut 4 kırmızı), düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. CashSession test projesi ve ilişkili Host test projeleri

## Out of scope

- Ürün kodu; istemci dosyaları; sınır değerlerinin değiştirilmesi

## Dependencies

- V1-RMD-1158

## Acceptance evidence

1. Test çıktıları, mutant listesi ve kök neden gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. CashSession HTTP/kilit testleri exit code 0

## Handoff

- None
