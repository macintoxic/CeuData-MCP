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
        
        var json = JsonSerializer.Serialize(response.Result);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            var result = JsonSerializer.Deserialize<QueryResult>(text!);

            Assert.True(result!.Success);
            Assert.NotEmpty(result.Data);
            Assert.All(result.Data, row => Assert.Equal("Active", row["Status"]?.ToString()));
            Assert.Contains(result.Metadata.Columns, c => c.Name == "Name");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse JSON: {json}", ex);
        }
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
        var json = JsonSerializer.Serialize(response.Result);
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
        var result = JsonSerializer.Deserialize<QueryResult>(text!);

        Assert.Equal(2, result!.Data.Count);
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
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SQL_TEST_PASSWORD", null);
    }
}
