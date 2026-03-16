using KafeAdisyon.Models;

namespace KafeAdisyon.Tests.TestInfrastructure
{
    public static class TestFactory
    {
        public static MenuItemModel Menu(string id, string name, double price, string category = "İçecek")
            => new() { Id = id, Name = name, Price = price, Category = category, IsActive = true };

        public static OrderItemModel OrderItem(string id, string menuItemId, double price, int qty = 1, string orderId = "order_1")
            => new() { Id = id, OrderId = orderId, MenuItemId = menuItemId, Price = price, Quantity = qty };

        public static OrderModel Order(string id = "order_1", string tableId = "table_1")
            => new() { Id = id, TableId = tableId, Status = "aktif", Total = 0 };

        public static TableModel Table(string id = "table_1", string name = "A-1", string status = "bos")
            => new() { Id = id, Name = name, Status = status };
    }
}
