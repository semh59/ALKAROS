# V1-RMD-881 sonucu

Belge görevi; kod, durum, karar ve kabul koşulu değişmedi. Bulgular: F0089 (V13-GOV-005), F0090 (V13-GOV-007).

- Üç dosyaya "Araştırma notu (2026-10-17)" eklendi (plan kayıtlarında Out of scope maddesi, docs dosyasında sonda bölüm) (`git diff --numstat`: 10+6+7 ekleme, 0 silme):
  `plan/v1.3/governance/V13-GOV-005-...md`, `docs/domain/token-integration-path-decision.md`, `plan/v1.4/qnb-esolutions/V13-GOV-007-...md`.
- Kaynak: 2026-10-17 dış bağımlılık araştırması; repodaki kayıtlı hâli V1-RMD-584 notlarıdır (V0-HUG-001, V13-FSC-005, V0-QNB-001).
- Token: "polling varsayılan, webhook gerekmez" ve "300 TR / X30 TR Cloud" ifadeleri Token HoReCa sayfasıyla (webhook zorunlu, Cloud yalnız X30TR) çelişiyor; tünel rotası gereksinimi işaretlendi.
- QNB: V13-FSC-005 NotApplicable gerekçesi GİB e-Adisyon özel entegratör listesiyle (QNB var, Token yok) çelişiyor.
- Doğrulanamadı olarak yazıldı: `belgeOid` ile `return` alanı ve `gonderenEtiket` (kod kütüphanesi "pasiftir" diyor); polling'in Token'da desteklenip desteklenmediği.
- F0099, F0457 ve kod dosyalarına dokunulmadı.

## Ürün kararı gerektirenler (Semih'e sorulmadı)
1. Token: webhook'lu Cloud + yeni tünel rotası mı, kablolu/uygulama içi yol mu.
2. V13-FSC-005 NotApplicable kararının yeniden değerlendirilmesi (ürün sahibi/mali müşavir).
