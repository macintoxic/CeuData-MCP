using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SharpMssqlMcp.Models;
using SharpMssqlMcp.Services;
using SharpMssqlMcp.Tools;

namespace SharpMssqlMcp;

class Program
{
    private static readonly Dictionary<string, IToolHandler> _tools = new();
    private static AppConfig _config = new();
    private static ConnectionManager? _connectionManager;
    private static QueryExecutor? _queryExecutor;

    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await Console.Error.WriteLineAsync("Sharp MSSQL MCP Server starting...");

        try
        {
            LoadConfiguration();
            _connectionManager = new ConnectionManager(_config);
            _queryExecutor = new QueryExecutor(_connectionManager);
            await ValidateConnectionsAsync();
            RegisterTools();

            while (!cts.Token.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cts.Token);
                if (line == null) break;

                if (string.IsNullOrWhiteSpace(line)) continue;

                await ProcessRequest(line, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Shutdown requested.");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Fatal error: {ex.GetType().Name}: {ex.Message}");
            await Console.Error.WriteLineAsync(ex.StackTrace);
        }
        finally
        {
            await Console.Error.WriteLineAsync("Sharp MSSQL MCP Server stopped.");
        }
    }

    private static void LoadConfiguration()
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();

        var configuration = configBuilder.Build();
        _config = configuration.Get<AppConfig>() ?? throw new InvalidOperationException("Failed to load configuration.");

        var substitutor = new EnvironmentSubstitutor();

        // Apply environment variable substitution to connection strings
        foreach (var key in _config.ConnectionStrings.Keys.ToList())
        {
            try
            {
                _config.ConnectionStrings[key] = substitutor.Substitute(_config.ConnectionStrings[key]);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to substitute environment variables in connection string '{key}': {ex.Message}");
                // We don't throw here to allow other datasources to work if possible
            }
        }

        // Validate DataSources
        foreach (var ds in _config.DataSources)
        {
            if (!_config.ConnectionStrings.ContainsKey(ds.Value.ConnectionStringKey))
            {
                Console.Error.WriteLine($"Warning: DataSource '{ds.Key}' references missing ConnectionStringKey '{ds.Value.ConnectionStringKey}'");
            }
            else
            {
                Console.Error.WriteLine($"Loaded DataSource: {ds.Key} ({ds.Value.Description})");
            }
        }
    }

    private static async Task ValidateConnectionsAsync()
    {
        if (_connectionManager == null) return;

        await Console.Error.WriteLineAsync("Validating datasource connections...");

        foreach (var dsName in _config.DataSources.Keys)
        {
            try
            {
                using var connection = _connectionManager.GetConnection(dsName);
                // We use a short timeout for startup validation
                var builder = new SqlConnectionStringBuilder(connection.ConnectionString)
                {
                    ConnectTimeout = 5 
                };
                connection.ConnectionString = builder.ConnectionString;

                await connection.OpenAsync();
                await Console.Error.WriteLineAsync($"[OK] {dsName}: Connection successful.");
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[FAIL] {dsName}: {ex.Message}");
            }
        }
    }

    private static void RegisterTools()
    {
        RegisterTool(new EchoToolHandler());
        if (_queryExecutor != null)
        {
            RegisterTool(new ExecuteQueryToolHandler(_queryExecutor));
        }
    }

    private static void RegisterTool(IToolHandler tool)
    {
        _tools[tool.Name] = tool;
    }

    static async Task ProcessRequest(string line, CancellationToken ct)
    {
        object? requestId = null;
        try
        {
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(line);
            if (request == null) return;

            requestId = GetIdValue(request.Id);

            // Log request method to stderr
            await Console.Error.WriteLineAsync($"Received method: {request.Method} (ID: {requestId})");

            JsonRpcResponse response;

            switch (request.Method)
            {
                case "initialize":
                    response = JsonRpcResponse.Success(requestId, new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "sharp-mssql-mcp", version = "1.0.0" }
                    });
                    break;

                case "tools/list":
                    response = JsonRpcResponse.Success(requestId, new
                    {
                        tools = _tools.Values.Select(t => new { name = t.Name, description = $"Handler for {t.Name}" })
                    });
                    break;

                case "tools/call":
                    response = await HandleToolCall(request, requestId, ct);
                    break;

                case "notifications/initialized":
                    return;

                default:
                    response = JsonRpcResponse.Failure(requestId, -32601, $"Method not found: {request.Method}");
                    break;
            }

            var jsonResponse = JsonSerializer.Serialize(response);
            await Console.Out.WriteLineAsync(jsonResponse);
        }
        catch (JsonException ex)
        {
            await Console.Error.WriteLineAsync($"Invalid JSON: {ex.Message}");
            var errorResponse = JsonRpcResponse.Failure(null, -32700, "Parse error");
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error processing request: {ex.Message}");
            var errorResponse = JsonRpcResponse.Failure(requestId, -32603, "Internal error");
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
        }
    }

    static async Task<JsonRpcResponse> HandleToolCall(JsonRpcRequest request, object? id, CancellationToken ct)
    {
        if (request.Params == null)
        {
            return JsonRpcResponse.Failure(id, -32602, "Invalid params: expected object");
        }

        if (!request.Params.Value.TryGetProperty("name", out var nameProp))
        {
            return JsonRpcResponse.Failure(id, -32602, "Missing tool name");
        }

        var toolName = nameProp.GetString();
        if (string.IsNullOrEmpty(toolName) || !_tools.TryGetValue(toolName, out var handler))
        {
            return JsonRpcResponse.Failure(id, -32602, $"Tool not found: {toolName}");
        }

        request.Params.Value.TryGetProperty("arguments", out var arguments);
        return await handler.HandleAsync(arguments, id, ct);
    }

    private static object? GetIdValue(JsonElement? idElement)
    {
        if (idElement == null) return null;
        var val = idElement.Value;
        return val.ValueKind switch
        {
            JsonValueKind.Number => val.GetInt64(),
            JsonValueKind.String => val.GetString(),
            _ => null
        };
    }
}
