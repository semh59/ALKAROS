# V1-RMD-1188 - Çekmece sayacı tek adla anılır; iki fişin ibare farkı belgelenir

- Task ID: V1-RMD-1188
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 17 (bulgu 5, 6, 7, 9):

1. Aynı sayaç kasa kapanış özetinde "Ödeme sırasında açılamayan çekmece", yönetici raporunda "Ödemede açılamayan darbe" diye iki adla geçer. Yönetici raporu kapanış özetindeki ada alınır, iki istemcide aynı kaldığı testle kilitlenir, `docs/qa/device-browser-test-plan.md` aynı adı kullanır.
2. Ödenmiş hesap fişi ("Bilgi Fişi", "Bu belge mali fiş değildir.", "Hesap: ...") ile ön hesap fişi ("BİLGİ FİŞİ", "MALİ DEĞERİ YOKTUR", "HESAP NO") V1-RMD-1177 kararıyla ayrı belgedir; hizalanmaz. `docs/domain/bill-pre-receipt.md` iki fişin ibare farkını ve nedenini yazar, mali müşavir teyit notu korunur.
3. Ödeme tutarı kutusunun 46 px görünmesi: dış kutu (`<label>`, dokunma hedefi) 48 px, ölçülen 46 px iç yazı alanıdır (kutu - 2 x 1 px kenarlık; V1-RMD-1145 canlı ölçümü); kod değişmez, gerekçe RESULT'a yazılır.
4. Rol atama yolundaki çift "roles" (`RoleManagementEndpoints` grup öneki + "/roles/..."): yayındaki PosTerminal istemcisi, testler ve idempotency listesinde kayıtlı; yol değişikliği geriye uyumsuz olduğu için yapılmaz, gerekçe RESULT'a yazılır.

## Owned surface

- `evidence/V1-RMD-1188/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.test.tsx
  - docs/qa/device-browser-test-plan.md
  - docs/domain/bill-pre-receipt.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (iki istemcideki sayaç adı aynı olmalı), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. PosTerminal ilgili vitest dosyaları ve istemci üretim derlemesi

## Out of scope

- Fiş şablonlarının birleştirilmesi; 48 px ve rol yolu için kod değişikliği; yeni sayaç

## Dependencies

- V1-RMD-1187

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi; madde 3 ve 4 için doğrulama kanıtı
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
