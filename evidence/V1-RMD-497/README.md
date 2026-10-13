# V1-RMD-497 - Faturalama güvenliği

## Kurallar

- Faturası üretilmiş bir kaynak setinin iptali reddedilir (`InvoiceSourceSetAlreadyInvoicedException`); set ve satırları olduğu gibi kalır. Eskiden iptal, satırların `cancelled` bayrağını açıp aynı borç kalemlerinin yeni sete girmesine ve çift faturalanmasına izin verirdi.
- Online sipariş fatura taslağı geçişi adayları rastgele sırayla alır (eskiden en eskiden başlıyordu). Satırı olmayan veya bilinmeyen platformlu siparişler 7 gün boyunca aday kalır; en eskiden başlayan sıralama bunların 50'sinin her turu doldurup yeni siparişleri engellemesine yol açıyordu.

## Kanıt

- `tests-module.log`: kaynak seçimi 13 test (yeni: faturalanmış set iptali). `tests-host.log`: OnlineOrdering 178 test (yeni: kalıcı başarısızlar yeni siparişi aç bırakmaz; gerçek Host + Postgres). Faturalama üretim testleri 45/45.
- `mutation.log`: 2 mutant, ikisi de testi kırdı; dosyalar hash ile geri yüklendi.
