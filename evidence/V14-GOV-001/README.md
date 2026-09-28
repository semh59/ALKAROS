# V14-GOV-001 - v1.4 customer PII draft (ahead of GATE-V14-ENTRY)

**2026-09-28: superseded.** `V14-CST-001` is now `Done` — the real
implementation lives in `src/Modules/CustomerData/Profiles/**`, referencing
this draft directly (see that task's own plan file). This folder is kept
as-is for history; nothing here is loaded by the running application.

Bu klasör, `V14-GOV-001`'in (bkz. `plan/v1.4/customer-data/
V14-GOV-001-v14-gate-unverified-draft-authorization.md`) kendi Owned
surface'ı altında ürettiği tek deliverable'dır: `V14-CST-001` (müşteri
PII sınırı) gerçekten başlayabilmeden önce, yalnız `V0-CMP-003`'ün
onaylanmış KVKK envanterine dayalı bir domain taslağı.

## Neden burada, `src/Modules/CustomerData/**` altında değil

`GATE-V14-ENTRY` → `GATE-V13-EXIT` zinciri hâlâ açık (v1.3'ün ödeme/
fiscal zinciri `V0-HUG-001`/`V0-CMP-001` yüzünden tamamlanmadı). Bu,
Token/QNB'deki "sadece credential eksik" durumundan farklı — **bilinçli
bir sıralama kararı**: v1.4 fatura domain'i, v1.3'ün fiscal stratejisi
netleşmeden tasarlanırsa yeniden yazım riski taşır. Semih bu riski bilerek
kabul edip yine de taslak istedi (`V13-GOV-006`/`V13-GOV-007` ile aynı
"standalone, gerçek Owned surface'a değil" deseni).

## Gerçek bulgu: vergi kimliği `Customer PII`'nin parçası DEĞİL

`V0-CMP-003`'ün kendi KVKK envanterini (`evidence/v0/compliance/
V0-CMP-003/kvkk-data-inventory.md`) okuyunca şu netleşti:

| Kategori | Alanlar | Erişim rolü |
| --- | --- | --- |
| **Customer PII** | name, phone, email, address | Manager, **Cashier** |
| **Invoice data** | customer name, **tax ID**, amount | Manager, **Finance** |

Yani vergi kimlik numarası (VKN/TCKN) `Customer PII`'nin bir alanı değil —
ayrı bir kategoride, ayrı bir erişim rolüyle (Cashier YOK, Finance VAR).
Bu, `V14-CST-001`'in kendi notuyla da tutarlı ("UBL zorunlu tanımlayıcı
gereksinimleri V14-INV-002 kapsamındadır") — vergi kimliği
`CustomerProfile`'a değil, `V14-INV-002`'nin fatura domain'ine ait
olmalı. Taslak bunu bilerek YANSITIYOR: `CustomerProfile`'da hiç vergi
kimliği alanı yok.

## Doğrulanan vs DOĞRULANMAYAN

**Doğrulanan (V0-CMP-003'ün gerçek envanterine göre, 8/8 test yeşil):**

- Alan seti (`name`/`phone`/`email`/`address`) envanterle birebir eşleşiyor.
- 10 yıllık saklama süresi (envanterdeki "10 years (tax)" değeri).
- Rol bazlı erişim: Cashier VE Manager ikisi de tam görür, başka rol hiçbir
  şey görmez (kısmi sızıntı yok).
- Anonimleştirme sonrası kayıt/ID hayatta kalıyor ama tanımlayıcı alanlar
  siliniyor (envanterin "Anonymize after retention" disposal action'ı).
- Anonimleştirilmiş bir müşteriye fatura kesme girişimi reddediliyor.

**DOĞRULANMAYAN:**

- Gerçek bir veritabanına hiç bağlanılmadı — migration/repository yok.
- `GATE-V14-ENTRY` kapanmadan bu taslağın gerçek domain modeliyle birebir
  aynı kalıp kalmayacağı garanti değil.

## `V14-CST-001` gerçekten başladığında yapılacaklar

1. `GATE-V14-ENTRY`'nin gerçekten kapandığını doğrula.
2. `src/Modules/CustomerData/Profiles/` altında gerçek projeyi oluştur,
   `CustomerProfile`/`CustomerProfileAccessPolicy`/
   `CustomerProfileRetention`'ı buradan taşı.
3. Gerçek bir migration + Postgres repository ekle.
4. `V14-CST-001`'in kendi Acceptance evidence'ını üret.
