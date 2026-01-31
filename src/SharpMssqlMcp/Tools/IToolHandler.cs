using System.Text.Json;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Tools;

public interface IToolHandler
{
    string Name { get; }
    Task<JsonRpcResponse> HandleAsync(JsonElement? arguments, object? id, CancellationToken ct);
}
