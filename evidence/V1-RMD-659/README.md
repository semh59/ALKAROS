# V1-RMD-659 kanıtı

Sonuç: F25'in kalan parçası kapandı. Altı Host ağacı (DualScreen, Experience.Billing, Experience.CustomerAccounts, Experience.Observability, Experience.SecurityAdministration, Observability) `ApprovedHostOrchestrationEdges` ve belge tablosuna girdi; yeni modül kenarı artık sessizce eklenemez.

- Listeler gerçek kullanımın birleşimidir, sıkılık taraması (`mutations.log`) ile doğrulandı: kullanılmayan kenarlar çıkarıldı (DualScreen→Inventory; CustomerAccounts ağacında Billing, Cash, Identity). DualScreen→CustomerAccounts kenarı `using` satırlarında görünmüyordu, tür bağımlılığından çıktı ve eklendi.
- Mutasyon: gerçek kenar çıkarılınca test kırılır (7 mutant, 6 öldü; sağ kalan CustomerAccounts/Billing kullanılmayan kenar çıktı, listeden atıldı ve ağacın kalan kenarları yeniden mutasyonla sınandı).
- Bilinen sınır: test yalnızca `ModuleAssemblies` listesindeki modülleri izler; Payments, QrOrdering, Secrets, QrRelay bu listede olmadığından bu ağaçlardaki kenarları izlenmez (listeye kayıt için bilgi amaçlı yazıldı). Listeyi genişletmek ayrı iştir; açık risk olarak kalır.
