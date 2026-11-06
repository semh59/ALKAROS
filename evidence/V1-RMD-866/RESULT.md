# V1-RMD-866 sonuç (F0248, F0746)

F0248 (gerçek): vaka geçmişinde "kimin" bilgisi yoktu; sunucu PerformedBy kimliğini dönüyordu, istemci alanı atıyordu ve kimlik kişi adı değil. Sunucu eylem yanıtına PerformedByName (identity.users.display_name, tek sorgu) ekledi; geçmiş tablosuna "İşlemi yapan" sütunu geldi. Tarama sistem kimliğiyle yazdığı için "Sistem", adı çözülemeyen (silinmiş kullanıcı) "Bilinmiyor" görünür; ham kimlik hiç gösterilmez.
F0746 (gerçek): tek paylaşılan not alanı durum geçişinden sonra temizlenmiyordu; sonraki vakanın geçişine yanlışlıkla giderdi. Geçiş başarılı olunca alan boşalır, hata durumunda yazılan not korunur.
Testler: Host ReconciliationCaseHttpTests 5/5 (red-server.log: derleme hatası, green-server.log); PosTerminal management-closing 23/23, tsc ve oxlint temiz (uyarılar önceden var, bu dosyalarda yok).
Mutasyon (cp+cmp geri alındı): not temizleme yok, ad yok, sistem etiketi yok, hatada da temizleme, sunucuda ad çözümü yok: altısı da öldü (ek: sistem kimliğinin ad önüne alınması).
Sınır: ad çözümü yalnız aktif/pasif ayrımı olmadan identity.users okur (anonimleştirilmiş kullanıcının adı kayıttaki gibi görünür); tarayıcıda elle denenmedi (jsdom).
