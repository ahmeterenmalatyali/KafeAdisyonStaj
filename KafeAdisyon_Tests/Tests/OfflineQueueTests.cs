using FluentAssertions;
using KafeAdisyon.Tests.TestInfrastructure;
using Xunit;

namespace KafeAdisyon.Tests
{
    /// <summary>
    /// InMemoryOfflineQueue (= üretim OfflineQueue'nun MAUI bağımsız karşılığı) testleri.
    /// Her test bağımsız bir queue örneği kullanır.
    /// </summary>
    public class OfflineQueueTests
    {
        private static InMemoryOfflineQueue NewQueue() => new();

        // ─── Temel CRUD ───────────────────────────────────────────────────────

        [Fact(DisplayName = "OfflineQueue: Başlangıçta kuyruk boş gelir")]
        public async Task InitialState_QueueIsEmpty()
        {
            var q = NewQueue();

            var items = await q.GetAllAsync();
            var count = await q.CountAsync();

            items.Should().BeEmpty();
            count.Should().Be(0);
        }

        [Fact(DisplayName = "OfflineQueue: Enqueue tek öğe ekler, Count 1 olur")]
        public async Task Enqueue_SingleItem_CountIsOne()
        {
            var q = NewQueue();

            await q.EnqueueAsync("AddItem", new { MenuItemId = "m1", Quantity = 2 });

            var count = await q.CountAsync();
            count.Should().Be(1);
        }

        [Fact(DisplayName = "OfflineQueue: Birden fazla Enqueue sırasını korur")]
        public async Task Enqueue_MultipleItems_OrderPreserved()
        {
            var q = NewQueue();

            await q.EnqueueAsync("CreateOrder", "table_1");
            await q.EnqueueAsync("AddItem",     new { MenuItemId = "m1" });
            await q.EnqueueAsync("CloseOrder",  new { OrderId = "o1" });

            var items = await q.GetAllAsync();

            items.Should().HaveCount(3);
            items[0].Operation.Should().Be("CreateOrder", "ilk giren ilk çıkmalı");
            items[1].Operation.Should().Be("AddItem");
            items[2].Operation.Should().Be("CloseOrder");
        }

        [Fact(DisplayName = "OfflineQueue: Remove var olan öğeyi siler")]
        public async Task Remove_ExistingItem_RemovedFromQueue()
        {
            var q = NewQueue();
            await q.EnqueueAsync("AddItem", "m1");
            await q.EnqueueAsync("AddItem", "m2");

            var items = await q.GetAllAsync();
            var firstId = items[0].Id;

            await q.RemoveAsync(firstId);

            var remaining = await q.GetAllAsync();
            remaining.Should().HaveCount(1);
            remaining[0].Id.Should().NotBe(firstId);
        }

        [Fact(DisplayName = "OfflineQueue: Remove var olmayan ID — kuyruk değişmez")]
        public async Task Remove_NonExistingId_QueueUnchanged()
        {
            var q = NewQueue();
            await q.EnqueueAsync("AddItem", "m1");

            await q.RemoveAsync("non_existing_guid");

            var count = await q.CountAsync();
            count.Should().Be(1, "geçersiz ID hiçbir şeyi etkilememeli");
        }

        [Fact(DisplayName = "OfflineQueue: IncrementRetry mevcut öğenin sayacını artırır")]
        public async Task IncrementRetry_ExistingItem_CountIncremented()
        {
            var q = NewQueue();
            await q.EnqueueAsync("AddItem", "m1");

            var item = (await q.GetAllAsync())[0];
            item.RetryCount.Should().Be(0, "ilk eklenişte retry sıfır olmalı");

            await q.IncrementRetryAsync(item.Id);
            await q.IncrementRetryAsync(item.Id);

            var updated = (await q.GetAllAsync())[0];
            updated.RetryCount.Should().Be(2);
        }

        [Fact(DisplayName = "OfflineQueue: Clear tüm öğeleri siler")]
        public async Task Clear_RemovesAllItems()
        {
            var q = NewQueue();
            await q.EnqueueAsync("AddItem",    "m1");
            await q.EnqueueAsync("AddItem",    "m2");
            await q.EnqueueAsync("CloseOrder", new { });

            await q.ClearAsync();

            var count = await q.CountAsync();
            count.Should().Be(0);
        }

        // ─── Payload serileştirme ──────────────────────────────────────────────

        [Fact(DisplayName = "OfflineQueue: Deserialize<string> string payload'ı doğru çözer")]
        public async Task Deserialize_StringPayload_ReturnsCorrectValue()
        {
            var q = NewQueue();
            await q.EnqueueAsync("CreateOrder", "table_42");

            var item = (await q.GetAllAsync())[0];
            var tableId = InMemoryOfflineQueue.Deserialize<string>(item.Payload);

            tableId.Should().Be("table_42");
        }

        [Fact(DisplayName = "OfflineQueue: Deserialize<T> nesne payload'ı doğru çözer")]
        public async Task Deserialize_ObjectPayload_ReturnsCorrectObject()
        {
            var q = NewQueue();
            var req = new { OrderId = "o99", MenuItemId = "m7", Quantity = 3, Price = 45.0 };
            await q.EnqueueAsync("AddItem", req);

            var item = (await q.GetAllAsync())[0];

            // anonim tip yerine somut tip ile çöz
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<
                System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>>(item.Payload);

            deserialized.Should().NotBeNull();
            deserialized!["OrderId"].GetString().Should().Be("o99");
            deserialized["Quantity"].GetInt32().Should().Be(3);
        }

        // ─── Eşzamanlılık ─────────────────────────────────────────────────────

        [Fact(DisplayName = "OfflineQueue: Eşzamanlı Enqueue — veri kaybı olmaz")]
        public async Task ConcurrentEnqueue_NoDataLoss()
        {
            var q = NewQueue();

            // 10 task aynı anda Enqueue çağırır
            var tasks = Enumerable.Range(0, 10)
                .Select(i => q.EnqueueAsync("Op" + i, i))
                .ToArray();

            await Task.WhenAll(tasks);

            var count = await q.CountAsync();
            count.Should().Be(10, "her task bir öğe eklemeli, kayıp olmamalı");
        }
    }
}
