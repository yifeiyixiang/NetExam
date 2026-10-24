using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LeaveApproval.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MySqlConnector;
using Xunit;

namespace LeaveApproval.IntegrationTests;

/// <summary>
/// 集成测试固定装置：使用真实 MySQL（leaveapproval_test 库）与真实 Redis。
/// 启动时删除并重建测试库（Program 在 Database:InitializeOnStartup=true 时执行 Migration + 种子数据）。
/// </summary>
public class LeaveApprovalApiFactory : WebApplicationFactory<Program>
{
    public const string TestDbName = "leaveapproval_test";
    public const string ServerConnString = "Server=localhost;Port=10010;User=root;Password=root;charset=utf8mb4";
    public static readonly string TestDbConnString = $"Server=localhost;Port=10010;Database={TestDbName};User=root;Password=root;charset=utf8mb4";

    public static readonly string UploadRoot = Path.Combine(Path.GetTempPath(), "leaveapproval-tests-" + Guid.NewGuid().ToString("N")[..8]);

    public LeaveApprovalApiFactory()
    {
        DropTestDatabase();
        FlushTestRedis();
    }

    /// <summary>测试专用：清空本地 Redis（缓存键可能残留上次运行的 detail:1 等脏数据）。</summary>
    private static void FlushTestRedis()
    {
        try
        {
            using var redis = StackExchange.Redis.ConnectionMultiplexer.Connect("localhost:6379,allowAdmin=true");
            foreach (var server in redis.GetServers())
            {
                if (!server.IsReplica)
                {
                    server.FlushDatabase();
                }
            }
        }
        catch (StackExchange.Redis.RedisConnectionException)
        {
            // Redis 不可达时让后续用例直接失败，给出更明确的错误
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.UseSetting("ConnectionStrings:Default", TestDbConnString);
        builder.UseSetting("Redis:ConnectionString", "localhost:6379");
        builder.UseSetting("Jwt:SecretKey", "IntegrationTest_Jwt_Secret_Key_Not_For_Production_0123456789");
        builder.UseSetting("Database:InitializeOnStartup", "true");
        builder.UseSetting("FileStorage:RootPath", UploadRoot);
    }

    private static void DropTestDatabase()
    {
        try
        {
            using var connection = new MySqlConnection(ServerConnString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS {TestDbName}";
            command.ExecuteNonQuery();
        }
        catch (MySqlException)
        {
            // 数据库不可达时让后续启动直接失败，给出更明确的错误
        }
    }
}

[CollectionDefinition("api")]
public class ApiTestCollection : ICollectionFixture<LeaveApprovalApiFactory>
{
}

/// <summary>集成测试扩展方法：登录并返回携带 Bearer Token 的 HttpClient。</summary>
public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        return response;
    }

    public static async Task<(HttpClient Client, JsonElement Body)> LoginAsAsync(this HttpClient client, string username, string password)
    {
        var response = await client.LoginAsync(username, password);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var token = body.GetProperty("accessToken").GetString()!;

        var authedClient = client;
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (authedClient, body);
    }

    public static async Task<HttpResponseMessage> ApproveAsync(this HttpClient client, long leaveId, string? comment)
    {
        return await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/approve", new { comment });
    }

    public static async Task<HttpResponseMessage> RejectAsync(this HttpClient client, long leaveId, string? comment)
    {
        return await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/reject", new { comment });
    }
}
