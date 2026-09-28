# V1-RMD-399 — Yetkilendirme hedefli derin denetimi

Tarih: 2026-09-28. Taban: `b1973f35` (görevin InProgress commit'i). Üretim kodu değiştirilmedi.

Yöntem V1-RMD-393/398 ile aynıdır: kapsam matrisi koddan önce yazıldı (`coverage-matrix.md`, commit `a6859e23`),
plan kararları kod okunmadan önce tarandı, her matris satırı gerçek sunucu + gerçek PostgreSQL üzerinde çalışan bir
probe ile sınandı, sonuç kör kalibrasyon, mutasyon denetimi ve bağımsız çürütme ile sınandı.

## 1. Karar taraması (bulgu sayılmayanlar)

Kod okunmadan önce `docs/domain/authorization-model.md`, `V1-IAM-001..031`, `docs/engineering/authz-wave-remediation-plan.md`
ve `docs/audit/INDEPENDENT_DEEP_AUDIT_2026-09-26.md` tarandı. Aşağıdakiler bilinçli karar ya da daha önce kapatılmış
bulgudur; probe'lar bunları yalnız geri dönüş (regresyon) açısından sınar:

| Konu | Kaynak | Denetimdeki durum |
| --- | --- | --- |
| Çevrimdışı bütçe imzalı JWT değil, sunucuda tutulan satır | V1-IAM-022 "Tasarım sapması" | Karar; bulgu değil. |
| Delegasyon süresi arka plan işi yerine sorgu süzgeciyle biter | V1-IAM-021, model §1 | Karar; bulgu değil. |
| Kendi grant isteğini / kendi davranışsal kısıtını onaylama yasağı | V1-RMD-316 (K9) | Düzeltilmiş; C1, C5 geçti. |
| Çevrimdışı mutabakatta başkasının `budgetId`'si | V1-IAM-025 bağımsız denetim notu | Düzeltilmiş; C7c geçti. |
| Grant tekrar oynatmada tutar/sebep/konu eşleşmesi | `AuthorizationGrantService.MatchesReplay` | Düzeltilmiş; kod okundu. |
| Müşteri ekranı eşleştirme ve ekran oturumu iptali yalnız oturum ister | V1-IAM-024 In scope | Karar; D2 izin listesinde. |
| NFC siparişi oturumsuz, doğrudan `Accepted` | V12-NFC-001 Goal ("restoranın yerel ağı üzerinden") | Karar; güven sınırı yerel ağdır. |
| Yönetim uçlarının çoğunun istemcisi yok | K10 / V1-RMD-296 (Planned) | Bilinen; bu denetimin konusu değil. |

## 2. Uç nokta envanteri

`endpoint-inventory.tsv`: uygulamanın uç nokta kaynağından makinece çıkarılan 321 rota×yöntem satırı; her satırda
oturumsuz ve izinsiz oturumlu isteğin gerçek HTTP sonucu ve sınıfı vardır.

- Oturumsuz (D1): 292 özel rota 401/403 döndü; 21 kamusal rota (sağlık, giriş, ekran eşleştirme, QR, NFC, imzalı
  platform webhook'ları, `/api/{**path}` 404 geri dönüşü) tasarım gereği açıktır; ilk turda ulaşılamayan 8 rota
  ikinci turda iyi biçimlenmiş istekle 401 döndü. **Oturumsuz ulaşılabilen özel rota yok.**
- İzinsiz oturum (D2): 186 mutasyon rotasından 161'i 401/403 döndü; 13'ü tasarım gereği yalnız oturum ister
  (kaynaklarıyla probe'da listeli); 7'si ikinci turda 403 döndü. **Kalan 8 rota koruma kontrolünü geçti**: kasa
  oturumunun 7 ucu (bkz. H-02) ve kartla tahsilat ucu (bkz. Q-01).
