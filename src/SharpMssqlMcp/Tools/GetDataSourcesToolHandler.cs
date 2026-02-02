using System.Text.Json;
using SharpMssqlMcp.Models;
using SharpMssqlMcp.Services;

namespace SharpMssqlMcp.Tools;

public class GetDataSourcesToolHandler : IToolHandler
{
    private readonly AppConfig _config;
    private readonly ConnectionManager _connectionManager;

    public GetDataSourcesToolHandler(AppConfig config, ConnectionManager connectionManager)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public string Name => "get_datasources";
    public string Description => "Returns a list of configured data sources.";
    public object InputSchema => new
    {
        type = "object",
        properties = new { },
        required = new string[] { }
    };

    public async Task<JsonRpcResponse> HandleAsync(JsonElement? arguments, object? id, CancellationToken ct)
    {
        var dataSources = new List<object>();

        foreach (var ds in _config.DataSources)
        {
            var dsName = ds.Key;
            var dsConfig = ds.Value;
            string server = "Unknown";
            string database = "Unknown";
            string status = "disconnected";
            string? lastError = null;

            try
            {
                var (connection, provider) = _connectionManager.GetConnection(dsName);
                using (connection)
                {
                    var info = provider.Strategy.GetConnectionStringInfo(connection.ConnectionString);
                    server = info.Server;
                    database = info.Database;

                    // Simple ping - reuse the connection we just created or open it
                    await connection.OpenAsync(ct);
                    status = "connected";
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            dataSources.Add(new
            {
                name = dsName,
                description = dsConfig.Description,
                server,
                database,
                status,
                lastError,
                lastCheckTime = DateTime.UtcNow.ToString("o")
            });
        }

        var mcpResult = new
        {
            content = new[]
            {
                new 
                { 
                    type = "text", 
                    text = JsonSerializer.Serialize(new { dataSources }, new JsonSerializerOptions { WriteIndented = true }) 
                }
            }
        };

        return JsonRpcResponse.Success(id, mcpResult);
    }
}
