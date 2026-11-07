# V1-RMD-875 - Çevrimiçi sipariş taraması açık bir vakaya her turda Deduplicated eylemi yazmaz

- Task ID: V1-RMD-875
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-864 ödeme kaynaklarında, vakası olan kaydı sorgudan eleyerek her 5 dakikalık turda açık vakaya 'Deduplicated' eylem satırı yazılmasını önledi. Çevrimiçi sipariş kaynaklarında aynı eleme sorguda yapılamaz: OnlineOrderReconciliationActions.ResolveAsync vakayı çözmeden önce kaynağı yeniden tarar ve vaka anahtarı hâlâ tespit ediliyorsa StillDiverged verir; açık vakaları taramadan çıkarmak her çözümü yanlışlıkla izinli yapardı. Bu yüzden eleme tarayıcıda yapılır: OnlineOrderReconciliationScanner.ScanAllAsync, anahtarı için etkin (Open/Investigating/Escalated) vaka zaten olan farkı tekrar vaka servisine göndermez (kaynak taraması ve çözüm denetimi aynen kalır). Böylece açık vaka başına günde 288 gereksiz eylem satırı birikmez.

## Owned surface

- `evidence/V1-RMD-875/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reconciliation/OnlineOrders/OnlineOrderReconciliationScanner.cs — etkin vakası olan farkı atla.
  - tests/Modules/Reconciliation/OnlineOrders/NotHandedOverSourcePairTests.cs — tekrar taramada eylem satırı testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: açık vakası olan fark üç taramadan sonra yalnız 'Created' eylemine sahip, 'Deduplicated' yok; kaynak sonucu sayımı (DiscrepancyCount) değişmez; vaka çözülünce StillDiverged denetimi çalışmaya devam eder (mevcut testler).
2. Mutasyon: atlamanın kaldırılması ve atlamanın yalnız Open durumuna bakması (cp+cmp geri alınır).

## Out of scope

- Ödeme kaynakları (V1-RMD-864'te yapıldı); Resolved vakaların yeniden okunması (kaynak sorguları değişmez).

## Dependencies

- None

## Acceptance evidence

1. Reconciliation.OnlineOrders ve Host Reconciliation testleri gerçek Postgres ile exit 0; çıktılar evidence/V1-RMD-875/.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
