# V1-RMD-1038 RESULT

Sorun: hesap iptalinden sonra siparis masada kalirsa "Hesap Iptal Edildi" ekraninda yalniz "Bekleyen hesaplara don" vardi.
Duzeltme: split-payment.js renderCancelled; hesap bir masaya aitse (state.billTable) ve siparis birlikte iptal edilmediyse birincil "Masaya don" (/tables) eklenir; backLink yardimcisi iki dugme icin ortak (onceki sabit BACK_LINK tekrarsiz).
Kanit: red.log (kirmizi), green StaticApps 549/549, mutations.txt M1-M4 killed, client-build-gate exit 0 (PosTerminal degismedi).
Elle senaryo: masali hesapta Iade ve iptal > Hesabi iptal et (siparisi iptal etme isaretsiz) > "Masaya don" gorunur; isaretliyse ve hizli satista gorunmez.
Not: /tables masa planina gider, ayni masayi ayrica acmaz (mevcut Geri baglantisiyla ayni hedef).
