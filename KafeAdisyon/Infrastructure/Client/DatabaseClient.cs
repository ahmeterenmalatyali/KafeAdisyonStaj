using Microsoft.Extensions.Configuration;
using Postgrest;

namespace KafeAdisyon.Infrastructure.Client;

/// <summary>
/// Supabase Postgrest istemcisini yapılandırıp tüm Infrastructure servislerine sağlar.
/// AdministrativeAffairsService'deki DbContext'in karşılığı.
/// </summary>
public class DatabaseClient
{
    public readonly Postgrest.Client Db;

    // Her sorgu için gereken kolonlar — created_at gereksiz yere çekilmez
    public const string TableColumns     = "id,name,status";
    public const string MenuItemColumns  = "id,name,category,price,is_active";
    public const string OrderColumns     = "id,table_id,status,total";
    public const string OrderItemColumns = "id,order_id,menu_item_id,quantity,price";

    public DatabaseClient(IConfiguration config)
    {
        var url = config["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url appsettings.json'da bulunamadı.");
        var key = config["Supabase:PublishableKey"]
            ?? throw new InvalidOperationException("Supabase:PublishableKey appsettings.json'da bulunamadı.");

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
