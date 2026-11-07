# V1-RMD-872 - Ok tuşuyla sekme gezinmesinin telemetride sayımı doğrulanır ve sözleşme olarak sabitlenir

- Task ID: V1-RMD-872
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kodda doğrulandı: moveBetweenTabs (design-system/primitives.tsx), ok tuşunda yeni sekmeye odaklanır ve next.click() çağırır; etkileşim telemetrisi click olaylarını yakaladığı için (installInteractionTelemetry.ts: sentetik tıklama ayrımı yok) ok tuşuyla seçilen her sekme bir 'click' kaydı üretir. Bu doğrudur: ok tuşu sekmeyi gerçekten seçer (otomatik etkinleştirme deseni), yani tıklamayla eşdeğer bir kullanıcı eylemidir ve tıklayan kullanıcıyla aynı anahtar/etkileşim sayısıyla görünür. Odak değiştirmeyen tuş basışları (değiştirici tuşlu ok) kayıt üretmemelidir. Davranış hiçbir testle sabit değil; bir sonraki değişiklikte sessizce değişmesin diye test eklenir. Üretim kodu değişmez.

## Owned surface

- `evidence/V1-RMD-872/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/interaction-telemetry/installInteractionTelemetry.test.ts — ok tuşu sekme gezinmesi testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Test: sekme listesinde ArrowRight tam bir click kaydı üretir (yeni seçilen sekmenin anahtarıyla), değiştirici tuşlu ArrowRight kayıt üretmez.
2. Mutasyon: moveBetweenTabs içindeki next.click() kaldırılırsa test düşer (cp+cmp geri alınır).

## Out of scope

- Üretim kodu değişikliği; sentetik tıklamayı ayıran yeni telemetri kuralı.

## Dependencies

- None

## Acceptance evidence

1. PosTerminal interaction-telemetry testleri exit 0; çıktılar evidence/V1-RMD-872/.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
