using System.Text.Json;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Tools;

public interface IToolHandler
{
    string Name { get; }
    string Description { get; }
    object InputSchema { get; }
    Task<JsonRpcResponse> HandleAsync(JsonElement? arguments, object? id, CancellationToken ct);
}
