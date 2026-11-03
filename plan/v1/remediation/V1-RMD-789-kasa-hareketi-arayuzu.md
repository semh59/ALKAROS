# V1-RMD-789 - Kasa hareketi arayüzü: zorunlu açıklama, onay özeti, onay bekleyen çıkışlar, kaybolmayan taslak, sıfır sayım uyarısı

- Task ID: V1-RMD-789
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin 18. ve 19. satırları (koddan doğrulandı) ile 1.020 kayıtlı programdan UI0040/UI0041/UI0042 (yeniden çizim tutar ve açıklamayı siler), UI0204'ün kasa oturumu sayfasındaki dil tutarsızlıkları ve F0915'in bu sayfadaki sekme parçası kapanır. V1-RMD-788 sunucu kurallarını (zorunlu gerekçe, kasa üst sınırı, eşik üstü yönetici onayı, kendi onayı ayarı) kasa oturumu sayfasına (`cash-session.js`) taşır:

1. Nakit hareketi formu: açıklama zorunlu alan ("Açıklama", isteğe bağlı ibaresi yok); boşsa sunucuya gitmeden Türkçe uyarı.
2. Onay özeti ekranı: kaydetmeden önce yön, tutar, açıklama, kasadaki beklenen nakit ve (çıkışta eşiği aşıyorsa) "yönetici onayı gerekecek" uyarısı gösterilir; "Onayla ve kaydet" / "Düzelt".
3. Başarıdan sonra bildirim ("Çıkış kaydedildi" ya da "Yönetici onayı bekliyor") ve oturumun hareket listesi (kim, saat, yön, tutar, açıklama, onaylayan, "kendi onayı" etiketi) açık oturum ekranında.
4. Onay bekleyen çıkışlar bölümü: `cash.session.override` izni olan kullanıcıya Onayla/Reddet (talebi kendisi açtıysa ve kendi onayı ayarı kapalıysa Onayla yok, nedeni yazılı), talep sahibine Vazgeç.
5. Satır 18: Giriş/Çıkış sekmeleri kaldırılır (yön, açık oturum ekranındaki "Nakit Giriş"/"Nakit Çıkış" düğmeleriyle yalnız bir kez seçilir; sekmeye basınca tutar ve not silinmesi ve yönün iki kez seçilmesi ortadan kalkar). Tutar, not, sayım alanları ve süpervizör açıklaması yeniden çizimde (meşgul durumu, hata) korunur (UI0040-42).
6. Satır 18: sıfır sayım (kupür toplamı ya da tek tutar 0 ₺) sorgusuz kabul edilmez; "Sayım 0 ₺ - çekmece boş mu?" onayı istenir.
7. Satır 19: kapanış ağ hatasında "kapanmış olabilir" uyarısıyla kasa durumu yeniden okunur; beklenen tutar okunamadıysa "Kasayı Kapat" kapalı ve "Beklenen tutarı yeniden oku" düğmesi vardır.
8. Sunucu hata kodları (yeni `CASH_OUT_EXCEEDS_DRAWER`, `CASH_MOVEMENT_REASON_INVALID`, `CASH_MOVEMENT_REQUESTS_PENDING`, `CASH_MOVEMENT_SELF_APPROVAL`, `CASH_MOVEMENT_REQUEST_STATE`, `IDEMPOTENCY_KEY_REUSED` ve mevcut kasa kodları) Türkçe sözlükten gösterilir; sunucunun ham mesajı ekrana basılmaz (`docs/UI_STYLE_GUIDE.md`).
9. Yeniden deneme anahtarı artık açıklamayı da içerir (sunucu tekrarda açıklamayı karşılaştırır): yön, tutar ya da açıklama değişirse yeni anahtar, aynıysa aynı anahtar.
10. Yönetici ayarları ekranı iki yeni ayarı Türkçe adla gösterir ve düzenletir: "Nakit çıkış onay eşiği" (TL tam sayı), "Yönetici kendi talebini onaylayabilir" (aç/kapat).
11. Kasa hareketi E2E senaryoları gerekçeli çıkışa ve onay özetine uyarlanır (kural gevşetilmez).

Kapsam dışı: React kasa kabuğunda kasa bağlantısı (satır 25), `/active` hata yolları (satır 16), stil (satır 17), dokunma hedefleri (satır 23, G8) ve ek: kupür girdisi ölçüleri.

## Owned surface

- `evidence/V1-RMD-789/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/cash-session-movement.test.js (V1-RMD-109) — yeni dosya: kasa hareketi arayüz testleri.
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js (V1-RMD-241) — form, onay özeti, liste, onay bölümü, taslak, sıfır sayım, hata sözlüğü.
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css (V1-RMD-377) — liste, özet ve uyarı stilleri.
  - src/Clients/PosTerminal/src/features/management-settings-history/models.ts (V1-RMD-684) — iki yeni ayarın adı, kuralı ve değer biçimi.
  - src/Clients/PosTerminal/src/features/management-settings-history/SettingsHistorySection.test.tsx (V1-RMD-684) — yeni ayarların etiketi.
  - tests/E2E/Cashier/specs/05-cash-session-lifecycle.spec.js (V1-RMD-756) — gerekçeli hareket, onay özeti, sıfır sayım.
  - tests/E2E/Cashier/specs/06-cash-session-conflict.spec.js (V13-PUI-002) — uyum gerekirse.
  - tests/E2E/Cashier/specs/21-accessibility-keyboard-and-labels.spec.js (V1-RMD-356) — alan etiketleri.
  - tests/E2E/Cashier/specs/24-cash-session-accessibility.spec.js (V1-RMD-361) — sekme kaldırılınca erişilebilirlik.
  - tests/E2E/Cashier/specs/28-cash-session-denomination-count.spec.js (V1-RMD-377) — sıfır sayım onayı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her davranış için önce kırmızı test (vitest + jsdom, `tests/Clients/StaticApps`), sonra düzeltme, sonra yeşil.
2. E2E senaryoları gerçek tarayıcıda koşar (kasa hareketi, sıfır sayım); koşulamazsa nedeni RESULT'ta yazılır.

## Out of scope

- Backend (V1-RMD-788); satır 16, 17, 23, 25.

## Dependencies

- V1-RMD-788

## Acceptance evidence

1. Kırmızı sonra yeşil: StaticApps vitest, PosTerminal vitest (ayarlar), lint ve tsc.
2. Mutasyon: her davranış bozulunca en az bir test kırılır; kaynak `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0; İngilizce sızıntı taraması (`docs/UI_STYLE_GUIDE.md` §4) temiz.
4. qa-frontend, qa-test-kalitesi ve qa-ux-tasarim incelemeleri sırayla yapılır, bulgular giderilir.
5. Semih'in elle deneyeceği senaryo: kasa açıkken Nakit Çıkış, tutarı yaz, açıklamayı boş bırak, uyarı gör; açıklamayı yaz, özeti gör, onayla; eşiğin üstünde "yönetici onayı bekliyor" çıkar, yönetici girip onaylar; sıfır sayımda "çekmece boş mu?" sorulur.

## Handoff

- None
