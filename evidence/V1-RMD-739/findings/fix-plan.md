# Düzeltme planı (Orta ve üstü bulgular; kaynak: findings/register.md, verdict-*.md, ui/*.md)

Bulgular 7 kök nedene toplanır. Her grup, ayrı düzeltme görevi (kırmızı test, düzeltme, mutasyon kanıtı) olarak açılır; Yüksek olanlar önce.

## K1. Eşzamanlılık ve atomiklik (para, stok, yetki) - Yüksek
V11-PUR-001 ve V11-RMD-002 (mal kabulde kilitsiz okuma, stok iki kez artar); V11-INV-007/002/008 (rebuild, bakiye farkı), V11-INV-003/006; V1-IAM-008 ve V1-RMD-668 (son yönetici koruması kilitsiz); V1-RMD-729 (idempotent tekrar durum kontrolünden sonra); V1-RMD-690 (oturum yarışı); V1-RMD-276 ve 727 (pilot: kapanış kilidi, iade); V1-ALT-001; V13-PUI-002; V1-RMD-314 (istemci anahtarı).

## K2. "Done" ama teslimat yok veya hiçbir yerden bağlı değil - Yüksek/Orta
V12-QRS-001 (masa QR token üretimi yok), V12-QRS-003 (müşteri oturumu iptali yok); V11-MNU-002 ve V11-RPT-001 (menü sayaç ve raporları bağlı değil); V15-SEC-003 ve V1-RMD-268 (retention_subjects boş, KVKK saklama fiilen çalışmıyor); V12-NFC-002 (yaş kısıtı bayrağını yazan yol yok); V12-RMD-009 ve V12-RMD-005 (vaka anahtarı, bekleme süresi); V1-KIT-003/004 ve V1-RMD-130 (yazdırma kurtarma, aynı 50 bilet); V1-RMD-656/695/715 (ham audit yazımı); V1-RMD-713/718/720/721/723/725; V1-RMD-380 (ana kasada seçenek yok); V1-RMD-719 (taslak kalem kuralı sunucuda yok); V11-INV-005; V1-OPS-002 (yedek sağlayıcı ve zamanlayıcı yok).

## K3. Kanıtsız Done (kanıt eksik, kod çalışıyor olabilir)
V0-REV-001..030 (teslimat dokümanları ve kanıt klasörleri hiç yok; 30 görev), V0-GOV-003/004/045/048, V1-GOV-036/037/039/041/043/088/090/092/094/096, V1-RMD-010/031/043, V1-CAT-004, V1-SEC-005 (commit'te kod farkı yok), V1-RMD-595/599/600/602, V13-GOV-005/007, V1-RMD-079/087. Çözüm: kanıtı yeniden üret ya da görevi Blocked/Reopened yap.

## K4. Silinmiş koda dayanan görev md ve kanıtları (kod doğru, belge bayat)
V1-RMD-134 ve 131-133 silmeleri: V1-FND-005/006/010/011/014/015/018, V1-CUI-001/002/003, V11-UI-001/002/003, V1-WTR-002/003, V1-IAM-020, V1-RMD-035 ve diğerleri. Çözüm: md'lere "silindi/yerine geçti" notu ve kanıt güncelleme; 739 Aşama 4'te toplu.

## K5. Mevzuat ve iş kuralı belirsizliği (Semih kararı: po-questions.md)
Ödeme/fiş (ÖKC, ikram KDV, bahşiş), saklama süreleri, e-Fatura "veya/ve", WCAG numaraları, yaş kısıtı, vardiya günü UTC/İstanbul/06:00.

## K6. Test ve doğrulama boşlukları
Kabul maddesini sınamayan statik metin testleri (WaiterPwa python testleri kırık: V1-RMD-051/102/008); kalıcı regresyon testi silinmiş (V1-RMD-179, 083); eşzamanlılık testleri eksik (V1-RMD-035, 737); audit rollback testleri.

## K7. Arayüz (ui/*.md)
Kullanıcı: ayar ekranlarına bağlantı yok (Yüksek), ölü mektup/anahtar yönetimi ekranı yok; hızlı satışta seçenek/not/ödeme geçişi yok; kasada yemek kartı. Tasarımcı: Cashier kendi token kopyaları, üç yazı tipi, odak halkası 5 renk; ham hata metni; terim sözlüğü; sessiz hata (KASA-1 serbest bırakma, idempotency anahtarı).
