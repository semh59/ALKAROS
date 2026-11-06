# V1-RMD-866 - Uzlaşma vakası geçmişinde işlemi yapanın adı; durum geçişinden sonra not alanı temizlenir

- Task ID: V1-RMD-866
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G10 (F0248, F0746): (1) Vaka geçmişi tablosu "kimin ne zaman ne yaptığını" göstermesi gerekirken yalnız eylem türü, zaman ve ayrıntıyı gösterir; sunucu PerformedBy kimliğini döndürür ama istemci modeli (CaseAction) alanı atar ve kimlik zaten kişi adı değildir. Sunucu eylem yanıtına işlemi yapanın görünen adını ekler (identity.users display_name; bulunamazsa boş), istemci adı gösterir; tarama sistem kimliğiyle yazılan eylemler "Sistem", adı çözülemeyenler "Bilinmiyor" görünür. (2) Vaka bölümünde tek paylaşılan not alanı, durum geçişi başarılı olunca temizlenmez; yazılan not sonraki vakanın geçişine yanlışlıkla gider. Geçiş başarılı olunca not alanı boşaltılır (hata durumunda korunur, yazılan not kaybolmaz).

## Owned surface

- `evidence/V1-RMD-866/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Reconciliation/ReconciliationCaseEndpoints.cs — eylem yanıtında ad.
  - tests/Host/Experience/Reconciliation/ReconciliationCaseHttpTests.cs — ad testi.
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.tsx — sütun ve not temizliği.
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.test.tsx — istemci testleri.
  - src/Clients/PosTerminal/src/features/management-closing/models.ts — CaseAction alanları, etiket.
  - src/Clients/PosTerminal/src/strings.ts — sütun başlığı ve sistem etiketi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Sunucu: CaseActionV1 yanıtına PerformedByName eklenir; adlar tek sorguyla çözülür.
2. İstemci: geçmişte "İşlemi yapan" sütunu; sistem kimliği "Sistem", çözülemeyen "Bilinmiyor".
3. Not alanı yalnız geçiş başarılı olunca temizlenir; testle sınanır (başarılı geçiş temizler, hata temizlemez).
4. Mutasyon: ad sorgusu, sütun, temizleme çağrısı, hata durumunda temizleme (cp+cmp ile geri alınır).

## Out of scope

- Not ekleme düğmesinin mevcut davranışı (zaten başarıda temizler).
- Vaka kaydı kendi alanları ve diğer ekran metinleri.

## Dependencies

- None

## Acceptance evidence

1. Host Reconciliation testleri gerçek Postgres (alkaros-test-pg, 55432) ve PosTerminal ilgili vitest dosyaları, tsc ve oxlint exit 0; çıktılar `evidence/V1-RMD-866/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
