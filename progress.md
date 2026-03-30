# progress.md — KafeAdisyon

Nerede kaldığımı ve ne yapmam gerektiğini takip etmek için tutuyorum.

---

## Proje Özeti

.NET MAUI + Supabase (Postgrest) ile çalışan kafe sipariş ve adisyon uygulaması.  
Hedef: Windows masaüstü + Android çıktısı.  
Maksimum 3-4 cihaz eş zamanlı kullanım, kendi sunucusu yok.

---

## Kullanılan Stack

| Katman | Teknoloji |
|--------|-----------|
| UI | .NET MAUI (.NET 9) |
| Backend | Postgrest.Client (Supabase direkt erişim) |
| Mimari | Clean Architecture + MVVM |
| IDE | Visual Studio 2022 Community |

---

## Supabase Tablo Şeması

```sql
tables      → id, name, status ('bos'|'dolu'), created_at
menu_items  → id, name, category, price (double), is_active, created_at
orders      → id, table_id, status ('aktif'|'odendi'), total (double), created_at
order_items → id, order_id, menu_item_id, quantity, price (double), created_at
```

Bağlantı bilgileri `appsettings.json` içinde — bu dosya `.gitignore`'da, repoya gitmiyor.

---

## Tamamlananlar

### Altyapı
- [x] Clean Architecture katmanları (Application / Infrastructure / Common)
- [x] `DatabaseClient` — Postgrest bağlantısı
- [x] `BaseResponse<T>` — ortak servis dönüş tipi
- [x] `IMenuService`, `IOrderService`, `ITableService` arayüzleri
- [x] Servis implementasyonları (MenuService, OrderService, TableService)
- [x] DI container kurulumu (`MauiProgram.cs`)
- [x] `appsettings.json` → `EmbeddedResource` olarak gömme
- [x] Environment variable desteği (CI/CD için)

### Admin Paneli
- [x] Masa düzeni — 14 masa, renk durumu (boş/dolu)
- [x] Masaya tıklayınca OrderPage açılıyor (read-only mod)
- [x] Menü ürünü ekleme
- [x] Menü ürünü düzenleme (modal — ad, kategori toggle, fiyat)
- [x] Menü ürünü silme (soft delete — `is_active = false`)
- [x] Sipariş görüntüleme (sadece okuma)
- [x] Hesabı kapatma
- [x] Hesap bölme (✂️ ürün seçip miktar ayarla → Öde ve Kaldır)
- [x] Çıkış butonu

### Garson Paneli
- [x] Masa düzeni — renk durumu
- [x] Sipariş açma ve ürün ekleme (optimistic update)
- [x] Ürün çıkarma
- [x] Hesabı kapatma
- [x] Sipariş iptal etme
- [x] Çıkış butonu

### Sipariş Ekranı
- [x] Mevcut sipariş yükleme
- [x] Menüden ürün ekleme
- [x] Ürün silme (−)
- [x] Boş sipariş otomatik temizleme
- [x] Ödeme al / masa kapat

### Testler
- [x] Unit testler — `KafeAdisyon_Tests`
  - OrderViewModelTests
  - SplitBillCalculatorTests
  - BaseResponseTests
- [x] Integration testler — `KafeAdisyon_IntegrationTests`
  - MenuServiceIntegrationTests
  - OrderServiceIntegrationTests
  - PerformanceTests (yanıt süresi eşikleri)

### Dokümantasyon & Versiyon Kontrol
- [x] `.gitignore` — bin/obj, appsettings.json, platform paketleri
- [x] `appsettings.example.json` şablonu
- [x] `README.md` — kurulum, mimari, test kapsamı
- [x] `AGENTS.md` — geliştirme notları ve gotcha'lar
- [x] GitHub'a push

---

## Çözülen Sorunlar

| Sorun | Çözüm |
|-------|-------|
| `TapGestureRecognizer` + `Border` Windows'u donduruyor | Tüm tıklanabilir öğeler `Button` yapıldı |
| `Shadow` Windows'ta crash | Kaldırıldı |
| `Supabase.Client.InitializeAsync()` deadlock | `Postgrest.Client` direkt kullanıldı |
| `Picker` Windows'ta seçili değeri göstermiyor | Toggle `Button` listesiyle değiştirildi |
| `PushModalAsync` kapanmayı beklemiyor | `TaskCompletionSource` + `WaitForCloseAsync()` |
| `decimal` Postgrest precision hatası | Tüm fiyat alanları `double` yapıldı |
| Çoklu silmede bazı ürünler atlanıyor | `RemoveItemDbOnlyAsync` → `ReloadOrderItemsAsync` |
| Ürün ekleme yavaş hissettiriyor | Optimistic update: önce UI, sonra DB |
| Storage SDK deadlock yapıyor | `SupabaseStorageService`'de HttpClient ile doğrudan REST API kullanıldı |

---

### PDF Rapor & Supabase Storage (33. Gün)
- [x] `ISalesReportService` arayüzü
- [x] `ReportDtos` — `OrderSummaryDto`, `OrderItemSummaryDto`, `ReportSummaryDto`
- [x] `SupabaseStorageService` — HttpClient ile Supabase Storage REST API (PUT + public URL)
- [x] `SalesReportService` — Supabase'den veri çekme + QuestPDF ile PDF üretimi + Storage yükleme
- [x] `SalesReportPage` — Admin paneli rapor ekranı (Bugün / Bu Hafta seçimi, indirme linki)
- [x] DI kaydı — `MauiProgram.cs`'e `SupabaseStorageService` ve `ISalesReportService` eklendi
- [x] Admin paneline "📊 Rapor" sekmesi eklendi

---

## Sonraki Adımlar (Opsiyonel)

- [ ] Günlük satış özeti ekranı (kapanan siparişlerin toplamı) ✅ 33. günde yapıldı
- [ ] Offline kuyruk — ağ yokken beklet, bağlanınca gönder ✅ 29-30. günlerde yapıldı
- [ ] XML doc comment'ler (`/// <summary>`)
- [ ] Rapor sayfasına özel tarih aralığı seçici (DatePicker — Windows uyumluluğu test edilmeli)
- [ ] Aylık rapor desteği
