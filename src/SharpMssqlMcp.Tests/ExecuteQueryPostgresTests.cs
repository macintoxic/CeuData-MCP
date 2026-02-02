using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SharpMssqlMcp.Models;
using SharpMssqlMcp.Services;
using SharpMssqlMcp.Tools;
using Xunit;

namespace SharpMssqlMcp.Tests;

public class ExecuteQueryPostgresTests : IDisposable
{
    private readonly AppConfig _config;
    private readonly ConnectionManager _connectionManager;
    private readonly QueryExecutor _queryExecutor;
    private readonly ExecuteQueryToolHandler _handler;

    public ExecuteQueryPostgresTests()
    {
        // Note: Assumes postgres is running on localhost:5432 via Docker
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables();

        var configuration = configBuilder.Build();
        _config = configuration.Get<AppConfig>() ?? throw new InvalidOperationException("Failed to load config");

        _connectionManager = new ConnectionManager(_config);
        _queryExecutor = new QueryExecutor(_connectionManager);
        _handler = new ExecuteQueryToolHandler(_queryExecutor);
    }

    [Fact(Skip = "Requires local Postgres container")]
    public async Task HandleAsync_PostgresSelect_ReturnsData()
    {
        // Arrange
        var args = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            dataSource = "postgres_sample",
            query = "SELECT * FROM Users WHERE Status = :status",
            parameters = new { status = "Active" }
        })).RootElement;

        // Act
        var response = await _handler.HandleAsync(args, 1, CancellationToken.None);

        // Assert
        Assert.Null(response.Error);
        
        // Result is MCP format: { content: [ { type: 'text', text: 'JSON here' } ] }
        var resultDoc = JsonDocument.Parse(JsonSerializer.Serialize(response.Result));
        var text = resultDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
        var queryResult = JsonSerializer.Deserialize<QueryResult>(text!);

        Assert.True(queryResult!.Success);
        Assert.NotEmpty(queryResult.Data);
        Assert.Equal("Postgres User", queryResult.Data[0]["name"]?.ToString());
    }

    public void Dispose() { }
}
