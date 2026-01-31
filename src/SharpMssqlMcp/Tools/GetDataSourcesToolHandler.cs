using System.Text.Json;
using Microsoft.Data.SqlClient;
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
                using var connection = _connectionManager.GetConnection(dsName);
                var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
                server = builder.DataSource;
                database = builder.InitialCatalog;

                // Simple ping
                var validationBuilder = new SqlConnectionStringBuilder(connection.ConnectionString)
                {
                    ConnectTimeout = 2
                };
                using var validationConn = new SqlConnection(validationBuilder.ConnectionString);
                await validationConn.OpenAsync(ct);
                status = "connected";
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

        return JsonRpcResponse.Success(id, new { dataSources });
    }
}
