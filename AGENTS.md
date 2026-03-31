# AGENTS.md — KafeAdisyon

Projeye yeni başlarken veya bir şeyi unutunca buraya bakıyorum.

---

## Genel Bilgi

- **Platform:** Windows masaüstü + Android (iOS/macOS hedeflenmiyor)
- **Dil:** C# / .NET 9 / .NET MAUI
- **Backend:** Postgrest.Client ile Supabase'e direkt bağlantı
- **Auth:** Supabase Auth — email/şifre + JWT token (`AuthService` → `SessionContext`)
- **Navigation:** Shell yok, `NavigationPage` + `Navigation.PushAsync()` kullanıyorum

---

## Kullanıcı Rolleri

| Rol | Sayfa | Ne yapabiliyor |
|-----|-------|----------------|
| Admin | `AdminPage` | Masaları görüntüle, menü yönet, siparişi oku (read-only), hesap kapat/böl, log görüntüle |
| Garson | `WaiterPage` | Masa aç, sipariş al, ürün ekle/çıkar, hesap kapat, iptal et |

`OrderPage` `isReadOnly` parametresiyle açılıyor:
- `true` (Admin): menü paneli gizli, − butonu yok, Hesap Böl butonu var
- `false` (Garson): menü paneli açık, − butonu var, İptal butonu var

---

## Renk Paleti

Bütün sayfalarda bunları kullanıyorum, değiştirmiyorum:

| Ne için | Değer |
|---------|-------|
| Sayfa arka planı | `#FAF7F2` |
| Header | `#3D2314` |
| İkincil buton/badge | `#5C3317` |
| Vurgu rengi | `#C8702A` |
| Ana metin | `#2C1810` |
| İkincil metin | `#A0856A` |
| Boş masa | `#E8F5E9` / border `#C8E6C9` |
| Dolu masa | `#FFEBEE` / border `#FFCDD2` |

---

## Kesinlikle Yapılmayacaklar

Bunları denedim, çalışmıyor:

- **`TapGestureRecognizer` + `Border`** → Windows ekranı donuyor. Tıklanabilir her şey `Button` olmalı.
- **`Shadow` property'si** → Windows'ta crash veriyor. Hiç kullanmıyorum.
- **`Supabase.Client.InitializeAsync()`** → deadlock yapıyor. Bunun yerine `Postgrest.Client` direkt kullanıyorum.
- **`Supabase SDK` Storage metotları** → aynı deadlock riski. `SupabaseStorageService`'de `HttpClient` + REST API kullanıyorum.
- **`Picker`** → Windows'ta seçili değeri göstermiyor. Kategori seçimi için toggle `Button` listesi yaptım.
- **`decimal` fiyat alanı** → Postgrest'te precision hatası veriyor (özellikle 5 ile biten fiyatlarda). Her yerde `double` kullanıyorum.
- **`StrokeShape="RoundRectangle CornerRadius='12'"`** → derleme hatası. Doğrusu: `StrokeShape="RoundRectangle 12"`.

---

## Fiyat Tipi

```csharp
// YANLIŞ — Postgrest'te bozuluyor
public decimal Price { get; set; }

// DOĞRU
public double Price { get; set; }
```

---

## QuestPDF Namespace Çakışması

`SalesReportService.cs` dosyasında `Colors` ve `IContainer` hem MAUI hem QuestPDF'de tanımlı. Alias ile çözülüyor:

```csharp
using QColors    = QuestPDF.Helpers.Colors;
using QContainer = QuestPDF.Infrastructure.IContainer;
```

Ayrıca `page.Header().Element(Action<IContainer>)` çalışmıyor — doğru kullanım:

```csharp
// YANLIŞ
page.Header().Element(ComposeHeader(summary));

// DOĞRU
page.Header().Column(col => ComposeHeader(col, summary));
```

---

## Supabase Auth — JWT Token

Login sonrası JWT token Postgrest header'ına enjekte edilmeli, aksi halde RLS çalışmaz:

```csharp
// AuthService.LoginAsync() içinde — login başarılıysa
_db.SetAuthToken(token);

// Logout sonrası anon key'e dön
_db.ClearAuthToken();
```

`DatabaseClient.SetAuthToken()` şunu yapıyor:
```csharp
Db.Options.Headers["Authorization"] = $"Bearer {jwtToken}";
```

---

## SessionContext

Login olan kullanıcının bilgilerini tüm uygulama boyunca taşır. `AddSingleton` olarak kayıtlı:

```csharp
_session.UserId      // Supabase Auth UUID
_session.UserName    // profiles.full_name
_session.Role        // "admin" | "garson"
_session.DeviceName  // kullanıcının girdiği cihaz adı
_session.IsLoggedIn  // bool
_session.IsAdmin     // bool
```

---

## Audit Log

Kritik işlemleri loglamak için `IAuditLogService.LogAsync(action, detail)` çağrılıyor.
`LogAsync` hata fırlatmaz — log yazılamaması asıl işlemi durdurmamalı.

```csharp
// OrderService.CloseOrderAsync içinde
await _audit.LogAsync("hesap_kapatma", $"Masa {tableId} — ₺{total:F2}");

// MenuService.UpdateMenuItemAsync içinde (sadece fiyat değiştiyse)
await _audit.LogAsync("fiyat_guncelleme", $"{name}: ₺{eskiFiyat} → ₺{yeniFiyat}");
```

