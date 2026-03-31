using Microsoft.Extensions.Configuration;
using Postgrest;

namespace KafeAdisyon.Infrastructure.Client;

/// <summary>
/// Supabase Postgrest istemcisini yapılandırıp tüm Infrastructure servislerine sağlar.
/// AdministrativeAffairsService'deki DbContext'in karşılığı.
///
/// Login sonrasında SetAuthToken() çağrılarak JWT token header'a enjekte edilir.
/// Bu sayede RLS politikaları auth.uid() üzerinden doğru çalışır.
/// </summary>
public class DatabaseClient
{
    public readonly Postgrest.Client Db;

    private readonly string _apiKey;

    // Her sorgu için gereken kolonlar — created_at gereksiz yere çekilmez
    public const string TableColumns = "id,name,status";
    public const string MenuItemColumns = "id,name,category,price,is_active";
    public const string OrderColumns = "id,table_id,status,total";
    public const string OrderItemColumns = "id,order_id,menu_item_id,quantity,price";
    public const string AuditLogColumns = "id,user_id,user_name,role,action,detail,device_name,created_at";

    public DatabaseClient(IConfiguration config)
    {
        var url = config["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url appsettings.json'da bulunamadı.");
        _apiKey = config["Supabase:PublishableKey"]
            ?? throw new InvalidOperationException("Supabase:PublishableKey appsettings.json'da bulunamadı.");

        var restUrl = url.TrimEnd('/') + "/rest/v1";

        Db = new Postgrest.Client(restUrl, new ClientOptions
        {
            Headers = new Dictionary<string, string>
            {
                { "apikey", _apiKey },
                { "Authorization", $"Bearer {_apiKey}" }
            }
        });
    }

    /// <summary>
    /// Login sonrası JWT token'ı Postgrest Authorization header'ına enjekte eder.
    /// RLS politikaları auth.uid() üzerinden bu token'ı okur.
    /// </summary>
    public void SetAuthToken(string jwtToken)
    {
        Db.Options.Headers["Authorization"] = $"Bearer {jwtToken}";
    }

    /// <summary>
    /// Logout sonrası anon key'e geri döner.
    /// </summary>
    public void ClearAuthToken()
    {
        Db.Options.Headers["Authorization"] = $"Bearer {_apiKey}";
    }
}