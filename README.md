# ☕ KafeAdisyon

**.NET MAUI** ile geliştirilmiş, **Supabase** altyapısını kullanan çok platformlu kafe adisyon uygulaması.  
Garsonlar masaları anlık takip edebilir, sipariş açıp kapatabilir; admin ise menüyü tam CRUD yetkisiyle yönetebilir.

> Staj projesi — [AdministrativeAffairsService](https://github.com/) projesindeki Clean Architecture ve servis katmanı yapısı esas alınarak bağımsız olarak geliştirilmiştir.

---

## 📱 Ekran Görüntüleri

| Giriş | Garson Paneli | Sipariş Ekranı | Hesap Paylaşımı | Admin Paneli |
|-------|---------------|----------------|-----------------|--------------|
| Rol seçimi | 14 masa, anlık doluluk rengi | Menü + sepet | Ürün bazlı bölme | Menü CRUD |

---

## ✨ Özellikler

### 👨‍💼 Garson
- 14 masadan oluşan interaktif salon haritası (A1–A4 · B1–B5 · C1–C5)
- Masa renginin anlık doluluk durumuna göre güncellenmesi (boş / dolu)
- Kategoriye göre filtrelenebilir menü
- Ürün ekleme / çıkarma işlemlerinde **optimistik UI** — veritabanı yanıtı beklenmeden ekran güncellenir
- Sipariş kalemi sıfırlandığında siparişin ve masa durumunun otomatik kapanması
- **Hesap paylaşımı (Split Bill):** Aynı masadaki birden fazla kişi ürünleri bölerek ayrı ayrı ödeyebilir
- **Offline destek** — internet kesildiğinde işlemler yerel kuyruğa alınır, bağlantı gelince otomatik olarak Supabase'e gönderilir

### 🔧 Admin
- Menü ürünü ekleme, düzenleme, soft-delete (pasife alma)
- Kategori bazlı listeleme
- Tüm tablolar üzerinde tam yetki

### 🔐 Genel
- Rol bazlı giriş ekranı (Garson / Admin)
- Supabase RLS kuralları ile veritabanı seviyesinde erişim kontrolü
- Environment variable desteği — CI/CD ortamlarında `appsettings.json` gerekmez

---

## 🏗️ Mimari

```
KafeAdisyon/
├── Application/
│   ├── Interfaces/          # IMenuService · IOrderService · ITableService · IConnectivityService
│   └── DTOs/RequestModels/  # Tip güvenli istek modelleri
├── Infrastructure/
│   ├── Client/              # DatabaseClient — Supabase/Postgrest bağlantısı
│   ├── Offline/             # OfflineQueue — kalıcı işlem kuyruğu
│   └── Services/            # MenuService · OrderService · TableService
│                            # ConnectivityService · OfflineAwareOrderService
├── ViewModels/              # MVVM — CommunityToolkit.Mvvm (ObservableProperty)
├── Views/                   # XAML sayfaları (Login · Waiter · Order · Admin)
├── Models/                  # Tablo eşleme modelleri
└── Common/
    └── BaseResponse<T>      # Tüm servis metodlarının ortak dönüş tipi
```

Mimari, staj yerindeki backend projesinin katmanlı yapısından öğrenilerek uyarlanmıştır:  
`AdministrativeAffairsService` → **Entity Framework + PostgreSQL + ASP.NET API**  
`KafeAdisyon` → **Supabase/Postgrest + .NET MAUI**

---

## 🛠️ Teknolojiler

| Katman | Teknoloji |
|--------|-----------|
| UI | .NET MAUI 9, XAML |
| MVVM | CommunityToolkit.Mvvm 8.4 |
| Backend | Supabase (Postgrest REST API) |
| ORM | supabase-csharp 0.16.2 |
| Yapılandırma | Microsoft.Extensions.Configuration.Json |
| Test | xUnit — Unit & Integration |
| Platform | Android · Windows |

---

## 🗄️ Veritabanı Şeması

```
tables          orders              order_items         menu_items
──────────      ──────────────      ───────────────     ──────────────
id              id                  id                  id
name            table_id ──────►    order_id ──────►    name
status          status              menu_item_id ──►    category
                total               quantity            price
                                    price               is_active
```

Supabase RLS politikaları ve trigger tanımları: [`supabase_rls_and_trigger.sql`](KafeAdisyon/supabase_rls_and_trigger.sql)

---

## 🚀 Kurulum

### Gereksinimler
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (MAUI iş yükü yüklü)
- Bir [Supabase](https://supabase.com) projesi

### 1. Repoyu klonla

```bash
git clone https://github.com/kullanici-adi/KafeAdisyonStaj.git
cd KafeAdisyonStaj
```

### 2. Yapılandırma

`appsettings.example.json` dosyasını kopyalayıp `appsettings.json` oluştur:

```json
{
  "Supabase": {
    "Url": "https://<proje-id>.supabase.co",
    "PublishableKey": "<anon-key>"
  }
}
```

> ⚠️ `appsettings.json` `.gitignore`'a eklenmiştir, repoya gönderilmez.

Alternatif olarak environment variable kullanabilirsin:

```bash
SUPABASE_URL=https://...
SUPABASE_KEY=sb_publishable_...
```

### 3. Veritabanını hazırla

Supabase SQL Editor'da [`supabase_rls_and_trigger.sql`](KafeAdisyon/supabase_rls_and_trigger.sql) dosyasını çalıştır.

### 4. Çalıştır

```bash
dotnet build
# Android
dotnet run -f net9.0-android
# Windows
dotnet run -f net9.0-windows10.0.19041.0
```

---

## 🧪 Testler

Proje iki test katmanına sahiptir:

```bash
# Unit testler
dotnet test KafeAdisyon_Tests/

# Integration testler (gerçek Supabase bağlantısı gerektirir)
dotnet test KafeAdisyon_IntegrationTests/
```

| Test | Kapsam |
|------|--------|
| `OrderViewModelTests` | Sipariş oluşturma, ürün ekleme/çıkarma |
| `SplitBillCalculatorTests` | Hesap bölme hesaplamaları |
| `BaseResponseTests` | Ortak yanıt modeli |
| `OfflineQueueTests` | Kuyruk CRUD, serileştirme, eşzamanlılık |
| `OfflineAwareOrderServiceTests` | Online/offline yönlendirme, flush mantığı, lock koruması |
| `MenuServiceIntegrationTests` | Menü CRUD — Supabase |
| `OrderServiceIntegrationTests` | Sipariş akışı — Supabase |
| `OfflineOrderServiceIntegrationTests` | Offline kuyruk → flush → Supabase doğrulama |
| `PerformanceTests` | Servis yanıt süresi eşikleri |

**Sonuç:** Unit 78/78 · Integration 33/33 — tüm testler geçiyor.

---

## 🔑 Teknik Detaylar

**Optimistik UI:** Ürün eklendiğinde kullanıcı arayüzü anında güncellenir, veritabanı yazımı arka planda gerçekleşir. Yazım başarısız olursa durum geri alınır.

**Race condition koruması:** `OrderViewModel` içinde `SemaphoreSlim(1,1)` kullanılarak aynı masa için eş zamanlı iki sipariş oluşturulması önlenir.

**Soft delete:** Menü ürünleri veritabanından silinmez; `is_active = false` olarak işaretlenir. Geçmiş siparişlerin bütünlüğü korunur.

**Split Bill:** `SplitBillPage` bir `TaskCompletionSource` üzerinden çalışır; modal kapatıldığında `PaidQuantities` sözlüğünü caller sayfaya geri döndürür, `OrderPage` kalan ürünleri doğrudan günceller.

**Offline destek:** `OfflineAwareOrderService`, Decorator Pattern ile `IOrderService` üstüne oturur. Bağlantı yokken yazma işlemleri MAUI `Preferences` tabanlı `OfflineQueue`'ya kaydedilir; kullanıcı arayüzüne `_offline_` önekli geçici ID ile anlık başarılı yanıt döndürülür. `ConnectivityChanged` eventi tetiklendiğinde `FlushQueueAsync` devreye girerek kuyruktaki işlemleri sırayla Supabase'e gönderir. Eş zamanlı flush çakışmalarını önlemek için `SemaphoreSlim(1,1)` kilidi kullanılır.

---

## 📁 Proje Yapısı

```
KafeAdisyonStaj/
├── KafeAdisyon/                  # Ana uygulama
├── KafeAdisyon_Tests/            # Unit testler
├── KafeAdisyon_IntegrationTests/ # Integration testler
├── .gitignore
└── KafeAdisyon.slnx
```

---

## 📄 Lisans

Bu proje staj amaçlı geliştirilmiştir. Herhangi bir lisans kısıtlaması yoktur.