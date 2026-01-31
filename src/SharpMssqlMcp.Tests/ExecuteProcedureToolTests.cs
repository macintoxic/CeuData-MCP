using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SharpMssqlMcp.Models;
using SharpMssqlMcp.Services;
using SharpMssqlMcp.Tools;
using Xunit;

namespace SharpMssqlMcp.Tests;

public class ExecuteProcedureToolTests : IDisposable
{
    private readonly AppConfig _config;
    private readonly ConnectionManager _connectionManager;
    private readonly ProcedureExecutor _procedureExecutor;
    private readonly ExecuteProcedureToolHandler _handler;

    public ExecuteProcedureToolTests()
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
        _procedureExecutor = new ProcedureExecutor(_connectionManager);
        _handler = new ExecuteProcedureToolHandler(_procedureExecutor);
    }

    [Fact]
    public async Task HandleAsync_SingleResultSet_ReturnsData()
    {
        // Arrange
        var args = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            dataSource = "test",
            procedure = "sp_GetUserById",
            parameters = new { id = 1 }
        })).RootElement;

        // Act
        var response = await _handler.HandleAsync(args, 1, CancellationToken.None);

        // Assert
        Assert.Null(response.Error);
        var result = Assert.IsType<ProcedureResult>(response.Result);
        Assert.True(result.Success);
        Assert.Single(result.ResultSets);
        Assert.Equal("John Doe", result.ResultSets[0].Data[0]["Name"]?.ToString());
    }

    [Fact]
    public async Task HandleAsync_MultipleResultSets_ReturnsData()
    {
        // Arrange
        var args = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            dataSource = "test",
            procedure = "sp_GetUserOrders",
            parameters = new { userId = 1 }
        })).RootElement;

        // Act
        var response = await _handler.HandleAsync(args, 2, CancellationToken.None);

        // Assert
        Assert.Null(response.Error);
        var result = Assert.IsType<ProcedureResult>(response.Result);
        Assert.Equal(2, result.ResultSets.Count);
        Assert.Equal("ResultSet1", result.ResultSets[0].Name);
        Assert.Equal("ResultSet2", result.ResultSets[1].Name);
        Assert.NotEmpty(result.ResultSets[1].Data);
    }

    [Fact]
    public async Task HandleAsync_OutputParameters_ReturnsValues()
    {
        // Arrange
        var args = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            dataSource = "test",
            procedure = "sp_GetUserCount",
            parameters = new { status = "Active", count = 0 }, // Initial value for output param slot
            options = new { includeOutputParameters = true }
        })).RootElement;

        // Note: Our current ProcedureExecutor logic for output params is a bit limited 
        // because it doesn't know which params are output without introspection.
        // Let's see if we can improve ProcedureExecutor to handle this by checking the parameter dictionary 
        // if we want to support output params better.
        // Actually, the current code just checks p.Direction. 
        // I need to update ProcedureExecutor to set ParameterDirection.InputOutput for all parameters by default 
        // to allow them to be captured as results, OR use introspection.
        
        // Act
        var response = await _handler.HandleAsync(args, 3, CancellationToken.None);

        // Assert
        Assert.Null(response.Error);
        var result = Assert.IsType<ProcedureResult>(response.Result);
        Assert.True(result.OutputParameters.ContainsKey("@count"));
        Assert.Equal(3, Convert.ToInt32(result.OutputParameters["@count"]));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SQL_TEST_PASSWORD", null);
    }
}
