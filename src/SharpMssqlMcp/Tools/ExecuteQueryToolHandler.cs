using System.Text.Json;
using SharpMssqlMcp.Models;
using SharpMssqlMcp.Services;

namespace SharpMssqlMcp.Tools;

public class ExecuteQueryToolHandler : IToolHandler
{
    private readonly QueryExecutor _queryExecutor;

    public ExecuteQueryToolHandler(QueryExecutor queryExecutor)
    {
        _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
    }

    public string Name => "execute_query";

    public async Task<JsonRpcResponse> HandleAsync(JsonElement? arguments, object? id, CancellationToken ct)
    {
        if (arguments == null || arguments.Value.ValueKind != JsonValueKind.Object)
        {
            return JsonRpcResponse.Failure(id, -32602, "Invalid arguments: expected object");
        }

        var args = arguments.Value;

        if (!args.TryGetProperty("dataSource", out var dsProp) || dsProp.ValueKind != JsonValueKind.String)
        {
            return JsonRpcResponse.Failure(id, -32602, "Missing or invalid 'dataSource' argument");
        }

        if (!args.TryGetProperty("query", out var queryProp) || queryProp.ValueKind != JsonValueKind.String)
        {
            return JsonRpcResponse.Failure(id, -32602, "Missing or invalid 'query' argument");
        }

        var dataSource = dsProp.GetString()!;
        var query = queryProp.GetString()!;

        // Parse optional parameters
        Dictionary<string, object?>? parameters = null;
        if (args.TryGetProperty("parameters", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
        {
            parameters = new Dictionary<string, object?>();
            foreach (var prop in paramsProp.EnumerateObject())
            {
                parameters[prop.Name] = GetValue(prop.Value);
            }
        }

        // Parse optional options
        int maxRows = 10000;
        int timeout = 300;

        if (args.TryGetProperty("options", out var optionsProp) && optionsProp.ValueKind == JsonValueKind.Object)
        {
            if (optionsProp.TryGetProperty("maxRows", out var maxRowsProp) && maxRowsProp.ValueKind == JsonValueKind.Number)
            {
                maxRows = maxRowsProp.GetInt32();
            }
            if (optionsProp.TryGetProperty("timeout", out var timeoutProp) && timeoutProp.ValueKind == JsonValueKind.Number)
            {
                timeout = timeoutProp.GetInt32();
            }
        }

        try
        {
            RequestValidator.ValidateQuery(query, parameters, timeout);
            var result = await _queryExecutor.ExecuteQueryAsync(dataSource, query, parameters, maxRows, timeout, ct);
            return JsonRpcResponse.Success(id, result);
        }
        catch (Exception ex)
        {
            return ErrorResponseBuilder.BuildError(id, ex, dataSource, query);
        }
    }

    private object? GetValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
