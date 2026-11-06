# V1-RMD-860 - Yaşayan belge sapmaları kodla hizalanır ve eski Done görev kayıtlarının güncel karşılıkları tek kayıtta toplanır

- Task ID: V1-RMD-860
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (belge-kod sapmaları): (1) bill-order-cardinality.md bileşik benzersizliği (bill_id, order_item_id) diyor, kod (bill_id, bill_item_id) kullanır ve order_item_id benzersizliği yalnız superseded_at boş satırlarda geçerlidir; (2) kasa-faz0-redesign-requirements.md V1-CUI-008/009 için Planned diyor, ikisi de Done; (3) authorization-model.md 97-98 'sabit indirim merdiveni yok' diyor, aynı belgenin 81 ve 152. satırları garsonun 100 TL'ye kadar indirimi yöneticisiz onayladığını söyler (2026-10-21 ürün sahibi kararı); (4) tests/E2E/Cashier/README.md dört senaryo sayıyor, dizinde 32 spec var; (5) plan/GATES.md 273 'bu 8 görevin' diyor, tablo 4 tüketici ve 12 bağımlılık çiftidir. Ayrıca eski Done görev md ve kanıt dosyalarındaki tarihsel sapmalar (kayma satır numaraları, eski sayılar, eksik kanıt klasörleri) bu görevin evidence/V1-RMD-860/HISTORICAL.md dosyasında kayıt başına güncel kod karşılığıyla listelenir; Done görev md dosyaları değiştirilmez.

## Owned surface

- `evidence/V1-RMD-860/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/domain/bill-order-cardinality.md — bileşik benzersizlik ve superseded_at koşulu.
  - docs/engineering/kasa-faz0-redesign-requirements.md — V1-CUI-008/009 durumları.
  - docs/domain/authorization-model.md — indirim merdiveni cümlesi.
  - tests/E2E/Cashier/README.md — spec sayısı notu.
  - plan/GATES.md — bağımlılık feragati tablosu cümlesi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her düzeltme önce kodla doğrulanır; belge koda uydurulur, kod değişmez.
2. Eski görev md dosyaları değiştirilmez; güncel karşılıklar HISTORICAL.md içinde dosya:satır ile verilir.
3. Plan ve tutarlılık denetimleri sıfır hata sıfır uyarı.

## Out of scope

- Kod ve migration; plan-audit sabitleri; Done görev md dosyaları; Token/QNB karar belgeleri.

## Dependencies

- None

## Acceptance evidence

1. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
2. Her düzeltmenin kod kanıtı evidence/V1-RMD-860/RESULT.md içinde dosya:satır olarak listeli.
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
