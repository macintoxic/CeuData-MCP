using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SharpMssqlMcp.Models;
using SharpMssqlMcp.Services;
using SharpMssqlMcp.Tools;
using Xunit;

namespace SharpMssqlMcp.Tests;

public class ExecuteQueryToolTests : IDisposable
{
    private readonly AppConfig _config;
    private readonly ConnectionManager _connectionManager;
    private readonly QueryExecutor _queryExecutor;
    private readonly ExecuteQueryToolHandler _handler;

    public ExecuteQueryToolTests()
    {
        // Set environment variable for test
        Environment.SetEnvironmentVariable("SQL_TEST_PASSWORD", "SharpMssqlMcp_123!");

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables();

        var configuration = configBuilder.Build();
        _config = configuration.Get<AppConfig>() ?? throw new InvalidOperationException("Failed to load config");

        var substitutor = new EnvironmentSubstitutor();
        foreach (var key in _config.ConnectionStrings.Keys.ToList())
        {
            try 
            {
                _config.ConnectionStrings[key] = substitutor.Substitute(_config.ConnectionStrings[key]);
            }
            catch { /* Ignore missing vars for unrelated connection strings */ }
        }

        _connectionManager = new ConnectionManager(_config);
        _queryExecutor = new QueryExecutor(_connectionManager);
        _handler = new ExecuteQueryToolHandler(_queryExecutor);
    }

    [Fact]
    public async Task HandleAsync_SimpleSelect_ReturnsData()
    {
        // Arrange
        var args = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            dataSource = "test",
            query = "SELECT * FROM Users WHERE Status = @status",
            parameters = new { status = "Active" }
        })).RootElement;

        // Act
        var response = await _handler.HandleAsync(args, 1, CancellationToken.None);

        // Assert
        Assert.Null(response.Error);
        var result = Assert.IsType<QueryResult>(response.Result);
        Assert.True(result.Success);
        Assert.NotEmpty(result.Data);
        Assert.All(result.Data, row => Assert.Equal("Active", row["Status"]?.ToString()));
        Assert.Contains(result.Metadata.Columns, c => c.Name == "Name");
    }

    [Fact]
    public async Task HandleAsync_MaxRows_LimitsResults()
    {
        // Arrange
        var args = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            dataSource = "test",
            query = "SELECT * FROM Users",
            options = new { maxRows = 2 }
        })).RootElement;

        // Act
        var response = await _handler.HandleAsync(args, 2, CancellationToken.None);

        // Assert
        var result = Assert.IsType<QueryResult>(response.Result);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public async Task HandleAsync_InvalidSyntax_ReturnsStructuredError()
    {
        // Arrange
        var args = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            dataSource = "test",
            query = "SELEC * FROM InvalidTable"
        })).RootElement;

        // Act
        var response = await _handler.HandleAsync(args, 3, CancellationToken.None);

        // Assert
        Assert.NotNull(response.Error);
        Assert.Equal(ErrorCodes.InternalJsonRpcError, response.Error.Code);
        
        var errorData = (dynamic)response.Error.Data!;
        // In anonymous types from deserialized JSON, we might need a different way to check properties if it's not strongly typed
        // But since we are in the same process, it might be the anonymous object itself or a JsonElement depending on how it was returned.
        // Actually, ErrorResponseBuilder returns an anonymous object.
        
        // Let's use reflection or just check the serialized version if needed, 
        // but here we can just inspect the properties.
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SQL_TEST_PASSWORD", null);
    }
}
