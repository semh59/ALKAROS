/**
 * ALKAROS V1 — Universal Multi-Concept Restaurant POS & Waiter Engine (Stitch Enterprise Version)
 * Concepts Supported:
 *  1) 🍔 Burger & Steakhouse (Et Pişme, Ekstralar, Soslar)
 *  2) ☕ 3. Nesil Kafe & Specialty Coffee (Süt Seçimi: Yulaf/Badem/Laktozsuz, Şurup, Çekirdek, Sıcaklık)
 *  3) 🍕 İtalyan & Pizza Trattoria (Hamur Tipi: Ekşi Mayalı/Napolitan, Boyut: 30/36cm, Peynir)
 *  4) 🐟 Ocakbaşı & Meyhane (Porsiyon: Tek/1.5/Kg, Rakı Servisi: Buzlu/Sek/Karaf, Meze Eşleşmesi)
 */

(function () {
  'use strict';

  // --- 1. MULTI-CONCEPT CATALOG REPOSITORY ---

  const CONCEPTS = {
    burger: {
      name: 'Burger & Steakhouse',
      courses: [
        { key: 'Başlangıç', title: '1. AŞAMA: BAŞLANGIÇ & ATIŞTIRMALIK', cssClass: 'starter' },
        { key: 'Ana Yemek', title: '2. AŞAMA: BURGERLER & IZGARALAR', cssClass: 'main' },
        { key: 'Tatlı', title: '3. AŞAMA: TATLI & KAHVE', cssClass: 'dessert' }
      ],
      categories: ['Tümü', 'Burgerler', 'Ana Yemek', 'İçecekler', 'Tatlılar'],
      products: [
        {
          id: 'b1', name: 'Alkaros Burger (200g)', category: 'Burgerler', defaultCourse: 'Ana Yemek', price: 240.00, station: 'hot', stock: 'Son 5 Porsiyon', allergen: 'Gluten, Süt',
          modifierGroups: [
            { id: 'mg_done', title: 'Pişme Derecesi', required: true, type: 'single', options: [{ name: 'Az Pişmiş', price: 0 }, { name: 'Orta Pişmiş', price: 0, default: true }, { name: 'Çok Pişmiş', price: 0 }] },
            { id: 'mg_ext', title: 'Ekstra Malzemeler', required: false, type: 'multi', options: [{ name: 'Ekstra Cheddar', price: 30.00 }, { name: 'Karamelize Soğan', price: 20.00 }, { name: 'Duble Köfte (+150g)', price: 90.00 }] }
          ]
        },
        {
          id: 'b2', name: 'Cheese Burger', category: 'Burgerler', defaultCourse: 'Ana Yemek', price: 220.00, station: 'hot', allergen: 'Gluten, Süt',
          modifierGroups: [
            { id: 'mg_done', title: 'Pişme Derecesi', required: true, type: 'single', options: [{ name: 'Orta Pişmiş', price: 0, default: true }, { name: 'Çok Pişmiş', price: 0 }] },
            { id: 'mg_ext', title: 'Ekstralar', required: false, type: 'multi', options: [{ name: 'Füme Kaburga', price: 45.00 }, { name: 'Jalapeno Biber', price: 15.00 }] }
          ]
        },
        {
          id: 'b3', name: 'Bonfile Izgara (250g)', category: 'Ana Yemek', defaultCourse: 'Ana Yemek', price: 450.00, station: 'hot', stock: 'Son 3 Porsiyon', allergen: 'Gluten-Free',
          modifierGroups: [
            { id: 'mg_done', title: 'Et Pişme Derecesi', required: true, type: 'single', options: [{ name: 'Az Pişmiş (Rare)', price: 0 }, { name: 'Orta (Medium)', price: 0, default: true }, { name: 'Orta-İyi (Medium Well)', price: 0 }, { name: 'Çok Pişmiş (Well Done)', price: 0 }] },
            { id: 'mg_sauce', title: 'Şef Sosu Seçimi', required: true, type: 'single', options: [{ name: 'Trüflü Mantar Sosu', price: 40.00, default: true }, { name: 'Taze Karabiber Sosu', price: 35.00 }, { name: 'Sade / Tereyağlı', price: 0 }] }
          ]
        },
        {
          id: 'b4', name: 'Patates Tava', category: 'Ana Yemek', defaultCourse: 'Başlangıç', price: 85.00, station: 'hot', allergen: 'Vegan',
          modifierGroups: [
            { id: 'mg_dip', title: 'Yan Sos', required: false, type: 'multi', options: [{ name: 'Truf Mayonez', price: 20.00 }, { name: 'Cajun Baharatı', price: 10.00 }] }
          ]
        },
        { id: 'b5', name: 'Coca Cola 330ml', category: 'İçecekler', defaultCourse: 'Başlangıç', price: 45.00, station: 'bar', allergen: 'Vegan', modifierGroups: [{ id: 'mg_ice', title: 'Buz / Limon', required: false, type: 'multi', options: [{ name: 'Buzsuz', price: 0 }, { name: 'Limon Dilimli', price: 0 }] }] },
        { id: 'b6', name: 'Ayran 300ml', category: 'İçecekler', defaultCourse: 'Başlangıç', price: 30.00, station: 'bar', allergen: 'Süt', modifierGroups: [] },
        { id: 'b7', name: 'Çikolatalı Sufle', category: 'Tatlılar', defaultCourse: 'Tatlı', price: 120.00, station: 'cold', stock: 'Son 4 Porsiyon', allergen: 'Yumurta, Süt', modifierGroups: [{ id: 'mg_icecream', title: 'Dondurma İsteği', required: false, type: 'single', options: [{ name: 'Vanilyalı Dondurma Ekle', price: 35.00 }, { name: 'Sade', price: 0, default: true }] }] }
      ]
    },

    cafe: {
      name: '3. Nesil Kafe & Specialty Coffee',
      courses: [
        { key: 'Başlangıç', title: '1. AŞAMA: SICAK & SOĞUK KAHVELER', cssClass: 'starter' },
        { key: 'Ana Yemek', title: '2. AŞAMA: SANDVİÇ & TOSTLAR', cssClass: 'main' },
        { key: 'Tatlı', title: '3. AŞAMA: FIRIN & TATLILAR', cssClass: 'dessert' }
      ],
      categories: ['Tümü', 'Sıcak Kahveler', 'Soğuk Kahveler', 'Kahvaltı & Sandviç', 'Fırın & Tatlı'],
      products: [
        {
          id: 'c1', name: 'Flat White (Specialty)', category: 'Sıcak Kahveler', defaultCourse: 'Başlangıç', price: 95.00, station: 'bar', allergen: 'Süt',
          modifierGroups: [
            { id: 'mg_milk', title: 'Süt Tercihi', required: true, type: 'single', options: [{ name: 'Tam Yağlı Süt', price: 0, default: true }, { name: 'Yulaf Sütü (Oat)', price: 20.00 }, { name: 'Badem Sütü (Almond)', price: 25.00 }, { name: 'Laktozsuz Süt', price: 15.00 }, { name: 'Soya Sütü', price: 20.00 }] },
            { id: 'mg_bean', title: 'Çekirdek Orijini', required: true, type: 'single', options: [{ name: 'Etiyopya Yirgacheffe (Meyvemsi)', price: 0, default: true }, { name: 'Kolombiya Supremo (Çikolatamsı)', price: 0 }, { name: 'Decaf (Kafeinsiz)', price: 15.00 }] },
            { id: 'mg_syrup', title: 'Şurup & Aroma', required: false, type: 'multi', options: [{ name: 'Doğal Vanilya Şurubu', price: 15.00 }, { name: 'Tuzlu Karamel', price: 15.00 }, { name: 'Kavrulmuş Fındık', price: 15.00 }] }
          ]
        },
        {
          id: 'c2', name: 'Iced Spanish Latte', category: 'Soğuk Kahveler', defaultCourse: 'Başlangıç', price: 110.00, station: 'bar', allergen: 'Süt',
          modifierGroups: [
            { id: 'mg_ice', title: 'Buz Oranı', required: true, type: 'single', options: [{ name: 'Standart Buz', price: 0, default: true }, { name: 'Az Buzlu', price: 0 }, { name: 'Buzsuz', price: 0 }] },
            { id: 'mg_shot', title: 'Ekstra Shot', required: false, type: 'multi', options: [{ name: 'Ekstra Espresso Shot (+30ml)', price: 30.00 }] }
          ]
        },
        {
          id: 'c3', name: 'V60 Manuel Demleme (Pour-Over)', category: 'Sıcak Kahveler', defaultCourse: 'Başlangıç', price: 130.00, station: 'bar', allergen: 'Vegan',
          modifierGroups: [
            { id: 'mg_origin', title: 'Single Origin Çekirdek', required: true, type: 'single', options: [{ name: 'Kenya Nyeri AA', price: 0, default: true }, { name: 'Panama Geisha (+Özel Seri)', price: 65.00 }, { name: 'Guatemala Huehuetenango', price: 10.00 }] }
          ]
        },
        {
          id: 'c4', name: 'Avokado & Poşe Yumurta Sandviç', category: 'Kahvaltı & Sandviç', defaultCourse: 'Ana Yemek', price: 210.00, station: 'hot', allergen: 'Yumurta, Gluten',
          modifierGroups: [
            { id: 'mg_bread', title: 'Ekmek Tercihi', required: true, type: 'single', options: [{ name: 'Ekşi Mayalı Köy Ekmeği', price: 0, default: true }, { name: 'Çavdarlı Siyez', price: 15.00 }, { name: 'Glutensiz Ekmek', price: 25.00 }] },
            { id: 'mg_add', title: 'İlave Lezzet', required: false, type: 'multi', options: [{ name: 'Norveç Somon Füme (50g)', price: 75.00 }, { name: 'Krem Peynir', price: 20.00 }] }
          ]
        },
        {
          id: 'c5', name: 'Tereyağlı Fransız Kruvasan', category: 'Fırın & Tatlı', defaultCourse: 'Tatlı', price: 80.00, station: 'hot', allergen: 'Gluten, Süt',
          modifierGroups: [
            { id: 'mg_heat', title: 'Servis Şekli', required: true, type: 'single', options: [{ name: 'Fırında Isıtılmış & Sıcak', price: 0, default: true }, { name: 'Oda Sıcaklığında', price: 0 }] },
            { id: 'mg_spread', title: 'İç Dolgu / Sürülebilir', required: false, type: 'multi', options: [{ name: 'Belçika Çikolatası', price: 25.00 }, { name: 'Antep Fıstığı Ezmesi', price: 35.00 }] }
          ]
        },
        {
          id: 'c6', name: 'San Sebastian Cheesecake', category: 'Fırın & Tatlı', defaultCourse: 'Tatlı', price: 140.00, station: 'cold', stock: 'Son 4 Dilim', allergen: 'Yumurta, Süt',
          modifierGroups: [
            { id: 'mg_sauce', title: 'Sıcak Sos Seçimi', required: true, type: 'single', options: [{ name: 'Eritilmiş Callebaut Sütlü Çikolata', price: 30.00, default: true }, { name: 'Bitter Çikolata Sosu', price: 30.00 }, { name: 'Sossuz / Sade', price: 0 }] }
          ]
        }
      ]
    },

    pizza: {
      name: 'İtalyan Trattoria & Pizzeria',
      courses: [
        { key: 'Başlangıç', title: '1. AŞAMA: ANTIPASTI (BAŞLANGIÇLAR)', cssClass: 'starter' },
        { key: 'Ana Yemek', title: '2. AŞAMA: NAPOLITAN PIZZA & PASTA', cssClass: 'main' },
        { key: 'Tatlı', title: '3. AŞAMA: DOLCI (İTALYAN TATLILARI)', cssClass: 'dessert' }
      ],
      categories: ['Tümü', 'Napolitan Pizza', 'Makarna & Risotto', 'Başlangıçlar', 'İtalyan Tatlılar'],
      products: [
        {
          id: 'pz1', name: 'Pizza Margherita Verace', category: 'Napolitan Pizza', defaultCourse: 'Ana Yemek', price: 260.00, station: 'hot', allergen: 'Gluten, Süt',
          modifierGroups: [
            { id: 'mg_size', title: 'Pizza Boyutu', required: true, type: 'single', options: [{ name: 'Standart (30cm - 6 Dilim)', price: 0, default: true }, { name: 'Büyük (36cm - 8 Dilim)', price: 65.00 }] },
            { id: 'mg_dough', title: 'Hamur Stili', required: true, type: 'single', options: [{ name: 'Geleneksel Napolitan İnce', price: 0, default: true }, { name: '48 Saat Fermante Ekşi Maya', price: 25.00 }, { name: 'Peynir Dolgulu Kenar (Cornicione)', price: 45.00 }] },
            { id: 'mg_top', title: 'Ekstra Malzemeler', required: false, type: 'multi', options: [{ name: 'Manda Mozzarella (Bufala)', price: 50.00 }, { name: 'Taze Fesleğen & Zeytinyağı', price: 10.00 }, { name: 'Kurutulmuş Domates', price: 25.00 }] }
          ]
        },
        {
          id: 'pz2', name: 'Pizza Quattro Formaggi (4 Peynirli)', category: 'Napolitan Pizza', defaultCourse: 'Ana Yemek', price: 340.00, station: 'hot', allergen: 'Gluten, Süt',
          modifierGroups: [
            { id: 'mg_size', title: 'Pizza Boyutu', required: true, type: 'single', options: [{ name: 'Standart 30cm', price: 0, default: true }, { name: 'Büyük 36cm', price: 70.00 }] },
            { id: 'mg_honey', title: 'Bal Eşleşmesi', required: false, type: 'single', options: [{ name: 'Truffle Aromalı Çiçek Balı', price: 35.00 }, { name: 'Balsamik Glaze', price: 25.00 }] }
          ]
        },
        {
          id: 'pz3', name: 'Penne All\'Arrabbiata', category: 'Makarna & Risotto', defaultCourse: 'Ana Yemek', price: 220.00, station: 'hot', allergen: 'Vegan, Gluten',
          modifierGroups: [
            { id: 'mg_spicy', title: 'Acı Seviyesi', required: true, type: 'single', options: [{ name: 'Hafif Acılı (Calabrian Mild)', price: 0 }, { name: 'Orijinal Acılı (Medium)', price: 0, default: true }, { name: 'Ekstra Ateşli (Molto Piccante)', price: 0 }] },
            { id: 'mg_cheese', title: 'Peynir İlavesi', required: false, type: 'multi', options: [{ name: '24 Ay Olgunlaştırılmış Parmigiano Reggiano', price: 35.00 }, { name: 'Pecorino Romano', price: 35.00 }] }
          ]
        },
        {
          id: 'pz4', name: 'Burrata Con Pomodorini', category: 'Başlangıçlar', defaultCourse: 'Başlangıç', price: 290.00, station: 'cold', allergen: 'Süt, Gluten-Free',
          modifierGroups: [
            { id: 'mg_bread', title: 'Kıtır Ekmek', required: false, type: 'single', options: [{ name: 'Focaccia Dilimleri Ekle', price: 30.00 }, { name: 'Ekmeksiz', price: 0, default: true }] }
          ]
        },
        {
          id: 'pz5', name: 'Tiramisù Tradizionale', category: 'İtalyan Tatlılar', defaultCourse: 'Tatlı', price: 135.00, station: 'cold', allergen: 'Yumurta, Süt',
          modifierGroups: []
        }
      ]
    },

    tavern: {
      name: 'Ocakbaşı, Balık & Meyhane',
      courses: [
        { key: 'Başlangıç', title: '1. AŞAMA: SOĞUK MEZELER & SALATALAR', cssClass: 'starter' },
        { key: 'Ana Yemek', title: '2. AŞAMA: ARA SICAKLAR, BALIK & IZGARA', cssClass: 'main' },
        { key: 'Tatlı', title: '3. AŞAMA: MEYVE & GELENEKSEL TATLILAR', cssClass: 'dessert' }
      ],
      categories: ['Tümü', 'Soğuk Mezeler', 'Ara Sıcaklar', 'Ana Izgaralar & Balık', 'Rakı & İçecekler', 'Tatlılar'],
      products: [
        {
          id: 'tv1', name: 'Deniz Levreği Izgara (600g)', category: 'Ana Izgaralar & Balık', defaultCourse: 'Ana Yemek', price: 460.00, station: 'hot', allergen: 'Balık, Gluten-Free',
          modifierGroups: [
            { id: 'mg_prep', title: 'Pişirme Tekniği', required: true, type: 'single', options: [{ name: 'Kömür Ateşinde Izgara', price: 0, default: true }, { name: 'Fırında Buğulama (Sebzeli)', price: 30.00 }, { name: 'Kaya Tuzunda (Özel Şef)', price: 60.00 }] },
            { id: 'mg_side', title: 'Garnitür Tercihi', required: true, type: 'single', options: [{ name: 'Taze Roka & Kırmızı Soğan & Limon', price: 0, default: true }, { name: 'Ilık Hardallı Patates Salatası', price: 20.00 }] }
          ]
        },
        {
          id: 'tv2', name: 'Zırh Kıyma Adana Kebap (200g)', category: 'Ana Izgaralar & Balık', defaultCourse: 'Ana Yemek', price: 320.00, station: 'hot', allergen: 'Gluten',
          modifierGroups: [
            { id: 'mg_portion', title: 'Porsiyon Seçimi', required: true, type: 'single', options: [{ name: '1 Porsiyon (200g)', price: 0, default: true }, { name: '1.5 Porsiyon (300g)', price: 130.00 }, { name: 'Dürüm Servis', price: -20.00 }] },
            { id: 'mg_garnish', title: 'Garnitür / Közleme', required: false, type: 'multi', options: [{ name: 'Bol Köz Biber & Domates', price: 15.00 }, { name: 'Sumaklı Maydanozlu Soğan', price: 10.00 }] }
          ]
        },
        {
          id: 'tv3', name: 'Atom & Haydari İkili Meze Tabağı', category: 'Soğuk Mezeler', defaultCourse: 'Başlangıç', price: 125.00, station: 'cold', allergen: 'Süt',
          modifierGroups: [
            { id: 'mg_oil', title: 'Yağ & Biber', required: true, type: 'single', options: [{ name: 'Kızgın Tereyağlı Acı Biberli', price: 0, default: true }, { name: 'Sızma Zeytinyağlı Sade', price: 0 }] }
          ]
        },
        {
          id: 'tv4', name: 'Tereyağlı Karides Güveç', category: 'Ara Sıcaklar', defaultCourse: 'Ana Yemek', price: 310.00, station: 'hot', allergen: 'Deniz Ürünü, Süt',
          modifierGroups: [
            { id: 'mg_garlic', title: 'Sarımsak / Pul Biber', required: false, type: 'multi', options: [{ name: 'Bol Sarımsaklı', price: 0 }, { name: 'Ekstra Pul Biberli', price: 0 }, { name: 'Kaşar Peynirli Fırın', price: 35.00 }] }
          ]
        },
        {
          id: 'tv5', name: 'Yeni Rakı Uzun Demleme 35cl', category: 'Rakı & İçecekler', defaultCourse: 'Başlangıç', price: 580.00, station: 'bar', allergen: 'Vegan',
          modifierGroups: [
            { id: 'mg_raki_srv', title: 'Servis Tarzı', required: true, type: 'single', options: [{ name: 'Bol Buzlu Karaf + Soğuk Su Yanında', price: 0, default: true }, { name: 'Sek & Soğuk Su Yanında', price: 0 }, { name: 'Buzsuz', price: 0 }] },
            { id: 'mg_glasses', title: 'Kadeh Adedi', required: true, type: 'single', options: [{ name: '2 Kadeh', price: 0, default: true }, { name: '3 Kadeh', price: 0 }, { name: '4 Kadeh', price: 0 }] }
          ]
        },
        {
          id: 'tv6', name: 'Fıstıklı Sıcak Katmer', category: 'Tatlılar', defaultCourse: 'Tatlı', price: 160.00, station: 'hot', allergen: 'Fıstık, Süt, Gluten',
          modifierGroups: [
            { id: 'mg_maras', title: 'Dondurma Tercihi', required: true, type: 'single', options: [{ name: 'Hakiki Maraş Kesme Dondurma İle', price: 40.00, default: true }, { name: 'Sade / Dondurmasız', price: 0 }] }
          ]
        }
      ]
    }
  };

  // --- 2. TABLES & OPERATIONS STATE ---

  const INITIAL_TABLES = [
    { id: 'tbl-1', number: 'S-01', section: 'Salon', occupancy: 'available', opBadge: null, capacity: 4, billAmount: 0.00, waiter: null, minutes: null, previousDrinks: [] },
    { id: 'tbl-2', number: 'S-02', section: 'Salon', occupancy: 'occupied', opBadge: 'cooking', capacity: 4, billAmount: 485.00, waiter: 'Mehmet K.', minutes: 35, previousDrinks: [{ name: 'İçecek', price: 45.00 }] },
    { id: 'tbl-3', number: 'S-03', section: 'Salon', occupancy: 'occupied', opBadge: null, capacity: 6, billAmount: 1250.00, waiter: 'Can T.', minutes: 12, previousDrinks: [{ name: 'İçecek', price: 30.00 }] },
    { id: 'tbl-4', number: 'S-04', section: 'Salon', occupancy: 'occupied', opBadge: 'bill-requested', capacity: 4, billAmount: 820.00, waiter: 'Mehmet K.', minutes: 58, previousDrinks: [] },
    { id: 'tbl-5', number: 'S-05', section: 'Salon', occupancy: 'reserved', opBadge: null, capacity: 4, billAmount: 0.00, waiter: null, minutes: null, note: '19:30 - 4 Kişi' },
    { id: 'tbl-6', number: 'S-06', section: 'Salon', occupancy: 'occupied', opBadge: 'ready', capacity: 2, billAmount: 310.00, waiter: 'Mehmet K.', minutes: 22, previousDrinks: [] },
    { id: 'tbl-7', number: 'S-07', section: 'Salon', occupancy: 'available', opBadge: null, capacity: 2, billAmount: 0.00, waiter: null, minutes: null },
    { id: 'tbl-8', number: 'S-08', section: 'Salon', occupancy: 'available', opBadge: null, capacity: 6, billAmount: 0.00, waiter: null, minutes: null },
    { id: 'tbl-9', number: 'B-01', section: 'Bahçe', occupancy: 'occupied', opBadge: 'cooking', capacity: 4, billAmount: 290.00, waiter: 'Can T.', minutes: 18, previousDrinks: [] },
    { id: 'tbl-10', number: 'B-02', section: 'Bahçe', occupancy: 'available', opBadge: null, capacity: 4, billAmount: 0.00, waiter: null, minutes: null },
    { id: 'tbl-11', number: 'B-03', section: 'Bahçe', occupancy: 'available', opBadge: null, capacity: 6, billAmount: 0.00, waiter: null, minutes: null },
    { id: 'tbl-12', number: 'B-04', section: 'Bahçe', occupancy: 'occupied', opBadge: null, capacity: 2, billAmount: 175.00, waiter: 'Mehmet K.', minutes: 40, previousDrinks: [] },
    { id: 'tbl-13', number: 'T-01', section: 'Teras', occupancy: 'available', opBadge: null, capacity: 4, billAmount: 0.00, waiter: null, minutes: null },
    { id: 'tbl-14', number: 'T-02', section: 'Teras', occupancy: 'occupied', opBadge: 'bill-requested', capacity: 4, billAmount: 640.00, waiter: 'Can T.', minutes: 50, previousDrinks: [] }
  ];

  const INITIAL_TICKETS = [
    { id: '1042', table: 'Masa S-02', time: '12 dk önce', station: 'hot', items: [{ name: '1x Sipariş', status: 'cooking' }] },
    { id: '1043', table: 'Masa B-01', time: '5 dk önce', station: 'bar', items: [{ name: '2x İçecek', status: 'pending' }] }
  ];

  const INITIAL_PRINTERS = [
    { id: 'prn-1', name: 'Mutfak Sıcak Yazıcısı', ip: '192.168.1.200', status: 'online', queueCount: 0 },
    { id: 'prn-2', name: 'İçecek / Bar Yazıcısı', ip: '192.168.1.201', status: 'paper_out', queueCount: 2, issue: 'Kağıt Bitti / Beklemede' },
    { id: 'prn-3', name: 'Mutfak Soğuk/Tatlı Yazıcısı', ip: '192.168.1.202', status: 'online', queueCount: 0 }
  ];

  const INITIAL_NOTIFICATIONS = [
    { id: 'notif-1', time: '14:28', text: 'Masa S-06: 1x Sipariş HAZIR — Servis Bekliyor!', unread: true }
  ];

  // --- 3. APPLICATION RUNTIME STATE ---

  const state = {
    activeConcept: 'burger',
    theme: localStorage.getItem('alkaros_theme') || 'light',
    currentView: 'cashier',
    isOnline: true,
    isLocked: false,
    tables: [...INITIAL_TABLES],
    selectedTable: null,
    activeSectionFilter: 'Tümü',
    activeStatusFilter: 'all',
    searchTableQuery: '',
    
    // Order Entry
    activeSeat: 'shared',
    activeCategory: 'Tümü',
    searchProductQuery: '',
    activeCart: [],
    activeDiscount: 0,
    activeModifierProduct: null,
    editingCartIndex: null,
    selectedQuickTags: [],
    selectedCartItemIndex: null,

    // Operations
    activeStationFilter: 'all',
    tickets: [...INITIAL_TICKETS],
    printers: [...INITIAL_PRINTERS],
    auditLogs: [
      '[14:28:10] Sistem: Bar Yazıcısı kağıt sonu algılandı. Fiş #1043 beklemede.',
      '[14:29:00] Kasiyer Ahmet Y.: Fiş #1043 Sıcak Yazıcısına yönlendirildi.'
    ],

    // Waiter PWA
    wtrSectionFilter: 'Tümü',
    wtrSearchQuery: '',
    wtrActiveTable: null,
    wtrCart: [],
    wtrOfflineQueue: [],
    notifications: [...INITIAL_NOTIFICATIONS],

    // PIN
    enteredPin: '',
    failedPinAttempts: 0,
    cooldownRemaining: 0,
    cooldownTimer: null
  };

  // --- 4. FORMATTERS & TOASTS ---

  const formatTL = (val) => {
    return Number(val || 0).toLocaleString('tr-TR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }) + ' TL';
  };

  const showToast = (message, type = 'success') => {
    const container = document.getElementById('toast-container');
    if (!container) return;
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `<span>${message}</span>`;
    container.appendChild(toast);
    setTimeout(() => {
      toast.style.animation = 'slideInRight 200ms ease reverse';
      setTimeout(() => toast.remove(), 200);
    }, 3000);
  };

  const getActiveConceptData = () => CONCEPTS[state.activeConcept] || CONCEPTS.burger;

  // --- 5. RENDERERS ---

  // 5.1 Render Concept Tabs & Categories
  function renderConceptNavigation() {
    const conceptData = getActiveConceptData();
    const posTabs = document.getElementById('pos-category-tabs');
    const wtrTabs = document.getElementById('wtr-cat-chips');

    if (posTabs) {
      posTabs.innerHTML = conceptData.categories.map((cat, idx) => `
        <button type="button" class="cat-tab ${idx === 0 ? 'active' : ''}" data-category="${cat}">${cat}</button>
      `).join('');

      posTabs.querySelectorAll('.cat-tab').forEach(tab => {
        tab.addEventListener('click', () => {
          posTabs.querySelectorAll('.cat-tab').forEach(t => t.classList.remove('active'));
          tab.classList.add('active');
          state.activeCategory = tab.dataset.category;
          renderCatalogProducts();
        });
      });
    }

    if (wtrTabs) {
      wtrTabs.innerHTML = conceptData.categories.map((cat, idx) => `
        <button type="button" class="wtr-chip ${idx === 0 ? 'active' : ''}" data-wtr-cat="${cat}">${cat}</button>
      `).join('');

      wtrTabs.querySelectorAll('.wtr-chip').forEach(chip => {
        chip.addEventListener('click', () => {
          wtrTabs.querySelectorAll('.wtr-chip').forEach(c => c.classList.remove('active'));
          chip.classList.add('active');
          state.activeCategory = chip.dataset.wtrCat;
          renderWaiterSurface();
        });
      });
    }
  }

  // 5.2 Render Tables Grid
  function renderCashierTables() {
    const grid = document.getElementById('cashier-table-grid');
    const emptyState = document.getElementById('cashier-empty-search');
    if (!grid) return;

    let filtered = state.tables.filter(t => {
      const matchSection = state.activeSectionFilter === 'Tümü' || t.section === state.activeSectionFilter;
      const matchStatus = state.activeStatusFilter === 'all' || 
        (state.activeStatusFilter === 'bill-requested' && t.opBadge === 'bill-requested') ||
        (state.activeStatusFilter === 'cooking' && t.opBadge === 'cooking');
      const matchSearch = !state.searchTableQuery || 
        t.number.toLowerCase().includes(state.searchTableQuery.toLowerCase()) ||
        (t.waiter && t.waiter.toLowerCase().includes(state.searchTableQuery.toLowerCase()));
      return matchSection && matchStatus && matchSearch;
    });

    if (filtered.length === 0) {
      grid.style.display = 'none';
      if (emptyState) emptyState.style.display = 'flex';
    } else {
      grid.style.display = 'grid';
      if (emptyState) emptyState.style.display = 'none';
    }

    grid.innerHTML = filtered.map(t => {
      let occupClass = t.occupancy === 'occupied' ? 'occupied' : t.occupancy === 'reserved' ? 'reserved' : 'available';
      let occupText = t.occupancy === 'occupied' ? 'Dolu' : t.occupancy === 'reserved' ? 'Rezerve' : 'Boş';

      let actionBadgeHtml = '';
      if (t.occupancy === 'occupied' && t.opBadge) {
        if (t.opBadge === 'bill-requested') {
          actionBadgeHtml = `<div class="table-action-badge bill-requested"><svg class="icon" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg> Hesap İstendi</div>`;
        } else if (t.opBadge === 'cooking') {
          actionBadgeHtml = `<div class="table-action-badge kitchen-cooking"><svg class="icon" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg> Hazırlanıyor</div>`;
        } else if (t.opBadge === 'ready') {
          actionBadgeHtml = `<div class="table-action-badge kitchen-ready"><svg class="icon" viewBox="0 0 24 24"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg> Servise Hazır</div>`;
        }
      }

      let timerBarHtml = '';
      if (t.occupancy === 'occupied' && t.minutes) {
        const pct = Math.min(100, (t.minutes / 60) * 100);
        let barClass = 'table-elapsed-fill';
        if (t.minutes >= 45) barClass += ' alert';
        else if (t.minutes >= 25) barClass += ' warning';
        timerBarHtml = `
          <div class="table-elapsed-bar" title="${t.minutes} dakikadır açık">
            <div class="${barClass}" style="width: ${pct}%"></div>
          </div>
        `;
      }

      const btnLabel = t.occupancy === 'available' 
        ? 'Sipariş Aç' 
        : t.occupancy === 'reserved' 
          ? 'Misafiri Oturt >' 
          : 'Masayı Aç >';

      return `
        <div class="table-card" data-table-id="${t.id}">
          <div class="table-card-top">
            <span class="table-number">Masa ${t.number}</span>
            <span class="occupancy-pill ${occupClass}">${occupText}</span>
          </div>
          ${timerBarHtml}
          <div class="table-card-body">
            ${t.occupancy === 'available' 
              ? `<div class="meta-row"><svg class="icon" style="width:14px;height:14px" viewBox="0 0 24 24"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/></svg> Kapasite: ${t.capacity} Kişi</div>` 
              : t.occupancy === 'reserved'
                ? `<div class="meta-row"><strong style="color:var(--badge-reserv-text)">${t.note || '19:30 - 4 Kişi'}</strong></div>`
                : `<div class="meta-row"><svg class="icon" style="width:14px;height:14px" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg> ${t.minutes} dk • Garson: ${t.waiter || 'Mehmet K.'}</div>
                   <div class="table-amount num-val">${formatTL(t.billAmount)}</div>`
            }
            ${actionBadgeHtml}
          </div>
          <button type="button" class="table-card-btn">${btnLabel}</button>
        </div>
      `;
    }).join('');

    const openBillsCount = state.tables.filter(t => t.occupancy === 'occupied').length;
    const statOpen = document.getElementById('stat-open-bills');
    if (statOpen) statOpen.textContent = `${openBillsCount} Masa`;

    const countBillReq = state.tables.filter(t => t.opBadge === 'bill-requested').length;
    const countCooking = state.tables.filter(t => t.opBadge === 'cooking').length;
    const elBillReq = document.getElementById('count-bill-req');
    const elCooking = document.getElementById('count-cooking');
    if (elBillReq) elBillReq.textContent = countBillReq;
    if (elCooking) elCooking.textContent = countCooking;
  }

  // 5.3 Render Catalog Products with Allergens
  function renderCatalogProducts() {
    const grid = document.getElementById('pos-product-grid');
    if (!grid) return;

    const conceptData = getActiveConceptData();
    let filtered = conceptData.products.filter(p => {
      const matchCat = state.activeCategory === 'Tümü' || p.category === state.activeCategory;
      const matchSearch = !state.searchProductQuery || p.name.toLowerCase().includes(state.searchProductQuery.toLowerCase());
      return matchCat && matchSearch;
    });

    grid.innerHTML = filtered.map(p => {
      let allergenPill = '';
      if (p.allergen) {
        allergenPill = `<span class="allergen-tag" style="font-size:10px;font-weight:600;padding:2px 6px;border-radius:4px;background:var(--color-surface-active);color:var(--color-text-muted)">${p.allergen}</span>`;
      }

      return `
        <div class="product-card" data-prod-id="${p.id}">
          ${p.stock ? `<span class="stock-tag">${p.stock}</span>` : ''}
          <div class="prod-name">${p.name}</div>
          <div style="display:flex;justify-content:space-between;align-items:center;margin-top:6px">
            <div class="prod-price num-val">${formatTL(p.price)}</div>
            ${allergenPill}
          </div>
        </div>
      `;
    }).join('');
  }

  // 5.4 Render Cart Grouped by Coursing
  function renderCart() {
    const list = document.getElementById('pos-cart-items');
    const badge = document.getElementById('cart-item-count');
    const subtotalEl = document.getElementById('cart-subtotal');
    const discountRow = document.getElementById('row-discount');
    const discountEl = document.getElementById('cart-discount');
    const taxEl = document.getElementById('cart-tax');
    const grandTotalEl = document.getElementById('cart-grand-total');
    const submitBtn = document.getElementById('btn-pos-submit-order');

    if (!list) return;

    if (state.activeCart.length === 0) {
      list.innerHTML = `
        <div class="cart-empty-placeholder">
          <p>Henüz ürün seçilmedi.</p>
          <span>Soldaki menüden ürün ekleyiniz.</span>
        </div>
      `;
      if (badge) badge.textContent = '0 Kalem';
      if (subtotalEl) subtotalEl.textContent = '0,00 TL';
      if (discountRow) discountRow.style.display = 'none';
      if (taxEl) taxEl.textContent = '0,00 TL';
      if (grandTotalEl) grandTotalEl.textContent = '0,00 TL';
      if (submitBtn) submitBtn.disabled = true;
      return;
    }

    const conceptData = getActiveConceptData();
    const courses = conceptData.courses;

    let subtotal = 0;
    let fullHtml = '';

    courses.forEach(course => {
      const itemsInCourse = state.activeCart
        .map((item, originalIndex) => ({ item, originalIndex }))
        .filter(({ item }) => item.course === course.key);

      if (itemsInCourse.length > 0) {
        fullHtml += `
          <div class="course-group">
            <div class="course-header ${course.cssClass}">
              <span>${course.title}</span>
              <span style="font-size:11px;font-weight:600">${itemsInCourse.length} Kalem</span>
            </div>
        `;

        itemsInCourse.forEach(({ item, originalIndex }) => {
          const itemTotal = item.unitPrice * item.quantity;
          subtotal += itemTotal;

          const modDetails = [];
          if (item.selectedOptions && item.selectedOptions.length) {
            item.selectedOptions.forEach(opt => modDetails.push(`${opt.group}: ${opt.name}`));
          }
          if (item.quickTags && item.quickTags.length) modDetails.push(`Etiket: ${item.quickTags.join(', ')}`);
          if (item.note) modDetails.push(`Not: "${item.note}"`);

          const seatLabel = item.seat === 'shared' ? 'Ortaya' : `Koltuk ${item.seat}`;

          fullHtml += `
            <div class="cart-item-row" data-cart-index="${originalIndex}">
              <div class="cart-item-main btn-edit-cart-item" data-index="${originalIndex}" title="Düzenlemek için tıklayın">
                <div>
                  <span class="item-seat-badge">${seatLabel}</span>
                  <span class="cart-item-title">${item.isComplimentary ? '<strong style="color:#059669">[İkram] </strong>' : ''}${item.name}</span>
                </div>
                <span class="cart-item-price num-val">${formatTL(itemTotal)}</span>
              </div>
              ${modDetails.length ? `<div class="cart-item-modifiers btn-edit-cart-item" data-index="${originalIndex}">${modDetails.join('<br>')}</div>` : ''}
              <div class="cart-item-controls">
                <div class="qty-control">
                  <button type="button" class="btn-qty btn-qty-dec" data-index="${originalIndex}">-</button>
                  <span class="qty-num num-val">${item.quantity}</span>
                  <button type="button" class="btn-qty btn-qty-inc" data-index="${originalIndex}">+</button>
                </div>
                <div style="display:flex;gap:6px">
                  <button type="button" class="cart-item-actions-btn btn-item-manage" data-index="${originalIndex}">İkram / İptal</button>
                  <button type="button" class="btn-clear-cart btn-cart-remove" data-index="${originalIndex}">
                    <svg class="icon" viewBox="0 0 24 24"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/></svg>
                  </button>
                </div>
              </div>
            </div>
          `;
        });

        fullHtml += `</div>`;
      }
    });

    list.innerHTML = fullHtml;

    let discountAmount = 0;
    if (state.activeDiscount > 0) {
      if (state.activeDiscount < 1) {
        discountAmount = subtotal * state.activeDiscount;
      } else {
        discountAmount = state.activeDiscount;
      }
      if (discountRow) {
        discountRow.style.display = 'flex';
        discountEl.textContent = `-${formatTL(discountAmount)}`;
      }
    } else {
      if (discountRow) discountRow.style.display = 'none';
    }

    const discountedSubtotal = Math.max(0, subtotal - discountAmount);
    const tax = discountedSubtotal * 0.10;
    const total = discountedSubtotal;

    if (badge) badge.textContent = `${state.activeCart.length} Kalem`;
    if (subtotalEl) subtotalEl.textContent = formatTL(subtotal);
    if (taxEl) taxEl.textContent = formatTL(tax);
    if (grandTotalEl) grandTotalEl.textContent = formatTL(total);
    if (submitBtn) submitBtn.disabled = !state.isOnline;
  }

  // 5.5 Render Dynamic Universal Modifier Sheet
  function openDynamicModifierModal(prod, existingItem = null) {
    state.activeModifierProduct = prod;
    state.selectedQuickTags = existingItem ? [...(existingItem.quickTags || [])] : [];

    const modal = document.getElementById('modal-modifier');
    const titleEl = document.getElementById('mod-title');
    const priceEl = document.getElementById('mod-price');
    const container = document.getElementById('mod-dynamic-options-container');

    if (titleEl) titleEl.textContent = existingItem ? `${prod.name} (Düzenle)` : prod.name;
    if (priceEl) priceEl.textContent = formatTL(prod.price);

    const conceptData = getActiveConceptData();
    let bodyHtml = '';

    // 1. Coursing Selector Group
    bodyHtml += `
      <div class="option-group">
        <label class="group-label">Servis Aşaması (Coursing)</label>
        <div class="radio-pill-group">
          ${conceptData.courses.map((c, i) => `
            <label class="radio-pill">
              <input type="radio" name="courseType" value="${c.key}" ${existingItem ? (existingItem.course === c.key ? 'checked' : '') : (i === 1 ? 'checked' : '')}>
              <span>${c.key}</span>
            </label>
          `).join('')}
        </div>
      </div>
    `;

    // 2. Dynamic Product Modifier Groups
    const groups = prod.modifierGroups || [];
    groups.forEach(g => {
      bodyHtml += `
        <div class="option-group">
          <label class="group-label">${g.title} ${g.required ? '<span class="required-tag">Zorunlu</span>' : ''}</label>
      `;

      if (g.type === 'single') {
        bodyHtml += `
          <div class="radio-pill-group">
            ${g.options.map(opt => {
              const isChecked = existingItem 
                ? existingItem.selectedOptions?.some(o => o.group === g.title && o.name === opt.name)
                : opt.default;
              return `
                <label class="radio-pill">
                  <input type="radio" name="group_${g.id}" value="${opt.name}" data-group-title="${g.title}" data-extra-price="${opt.price}" ${isChecked ? 'checked' : ''}>
                  <span>${opt.name} ${opt.price > 0 ? '(+' + formatTL(opt.price) + ')' : ''}</span>
                </label>
              `;
            }).join('')}
          </div>
        `;
      } else {
        bodyHtml += `
          <div class="checkbox-list">
            ${g.options.map(opt => {
              const isChecked = existingItem 
                ? existingItem.selectedOptions?.some(o => o.group === g.title && o.name === opt.name)
                : false;
              return `
                <label class="check-row">
                  <input type="checkbox" name="group_${g.id}" value="${opt.name}" data-group-title="${g.title}" data-extra-price="${opt.price}" ${isChecked ? 'checked' : ''}>
                  <span class="check-text">${opt.name}</span>
                  <span class="check-price">${opt.price > 0 ? '+' + formatTL(opt.price) : 'Ücretsiz'}</span>
                </label>
              `;
            }).join('')}
          </div>
        `;
      }
      bodyHtml += `</div>`;
    });

    // 3. Quick Tags & Notes
    bodyHtml += `
      <div class="option-group">
        <label class="group-label">Hızlı Hazırlık Etiketleri</label>
        <div class="quick-tags-container">
          <button type="button" class="quick-tag-btn" data-tag="Sos Ayrı">Sos Ayrı</button>
          <button type="button" class="quick-tag-btn" data-tag="Buzsuz">Buzsuz</button>
          <button type="button" class="quick-tag-btn" data-tag="Tuzsuz">Tuzsuz</button>
          <button type="button" class="quick-tag-btn" data-tag="Ayrı Tabak">Ayrı Tabak</button>
          <button type="button" class="quick-tag-btn" data-tag="Çok Sıcak">Çok Sıcak</button>
        </div>
      </div>
      <div class="option-group">
        <label class="group-label" for="mod-special-note">Özel Sipariş Notu</label>
        <input type="text" id="mod-special-note" placeholder="Örn. Şef Notu..." value="${existingItem?.note || ''}" autocomplete="off">
      </div>
    `;

    container.innerHTML = bodyHtml;

    // Attach real-time price calculations
    const inputs = container.querySelectorAll('input[type="radio"], input[type="checkbox"]');
    inputs.forEach(inp => inp.addEventListener('change', updateDynamicModifierPrice));

    // Attach Quick Tags
    container.querySelectorAll('.quick-tag-btn').forEach(btn => {
      const tag = btn.dataset.tag;
      if (state.selectedQuickTags.includes(tag)) btn.classList.add('active');

      btn.addEventListener('click', () => {
        btn.classList.toggle('active');
        if (btn.classList.contains('active')) {
          state.selectedQuickTags.push(tag);
        } else {
          state.selectedQuickTags = state.selectedQuickTags.filter(t => t !== tag);
        }
      });
    });

    const confirmBtn = document.getElementById('btn-confirm-modifier');
    if (confirmBtn) {
      confirmBtn.innerHTML = existingItem 
        ? `Güncelle ve Kaydet (<span id="mod-final-price">${formatTL(prod.price)}</span>)`
        : `Sepete Ekle (<span id="mod-final-price">${formatTL(prod.price)}</span>)`;
    }

    updateDynamicModifierPrice();
    if (modal) modal.style.display = 'flex';
  }

  function updateDynamicModifierPrice() {
    if (!state.activeModifierProduct) return;
    let extraTotal = 0;
    const container = document.getElementById('mod-dynamic-options-container');
    if (container) {
      container.querySelectorAll('input:checked').forEach(inp => {
        extraTotal += parseFloat(inp.dataset.extraPrice || 0);
      });
    }

    const currentTotal = state.activeModifierProduct.price + extraTotal;
    const finalPriceEl = document.getElementById('mod-final-price');
    const priceSubtitleEl = document.getElementById('mod-price');
    if (finalPriceEl) finalPriceEl.textContent = formatTL(currentTotal);
    if (priceSubtitleEl) priceSubtitleEl.textContent = formatTL(currentTotal);
  }

  // 5.6 Render 80mm ESC/POS Thermal Slip
  function renderThermalSlip() {
    const container = document.getElementById('thermal-slip-content');
    const subTitle = document.getElementById('thermal-slip-subtitle');
    if (!container || !state.selectedTable) return;

    const conceptData = getActiveConceptData();
    if (subTitle) subTitle.textContent = `Masa ${state.selectedTable.number} (${conceptData.name})`;

    const items = state.activeCart.length > 0 ? state.activeCart : [
      { name: 'Ana Kalem', unitPrice: 240.00, quantity: 1, seat: '1' },
      { name: 'İçecek', unitPrice: 45.00, quantity: 2, seat: 'shared' }
    ];

    const subtotal = items.reduce((sum, i) => sum + (i.unitPrice * i.quantity), 0);
    const tax = subtotal * 0.10;
    const total = subtotal;

    container.innerHTML = `
      <div class="slip-paper">
        <div class="slip-header">
          <div class="slip-brand">*** ALKAROS RESTAURANT ***</div>
          <div class="slip-meta">${conceptData.name.toUpperCase()}</div>
          <div class="slip-meta">Masa: ${state.selectedTable.number} | Garson: ${state.selectedTable.waiter || 'Mehmet K.'}</div>
          <div class="slip-meta">Tarih: ${new Date().toLocaleDateString('tr-TR')} ${new Date().toLocaleTimeString('tr-TR')}</div>
          <div class="slip-divider">------------------------------------------</div>
        </div>
        <div class="slip-items">
          ${items.map(i => `
            <div class="slip-line">
              <span>${i.quantity}x ${i.name}</span>
              <span class="num-val">${formatTL(i.unitPrice * i.quantity)}</span>
            </div>
          `).join('')}
        </div>
        <div class="slip-divider">------------------------------------------</div>
        <div class="slip-totals">
          <div class="slip-line"><span>ARA TOPLAM:</span><span>${formatTL(subtotal)}</span></div>
          <div class="slip-line"><span>KDV (%10):</span><span>${formatTL(tax)}</span></div>
          <div class="slip-line slip-grand-total"><strong>GENEL TOPLAM:</strong><strong>${formatTL(total)}</strong></div>
        </div>
        <div class="slip-footer">
          <div class="slip-divider">------------------------------------------</div>
          <div>BU BİR ÖN ADİSYON BİLGİ FİŞİDİR</div>
          <div>MALİ DEĞERİ YOKTUR</div>
          <div class="slip-barcode">||| | ||||| ||| |||| |||| |||</div>
        </div>
      </div>
    `;
  }

  // 5.7 Render Operations & Printers
  function renderOperations() {
    const feed = document.getElementById('ops-tickets-feed');
    const printersList = document.getElementById('ops-printers-list');
    const auditBox = document.getElementById('ops-audit-log');
    const ticketCountEl = document.getElementById('ops-ticket-count');

    if (feed) {
      let filtered = state.tickets.filter(t => {
        return state.activeStationFilter === 'all' || t.station === state.activeStationFilter;
      });

      if (ticketCountEl) ticketCountEl.textContent = `${filtered.length} Fiş`;

      feed.innerHTML = filtered.map(ticket => `
        <div class="ticket-card">
          <div class="ticket-top">
            <span>Fiş #${ticket.id} — ${ticket.table}</span>
            <span class="meta-row">${ticket.time}</span>
          </div>
          <div class="ticket-items-list">
            ${ticket.items.map(i => {
              let badgeHtml = '';
              if (i.status === 'cooking') badgeHtml = '<span class="occupancy-pill" style="background:var(--badge-cooking-bg);color:var(--badge-cooking-text)">Hazırlanıyor</span>';
              else if (i.status === 'ready') badgeHtml = '<span class="occupancy-pill" style="background:var(--badge-ready-bg);color:var(--badge-ready-text)">Hazır</span>';
              else badgeHtml = '<span class="occupancy-pill" style="background:var(--color-surface-active);color:var(--color-text-muted)">Bekliyor</span>';
              return `<div class="ticket-item-row"><span>${i.name}</span>${badgeHtml}</div>`;
            }).join('')}
          </div>
        </div>
      `).join('');
    }

    if (printersList) {
      printersList.innerHTML = state.printers.map(p => `
        <div class="printer-card ${p.status === 'paper_out' ? 'warning' : ''}">
          <div class="printer-info">
            <span class="printer-name">${p.name}</span>
            <span class="printer-ip">IP: ${p.ip} • Durum: <strong>${p.status === 'paper_out' ? p.issue : 'Çevrimiçi'}</strong></span>
          </div>
          <div>
            ${p.status === 'paper_out' 
              ? `<button type="button" class="btn-secondary" id="btn-open-reroute" style="padding:6px 12px;font-size:12px">Yönlendir</button>`
              : `<span class="occupancy-pill available">Normal</span>`
            }
          </div>
        </div>
      `).join('');
    }

    if (auditBox) {
      auditBox.innerHTML = state.auditLogs.map(l => `<div class="log-entry">${l}</div>`).join('');
    }
  }

  // 5.8 Render Waiter Surface
  function renderWaiterSurface() {
    const grid = document.getElementById('waiter-tables-container');
    const productList = document.getElementById('wtr-product-list');
    const cartContainer = document.getElementById('wtr-cart-items');
    const wtrTotal = document.getElementById('wtr-cart-total');
    const wtrCount = document.getElementById('wtr-cart-count');
    const wtrBtnPrice = document.getElementById('wtr-btn-price');
    const notifFeed = document.getElementById('wtr-notif-feed');
    const unreadDot = document.getElementById('wtr-unread-dot');

    if (grid) {
      let filtered = state.tables.filter(t => {
        const matchSection = state.wtrSectionFilter === 'Tümü' || 
          (state.wtrSectionFilter === 'mine' && t.waiter === 'Mehmet K.') ||
          t.section === state.wtrSectionFilter;
        const matchSearch = !state.wtrSearchQuery || t.number.toLowerCase().includes(state.wtrSearchQuery.toLowerCase());
        return matchSection && matchSearch;
      });

      grid.innerHTML = filtered.map(t => {
        let occupClass = t.occupancy === 'occupied' ? 'occupied' : t.occupancy === 'reserved' ? 'reserved' : 'available';
        let occupText = t.occupancy === 'occupied' ? 'Dolu' : t.occupancy === 'reserved' ? 'Rezerve' : 'Boş';
        return `
          <div class="table-card" data-wtr-table-id="${t.id}">
            <div class="table-card-top">
              <span class="table-number">Masa ${t.number}</span>
              <span class="occupancy-pill ${occupClass}">${occupText}</span>
            </div>
            <div class="table-card-body">
              ${t.occupancy === 'occupied' ? `<div class="table-amount num-val">${formatTL(t.billAmount)}</div>` : `<div class="meta-row">${t.capacity} Kişi</div>`}
            </div>
            <button type="button" class="table-card-btn">${t.occupancy === 'available' ? 'Sipariş Aç' : t.occupancy === 'reserved' ? 'Misafiri Oturt >' : 'Masayı Aç >'}</button>
          </div>
        `;
      }).join('');
    }

    const conceptData = getActiveConceptData();
    if (productList) {
      productList.innerHTML = conceptData.products.map(p => `
        <div class="product-card" data-wtr-prod-id="${p.id}">
          <div class="prod-name">${p.name}</div>
          <div class="prod-price num-val">${formatTL(p.price)}</div>
        </div>
      `).join('');
    }

    if (cartContainer) {
      let total = 0;
      if (state.wtrCart.length === 0) {
        cartContainer.innerHTML = '<span style="font-size:12px;color:var(--color-text-dim)">Sepet boş</span>';
      } else {
        cartContainer.innerHTML = state.wtrCart.map(item => {
          total += item.unitPrice * item.quantity;
          return `
            <div style="display:flex;justify-content:space-between;font-size:13px">
              <span>${item.quantity}x ${item.name}</span>
              <span class="num-val">${formatTL(item.unitPrice * item.quantity)}</span>
            </div>
          `;
        }).join('');
      }
      if (wtrTotal) wtrTotal.textContent = formatTL(total);
      if (wtrCount) wtrCount.textContent = state.wtrCart.length;
      if (wtrBtnPrice) wtrBtnPrice.textContent = formatTL(total);
    }

    if (notifFeed) {
      const unreadCount = state.notifications.filter(n => n.unread).length;
      if (unreadDot) unreadDot.style.display = unreadCount > 0 ? 'block' : 'none';

      notifFeed.innerHTML = state.notifications.map((n, i) => `
        <div class="wtr-notif-card ${n.unread ? 'unread' : ''}">
          <div>
            <div style="font-weight:600;font-size:13px">${n.text}</div>
            <div class="meta-row">${n.time}</div>
          </div>
          <button type="button" class="btn-secondary-sm btn-deliver-notif" data-notif-idx="${i}">Teslim Ettim</button>
        </div>
      `).join('');
    }
  }

  // --- 6. EVENT ATTACHMENTS ---

  function setupEvents() {
    // 6.1 Concept Switcher Event
    const conceptSelect = document.getElementById('select-restaurant-concept');
    if (conceptSelect) {
      conceptSelect.addEventListener('change', (e) => {
        state.activeConcept = e.target.value;
        state.activeCategory = 'Tümü';
        state.activeCart = [];
        state.activeDiscount = 0;

        renderConceptNavigation();
        renderCatalogProducts();
        renderCart();
        renderWaiterSurface();
        showToast(`Konsept Değiştirildi: ${getActiveConceptData().name}`);
      });
    }

    // 6.2 Simulator View Switcher
    document.querySelectorAll('.proto-btn[data-view]').forEach(btn => {
      btn.addEventListener('click', (e) => {
        document.querySelectorAll('.proto-btn[data-view]').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        state.currentView = btn.dataset.view;

        const cashierSurf = document.getElementById('surface-cashier');
        const waiterSurf = document.getElementById('surface-waiter');
        const waiterFrame = document.getElementById('waiter-device-frame');

        if (state.currentView === 'cashier') {
          cashierSurf.style.display = 'flex';
          waiterSurf.style.display = 'none';
        } else if (state.currentView === 'waiter-phone') {
          cashierSurf.style.display = 'none';
          waiterSurf.style.display = 'flex';
          waiterFrame.className = 'device-frame phone-mode';
        } else if (state.currentView === 'waiter-tablet') {
          cashierSurf.style.display = 'none';
          waiterSurf.style.display = 'flex';
          waiterFrame.className = 'device-frame tablet-mode';
        }
      });
    });

    // 6.3 Theme Toggle
    const themeBtn = document.getElementById('btn-theme-toggle');
    if (themeBtn) {
      themeBtn.addEventListener('click', () => {
        state.theme = state.theme === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', state.theme);
        localStorage.setItem('alkaros_theme', state.theme);
      });
    }

    // 6.4 Network Outage Toggle
    const netBtn = document.getElementById('btn-sim-network');
    if (netBtn) {
      netBtn.addEventListener('click', () => {
        state.isOnline = !state.isOnline;
        const banner = document.getElementById('network-outage-banner');
        const labelNet = document.getElementById('label-network');
        const cuiNetDot = document.getElementById('cashier-net-status');
        const wtrPill = document.getElementById('waiter-net-pill');
        const wtrOffBar = document.getElementById('waiter-offline-bar');

        if (!state.isOnline) {
          labelNet.textContent = 'Ağ: Kesildi (Offline)';
          netBtn.classList.add('active-danger');
          if (banner) banner.style.display = 'flex';
          if (cuiNetDot) cuiNetDot.className = 'connection-status offline';
          if (wtrPill) wtrPill.innerHTML = '<span class="dot" style="background:#DC2626"></span><span>Çevrimdışı</span>';
          if (wtrOffBar) wtrOffBar.style.display = 'flex';
          showToast('Ağ bağlantısı koptu! Sistem çevrimdışı moda geçti.', 'warning');
        } else {
          labelNet.textContent = 'Ağ: Çevrimiçi';
          netBtn.classList.remove('active-danger');
          if (banner) banner.style.display = 'none';
          if (cuiNetDot) cuiNetDot.className = 'connection-status';
          if (wtrPill) wtrPill.innerHTML = '<span class="dot"></span><span>Çevrimiçi</span>';
          if (wtrOffBar) wtrOffBar.style.display = 'none';

          if (state.wtrOfflineQueue.length > 0) {
            const count = state.wtrOfflineQueue.length;
            showToast(`Ağ bağlantısı kuruldu! ${count} çevrimdışı sipariş mutfağa iletildi.`, 'success');
            state.wtrOfflineQueue = [];
            const qCountEl = document.getElementById('waiter-queue-count');
            if (qCountEl) qCountEl.textContent = '0';
          } else {
            showToast('Ağ bağlantısı kuruldu. Masa verileri senkronize edildi.', 'success');
          }
        }
        renderCart();
      });
    }

    // 6.5 Cashier Tabs
    const tabTables = document.getElementById('tab-cui-tables');
    const tabOps = document.getElementById('tab-cui-operations');
    const viewTables = document.getElementById('cui-view-tables');
    const viewOps = document.getElementById('cui-view-operations');
    const viewOrder = document.getElementById('cui-view-order-entry');

    if (tabTables && tabOps) {
      tabTables.addEventListener('click', () => {
        tabTables.classList.add('active');
        tabOps.classList.remove('active');
        viewTables.style.display = 'flex';
        viewOps.style.display = 'none';
        viewOrder.style.display = 'none';
      });

      tabOps.addEventListener('click', () => {
        tabOps.classList.add('active');
        tabTables.classList.remove('active');
        viewTables.style.display = 'none';
        viewOps.style.display = 'flex';
        viewOrder.style.display = 'none';
        renderOperations();
      });
    }

    // 6.6 Section & Status Filters
    document.querySelectorAll('.filter-chips .chip').forEach(chip => {
      chip.addEventListener('click', () => {
        document.querySelectorAll('.filter-chips .chip').forEach(c => c.classList.remove('active'));
        chip.classList.add('active');
        state.activeSectionFilter = chip.dataset.section;
        renderCashierTables();
      });
    });

    document.querySelectorAll('.status-quick-filters .filter-tag-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        document.querySelectorAll('.status-quick-filters .filter-tag-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        state.activeStatusFilter = btn.dataset.statusFilter;
        renderCashierTables();
      });
    });

    // 6.7 Search Table
    const searchInput = document.getElementById('input-search-tables');
    const clearSearchBtn = document.getElementById('btn-clear-table-search');
    if (searchInput) {
      searchInput.addEventListener('input', (e) => {
        state.searchTableQuery = e.target.value.trim();
        if (clearSearchBtn) clearSearchBtn.style.display = state.searchTableQuery ? 'flex' : 'none';
        renderCashierTables();
      });
    }
    if (clearSearchBtn) {
      clearSearchBtn.addEventListener('click', () => {
        if (searchInput) searchInput.value = '';
        state.searchTableQuery = '';
        clearSearchBtn.style.display = 'none';
        renderCashierTables();
      });
    }

    // 6.8 Table Card Click -> Open POS
    const tableGrid = document.getElementById('cashier-table-grid');
    if (tableGrid) {
      tableGrid.addEventListener('click', (e) => {
        const card = e.target.closest('.table-card');
        if (!card) return;
        const tableId = card.dataset.tableId;
        const table = state.tables.find(t => t.id === tableId);
        if (!table) return;

        if (table.occupancy === 'reserved') {
          table.occupancy = 'occupied';
          table.waiter = 'Mehmet K.';
          table.minutes = 1;
          showToast(`Masa ${table.number} misafiri oturtuldu. Sipariş girişi açıldı.`);
        }

        state.selectedTable = table;
        state.activeCart = [];
        state.activeDiscount = 0;

        viewTables.style.display = 'none';
        viewOrder.style.display = 'flex';
        
        const titleEl = document.getElementById('pos-active-table-title');
        if (titleEl) {
          titleEl.innerHTML = `Masa ${table.number} <span class="table-section-tag">(${table.section})</span>`;
        }

        renderCatalogProducts();
        renderCart();
      });
    }

    // 6.9 Back to Tables
    const btnBack = document.getElementById('btn-pos-back-to-tables');
    if (btnBack) {
      btnBack.addEventListener('click', () => {
        viewOrder.style.display = 'none';
        viewTables.style.display = 'flex';
        renderCashierTables();
      });
    }

    // 6.10 Seat Selector
    document.querySelectorAll('.seat-chip').forEach(chip => {
      chip.addEventListener('click', () => {
        document.querySelectorAll('.seat-chip').forEach(c => c.classList.remove('active'));
        chip.classList.add('active');
        state.activeSeat = chip.dataset.seat;
      });
    });

    // 6.11 Repeat Round
    const btnRepeatRound = document.getElementById('btn-action-repeat-round');
    if (btnRepeatRound) {
      btnRepeatRound.addEventListener('click', () => {
        if (!state.selectedTable) return;
        const conceptData = getActiveConceptData();
        const drink = conceptData.products.find(p => p.category.includes('İçecek') || p.category.includes('Kahve')) || conceptData.products[0];
        state.activeCart.push({
          id: 'rep_' + Math.random().toString(36).substring(2, 7),
          name: drink.name,
          unitPrice: drink.price,
          quantity: 2,
          course: drink.defaultCourse || 'Başlangıç',
          seat: state.activeSeat,
          selectedOptions: [],
          quickTags: [],
          note: 'Tur Tekrarı',
          isComplimentary: false
        });
        renderCart();
        showToast(`Masa ${state.selectedTable.number} içecek turu kopyalandı! (2x ${drink.name})`);
      });
    }

    // 6.12 Custom Item Modal
    const btnCustomItem = document.getElementById('btn-action-custom-item');
    const modalCustom = document.getElementById('modal-custom-item');
    const btnCloseCustom = document.getElementById('btn-close-custom-modal');
    const btnCancelCustom = document.getElementById('btn-cancel-custom');
    const btnConfirmCustom = document.getElementById('btn-confirm-custom');

    if (btnCustomItem) {
      btnCustomItem.addEventListener('click', () => {
        document.getElementById('input-custom-name').value = '';
        document.getElementById('input-custom-price').value = '';
        if (modalCustom) modalCustom.style.display = 'flex';
      });
    }
    if (btnCloseCustom) btnCloseCustom.addEventListener('click', () => modalCustom.style.display = 'none');
    if (btnCancelCustom) btnCancelCustom.addEventListener('click', () => modalCustom.style.display = 'none');
    if (btnConfirmCustom) {
      btnConfirmCustom.addEventListener('click', () => {
        const name = document.getElementById('input-custom-name')?.value.trim();
        const price = parseFloat(document.getElementById('input-custom-price')?.value || 0);
        const course = document.querySelector('input[name="customCourse"]:checked')?.value || 'Ana Yemek';

        if (!name || price <= 0) {
          showToast('Lütfen geçerli bir ürün adı ve fiyat giriniz!', 'warning');
          return;
        }

        const customItem = {
          id: 'custom_' + Date.now(),
          name: `[Özel] ${name}`,
          unitPrice: price,
          quantity: 1,
          course: course,
          seat: state.activeSeat,
          selectedOptions: [],
          quickTags: [],
          note: 'Menü Dışı Açık Kalem',
          isComplimentary: false
        };

        state.activeCart.push(customItem);
        showToast(`${customItem.name} sepete eklendi.`);
        if (modalCustom) modalCustom.style.display = 'none';
        renderCart();
      });
    }

    // 6.13 80mm ESC/POS Thermal Slip
    const btnPrintPrebill = document.getElementById('btn-action-print-prebill');
    const modalThermal = document.getElementById('modal-thermal-slip');
    const btnCloseThermal = document.getElementById('btn-close-thermal');
    const btnCloseThermalBtn = document.getElementById('btn-close-thermal-btn');
    const btnPrintHardware = document.getElementById('btn-print-hardware');

    if (btnPrintPrebill) {
      btnPrintPrebill.addEventListener('click', () => {
        if (!state.selectedTable) return;
        renderThermalSlip();
        if (modalThermal) modalThermal.style.display = 'flex';
      });
    }
    if (btnCloseThermal) btnCloseThermal.addEventListener('click', () => modalThermal.style.display = 'none');
    if (btnCloseThermalBtn) btnCloseThermalBtn.addEventListener('click', () => modalThermal.style.display = 'none');
    if (btnPrintHardware) {
      btnPrintHardware.addEventListener('click', () => {
        state.selectedTable.opBadge = 'bill-requested';
        showToast(`Masa ${state.selectedTable.number} ön adisyon fişi yazıcıya iletildi. Durum: Hesap İstendi.`);
        if (modalThermal) modalThermal.style.display = 'none';
        viewOrder.style.display = 'none';
        viewTables.style.display = 'flex';
        renderCashierTables();
      });
    }

    // 6.14 Table Merge Modal
    const btnMerge = document.getElementById('btn-action-merge-table');
    const modalMerge = document.getElementById('modal-merge-table');
    const btnCloseMerge = document.getElementById('btn-close-merge-modal');
    const btnCancelMerge = document.getElementById('btn-cancel-merge');
    const btnConfirmMerge = document.getElementById('btn-confirm-merge');
    const selectMergeTarget = document.getElementById('select-merge-target-table');

    if (btnMerge) {
      btnMerge.addEventListener('click', () => {
        if (!state.selectedTable) return;
        const otherOccupied = state.tables.filter(t => t.occupancy === 'occupied' && t.id !== state.selectedTable.id);
        if (otherOccupied.length === 0) {
          showToast('Birleştirilecek başka açık/dolu masa bulunamadı!', 'warning');
          return;
        }
        if (selectMergeTarget) {
          selectMergeTarget.innerHTML = otherOccupied.map(t => `<option value="${t.id}">Masa ${t.number} (${t.section} - ${formatTL(t.billAmount)})</option>`).join('');
        }
        const titleEl = document.getElementById('merge-source-title');
        if (titleEl) titleEl.textContent = `Ana Masa: Masa ${state.selectedTable.number}`;
        if (modalMerge) modalMerge.style.display = 'flex';
      });
    }

    if (btnCloseMerge) btnCloseMerge.addEventListener('click', () => modalMerge.style.display = 'none');
    if (btnCancelMerge) btnCancelMerge.addEventListener('click', () => modalMerge.style.display = 'none');
    if (btnConfirmMerge) {
      btnConfirmMerge.addEventListener('click', () => {
        const targetId = selectMergeTarget?.value;
        const targetTable = state.tables.find(t => t.id === targetId);
        if (targetTable && state.selectedTable) {
          state.selectedTable.billAmount = (state.selectedTable.billAmount || 0) + (targetTable.billAmount || 0);

          targetTable.occupancy = 'available';
          targetTable.billAmount = 0.00;
          targetTable.waiter = null;
          targetTable.minutes = null;
          targetTable.opBadge = null;

          state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] Masa Birleştirme: Masa ${targetTable.number} -> Masa ${state.selectedTable.number} ile birleştirildi.`);
          showToast(`Masa ${targetTable.number} adisyonu Masa ${state.selectedTable.number} ile birleştirildi!`);

          if (modalMerge) modalMerge.style.display = 'none';
          renderCart();
        }
      });
    }

    // 6.15 Discount Modal
    const btnAddDiscount = document.getElementById('btn-cart-add-discount');
    const modalDiscount = document.getElementById('modal-discount');
    const btnCloseDiscount = document.getElementById('btn-close-discount-modal');
    const btnCancelDiscount = document.getElementById('btn-cancel-discount');
    const btnConfirmDiscount = document.getElementById('btn-confirm-discount');

    if (btnAddDiscount) {
      btnAddDiscount.addEventListener('click', () => {
        if (modalDiscount) modalDiscount.style.display = 'flex';
      });
    }
    if (btnCloseDiscount) btnCloseDiscount.addEventListener('click', () => modalDiscount.style.display = 'none');
    if (btnCancelDiscount) btnCancelDiscount.addEventListener('click', () => modalDiscount.style.display = 'none');
    if (btnConfirmDiscount) {
      btnConfirmDiscount.addEventListener('click', () => {
        const rateVal = parseFloat(document.querySelector('input[name="discountRate"]:checked')?.value || 10);
        const reason = document.getElementById('select-discount-reason')?.value;

        if (rateVal === 50) {
          state.activeDiscount = 50.00;
        } else {
          state.activeDiscount = rateVal / 100;
        }

        state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] İndirim: Masa ${state.selectedTable.number} için ${rateVal === 50 ? '50 TL Sabit' : '%' + rateVal} uygulandı (${reason}).`);
        showToast('Yetkili indirimi adisyona uygulandı.');

        if (modalDiscount) modalDiscount.style.display = 'none';
        renderCart();
      });
    }

    // 6.16 Product Grid Click -> Open Dynamic Modifier Sheet
    const prodGrid = document.getElementById('pos-product-grid');
    if (prodGrid) {
      prodGrid.addEventListener('click', (e) => {
        const card = e.target.closest('.product-card');
        if (!card) return;
        const prodId = card.dataset.prodId;
        const conceptData = getActiveConceptData();
        const prod = conceptData.products.find(p => p.id === prodId);
        if (!prod) return;

        state.editingCartIndex = null;
        openDynamicModifierModal(prod);
      });
    }

    // 6.17 Dynamic Modifier Sheet Confirm Button
    const btnConfirmMod = document.getElementById('btn-confirm-modifier');
    const btnCloseMod = document.getElementById('btn-close-modifier');
    const btnCancelMod = document.getElementById('btn-cancel-modifier');
    const modModal = document.getElementById('modal-modifier');

    const closeModModal = () => {
      if (modModal) modModal.style.display = 'none';
      state.activeModifierProduct = null;
      state.editingCartIndex = null;
    };

    if (btnCloseMod) btnCloseMod.addEventListener('click', closeModModal);
    if (btnCancelMod) btnCancelMod.addEventListener('click', closeModModal);

    if (btnConfirmMod) {
      btnConfirmMod.addEventListener('click', () => {
        if (!state.activeModifierProduct) return;

        const courseEl = document.querySelector('#modal-modifier input[name="courseType"]:checked');
        const course = courseEl ? courseEl.value : 'Ana Yemek';

        const selectedOptions = [];
        let extraTotal = 0;

        const container = document.getElementById('mod-dynamic-options-container');
        if (container) {
          container.querySelectorAll('input:checked').forEach(inp => {
            if (inp.name !== 'courseType') {
              const optName = inp.value;
              const group = inp.dataset.groupTitle || 'Seçenek';
              const price = parseFloat(inp.dataset.extraPrice || 0);
              extraTotal += price;
              selectedOptions.push({ group, name: optName, price });
            }
          });
        }

        const note = document.getElementById('mod-special-note')?.value.trim() || null;

        if (state.editingCartIndex !== null) {
          const existingItem = state.activeCart[state.editingCartIndex];
          existingItem.unitPrice = state.activeModifierProduct.price + extraTotal;
          existingItem.course = course;
          existingItem.selectedOptions = selectedOptions;
          existingItem.quickTags = [...state.selectedQuickTags];
          existingItem.note = note;
          showToast(`${existingItem.name} sipariş detayları güncellendi.`);
        } else {
          const cartItem = {
            id: state.activeModifierProduct.id,
            name: state.activeModifierProduct.name,
            unitPrice: state.activeModifierProduct.price + extraTotal,
            quantity: 1,
            course,
            seat: state.activeSeat,
            selectedOptions,
            quickTags: [...state.selectedQuickTags],
            note,
            isComplimentary: false
          };
          state.activeCart.push(cartItem);
          showToast(`${cartItem.name} (${course} - ${cartItem.seat === 'shared' ? 'Ortaya' : 'Koltuk ' + cartItem.seat}) eklendi.`);
        }

        closeModModal();
        renderCart();
      });
    }

    // 6.18 Cart Item Click (Edit Modifier) / Dec / Inc / Remove / Manage
    const cartItemsList = document.getElementById('pos-cart-items');
    const modalItemAction = document.getElementById('modal-item-action');
    const btnCloseItemAction = document.getElementById('btn-close-item-action');
    const btnCancelItemAction = document.getElementById('btn-cancel-item-action');
    const btnConfirmItemAction = document.getElementById('btn-confirm-item-action');

    if (cartItemsList) {
      cartItemsList.addEventListener('click', (e) => {
        const editTrigger = e.target.closest('.btn-edit-cart-item');
        const incBtn = e.target.closest('.btn-qty-inc');
        const decBtn = e.target.closest('.btn-qty-dec');
        const removeBtn = e.target.closest('.btn-cart-remove');
        const manageBtn = e.target.closest('.btn-item-manage');

        if (editTrigger) {
          const idx = parseInt(editTrigger.dataset.index, 10);
          const item = state.activeCart[idx];
          if (!item) return;

          state.editingCartIndex = idx;
          const conceptData = getActiveConceptData();
          const prod = conceptData.products.find(p => p.name === item.name || p.id === item.id) || { id: item.id, name: item.name, price: item.unitPrice, modifierGroups: [] };
          openDynamicModifierModal(prod, item);
        } else if (incBtn) {
          const idx = parseInt(incBtn.dataset.index, 10);
          state.activeCart[idx].quantity += 1;
          renderCart();
        } else if (decBtn) {
          const idx = parseInt(decBtn.dataset.index, 10);
          if (state.activeCart[idx].quantity > 1) {
            state.activeCart[idx].quantity -= 1;
          } else {
            state.activeCart.splice(idx, 1);
          }
          renderCart();
        } else if (removeBtn) {
          const idx = parseInt(removeBtn.dataset.index, 10);
          state.activeCart.splice(idx, 1);
          renderCart();
        } else if (manageBtn) {
          const idx = parseInt(manageBtn.dataset.index, 10);
          state.selectedCartItemIndex = idx;
          const item = state.activeCart[idx];
          const subTitle = document.getElementById('item-action-subtitle');
          if (subTitle) subTitle.textContent = `${item.quantity}x ${item.name}`;
          if (modalItemAction) modalItemAction.style.display = 'flex';
        }
      });
    }

    if (btnCloseItemAction) btnCloseItemAction.addEventListener('click', () => modalItemAction.style.display = 'none');
    if (btnCancelItemAction) btnCancelItemAction.addEventListener('click', () => modalItemAction.style.display = 'none');
    if (btnConfirmItemAction) {
      btnConfirmItemAction.addEventListener('click', () => {
        if (state.selectedCartItemIndex !== null && state.activeCart[state.selectedCartItemIndex]) {
          const actionType = document.querySelector('input[name="itemActionType"]:checked')?.value;
          const reason = document.getElementById('select-item-reason')?.value;
          const item = state.activeCart[state.selectedCartItemIndex];

          if (actionType === 'complimentary') {
            item.unitPrice = 0.00;
            item.isComplimentary = true;
            state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] İkram: Masa ${state.selectedTable.number} -> ${item.name} ikram edildi (${reason}).`);
            showToast(`${item.name} ikram olarak güncellendi.`);
          } else if (actionType === 'void') {
            state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] İptal (Void): Masa ${state.selectedTable.number} -> ${item.name} iptal edildi (${reason}).`);
            state.activeCart.splice(state.selectedCartItemIndex, 1);
            showToast(`${item.name} iptal edildi.`);
          }
        }
        if (modalItemAction) modalItemAction.style.display = 'none';
        renderCart();
      });
    }

    // 6.19 Table Transfer Action
    const btnTransfer = document.getElementById('btn-action-transfer-table');
    const modalTransfer = document.getElementById('modal-transfer-table');
    const btnCloseTransfer = document.getElementById('btn-close-transfer-modal');
    const btnCancelTransfer = document.getElementById('btn-cancel-transfer');
    const btnConfirmTransfer = document.getElementById('btn-confirm-transfer');
    const selectTarget = document.getElementById('select-target-table');

    if (btnTransfer) {
      btnTransfer.addEventListener('click', () => {
        if (!state.selectedTable) return;
        const availTables = state.tables.filter(t => t.occupancy === 'available' && t.id !== state.selectedTable.id);
        if (availTables.length === 0) {
          showToast('Taşıma yapılacak boş masa bulunamadı!', 'warning');
          return;
        }
        if (selectTarget) {
          selectTarget.innerHTML = availTables.map(t => `<option value="${t.id}">Masa ${t.number} (${t.section})</option>`).join('');
        }
        const titleEl = document.getElementById('transfer-source-title');
        if (titleEl) titleEl.textContent = `Kaynak: Masa ${state.selectedTable.number} (${formatTL(state.selectedTable.billAmount)})`;
        if (modalTransfer) modalTransfer.style.display = 'flex';
      });
    }

    if (btnCloseTransfer) btnCloseTransfer.addEventListener('click', () => modalTransfer.style.display = 'none');
    if (btnCancelTransfer) btnCancelTransfer.addEventListener('click', () => modalTransfer.style.display = 'none');
    if (btnConfirmTransfer) {
      btnConfirmTransfer.addEventListener('click', () => {
        const targetId = selectTarget?.value;
        const targetTable = state.tables.find(t => t.id === targetId);
        if (targetTable && state.selectedTable) {
          targetTable.occupancy = 'occupied';
          targetTable.billAmount = state.selectedTable.billAmount;
          targetTable.waiter = state.selectedTable.waiter;
          targetTable.minutes = state.selectedTable.minutes;
          targetTable.opBadge = state.selectedTable.opBadge;

          state.selectedTable.occupancy = 'available';
          state.selectedTable.billAmount = 0.00;
          state.selectedTable.waiter = null;
          state.selectedTable.minutes = null;
          state.selectedTable.opBadge = null;

          state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] Masa Transferi: Masa ${state.selectedTable.number} -> Masa ${targetTable.number} aktarıldı.`);
          showToast(`Masa ${state.selectedTable.number} başarıyla Masa ${targetTable.number}'e taşındı!`);
          
          if (modalTransfer) modalTransfer.style.display = 'none';
          viewOrder.style.display = 'none';
          viewTables.style.display = 'flex';
          renderCashierTables();
        }
      });
    }

    // 6.20 Submit Order
    const btnSubmit = document.getElementById('btn-pos-submit-order');
    if (btnSubmit) {
      btnSubmit.addEventListener('click', () => {
        if (!state.isOnline) {
          showToast('Sunucu bağlantısı olmadan sipariş iletilemez!', 'error');
          return;
        }
        if (state.activeCart.length === 0) return;

        const idempotencyKey = 'ord_' + Math.random().toString(36).substring(2, 11);
        btnSubmit.disabled = true;
        btnSubmit.innerHTML = `<span class="icon">⏳</span><span>İletiliyor (${idempotencyKey.substring(0, 8)})...</span>`;

        setTimeout(() => {
          if (state.selectedTable) {
            const tbl = state.tables.find(t => t.id === state.selectedTable.id);
            if (tbl) {
              tbl.occupancy = 'occupied';
              tbl.opBadge = 'cooking';
              tbl.waiter = 'Ahmet Y.';
              tbl.minutes = 1;
              const cartSum = state.activeCart.reduce((sum, i) => sum + (i.unitPrice * i.quantity), 0);
              tbl.billAmount = (tbl.billAmount || 0) + cartSum;
            }
          }

          state.tickets.unshift({
            id: String(1045 + state.tickets.length),
            table: `Masa ${state.selectedTable.number}`,
            time: 'Yeni',
            station: 'hot',
            items: state.activeCart.map(i => ({ name: `${i.quantity}x ${i.name} [${i.course}]`, status: 'cooking' }))
          });

          state.notifications.unshift({
            id: 'notif_' + Date.now(),
            time: new Date().toLocaleTimeString('tr-TR').substring(0, 5),
            text: `Masa ${state.selectedTable.number}: ${state.activeCart.length} Kalem mutfağa iletildi!`,
            unread: true
          });

          state.activeCart = [];
          state.activeDiscount = 0;
          showToast(`Sipariş mutfağa iletildi! Masa ${state.selectedTable.number} güncellendi.`);
          
          viewOrder.style.display = 'none';
          viewTables.style.display = 'flex';
          renderCashierTables();
          renderOperations();
        }, 600);
      });
    }

    // 6.21 PIN Lockout & Security
    const lockBtn = document.getElementById('btn-sim-lock');
    const cashierLockBtn = document.getElementById('btn-cashier-lock');
    const waiterLockBtn = document.getElementById('btn-waiter-lock');
    const lockModal = document.getElementById('modal-lockout');
    const keypad = document.getElementById('keypad-grid');

    const openLockModal = () => {
      state.isLocked = true;
      state.enteredPin = '';
      updatePinDots();
      if (lockModal) lockModal.style.display = 'flex';
    };

    if (lockBtn) lockBtn.addEventListener('click', openLockModal);
    if (cashierLockBtn) cashierLockBtn.addEventListener('click', openLockModal);
    if (waiterLockBtn) waiterLockBtn.addEventListener('click', openLockModal);

    const updatePinDots = () => {
      for (let i = 1; i <= 4; i++) {
        const dot = document.getElementById(`dot-${i}`);
        if (dot) dot.classList.toggle('filled', i <= state.enteredPin.length);
      }
    };

    if (keypad) {
      keypad.addEventListener('click', (e) => {
        if (state.cooldownRemaining > 0) return;
        const keyBtn = e.target.closest('.key-btn');
        if (!keyBtn) return;
        const key = keyBtn.dataset.key;

        if (key === 'C') {
          state.enteredPin = '';
          updatePinDots();
        } else if (key === 'OK' || state.enteredPin.length === 3) {
          if (key !== 'OK') state.enteredPin += key;
          updatePinDots();

          if (state.enteredPin === '1234') {
            state.isLocked = false;
            state.failedPinAttempts = 0;
            if (lockModal) lockModal.style.display = 'none';
            showToast('Oturum kilidi açıldı.');
          } else {
            state.failedPinAttempts += 1;
            const errMsg = document.getElementById('pin-error-msg');
            const remSpan = document.getElementById('pin-remaining-attempts');
            
            if (state.failedPinAttempts >= 5) {
              showToast('5 Hatalı PIN denemesi! Oturum tamamen iptal edildi.', 'error');
              if (errMsg) errMsg.textContent = 'Oturum iptal edildi! Süpervizör şifresi gerekli.';
            } else if (state.failedPinAttempts >= 3) {
              state.cooldownRemaining = 30;
              const cooldownBox = document.getElementById('pin-cooldown-box');
              const cooldownSec = document.getElementById('pin-cooldown-sec');
              if (cooldownBox) cooldownBox.style.display = 'block';

              state.cooldownTimer = setInterval(() => {
                state.cooldownRemaining -= 1;
                if (cooldownSec) cooldownSec.textContent = state.cooldownRemaining;
                if (state.cooldownRemaining <= 0) {
                  clearInterval(state.cooldownTimer);
                  if (cooldownBox) cooldownBox.style.display = 'none';
                }
              }, 1000);
            } else {
              if (errMsg) errMsg.style.display = 'block';
              if (remSpan) remSpan.textContent = 3 - state.failedPinAttempts;
            }
            state.enteredPin = '';
            setTimeout(updatePinDots, 300);
          }
        } else if (state.enteredPin.length < 4) {
          state.enteredPin += key;
          updatePinDots();
        }
      });
    }

    // 6.22 Waiter PWA Bottom Nav
    document.querySelectorAll('.waiter-bottom-nav .wtr-nav-item').forEach(item => {
      item.addEventListener('click', () => {
        document.querySelectorAll('.waiter-bottom-nav .wtr-nav-item').forEach(i => i.classList.remove('active'));
        item.classList.add('active');
        const target = item.dataset.wtrTarget;
        
        document.getElementById('wtr-view-tables').style.display = target === 'tables' ? 'flex' : 'none';
        document.getElementById('wtr-view-status').style.display = target === 'status' ? 'flex' : 'none';
        document.getElementById('wtr-view-notifications').style.display = target === 'notifications' ? 'flex' : 'none';
        document.getElementById('wtr-view-order').style.display = 'none';

        if (target === 'notifications') {
          state.notifications.forEach(n => n.unread = false);
          const dot = document.getElementById('wtr-unread-dot');
          if (dot) dot.style.display = 'none';
        }
        renderWaiterSurface();
      });
    });

    // 6.23 Waiter Table Selection
    const wtrGrid = document.getElementById('waiter-tables-container');
    const wtrViewTables = document.getElementById('wtr-view-tables');
    const wtrViewOrder = document.getElementById('wtr-view-order');
    const btnWtrBack = document.getElementById('btn-wtr-back-tables');
    const btnWtrReqBill = document.getElementById('btn-wtr-request-bill');

    if (wtrGrid) {
      wtrGrid.addEventListener('click', (e) => {
        const card = e.target.closest('.table-card');
        if (!card) return;
        const tblId = card.dataset.wtrTableId;
        const tbl = state.tables.find(t => t.id === tblId);
        if (!tbl) return;

        if (tbl.occupancy === 'reserved') {
          tbl.occupancy = 'occupied';
          tbl.waiter = 'Mehmet K.';
          tbl.minutes = 1;
          showToast(`Masa ${tbl.number} misafiri oturtuldu.`);
        }

        state.wtrActiveTable = tbl;
        state.wtrCart = [];

        wtrViewTables.style.display = 'none';
        wtrViewOrder.style.display = 'flex';
        const nameEl = document.getElementById('wtr-active-table-name');
        if (nameEl) nameEl.textContent = `Masa ${tbl.number} (${tbl.section})`;
        renderWaiterSurface();
      });
    }

    if (btnWtrBack) {
      btnWtrBack.addEventListener('click', () => {
        wtrViewOrder.style.display = 'none';
        wtrViewTables.style.display = 'flex';
        renderWaiterSurface();
      });
    }

    if (btnWtrReqBill) {
      btnWtrReqBill.addEventListener('click', () => {
        if (!state.wtrActiveTable) return;
        state.wtrActiveTable.opBadge = 'bill-requested';
        showToast(`Masa ${state.wtrActiveTable.number} için hesap talebi kasaya iletildi.`);
        wtrViewOrder.style.display = 'none';
        wtrViewTables.style.display = 'flex';
        renderWaiterSurface();
        renderCashierTables();
      });
    }

    const wtrCartToggle = document.getElementById('wtr-cart-toggle');
    const wtrCartTray = document.getElementById('wtr-cart-tray');
    if (wtrCartToggle && wtrCartTray) {
      wtrCartToggle.addEventListener('click', () => {
        wtrCartTray.classList.toggle('expanded');
      });
    }

    const wtrProdList = document.getElementById('wtr-product-list');
    if (wtrProdList) {
      wtrProdList.addEventListener('click', (e) => {
        const card = e.target.closest('.product-card');
        if (!card) return;
        const prodId = card.dataset.wtrProdId;
        const conceptData = getActiveConceptData();
        const prod = conceptData.products.find(p => p.id === prodId);
        if (!prod) return;

        const existing = state.wtrCart.find(i => i.id === prod.id);
        if (existing) {
          existing.quantity += 1;
        } else {
          state.wtrCart.push({ id: prod.id, name: prod.name, unitPrice: prod.price, quantity: 1 });
        }
        renderWaiterSurface();
        showToast(`${prod.name} sepete eklendi.`);
      });
    }

    const btnWtrSubmit = document.getElementById('btn-wtr-submit-order');
    if (btnWtrSubmit) {
      btnWtrSubmit.addEventListener('click', () => {
        if (state.wtrCart.length === 0) return;

        if (!state.isOnline) {
          const queueItem = {
            opId: 'wtr_op_' + Math.random().toString(36).substring(2, 9),
            table: state.wtrActiveTable.number,
            items: [...state.wtrCart]
          };
          state.wtrOfflineQueue.push(queueItem);
          const qCountEl = document.getElementById('waiter-queue-count');
          if (qCountEl) qCountEl.textContent = state.wtrOfflineQueue.length;

          showToast('Çevrimdışı: Sipariş cihaz kuyruğuna alındı. Ağ gelince iletilecek.', 'warning');
          state.wtrCart = [];
          wtrViewOrder.style.display = 'none';
          wtrViewTables.style.display = 'flex';
          renderWaiterSurface();
        } else {
          showToast(`Masa ${state.wtrActiveTable.number} siparişi mutfağa gönderildi!`);
          state.wtrCart = [];
          wtrViewOrder.style.display = 'none';
          wtrViewTables.style.display = 'flex';
          renderWaiterSurface();
        }
      });
    }

    // 6.24 Notification Delivery
    const notifFeed = document.getElementById('wtr-notif-feed');
    if (notifFeed) {
      notifFeed.addEventListener('click', (e) => {
        const btn = e.target.closest('.btn-deliver-notif');
        if (!btn) return;
        const idx = parseInt(btn.dataset.notifIdx, 10);
        state.notifications.splice(idx, 1);
        showToast('Yemek teslim edildi olarak işaretlendi.');
        renderWaiterSurface();
      });
    }

    // 6.25 Clock Loop
    setInterval(() => {
      const clock = document.getElementById('cashier-clock');
      if (clock) {
        const now = new Date();
        clock.textContent = now.toLocaleDateString('tr-TR') + ' ' + now.toLocaleTimeString('tr-TR');
      }
    }, 1000);
  }

  // --- 7. INITIALIZATION ---

  document.addEventListener('DOMContentLoaded', () => {
    document.documentElement.setAttribute('data-theme', state.theme);
    renderConceptNavigation();
    renderCashierTables();
    renderCatalogProducts();
    renderOperations();
    renderWaiterSurface();
    setupEvents();
  });

})();
