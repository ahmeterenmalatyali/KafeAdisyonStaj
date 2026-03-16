using KafeAdisyon.IntegrationTests.Infrastructure;
using Xunit;

namespace KafeAdisyon.IntegrationTests
{
    /// <summary>
    /// Tüm DB testleri bu collection'ı kullanır.
    /// xUnit bu sayede fixture'ı bir kez oluşturur, testler sırayla çalışır.
    /// </summary>
    [CollectionDefinition("Database")]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
}
