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
| Auth | Supabase Auth (JWT — email/şifre) |
| Mimari | Clean Architecture + MVVM |
| IDE | Visual Studio 2022 Community |

---

## Supabase Tablo Şeması

```sql
tables      → id, name, status ('bos'|'dolu'), created_at
menu_items  → id, name, category, price (double), is_active, created_at
orders      → id, table_id, status ('aktif'|'odendi'), total (double), created_at
order_items → id, order_id, menu_item_id, quantity, price (double), created_at
profiles    → id (auth.users FK), full_name, role ('admin'|'garson'), created_at
audit_logs  → id, user_id, user_name, role, action, detail, device_name, created_at
```

Bağlantı bilgileri `appsettings.json` içinde — bu dosya `.gitignore`'da, repoya gitmiyor.

---

## Tamamlananlar

### Altyapı
- [x] Clean Architecture katmanları (Application / Infrastructure / Common)
- [x] `DatabaseClient` — Postgrest bağlantısı + `SetAuthToken` / `ClearAuthToken`
- [x] `BaseResponse<T>` — ortak servis dönüş tipi
- [x] `IMenuService`, `IOrderService`, `ITableService` arayüzleri
- [x] Servis implementasyonları (MenuService, OrderService, TableService)
- [x] DI container kurulumu (`MauiProgram.cs`)
- [x] `appsettings.json` → `EmbeddedResource` olarak gömme
- [x] Environment variable desteği (CI/CD için)

### Auth & Session
- [x] Supabase Auth — email/şifre + JWT token tabanlı giriş
- [x] `IAuthService` + `AuthService` — Supabase Auth REST API ile login/logout
- [x] `SessionContext` — login olan kullanıcıyı + cihaz adını tutan singleton
- [x] `ProfileModel` — `profiles` tablosu Postgrest modeli
- [x] Login sonrası rol bazlı yönlendirme (admin → AdminPage, garson → WaiterPage)
- [x] `LoginPage` yeniden yazıldı — email / şifre / cihaz adı formu

### İşlem Takibi (Audit Log)
- [x] `IAuditLogService` + `AuditLogService` — log yazma ve okuma
- [x] `AuditLogModel` — `audit_logs` tablosu Postgrest modeli
- [x] Supabase'de `profiles` ve `audit_logs` tabloları + RLS politikaları
- [x] Hesap kapatma loglanıyor (`hesap_kapatma`)
- [x] Sipariş iptali loglanıyor (`siparis_iptali`)
- [x] Fiyat güncelleme loglanıyor (`fiyat_guncelleme`)
- [x] Ürün ekleme loglanıyor (`urun_ekleme`)
- [x] Ürün silme loglanıyor (`urun_silme`)
- [x] `AuditLogPage` — Admin paneli işlem takibi ekranı (kullanıcı, cihaz, zaman)
- [x] AdminPage'e "🔍 Log" sekmesi eklendi

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
| `QuestPDF` — `Colors` / `IContainer` namespace çakışması | `using QColors = QuestPDF.Helpers.Colors` alias'ı eklendi |
| `StrokeShape="RoundRectangle CornerRadius='12'"` derleme hatası | `StrokeShape="RoundRectangle 12"` syntax'ına geçildi |
| `AuditLogService.cs` içine yanlış kod yapıştırıldı | Dosya içeriği `AuthService` ile aynıydı, doğru `AuditLogService` kodu yazıldı |

---

### PDF Rapor & Supabase Storage (33. Gün)
- [x] `ISalesReportService` arayüzü
- [x] `ReportDtos` — `OrderSummaryDto`, `OrderItemSummaryDto`, `ReportSummaryDto`
- [x] `SupabaseStorageService` — HttpClient ile Supabase Storage REST API (PUT + public URL)
- [x] `SalesReportService` — Supabase'den veri çekme + QuestPDF ile PDF üretimi + Storage yükleme
- [x] `SalesReportPage` — Admin paneli rapor ekranı (Bugün / Bu Hafta seçimi, indirme linki)
- [x] DI kaydı — `MauiProgram.cs`'e `SupabaseStorageService` ve `ISalesReportService` eklendi
- [x] Admin paneline "📊 Rapor" sekmesi eklendi

### Supabase Auth & İşlem Takibi (34. Gün)
- [x] Supabase Auth entegrasyonu — JWT token tabanlı email/şifre girişi
- [x] `profiles` ve `audit_logs` tabloları Supabase'de oluşturuldu + RLS politikaları
- [x] `SessionContext` singleton — kullanıcı bilgilerini uygulama boyunca taşıyor
- [x] `AuthService` — Supabase Auth REST API ile login/logout (SDK deadlock'u nedeniyle HttpClient kullanıldı)
- [x] `AuditLogService` — tüm kritik işlemler otomatik loglanıyor
- [x] `LoginPage` yeniden tasarlandı — email / şifre / cihaz adı formu
- [x] `AuditLogPage` — Admin'in işlem geçmişini göreceği ekran
- [x] AdminPage'e 4. sekme eklendi: "🔍 Log"
- [x] `DatabaseClient.SetAuthToken()` — login sonrası JWT Postgrest header'ına enjekte ediliyor

---

## Sonraki Adımlar (Opsiyonel)

- [ ] XML doc comment'ler (`/// <summary>`)
- [ ] Rapor sayfasına özel tarih aralığı seçici (DatePicker — Windows uyumluluğu test edilmeli)
- [ ] Aylık rapor desteği
- [ ] Logout butonu WaiterPage'e de eklenmeli