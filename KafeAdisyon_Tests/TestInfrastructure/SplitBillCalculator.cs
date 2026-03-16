// SplitBillCalculator — SplitBillPage'deki hesap bölme mantığının
// saf (MAUI bağımlılığı olmayan) versiyonu. UI kodu yoktur.

using KafeAdisyon.Models;

namespace KafeAdisyon.Tests.TestInfrastructure
{
    public class SplitBillCalculator
    {
        private readonly List<OrderItemModel> _orderItems;
        private readonly Dictionary<string, int> _selectedQty;

        public SplitBillCalculator(List<OrderItemModel> orderItems)
        {
            _orderItems = orderItems;
            _selectedQty = orderItems.ToDictionary(i => i.Id, _ => 0);
        }

        // SplitBillPage.UpdateTotal() ile birebir aynı
        public double GetSelectedTotal()
        {
            double total = 0;
            foreach (var item in _orderItems)
                total += item.Price * (_selectedQty.TryGetValue(item.Id, out int q) ? q : 0);
            return total;
        }

        // SplitBillPage.OnPayClicked → OrderPage.OnSplitBillClicked remaining hesabı
        public double GetRemainingTotal()
        {
            double total = 0;
            foreach (var item in _orderItems)
            {
                int sel = _selectedQty.TryGetValue(item.Id, out int q) ? q : 0;
                int rem = item.Quantity - sel;
                if (rem > 0) total += item.Price * rem;
            }
            return total;
        }

        public double GetOrderTotal() => _orderItems.Sum(i => i.Price * i.Quantity);

        public bool SetSelection(string itemId, int qty)
        {
            var item = _orderItems.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return false;
            if (qty < 0 || qty > item.Quantity) return false;
            _selectedQty[itemId] = qty;
            return true;
        }

        public void SelectAll()
        {
            foreach (var item in _orderItems)
                _selectedQty[item.Id] = item.Quantity;
        }

        public void SelectNone()
        {
            foreach (var key in _selectedQty.Keys.ToList())
                _selectedQty[key] = 0;
        }

        // OrderPage.OnSplitBillClicked mantığı — toRemove/toUpdate listelerini döner
        public (List<OrderItemModel> ToRemove, List<(OrderItemModel Item, int Remaining)> ToUpdate)
            ComputeChanges()
        {
            var toRemove = new List<OrderItemModel>();
            var toUpdate = new List<(OrderItemModel, int)>();

            foreach (var kv in _selectedQty.Where(k => k.Value > 0))
            {
                var item = _orderItems.FirstOrDefault(i => i.Id == kv.Key);
                if (item == null) continue;
                int remaining = item.Quantity - kv.Value;
                if (remaining <= 0) toRemove.Add(item);
                else toUpdate.Add((item, remaining));
            }
            return (toRemove, toUpdate);
        }

        // OrderPage.RecalcTotal() mantığı — değişiklikler uygulandıktan sonra kalan toplam
        public double ComputePostSplitTotal()
        {
            var (toRemove, toUpdate) = ComputeChanges();
            var removeIds = toRemove.Select(i => i.Id).ToHashSet();
            var updateMap = toUpdate.ToDictionary(t => t.Item.Id, t => t.Remaining);

            double total = 0;
            foreach (var item in _orderItems)
            {
                if (removeIds.Contains(item.Id)) continue;
                int qty = updateMap.TryGetValue(item.Id, out int r) ? r : item.Quantity;
                total += item.Price * qty;
            }
            return total;
        }

        public int GetSelection(string itemId) =>
            _selectedQty.TryGetValue(itemId, out int q) ? q : 0;
    }
}
