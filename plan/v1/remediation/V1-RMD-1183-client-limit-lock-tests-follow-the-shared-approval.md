# V1-RMD-1183 - İstemci sınır kilidi testleri ortak onay doğrulayıcısını izler (3 kırmızı kök neden)

- Task ID: V1-RMD-1183
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

`ClientLimitLockTests` içinde 3 test kırmızıdır (kasa ekranı not sınırı, 2 iade ekranı). Kök neden: ürün regresyonu yok; V1-RMD-1173..1176 yerinde yönetici onayını `InPlaceManagerApproval` ortak bileşenine taşıdı, kullanıcı adı 64 ve parola 256 doğrulaması artık yalnız orada durur; testin aradığı `request.Username.Length > N` kalıbı kalmadı. İstemci sınırları sunucuyla hâlâ aynıdır.

1. Kimlik sınırları `InPlaceManagerApproval.cs` kaynağındaki `username.Length > 64` ve `password.Length > 256` ifadelerine bağlanır; sunucu bu sayıları taşımıyorsa test kırılır.
2. Onay alanları bulunur, listelenmez: istemcideki her `type="password"` alanı parola sınırını, çiftindeki kullanıcı adı alanı kullanıcı adı sınırını taşımalıdır (kasa çıkışı, iade, fiş sıfırlama, kapanış farkı, indirim/ikram onayı); adlandırması farklı yeni bir parola alanı sınıflanmadığı için kırar.
3. Sunucuda `.ManagerUsername` kullanan her dosya ortak doğrulayıcıyı çağırmalı ve kendi uzunluk denetimini taşımamalıdır; yeni bir uç nokta kendi kuralını yazarsa kırar.
4. Harici POS fiş kutusu (32) mevcut kilitle kapsanır ve yeni sayım sınıflamasında sapma çıkarmaz.

## Owned surface

- `evidence/V1-RMD-1183/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/CashSession/ClientLimitLockTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (mevcut 3 kırmızı), düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. CashSession ve PaymentTender ilişkili test projeleri

## Out of scope

- Ürün kodu; istemci dosyaları; sınır değerlerinin değiştirilmesi

## Dependencies

- V1-RMD-1182

## Acceptance evidence

1. Test çıktıları, mutant listesi ve kök neden gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. ClientLimitLockTests tamamı yeşil; CashSession ve PaymentTender sınıfları exit code 0

## Handoff

- None
