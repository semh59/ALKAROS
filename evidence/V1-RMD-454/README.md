# V1-RMD-454 - kabul kanıtı

- Sorun: master'a birleştirme push'unda `Task scope enforcement` yalnız head commit mesajındaki Task ID'lere bakıyordu;
  #14 birleştirmesinde (koşu 36572194262) yalnız V1-RMD-452 bulundu, farktaki V1-RMD-453 dosyaları kapsam ihlali sayıldı.
- Düzeltme (`7263d205`): push olayında ID'ler `github.event.commits[*].message` ile head commit'ten birlikte toplanır.
- Kanıt: master push koşusu 36607400298'de `Task scope enforcement` işi başarılı (aynı düzeltmeden önceki #15 birleştirme
  koşusu 36601629073'te aynı iş kırmızıydı).
- Aynı koşuda `Validate plan, PDF trace and project manifest` adımı düştü: manifest ile `plan/AUDIT_REPORT.md` arasında
  1 satır / 159 bayt sapma (manifest, audit raporundan önce üretilmişti). Bu görevin kapanış commit'inde önce
  `generate-audit-report`, sonra `generate-manifest` çalıştırılarak giderildi; `validate` ve `verify-manifest` 0 hata.
- Sınır: GitHub push olayı en fazla 20 commit mesajı taşır; 20'den uzun bir PR birleşirse aynı hata çıkabilir.
