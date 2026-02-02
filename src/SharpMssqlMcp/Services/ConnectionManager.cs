using System.Data.Common;
using Microsoft.Data.SqlClient;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Services;

public class ConnectionManager
{
    private readonly AppConfig _config;

    public ConnectionManager(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public (DbConnection Connection, IDataSourceProvider Provider) GetConnection(string dataSourceName)
    {
        if (string.IsNullOrEmpty(dataSourceName))
        {
            throw new ArgumentException("DataSource name cannot be empty.", nameof(dataSourceName));
        }

        if (!_config.DataSources.TryGetValue(dataSourceName, out var dsConfig))
        {
            throw new KeyNotFoundException($"DataSource '{dataSourceName}' not found in configuration.");
        }

        if (!_config.ConnectionStrings.TryGetValue(dsConfig.ConnectionStringKey, out var connectionString))
        {
            throw new KeyNotFoundException($"ConnectionStringKey '{dsConfig.ConnectionStringKey}' referenced by DataSource '{dataSourceName}' not found.");
        }

        var strategy = DbProviderFactory.GetStrategy(dsConfig.Provider);
        return (strategy.CreateConnection(connectionString), new DataSourceProvider { Strategy = strategy, Name = dataSourceName });
    }

    public IDbStrategy GetStrategy(string dataSourceName)
    {
        if (!_config.DataSources.TryGetValue(dataSourceName, out var dsConfig))
        {
            throw new KeyNotFoundException($"DataSource '{dataSourceName}' not found in configuration.");
        }
        return DbProviderFactory.GetStrategy(dsConfig.Provider);
    }
}

public interface IDataSourceProvider
{
    IDbStrategy Strategy { get; }
    string Name { get; }
}

public class DataSourceProvider : IDataSourceProvider
{
    public required IDbStrategy Strategy { get; init; }
    public required string Name { get; init; }
}
