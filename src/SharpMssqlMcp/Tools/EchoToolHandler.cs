using System.Text.Json;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Tools;

public class EchoToolHandler : IToolHandler
{
    public string Name => "echo";

    public Task<JsonRpcResponse> HandleAsync(JsonElement? arguments, object? id, CancellationToken ct)
    {
        // Simply return the arguments back as the result
        return Task.FromResult(JsonRpcResponse.Success(id, new { echo = arguments }));
    }
}
