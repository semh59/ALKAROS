# V1-RMD-1162 - Kasa satış ekranında isteğe bağlı ürün notu ("soğansız", "az pişmiş")

- Task ID: V1-RMD-1162
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

`V1-RMD-1161` kasa ucuna isteğe bağlı `specialInstructions` ekledi ve kasa anlık görüntüsü satır notunu taşır. Bu görev PosTerminal kasa satış ekranına notu bağlar. Sunucuda kalem notu yalnız ekleme anında verilir (sonradan düzenleme yoktur), bu yüzden not ürüne dokunmadan önce hazırlanır.

1. Ürün ızgarasının yanında "Not ekle" düğmesi: tek dokunuşla tek satırlık not alanını açar; yazılan not **sıradaki** ürün eklemesine gider, ekleme başarılı olunca alan kapanır ve temizlenir; ekleme başarısız olursa not durur (yeniden denenebilir). Not yazılmadığı sürece ürün ekleme akışı bire bir aynıdır: ürüne tek dokunuş = tek istek, ek dokunuş yoktur, istek gövdesinde `specialInstructions` alanı yoktur. Seçenekli üründe not, seçenek penceresindeki "Siparişe ekle" ile birlikte gider.
2. Alan sınırı sunucu sınırıdır (ikisi de UTF-16 birimi sayar; V1-RMD-1161 qa bulgusu): `maxLength` 200, `DualScreenStore.MaxSpecialInstructionsLength` ile aynı; `ClientLimitLockTests` yeni bir testle iki tarafın birlikte oynamasını kilitler (V1-RMD-1159 kuralı: istemci sayısı sunucu kaynağından gelir). Dokunma hedefi 48 px, yazı 14 px ve üstü (`touchTargets.test.ts` ve `cashierTextSize.test.ts` kapsar).
3. Sepet satırında not görünür (`Not: soğansız`). Kaldırılan satırı "Geri al" ile geri koyarken not da geri gelir.
4. Aynı ürün notsuz satırdan ayrı satır olur: bu kural sunucudadır (V1-RMD-1161); istemci birleştirme yapmaz, yalnız sunucunun döndürdüğü satırları gösterir. "Mutfağa göndermeden ekle" kipinde not alanı gösterilmez (o uç not taşımaz; sessizce yok sayılmaz).
5. KVKK değerlendirmesi kodla: satır notu `orders.order_items.notes` olarak zaten `docs/compliance/kvkk-retention-runbook.md` saklama işinin kapsamındadır (5 yıl sonra anonimleştirilir) ve müşteri ekranına gitmez; yeni saklama mekanizması gerekmez. Alanın altında tek cümlelik Türkçe uyarı: kişisel bilgi yazılmaması.

## Owned surface

- `evidence/V1-RMD-1162/**`
- `src/Clients/PosTerminal/src/routes/CashierItemNote.tsx`
- `src/Clients/PosTerminal/src/routes/Cashier.item-note.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/contracts.ts
  - src/Clients/PosTerminal/src/api.ts
  - src/Clients/PosTerminal/src/api.test.ts
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/touchTargets.test.ts
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - tests/Host/Experience/CashSession/ClientLimitLockTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon (`cp` + `cmp` ile geri alınır)
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (`clientBuildGate.js`) exit code 0

## Out of scope

- Host kodu ve sözleşmeleri (V1-RMD-1161 tamamlandı)
- Vanilla Cashier ve WaiterPwa istemcileri
- Beklet (sunucuda park sözleşmesi yok; ayrı ürün kararı)
- Mevcut kaleme sonradan not ekleme/düzenleme (sunucu sözleşmesi yok)

## Dependencies

- V1-RMD-1161

## Acceptance evidence

1. Kırmızı test, test çıktıları, mutant listesi
2. Notsuz akışın dokunuş sayısı kanıtı (ürüne tek dokunuş = tek istek, gövdede not alanı yok); notlu akış, seçenekli ürün, başarısız ekleme, geri al ve kipte gizlenme testleri
3. `ClientLimitLockTests` yeni sınır testi dahil yeşil
4. İki ardışık bağımsız qa-* incelemesi
5. `node tools/client-build-gate/clientBuildGate.js --base <başlangıç commit'i>` exit code 0; plan audit ve consistency audit 0 hata 0 uyarı

## Handoff

- V1-RMD-1163
