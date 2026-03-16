# AGENTS.md — KafeAdisyon

Projeye yeni başlarken veya bir şeyi unutunca buraya bakıyorum.

---

## Genel Bilgi

- **Platform:** Windows masaüstü + Android (iOS/macOS hedeflenmiyor)
- **Dil:** C# / .NET 9 / .NET MAUI
- **Backend:** Postgrest.Client ile Supabase'e direkt bağlantı
- **Auth yok** — giriş ekranında sadece iki buton var: Admin ve Garson
- **Navigation:** Shell yok, `NavigationPage` + `Navigation.PushAsync()` kullanıyorum

---

## Kullanıcı Rolleri

| Rol | Sayfa | Ne yapabiliyor |
|-----|-------|----------------|
| Admin | `AdminPage` | Masaları görüntüle, menü yönet, siparişi oku (read-only), hesap kapat/böl |
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
- **`Picker`** → Windows'ta seçili değeri göstermiyor. Kategori seçimi için toggle `Button` listesi yaptım.
- **`decimal` fiyat alanı** → Postgrest'te precision hatası veriyor (özellikle 5 ile biten fiyatlarda). Her yerde `double` kullanıyorum.

---

## Fiyat Tipi

```csharp
// YANLIŞ — Postgrest'te bozuluyor
public decimal Price { get; set; }

// DOĞRU
public double Price { get; set; }
```

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

## Klasör Yapısı

```
KafeAdisyon/
├── Application/
│   ├── Interfaces/          → IMenuService, IOrderService, ITableService
│   └── DTOs/RequestModels/
├── Infrastructure/
│   ├── Client/              → DatabaseClient.cs
│   └── Services/            → MenuService, OrderService, TableService
├── ViewModels/              → AdminViewModel, OrderViewModel
├── Views/
│   ├── LoginPage.xaml
│   ├── Admin/               → AdminPage, EditMenuItemPage
│   ├── Waiter/              → WaiterPage
│   └── Order/               → OrderPage, SplitBillPage
├── Models/                  → TableModel, MenuItemModel, OrderModel, OrderItemModel
└── Common/                  → BaseResponse<T>
```



## Derleme Hatası Alınca Bakılacaklar

- Model namespace'i `Postgrest.Attributes` / `Postgrest.Models` olmalı — `Supabase.Postgrest` yazınca derleme hatası veriyor.
- Visual Studio bazen otomatik `using Android...` ekliyor, Windows build'ini kırıyor — build hatasında ilk bakılan yer.
