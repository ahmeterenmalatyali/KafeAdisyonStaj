using System.Text.Json;

namespace KafeAdisyon.Infrastructure.Offline;
public class OfflineQueue
{
    private const string PrefsKey = "offline_order_queue";
    private readonly SemaphoreSlim _lock = new(1, 1);

    public record QueueItem(
        string Id,           // Guid — tekil tanımlayıcı
        string Operation,    // "AddItem" | "UpdateQuantity" | "RemoveItem" | "CreateOrder" | "CloseOrder" | "UpdateTableStatus"
        string Payload,      // JSON olarak serileştirilmiş istek modeli
        DateTime CreatedAt,
        int RetryCount = 0
    );

    public async Task<List<QueueItem>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var json = Preferences.Default.Get(PrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return new List<QueueItem>();

            return JsonSerializer.Deserialize<List<QueueItem>>(json)
                   ?? new List<QueueItem>();
        }
        finally { _lock.Release(); }
    }

    public async Task<int> CountAsync()
        => (await GetAllAsync()).Count;


    public async Task EnqueueAsync(string operation, object payload)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadRawAsync();
            items.Add(new QueueItem(
                Id: Guid.NewGuid().ToString(),
                Operation: operation,
                Payload: JsonSerializer.Serialize(payload),
                CreatedAt: DateTime.UtcNow
            ));
            Save(items);
        }
        finally { _lock.Release(); }
    }

    public async Task RemoveAsync(string itemId)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadRawAsync();
            items.RemoveAll(i => i.Id == itemId);
            Save(items);
        }
        finally { _lock.Release(); }
    }

    public async Task IncrementRetryAsync(string itemId)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadRawAsync();
            var idx = items.FindIndex(i => i.Id == itemId);
            if (idx >= 0)
            {
                var old = items[idx];
                items[idx] = old with { RetryCount = old.RetryCount + 1 };
                Save(items);
            }
        }
        finally { _lock.Release(); }
    }

    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try { Preferences.Default.Remove(PrefsKey); }
        finally { _lock.Release(); }
    }
    private Task<List<QueueItem>> ReadRawAsync()
    {
        var json = Preferences.Default.Get(PrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return Task.FromResult(new List<QueueItem>());

        return Task.FromResult(
            JsonSerializer.Deserialize<List<QueueItem>>(json) ?? new List<QueueItem>());
    }

    private static void Save(List<QueueItem> items)
    {
        var json = JsonSerializer.Serialize(items);
        Preferences.Default.Set(PrefsKey, json);
    }

    public static T Deserialize<T>(string payload)
        => JsonSerializer.Deserialize<T>(payload)
           ?? throw new InvalidOperationException($"Payload {typeof(T).Name} olarak çözülemedi.");
}