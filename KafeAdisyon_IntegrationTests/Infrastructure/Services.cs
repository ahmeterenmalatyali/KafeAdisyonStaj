using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Models;
using Microsoft.Extensions.Configuration;
using Postgrest;

// ─── DatabaseClient ────────────────────────────────────────────────
namespace KafeAdisyon.Infrastructure.Client
{
    public class DatabaseClient
    {
        public readonly Postgrest.Client Db;
        public const string TableColumns = "id,name,status";
        public const string MenuItemColumns = "id,name,category,price,is_active";
        public const string OrderColumns = "id,table_id,status,total";
        public const string OrderItemColumns = "id,order_id,menu_item_id,quantity,price";

        public DatabaseClient(IConfiguration config)
        {
            var url = config["Supabase:Url"]!;
            var key = config["Supabase:PublishableKey"]!;
            var restUrl = url.TrimEnd('/') + "/rest/v1";
            Db = new Postgrest.Client(restUrl, new ClientOptions
            {
                Headers = new Dictionary<string, string>
                {
                    { "apikey", key },
                    { "Authorization", $"Bearer {key}" }
                }
            });
        }
    }
}

// ─── TableService ──────────────────────────────────────────────────
namespace KafeAdisyon.Infrastructure.Services
{
    using KafeAdisyon.Infrastructure.Client;

    public class TableService : ITableService
    {
        private readonly DatabaseClient _c;
        public TableService(DatabaseClient c) => _c = c;

        public async Task<BaseResponse<List<TableModel>>> GetAllTablesAsync()
        {
            try
            {
                var r = await _c.Db.Table<TableModel>().Select(DatabaseClient.TableColumns).Get();
                return BaseResponse<List<TableModel>>.SuccessResult(r.Models);
            }
            catch (Exception ex) { return BaseResponse<List<TableModel>>.ErrorResult(ex.Message); }
        }

        public async Task<BaseResponse<object>> UpdateTableStatusAsync(UpdateTableStatusRequest req)
        {
            try
            {
                await _c.Db.Table<TableModel>().Where(t => t.Id == req.TableId).Set(t => t.Status, req.Status).Update();
                return BaseResponse<object>.SuccessResult(null);
            }
            catch (Exception ex) { return BaseResponse<object>.ErrorResult(ex.Message); }
        }
    }

    // ─── MenuService ───────────────────────────────────────────────
    public class MenuService : IMenuService
    {
        private readonly DatabaseClient _c;
        public MenuService(DatabaseClient c) => _c = c;

        public async Task<BaseResponse<List<MenuItemModel>>> GetAllMenuItemsAsync()
        {
            try
            {
                var r = await _c.Db.Table<MenuItemModel>().Select(DatabaseClient.MenuItemColumns).Where(m => m.IsActive == true).Get();
                return BaseResponse<List<MenuItemModel>>.SuccessResult(r.Models);
            }
            catch (Exception ex) { return BaseResponse<List<MenuItemModel>>.ErrorResult(ex.Message); }
        }

        public async Task<BaseResponse<MenuItemModel>> AddMenuItemAsync(AddMenuItemRequest req)
        {
            try
            {
                var item = new MenuItemModel { Name = req.Name, Category = req.Category, Price = req.Price };
                var r = await _c.Db.Table<MenuItemModel>().Insert(item);
                return BaseResponse<MenuItemModel>.SuccessResult(r.Models.First());
            }
            catch (Exception ex) { return BaseResponse<MenuItemModel>.ErrorResult(ex.Message); }
        }

        public async Task<BaseResponse<object>> UpdateMenuItemAsync(UpdateMenuItemRequest req)
        {
            try
            {
                var item = new MenuItemModel { Id = req.Id, Name = req.Name, Category = req.Category, Price = req.Price, IsActive = req.IsActive };
                await _c.Db.Table<MenuItemModel>().Update(item);
                return BaseResponse<object>.SuccessResult(null);
            }
            catch (Exception ex) { return BaseResponse<object>.ErrorResult(ex.Message); }
        }

