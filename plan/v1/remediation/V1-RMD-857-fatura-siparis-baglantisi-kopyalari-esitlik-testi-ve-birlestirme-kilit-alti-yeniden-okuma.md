# V1-RMD-857 - Fatura-sipariş bağlantısının üç SQL kopyası eşit kalır; birleştirmede kilit altı yeniden okuma yarışı sınanır

- Task ID: V1-RMD-857
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9/G11: (1) 'bu fatura bu siparişin' koşulu (birleştirilmiş faturada order_id boştur, siparişler kalemlerden bulunur) modül sınırı yüzünden üç yerde kopyadır: Billing/BillFoundation/BillOrderLink.Matches, Tables/TableBillOrderLink.Matches ve Reporting/BusinessDayTotals/BusinessDayTotalsReader içindeki iki satır içi kopya. Biri değişirse diğerleri sessizce ayrışır ve birleştirilmiş faturalı siparişler masa, rapor ya da iptal yolunda yanlış davranır; kopyaları denetleyen test yok. Modüller birbirine başvuramadığı için tek kaynağa indirgeme mümkün değil; yerine Host testi üç kopyanın aynı koşulu taşıdığını sınar. (2) Birleştirme, siparişleri kilitleyip tekrar kontrol eder (LockAndCheckOrdersAsync: iptal, başka masa, kasaya gönderilmiş), ama bu kilit altı yeniden okuma yarış halinde hiç sınanmamış. Kilit tutulurken iptal edilen siparişin faturalanamadığı sınanır.

## Owned surface

- `evidence/V1-RMD-857/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Billing/BillOrderLinkCopiesTests.cs — üç kopyanın eşitliği (yeni dosya).
  - tests/Host/Experience/Billing/MergeOrdersHttpTests.cs — birleştirme kilit altı yeniden okuma yarışı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Eşitlik testi: Billing'in genel Matches çıktısı kanonik sayılır; Tables'ın iç sınıfı yansıtma ile, Reporting'in SQL sabiti yansıtma ile okunur; takma adlar kanonik biçime çevrilip boşluklar sadeleştirilerek karşılaştırılır.
2. Yarış testi deterministik: test bağlantısı bir siparişin satırını kilitleyip iptal eder (commit edilmemiş), birleştirme isteği bekler, commit sonrası birleştirme reddedilir ve fatura oluşmaz.
3. Mutasyon: Tables ve Reporting kopyalarında bir koşul değiştirilir; kilitte FOR UPDATE ve durum denetimi kaldırılır (cp+cmp ile geri alınır).

## Out of scope

- Üretim kodu; kopyaların tek kaynağa indirilmesi (modül sınırı).
- Tek sipariş iptalinin kardeş siparişleri kasa kuyruğuna bildirmesi (V1-RMD-858).

## Dependencies

- None

## Acceptance evidence

1. Billing host testleri gerçek Postgres (alkaros-test-pg, 55432) ile exit 0; çıktılar `evidence/V1-RMD-857/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
