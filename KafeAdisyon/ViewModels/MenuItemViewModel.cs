using System.Windows.Input;
using KafeAdisyon.Models;

namespace KafeAdisyon.ViewModels;

/// <summary>
/// CollectionView DataTemplate için wrapper ViewModel.
/// Her MenuItemModel'ı sararak DataTemplate içindeki Button.Command'a bağlanır.
/// </summary>
public class MenuItemViewModel
{
    private readonly MenuItemModel _model;
    private readonly OrderViewModel _orderVm;

    public string Id           => _model.Id;
    public string Name         => _model.Name;
    public string Category     => _model.Category;
    public double Price        => _model.Price;
    public string PriceDisplay => $"₺{_model.Price:F0}";

    public ICommand AddCommand { get; }

    public MenuItemViewModel(MenuItemModel model, OrderViewModel orderVm)
    {
        _model = model;
        _orderVm = orderVm;

        AddCommand = new Command(async () =>
        {
            if (_orderVm.CurrentOrder == null)
                await _orderVm.EnsureOrderCreatedAsync(_orderVm.TableId);

            _orderVm.AddItemOptimistic(_model);
            _orderVm.RaiseMenuItemAdded(_model);
            _ = _orderVm.SyncItemToDbAsync(_model);
        });
    }
}
