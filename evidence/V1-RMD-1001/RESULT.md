# V1-RMD-1001 sonuc

- Masa plani sag paneli (routes/TableOrderBridge.tsx, TableWorkspace billPanel yuvasi): masa adi, kalemler, toplam; Sepet/Odeme: Odeme tek dolu dugme; Urun ekle / Siparis al; Bol, Masa tasi, Iptal / iade (yalniz hesap varken). Hesabi acik masada Urun ekle kapali ve nedeni yazili. Kalem okumasi orders.create ister; yoksa okunmaz.
- Sepet: .ticket-footer sabit alt bolum (Mutfaga gonder (n), Tahsil et, mutfaksiz ekle); kalem listesi kayar. Masa adi sepet basliginda (Masa T-4).
- Kat plani cizimi: planStatus simgesi ve adi (Hesap istedi, Odeme bekliyor).
- Testler: client-all.log 1274 gecti; oxlint, tsc, vite build 0; mutations.txt M1-M15 killed; red-*.log kirmizi.
- Acik kalan (qa notu, engelleyici degil): iki ic ice kaydirma (footer max-height 60%) 768x1024 gercek ekranda olculecek; Bol ile Iptal/iade ayni faturalama ekranina gider; masasiz hizli satista etiket de "Mutfaga gonder (n)"; yalniz hesabi olan (siparissiz) masada kalem okunmaz; kat plani rengi ham durumdan.
