# V1-RMD-1182 - Harici POS kartı: pilot belgeleri (hızlı başlangıç, gün kontrol listesi, geri bildirim formu) ve ÖKC/mali müşavir notu

- Task ID: V1-RMD-1182
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: documentation
- Surface state: Planned

## Goal

Pilotta kart terminali bağlı değilken kartla ödemeyi kasiyerin nasıl kaydedeceği ve akşam nasıl mutabık kalacağı yazılır; her ekran adı ve düğme metni V1-RMD-1181 sonrası koddan doğrulanır. `docs/operations/pilot/kasiyer-hizli-baslangic.md` gerçek düğme metniyle "Harici POS kartı" adımını (fiş numarası, kart numarası yazılmaz, aynı fiş iki kez girilmez) ekler; `pilot-gunu-kontrol-listesi.md` akşam adımı "kart fişleri toplamını banka POS gün sonu ile karşılaştır" (ALKAROS gün kapanışındaki "Harici POS kartı" satırı ile) ekler; `pilot-geri-bildirim-formu.md` kart satırı ekler; `elle-satis-defteri.md` ile çelişki varsa düzeltilir. Yeni `docs/operations/pilot/harici-pos-karti-okc-notu.md`: bu ödeme türü mali fiş/ÖKC fişi yerine geçmez, satış belgesi ayrıca düzenlenir, mutabakat elle yapılır; `kasa-review/36-mali-mevzuat-arastirmasi.md` bulguları (ÖKC kapsamındaki işletmede satış yazılımının YN ÖKC'ye bağlı olması, belgesiz tahsilatın cezası, ön fiş mali fiş değildir, doğrulanamayan noktalar) bağlanır; hukuki görüş verilmez; mali müşavire tek net soru yazılır. `docs/README.md` dizinine bağlantı eklenir.

## Owned surface

- `evidence/V1-RMD-1182/**`
- `docs/operations/pilot/harici-pos-karti-okc-notu.md`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/operations/pilot/kasiyer-hizli-baslangic.md
  - docs/operations/pilot/pilot-gunu-kontrol-listesi.md
  - docs/operations/pilot/pilot-geri-bildirim-formu.md
  - docs/operations/pilot/elle-satis-defteri.md
  - docs/README.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Yukarıdaki belge değişiklikleri; her düğme metni, ekran adı ve sayının koddaki kaynağını gösteren doğrulama listesi
2. İki bağımsız qa incelemesi (qa-dokuman-bagimlilik, qa-ux-tasarim)
3. plan-audit validate ve consistency-audit temiz

## Out of scope

- Kod, ekran metni, şema; hukuki görüş; mali müşavir onayı (belge, onay gerektiğini yazar)

## Dependencies

- V1-RMD-1181

## Acceptance evidence

1. Doğrulama listesi (belge → kod/belge kaynağı)
2. plan-audit ve consistency-audit çıktıları
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
