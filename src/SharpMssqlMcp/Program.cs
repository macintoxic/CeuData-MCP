using System.Text.Json;

namespace SharpMssqlMcp;

class Program
{
    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Redirect logs to stderr so they don't corrupt the MCP channel (stdout)
        await Console.Error.WriteLineAsync("Sharp MSSQL MCP Server starting...");

        try
        {
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
            await Console.Error.WriteLineAsync($"Fatal error: {ex.Message}");
        }
        finally
        {
            await Console.Error.WriteLineAsync("Sharp MSSQL MCP Server stopped.");
        }
    }

    static async Task ProcessRequest(string line, CancellationToken ct)
    {
        try
        {
            // Basic parsing for Step 2
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            // Log the request to stderr for debugging
            await Console.Error.WriteLineAsync($"Received: {line}");

            // Basic echo response for now (to be replaced by proper routing in Step 4)
            if (root.TryGetProperty("id", out var idProp))
            {
                var response = new
                {
                    jsonrpc = "2.0",
                    id = idProp.ValueKind == JsonValueKind.Number ? (object)idProp.GetInt64() : idProp.GetString(),
                    result = new { message = "Request received" }
                };
                
                var jsonResponse = JsonSerializer.Serialize(response);
                await Console.Out.WriteLineAsync(jsonResponse);
            }
        }
        catch (JsonException ex)
        {
            await Console.Error.WriteLineAsync($"Invalid JSON: {ex.Message}");
            // Send standard JSON-RPC error
            var errorResponse = new
            {
                jsonrpc = "2.0",
                error = new { code = -32700, message = "Parse error" },
                id = (object?)null
            };
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
        }
    }
}
