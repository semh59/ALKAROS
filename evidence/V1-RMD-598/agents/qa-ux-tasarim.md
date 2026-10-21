# qa-ux-tasarim — ham rapor özeti (SHA eb93ec69, kod okuması; ekran görüntüsü alınmadı)

Semih senaryosu ekrandan uca yapılamıyor: garson masa sınırı için düzenleme ekranı yok, 4. masada Türkçe uyarı yok, geçmişte kişi adı yerine kimlik parçası görünüyor.

| Kimlik | Önem | Dosya | Bulgu |
| --- | --- | --- | --- |
| UX-B1 | Yüksek | SettingsHistorySection.tsx:20 | "Değiştiren" sütunu kullanıcı kimliğinin ilk 8 hanesini basıyor, ad yok |
| UX-B2 | Yüksek | BusinessIdentitySettings.tsx:133,150; models.ts:13 | Garson sınırı ve diğer ayarlar için düzenleme ekranı yok (yalnız ad, renk, logo) |
| UX-B3 | Yüksek | SuggestedWaiterResolver.cs:63 | Sınıra ulaşan garson sessizce öneriden çıkıyor; Türkçe uyarı yok, "0 = sınırsız" anlatılmıyor |
| UX-B4 | Orta | BusinessIdentitySettings.tsx:84-86 | Durum okunamazsa alanlar kilitli, "Tekrar dene" yok |
| UX-B5 | Orta | BusinessIdentitySettings.tsx:354 | Hata mesajı sayfa altında, kaydet düğmesinden uzak |
| UX-B6 | Orta | BusinessIdentitySettings.tsx:347 | "Kaldır" logoyu onaysız siliyor |
| UX-B7 | Orta | BusinessIdentitySettings.tsx:132-150 | Kaydetmeden hemen önce rowVersion okunuyor, eşzamanlı değişiklik ezilir |
| UX-B8 | Orta | models.ts:27 | Haritada olmayan ayar "Tanımsız ayar" görünüyor |
| UX-B9 | Düşük | models.ts:31 | Değerler birimsiz |
| UX-B10 | Düşük | SettingsHistorySection.tsx:16,35 | Dar ekranda tablo kaydırma kabı yok (doğrulanamadı) |
| UX-B11 | Düşük | management-settings-history.css | Kullanılmayan msh__* kuralları |
| UX-B12 | Düşük | BusinessIdentitySettings.tsx (çok yer) | Ham ApiError.message gösteriliyor |
| UX-B13 | Düşük | BusinessIdentitySettings.tsx:14 | Gerekçe metninde "PosTerminal" ürün adı geçmişe düşüyor |
| UX-B14 | Düşük | models.ts:37 | Yalnız "Initial registration" çevriliyor |
| UX-B15 | Düşük | business-identity-settings.css | --ds-* yerine eski değişkenler |
| UX-B16 | Düşük | SellerProfileCard.tsx:51-63 | VKN/TCKN istemcide doğrulanmıyor |
| UX-B17 | Düşük | BusinessIdentitySettings.tsx:246,258 | Ekran kendi girişini istiyor, açık oturumu kullanmıyor |

Temiz alanlar: metinler Türkçe, tarih tr-TR, nesne URL'leri serbest bırakılıyor. 12/74 kalem (istemci dilimi) kontrol edildi.
Fırsatlar: "Ayarlar → Garson masa sınırı → 3 → Kaydet" 4 dokunuşta; satıra tıklayınca değer ve geçmiş yan yana; geçmişte "Ali 4 → 3 yaptı" özeti.
