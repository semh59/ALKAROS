# Bağımsız çürütme — özet kaydı

Ayrı bir ajan (salt okunur) her aday bulguyu karar belgeleri, probe doğruluğu, mevcut azaltıcılar ve açık görevler
açısından çürütmeye çalıştı. Kararları:

| Kimlik | Karar | Önem | Ana gerekçe |
| --- | --- | --- | --- |
| H-01 | Doğrulandı | Düşük | `DualScreenApplication.Endpoints.cs:75/:83/:193` 12 saat; V1-IAM-031 yalnız kullanılmayan `SessionTokenIssuer.DefaultLifetime`'ı değiştirdi; V1-RMD-277'deki "12 saat" mevcut davranışı anlatır, karar değildir. |
| H-02 | Doğrulandı | Yüksek | `DualScreenApplication.CashSession.cs` bütün uçlar yalnız `RequireCashierAsync`; `CashDrawer` çağıranı yok; `cash-session-design.md` §6 rol tablosu kodda yok; eşleme tablosu kasa uçlarından önce yazıldı. F-09'u kapsar, F-08'den ayrıdır. |
| H-03 | Doğrulandı | Orta | Yönetim çerezinin tek üreticisi `Endpoints.cs:80-84` (`catalog.manage`); `supervisor:` yalnız arama SQL'inde ve negatif test fikstürlerinde geçiyor. |
| H-04 | Doğrulandı | Düşük | `CreateAsync`'in modül dışında çağıranı yok; delegasyon ekleyen migration/seed yok. |
| H-05 | Doğrulandı | Orta | `OfflineReconciliationEndpoints.cs:68-71` rolü doğrulamıyor (yorum aksini söylüyor); `OfflineGrantReconciler.cs:104-105,:121`. |
| H-06 | Doğrulandı | Düşük-Orta | `DecideAsync` içinde kendi hesabı kontrolü yok; hizmet eden kullanıcı istemciden geliyor. |
| H-07 | Düşürüldü (karar sorusu) | Düşük | Sahipsiz hesap için karar yok; yönetici yine karar veriyor. |
| Q-01 | Açık karar sorusu | Düşük-Orta | Ödeme alma izni modelde yok; kart işleyici bugün yer tutucu. |
| S-01 | Bilinen | Düşük | V1-RMD-131 Out of scope notu; `catalogApi.ts:32` mesajı ekrana taşıyor. |
| T-01 | Doğrulandı (test boşluğu) | Düşük | Oturumu olan pasif kullanıcıyı sınayan test yok; ürün yolu kullanıcı pasifleştirmiyor. |

Ajanın ek bulguları (denetçi tarafından kodda doğrulandı): N-1 çok rollü kullanıcıda `roleIds[0]` sırasız
(`PostgresRoleRepository.GetRoleIdsForUserAsync`, `ORDER BY` yok); N-2 personeli pasifleştiren ürün yolu yok
(kaynakta `active = false` yazan kod yok); N-3 `OfflineReconciliationEndpoints.cs:62-69` yanıltıcı yorum (H-05).
