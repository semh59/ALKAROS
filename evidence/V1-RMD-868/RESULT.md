# V1-RMD-868 sonuç (F0082)

Billing ve Kitchen `using Npgsql` kullanıyordu ama paketi yalnız Orders'tan geçişli alıyordu (before.txt: "Geçişli Paket"). İkisine merkezi sürümle `PackageReference Include="Npgsql"` eklendi (9.0.2 değişmedi); after.txt: "Üst Düzey Paket".
Plan düzeltmesi: planda "yalnız iki kilit değişir" denmişti, yanlış. Kilitler proje bağımlılıklarının paketlerini de yazdığı için Billing/Kitchen'e bağımlı 100 proje daha kilitli geri yüklemede NU1004 verdi (red-locked-*.log, 9/9 test projesi). Çözüm bütününün `dotnet restore ALKAROS.slnx --force-evaluate` ile yenilenmesi; toplam 102 kilit, hepsi yalnız bir "Npgsql" satırı ekler/Direct'e çevirir (inceleme teyit etti). Plan allowlist'i genişletildi (ayrı plan commit'i).
Doğrulama: `restore --locked-mode` çözüm bütününde rc=0, Billing 4 ve Kitchen 5 test projesi yeşil (g-*.log), iki proje derlenir.
Mutasyon (cp+cmp geri alındı): Billing'den PackageReference silinince kilitli geri yükleme NU1004 ile düşer.
Sınır: tests/Host/MigrationComposition test projesi de Npgsql kullanıp doğrudan bildirmiyor (düşük, kapsam src); borç olarak raporda.
