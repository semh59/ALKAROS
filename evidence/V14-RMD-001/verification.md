# V14-RMD-001 — doğrulama

`dotnet test tests/Modules/CustomerData/AnonymizationState -c Release` (PostgreSQL 18.6):

| Sürüm | Ardışık koşu | Geçen | Başarısız |
| --- | --- | --- | --- |
| Düzeltmeden önce (`bf4205e`) | 10 | 2 | 8 |
| Düzeltmeden sonra | 20 | 20 | 0 |

Test hâlâ gidiş-dönüşte tam eşitlik doğruluyor; yalnız yazılan değer PostgreSQL'in saklayabileceği mikrosaniye
hassasiyetine kesildi.
