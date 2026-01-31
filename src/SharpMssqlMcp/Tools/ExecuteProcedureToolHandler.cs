using System.Text.Json;
using SharpMssqlMcp.Models;
using SharpMssqlMcp.Services;

namespace SharpMssqlMcp.Tools;

public class ExecuteProcedureToolHandler : IToolHandler
{
    private readonly ProcedureExecutor _procedureExecutor;

    public ExecuteProcedureToolHandler(ProcedureExecutor procedureExecutor)
    {
        _procedureExecutor = procedureExecutor ?? throw new ArgumentNullException(nameof(procedureExecutor));
    }

    public string Name => "execute_procedure";

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

        if (!args.TryGetProperty("procedure", out var procProp) || procProp.ValueKind != JsonValueKind.String)
        {
            return JsonRpcResponse.Failure(id, -32602, "Missing or invalid 'procedure' argument");
        }

        var dataSource = dsProp.GetString()!;
        var procedure = procProp.GetString()!;

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
        int timeout = 300;
        bool includeOutputParameters = false;

        if (args.TryGetProperty("options", out var optionsProp) && optionsProp.ValueKind == JsonValueKind.Object)
        {
            if (optionsProp.TryGetProperty("timeout", out var timeoutProp) && timeoutProp.ValueKind == JsonValueKind.Number)
            {
                timeout = timeoutProp.GetInt32();
            }
            if (optionsProp.TryGetProperty("includeOutputParameters", out var includeProp) && 
                (includeProp.ValueKind == JsonValueKind.True || includeProp.ValueKind == JsonValueKind.False))
            {
                includeOutputParameters = includeProp.GetBoolean();
            }
        }

        try
        {
            RequestValidator.ValidateProcedure(procedure, timeout);
            var result = await _procedureExecutor.ExecuteProcedureAsync(dataSource, procedure, parameters, timeout, includeOutputParameters, ct);
            return JsonRpcResponse.Success(id, result);
        }
        catch (Exception ex)
        {
            return ErrorResponseBuilder.BuildError(id, ex, dataSource, procedure);
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
