# V1-RMD-1160 - Kasa masa panelinde mutfağa giden kalem için iptal isteği

- Task ID: V1-RMD-1160
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Sunucuda gönderilmiş ama servis edilmemiş kalemin iptali tamdır (`POST /api/v1/terminals/{terminalId}/orders/{orderId}/items/{itemId}/void-sent`, `VoidSentItemRequestV1`, `bills.void` izni, kasiyer rolünde yönetici onaylı talep, sonuç `Applied` veya `Pending`); garson uygulamasında vardır, kasa (PosTerminal) masa panelinde yoktur. Masa panelindeki her kalem satırında sunucunun `canVoidSent` bayrağı doğruysa kalemin kendi satırında "Mutfaktaki ürünü iptal et" eylemi çıkar: gerekçe seçilir (varsayılan seçili değil), onay düğmesi kalemi, masayı ve tutarı adlandırır. Yönetici onayı gerekirse "yönetici onayına gönderildi" bilgisi tek yerde gösterilir ve aynı istek aynı anahtarla yeniden gönderilince onaylanmış talep uygulanır.

Kalem notu ve Beklet için sunucu sözleşmesi bu görevin doğrulamasında eksik bulunduysa yapılmaz; eksiğin tam yeri `evidence/V1-RMD-1160/CONTRACTS.md` ve `RESULT.md` içinde yazılır.

## Owned surface

- `evidence/V1-RMD-1160/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/contracts.ts
  - src/Clients/PosTerminal/src/api.ts
  - src/Clients/PosTerminal/src/api.test.ts
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (`clientBuildGate.js`) exit code 0

## Out of scope

- Host kodu ve sözleşmeleri (kalem notu alanı, park/taslak sözleşmesi dahil)
- Vanilla Cashier ve WaiterPwa istemcileri
- Servis edilmiş kalemin iptali (sunucu bu uçta herkese reddeder)
- Serbest metin iptal notu alanı

## Dependencies

- V1-RMD-1159

## Acceptance evidence

1. Üç istemci talebinin sunucu sözleşmesi ve yetki doğrulaması (`CONTRACTS.md`), kırmızı test, test çıktıları, mutant listesi
2. İki ardışık bağımsız qa-* incelemesi
3. `node tools/client-build-gate/clientBuildGate.js --base <başlangıç commit'i>` exit code 0

## Handoff

- None