Aksiyon sabitleri: `hesap_kapatma` | `siparis_iptali` | `fiyat_guncelleme` | `urun_ekleme` | `urun_silme`

---

## Modal Sayfa Kapatma

`PushModalAsync` kapanmayı beklemiyor, sonucu almak için şu pattern'i kullanıyorum:

```csharp
// Modal sayfa içinde
private readonly TaskCompletionSource _tcs = new();
public Task WaitForCloseAsync() => _tcs.Task;

// Kapatırken
await Navigation.PopModalAsync();
_tcs.TrySetResult();

// Açan sayfa
var page = new MyModalPage(...);
await Navigation.PushModalAsync(page);
await page.WaitForCloseAsync();
```

---

## Optimistic Update

Ürün eklerken DB yanıtını beklemeden UI'ı güncelliyorum:

```csharp
// 1) Anında UI'a yansıt
_vm.AddItemOptimistic(item);
BuildOrderItems();

// 2) Arka planda DB'ye yaz
await _vm.SyncItemToDbAsync(item);
```

---

## Çoklu Silme

Birden fazla ürün silinirken collection'a dokunmuyorum, önce hepsini DB'ye yazıp sonra yeniden yüklüyorum:

```csharp
foreach (var x in toProcess)
    await _vm.RemoveItemDbOnlyAsync(x.Item);

await _vm.ReloadOrderItemsAsync();
```

---

## Offline Kuyruk

`OfflineAwareOrderService` bir Decorator'dır — `IOrderService`'i sararak bağlantı kontrolü ekler:

```
Bağlantı VAR  → isteği doğrudan OrderService'e iletir
Bağlantı YOK  → işlemi OfflineQueue'ya kaydeder, optimistic yanıt döner
Bağlantı gelince → ConnectivityChanged eventi → FlushQueueAsync() otomatik tetiklenir
```

**`FlushQueueAsync` içinde lock var (`SemaphoreSlim(1,1)`).**  
`SetConnected(true)` hem event'i hem de lock'u tetikler. Hemen arkasından manuel `FlushQueueAsync()` çağırırsan `WaitAsync(0)` lock'u alamaz ve boş döner.  
Bunu önlemek için `FakeConnectivityService.SetConnectedSilently(bool)` kullan:

```csharp
// TEST — YANLIŞ: event → async handler lock'u alır → manuel çağrı boş döner
conn.SetConnected(true);
await svc.FlushQueueAsync(); // WaitAsync(0) başarısız, hiçbir şey yapmaz

// TEST — DOĞRU: event tetiklenmez, lock serbest kalır
conn.SetConnectedSilently(true);
await svc.FlushQueueAsync(); // lock alınır, flush çalışır
```

---

## Offline Test Delay Değerleri

Supabase yanıt süresi ~400ms (perf testlerinde ölçüldü).  
Flush async arka planda çalıştığı için `Task.Delay` yeterince uzun olmalı:

```csharp
conn.SetConnected(true);
await Task.Delay(1500); // tek işlem için yeterli; 2 işlem varsa da güvenli marj
```

300ms veya 500ms delay ile flush tamamlanmadan kontrol yapılır → test yanlış başarısız olur.

---

## Integration Test — Stale Sipariş Temizleme

`GetAnyTable()` hep aynı tabloyu (`.First()`) seçer. Önceki test çalışması cleanup yapmadan biterse o tabloda "aktif" bir sipariş kalabilir. Her integration testinin başında şunu ekle:

```csharp
var stale = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
if (stale.Data != null)
    await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
        { OrderId = stale.Data.Id, TableId = table.Id, FinalTotal = 0 });
```

---

## Klasör Yapısı

```
KafeAdisyon/
├── Application/
│   ├── Interfaces/          → IMenuService, IOrderService, ITableService,
│   │                          IConnectivityService, ISalesReportService,
│   │                          IAuthService, IAuditLogService
│   └── DTOs/RequestModels/  → ReportDtos (OrderSummaryDto, ReportSummaryDto...)
├── Infrastructure/
│   ├── Client/              → DatabaseClient.cs
│   ├── Offline/             → OfflineQueue.cs
│   └── Services/            → MenuService, OrderService, TableService,
│                               ConnectivityService, OfflineAwareOrderService,
│                               SalesReportService, SupabaseStorageService,
│                               AuthService, AuditLogService, SessionContext
├── ViewModels/              → AdminViewModel, OrderViewModel
├── Views/
│   ├── LoginPage.xaml       → email/şifre/cihaz adı formu
│   ├── Admin/               → AdminPage, EditMenuItemPage, SalesReportPage, AuditLogPage
│   ├── Waiter/              → WaiterPage
│   └── Order/               → OrderPage, SplitBillPage
├── Models/                  → TableModel, MenuItemModel, OrderModel, OrderItemModel,
│                               ProfileModel, AuditLogModel
└── Common/                  → BaseResponse<T>
```

---

## Supabase Storage

SDK yerine `HttpClient` + REST API:

```csharp
// YANLIŞ — SDK deadlock yapıyor
await supabase.Storage.From("reports").Upload(bytes, path);

// DOĞRU — HttpClient ile doğrudan PUT
var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = content };
request.Headers.Add("x-upsert", "true");
await _http.SendAsync(request);
```

Public URL formatı:
```
{Supabase:Url}/storage/v1/object/public/{bucket}/{fileName}
```

Bucket'ı Public olarak işaretlemeyi unutma:
`Supabase Dashboard → Storage → reports bucket → Make Public`