        public async Task<BaseResponse<object>> DeleteMenuItemAsync(string id)
        {
            try
            {
                await _c.Db.Table<MenuItemModel>().Where(m => m.Id == id).Set(m => m.IsActive, false).Update();
                return BaseResponse<object>.SuccessResult(null);
            }
            catch (Exception ex) { return BaseResponse<object>.ErrorResult(ex.Message); }
        }
    }

    // ─── OrderService ──────────────────────────────────────────────
    public class OrderService : IOrderService
    {
        private readonly DatabaseClient _c;
        private readonly ITableService _ts;
        public OrderService(DatabaseClient c, ITableService ts) { _c = c; _ts = ts; }

        public async Task<BaseResponse<OrderModel?>> GetActiveOrderByTableAsync(string tableId)
        {
            try
            {
                var r = await _c.Db.Table<OrderModel>().Select(DatabaseClient.OrderColumns).Where(o => o.Status == "aktif" && o.TableId == tableId).Get();
                return BaseResponse<OrderModel?>.SuccessResult(r.Models.FirstOrDefault());
            }
            catch (Exception ex) { return BaseResponse<OrderModel?>.ErrorResult(ex.Message); }
        }

        public async Task<BaseResponse<OrderModel>> CreateOrderAsync(string tableId)
        {
            try
            {
                var order = new OrderModel { TableId = tableId, Status = "aktif", Total = 0 };
                var r = await _c.Db.Table<OrderModel>().Insert(order);
                return BaseResponse<OrderModel>.SuccessResult(r.Models.First());
            }
            catch (Exception ex) { return BaseResponse<OrderModel>.ErrorResult(ex.Message); }
        }

        public async Task<BaseResponse<object>> CloseOrderAsync(CloseOrderRequest req)
        {
            try
            {
                await _c.Db.Table<OrderModel>().Where(o => o.Id == req.OrderId).Set(o => o.Status, "odendi").Set(o => o.Total, req.FinalTotal).Update();
                await _ts.UpdateTableStatusAsync(new UpdateTableStatusRequest { TableId = req.TableId, Status = "bos" });
                return BaseResponse<object>.SuccessResult(null);
            }
            catch (Exception ex) { return BaseResponse<object>.ErrorResult(ex.Message); }
        }

        public async Task<BaseResponse<List<OrderItemModel>>> GetOrderItemsAsync(string orderId)
        {
            try
            {
                var r = await _c.Db.Table<OrderItemModel>().Select(DatabaseClient.OrderItemColumns).Where(i => i.OrderId == orderId).Get();
                return BaseResponse<List<OrderItemModel>>.SuccessResult(r.Models);
            }
            catch (Exception ex) { return BaseResponse<List<OrderItemModel>>.ErrorResult(ex.Message); }
        }

        public async Task<BaseResponse<OrderItemModel>> AddOrderItemAsync(AddOrderItemRequest req)
        {
            try
            {
                var item = new OrderItemModel { OrderId = req.OrderId, MenuItemId = req.MenuItemId, Quantity = req.Quantity, Price = req.Price };
                var r = await _c.Db.Table<OrderItemModel>().Insert(item);
                return BaseResponse<OrderItemModel>.SuccessResult(r.Models.First());
            }
            catch (Exception ex) { return BaseResponse<OrderItemModel>.ErrorResult(ex.Message); }
        }

        public async Task<BaseResponse<object>> UpdateOrderItemQuantityAsync(UpdateOrderItemQuantityRequest req)
        {
            try
            {
                await _c.Db.Table<OrderItemModel>().Where(i => i.Id == req.ItemId).Set(i => i.Quantity, req.Quantity).Update();
                return BaseResponse<object>.SuccessResult(null);
            }
            catch (Exception ex) { return BaseResponse<object>.ErrorResult(ex.Message); }
        }

        public async Task<BaseResponse<object>> RemoveOrderItemAsync(string itemId)
        {
            try
            {
                await _c.Db.Table<OrderItemModel>().Where(i => i.Id == itemId).Delete();
                return BaseResponse<object>.SuccessResult(null);
            }
            catch (Exception ex) { return BaseResponse<object>.ErrorResult(ex.Message); }
        }
    }
}