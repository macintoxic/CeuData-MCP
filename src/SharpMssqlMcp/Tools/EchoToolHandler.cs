using System.Text.Json;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Tools;

public class EchoToolHandler : IToolHandler
{
    public string Name => "echo";
    public string Description => "Echoes back the input arguments.";
    public object InputSchema => new
    {
        type = "object",
        properties = new { },
        required = new string[] { }
    };

    public Task<JsonRpcResponse> HandleAsync(JsonElement? arguments, object? id, CancellationToken ct)
    {
        // Simply return the arguments back as the result
        var mcpResult = new
        {
            content = new[]
            {
                new 
                { 
                    type = "text", 
                    text = JsonSerializer.Serialize(new { echo = arguments }, new JsonSerializerOptions { WriteIndented = true }) 
                }
            }
        };
        return Task.FromResult(JsonRpcResponse.Success(id, mcpResult));
    }
}
