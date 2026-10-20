# Doğrulayıcı kararı (qa-bulgu-dogrulayici, Opus, HEAD c41c1eec)

26 ham-tekil bulgunun tamamı kodla yeniden açıldı; sanitizer iddiaları (F02, F03, F05-F08) probe ile yeniden üretildi (probe: tmp/qa/V1-RMD-595/qa-bulgu-dogrulayici/probe, yerel; commit edilmedi).
Sonuç: 15 DOĞRULANDI, 11 KISMEN, 0 tamamen ÇÜRÜTÜLDÜ (F17'nin "çift iz" kısmı ve F24'ün "hata yolu testi yok" ile "yedek betiği" kısımları çürütüldü). 2 yeni bulgu (Y1, Y2).

| ID | Karar | Önem | Not |
|---|---|---|---|
| F01 | DOĞRULANDI | Yüksek | 27 AppendAsync noktasının 25'i iş commit'inden sonra ayrı bağlantıda (13'ü parayla ilgili); void/comp/sent-void için işlem içi ikinci kayıt zaten var (Y1). |
| F02 | DOĞRULANDI | Düşük | Çalıştırıldı; bugünkü üretim girdileri tetikleyemez (gizli kusur). |
| F03 | DOĞRULANDI | Düşük | Çalıştırıldı; üretimdeki iki occurredAt kaynağı UTC. |
| F04 | KISMEN | Orta | Mekanizma doğru; ölçülen süre daha düşük (44 ms ilk, ~135 ms ort.). |
| F05 | DOĞRULANDI | Orta | 13 anahtarın 13'ü redakte edilmedi (çalıştırıldı). |
| F06 | DOĞRULANDI | Düşük | |
| F07 | DOĞRULANDI | Düşük | |
| F08 | DOĞRULANDI | Düşük | Ek: `token: Bearer eyJabc` JWT kısmı sızıyor. |
| F09 | DOĞRULANDI (yapı) | Düşük | 6 ham INSERT sanitizer'sız; kişisel veri taşıdığı doğrulanamadı. |
| F10 | DOĞRULANDI | Orta (karar) | username 10 yıl saklanıyor. |
| F11 | DOĞRULANDI | Orta | İfade düzeyi tetikleyici yok, aynı rol, down DROP CASCADE (kod okuması; canlı deneme güvenlik freniyle engellendi). |
| F12 | KISMEN | Düşük | Kimlik yok doğru, "yetki yok" abartılı. |
| F13 | KISMEN | Düşük | |
| F14 | DOĞRULANDI | Düşük | |
| F15 | KISMEN | Düşük | Store bölümlü şemada Host testlerinde dolaylı test ediliyor. |
| F16 | KISMEN | Orta | Mutant sayıları doğrulanamadı; test boşlukları kod okumasıyla doğru. |
| F17 | KISMEN | Düşük | "Yeniden denemede çift iz" çürütüldü (aynı (id, occurred_at) PK ihlali verir). |
| F18 | DOĞRULANDI | Düşük | |
| F19 | DOĞRULANDI | Düşük | |
| F20 | DOĞRULANDI | Düşük | |
| F21 | KISMEN | Düşük | İndeks kullanım iddiası çıkarım. |
| F22 | KISMEN | Orta (karar) | Kod içi tutarsızlık doğrulandı; yasal süre iddiaları doğrulanamadı. |
| F23 | DOĞRULANDI | Düşük | Doküman sapması. |
| F24 | KISMEN | Orta | Uyarı yolu, master key, WAL budama, kurtarma sonrası adım eksikleri doğrulandı. |
| F25 | DOĞRULANDI | Düşük | |
| F26 | KISMEN | Düşük | Tarihsel; kanıt görev dosyalarında. |
| Y1 | yeni | Düşük | Void/comp/sent-void için çift denetim satırı (işlem içi + sonrası). |
| Y2 | yeni | Düşük | Konsol özeti ile olaydaki satır sayısı farklı olabilir (iki ayrı sayım). |

Düzeltme yan etkileri (doğrulayıcı): F02 düzeltmesi regex yedek yoluna düşürür (F07/F08 geçerli olur); F05 genişletmesi F06 yanlış pozitiflerini artırır; F01 çözümü F09 ham INSERT'leri de store'a taşımalı; F01 sonrası Y1'deki ikinci kayıt kaldırılmalı.
