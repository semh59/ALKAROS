# Haiku 5.5 iş planı (mekanik işler) ve kontrol kuralı

Kural: Haiku yalnızca mekanik, kuralı net işleri yapar; karar, kod düzeltmesi ve "bu gerçek hata mı" yargısı Sonnet'te kalır. Her teslimi ben sayı ve örnek karşılaştırmasıyla doğrularım; doğrulanmayan teslim kabul edilmez. En çok 2 ajan eşzamanlı.

| # | İş | Girdi | Çıktı | Kontrol |
|---|---|---|---|---|
| H1 | Düşük bulguları çıkar (B001-B070) | tasks/B001..B070.md | findings/low-A.md | satır sayısı = kartlardaki `[Düşük]` sayısı; 10 rastgele satırı karta karşı doğrula |
| H2 | Düşük bulguları çıkar (B071-B139) | tasks/B071..B139.md | findings/low-B.md | aynı |
| H3 | Düşük kaydını kök nedene sınıfla | low-A/B.md | findings/low-classified.md | sınıf dağılımı, 10 örnek |
| H4 | Bayat md (K4) listesini çıkar: silinmiş dosyaya atıf yapan görev md'leri | plan/**, git silinmiş yollar | findings/stale-md.md | 10 örnek dosyanın gerçekten yok olduğunu `git ls-files` ile doğrula |

Sonrası: sınıflanmış Düşük ve Orta bulgular kök neden bazında toplu görevlere bölünür; kod değiştiren her iş Sonnet'te (kırmızı test, düzeltme, mutasyon kanıtı).
