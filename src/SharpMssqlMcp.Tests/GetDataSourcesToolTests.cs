using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SharpMssqlMcp.Models;
using SharpMssqlMcp.Services;
using SharpMssqlMcp.Tools;
using Xunit;

namespace SharpMssqlMcp.Tests;

public class GetDataSourcesToolTests : IDisposable
{
    private readonly AppConfig _config;
    private readonly ConnectionManager _connectionManager;
    private readonly GetDataSourcesToolHandler _handler;

    public GetDataSourcesToolTests()
    {
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
            try { _config.ConnectionStrings[key] = substitutor.Substitute(_config.ConnectionStrings[key]); } catch { }
        }

        _connectionManager = new ConnectionManager(_config);
        _handler = new GetDataSourcesToolHandler(_config, _connectionManager);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAllDataSources()
    {
        // Act
        var response = await _handler.HandleAsync(null, 1, CancellationToken.None);

        // Assert
        Assert.Null(response.Error);
        var result = (dynamic)response.Result!;
        
        // Since Result is an object with a dataSources property, we need to handle it.
        // In our implementation it's: { content: [ { type: "text", text: "{ \"dataSources\": [...] }" } ] }
        
        var json = JsonSerializer.Serialize(response.Result);
        using var doc = JsonDocument.Parse(json);
        var contentArray = doc.RootElement.GetProperty("content");
        var text = contentArray[0].GetProperty("text").GetString();
        
        using var resultDoc = JsonDocument.Parse(text!);
        var dataSources = resultDoc.RootElement.GetProperty("dataSources");

        Assert.True(dataSources.GetArrayLength() >= 1);
        
        bool foundTest = false;
        foreach (var ds in dataSources.EnumerateArray())
        {
            if (ds.GetProperty("name").GetString() == "test") // Updated from 'gemini_test'
            {
                foundTest = true;
                Assert.Equal("connected", ds.GetProperty("status").GetString());
                Assert.Equal("SharpMssqlMcpTest", ds.GetProperty("database").GetString());
            }
        }
        Assert.True(foundTest);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SQL_TEST_PASSWORD", null);
    }
}
