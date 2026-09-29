# V1-RMD-398 - Stok/reçete akışı hedefli derin denetimi (reçete → porsiyon rezervasyonu → satış tüketimi → iptal/iade → fire/sayım → üretim → satın alma → maliyet)

- Task ID: V1-RMD-398
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

`Done` işaretli stok/reçete akışını V1-RMD-393 ile aynı yöntemle, gerçek PostgreSQL üzerinde çalışan probe
testleriyle denetlemek: reçete sürümü → siparişte porsiyon rezervasyonu → mutfağa gönderim/servis ile tüketim →
iptal/void/comp ile iade → fire ve fiziksel sayım → üretim partisi → satın alma teslim alma → maliyet anlık
görüntüsü ve stok bakiyesi. Her iddia çalıştırılmış kanıta bağlanır; üretim kodu değiştirilmez, bulgular ayrı
düzeltme görevleri olarak raporlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-398-stock-recipe-flow-targeted-audit.md`
- `evidence/V1-RMD-398/**`

## In scope

- Stok invariant'larının yazılı listesi (ör. bakiye = hareket defterinin toplamı; rezervasyon hiçbir zaman
  kullanılabilir miktarı aşmaz; iptal edilen rezervasyon veya void edilen satış tüketimi tam olarak geri verir;
  birim dönüşümü ve fire faktörü C9 sırasıyla uygulanır; sayım farkı defterde görünür).
- Akış adımı × boyut kapsam matrisi (mutlu yol, tekrar/idempotency, eşzamanlılık, kardinalite, yanlış durum,
  yetki, hata yolu/atomiklik); her hücre sağlam/bulgu/denetlenemedi olarak kapatılır.
- Kör kalibrasyon (bağımsız tohumlanmış hata, mühürlü açıklama), mutasyon denetimi ve bulguların bağımsız
  çürütülmesi.

## Out of scope

- Üretim kodu, test projeleri, migration veya başka görev dosyası değişikliği.
- Para akışı (V1-RMD-393), yetkilendirme denetimi (ayrı görev), dış entegrasyonlar.

## Dependencies

- V1-RMD-393
- V1-RMD-394

## Acceptance evidence

- `evidence/V1-RMD-398/REPORT.md`: invariantlar, kapsam matrisi, bulgular (dosya:satır, yeniden üretim, probe
  adı, doğrulayıcı sonucu), mutasyon ve kalibrasyon sonuçları, denetlenemeyenler.
- `evidence/V1-RMD-398/probes/`: probe kaynakları ve gerçek koşu çıktıları.
- Semih'in elle deneyebileceği senaryo: her bulgu için rapordaki yeniden üretim adımları.

## Handoff

- None
