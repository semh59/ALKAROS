# V1-RMD-1177 - Ödeme öncesi Hesap ön fişi: "BİLGİ FİŞİ" başlığı, "MALİ DEĞERİ YOKTUR" ibaresi, tarih ve saat, ÖKC fişi gibi görünmeme

- Task ID: V1-RMD-1177
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Mali mevzuat araştırması (Soru 2, ön hesap fişi) sonucu: ÖKC bilgi fişlerinde kullanılan ibare "BİLGİ FİŞİ" ve "MALİ DEĞERİ YOKTUR"dur; ön hesap fişimizdeki "Bu belge mali fiş değildir." hiçbir kaynakta zorunlu değildir; adisyon niteliği taşıyabilen çıktıda basım saati önemlidir; fiş ÖKC fişi görünümü taşımamalıdır. Ödeme öncesi Hesap ön fişi (`preReceipt.ts`) için:

1. Başlık "BİLGİ FİŞİ", ibare "MALİ DEĞERİ YOKTUR" olur ("Bu belge mali fiş değildir." bu ibareye alınır).
2. Fişe basım tarihi ve saati ayrı etiketli satırlarla (Tarih, Saat) yazılır; saat işletme saat dilimindedir.
3. Numara satırı "Hesap fişi no: N" yerine "HESAP NO: N" olur: ÖKC fişindeki "FİŞ NO" ile karışmaz, adisyon/hesap numarası gibi okunur. Sayaç, uç ve numara değeri değişmez; fişte mali sembol, sicil, Z no, KDV dökümü yoktur.
4. `docs/domain/bill-pre-receipt.md` yeni ibareyle güncellenir; mali müşavir teyit notu (ibare ve e-Adisyon) korunur.

Ödenmiş hesabın fişi (`split-payment.js`, V1-RMD-887) ve sunucu sayfa şablonu (sunucu yalnız veri döner, fiş metni istemcide çizilir) değişmez.

## Owned surface

- `evidence/V1-RMD-1177/**`
- `docs/domain/bill-pre-receipt.md`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/pending-checks/preReceipt.ts
  - src/Clients/PosTerminal/src/features/pending-checks/preReceipt.test.ts
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (tarayıcı ön fiş çıktısı testi), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. PosTerminal ilgili testleri ve istemci üretim derlemesi exit code 0

## Out of scope

- Ödenmiş hesabın fişi ve "Bu belge mali fiş değildir." satırı orada; sunucu ucu, sayaç, migration; e-Adisyon ve adisyon-zorunlu işletme bayrağı

## Dependencies

- V1-RMD-1158

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; ad kararının gerekçesi
2. Plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
